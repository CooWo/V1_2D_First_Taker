using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Character;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Raid
{
    /// <summary>레이드 파티원 한 명 (플레이어 또는 동료).</summary>
    public sealed class RaidMember
    {
        public string Name;
        public PlayerCharacter Character;
        public bool IsLocalPlayer;
        public float Damage;
    }

    /// <summary>
    /// 파티 구성 공급원. 지금은 AI 동료 3명을 만들고, 온라인 4인 협동이 붙으면
    /// 네트워크 파티원으로 교체할 수 있도록 분리해 둡니다 (DIP).
    /// </summary>
    public interface IRaidPartySource
    {
        IReadOnlyList<RaidMember> CreateAllies(GameMeta meta, ICombatWorld world, int level);
    }

    /// <summary>로컬 AI 동료 3명 (플레이어와 다른 직업, 같은 레벨의 희귀 장비).</summary>
    public sealed class BotPartySource : IRaidPartySource
    {
        public IReadOnlyList<RaidMember> CreateAllies(GameMeta meta, ICombatWorld world, int level)
        {
            var classes = meta.Content.Classes.All.Where(c => c.Id != meta.Profile.ClassId).Take(3);
            var members = new List<RaidMember>();
            foreach (var characterClass in classes)
            {
                var ally = new PlayerCharacter(world, meta.Content.Skills, meta.Content.Supports, meta.Content.Sets, null, "동료 " + characterClass.Name);
                ally.Combatant.VisualKey = AllyVisual(characterClass.Id);
                ally.ChangeClass(characterClass);
                ally.Progress.Restore(level, 0f);
                foreach (var (slot, skill) in GameMeta.StarterKit(characterClass.Id))
                {
                    var item = meta.Loot.Create(slot, ItemRarity.Rare, level, skill);
                    item.Requirements = default;
                    ally.Equipment.ForceEquip(item, slot);
                }
                ally.Combatant.Health.ResetToFull();
                ally.Combatant.Resource.Fill();
                members.Add(new RaidMember { Name = ally.Combatant.Name, Character = ally });
            }
            return members;
        }

        static string AllyVisual(string classId)
        {
            switch (classId)
            {
                case "knight": return "knight_m";
                case "mage": return "wizzard_f";
                case "archer": return "elf_m";
                default: return "lizard_m";
            }
        }
    }

    public enum RaidOutcome
    {
        InProgress,
        Victory,
        TimeUp,
        Wipe
    }

    public sealed class RaidResult
    {
        public RaidOutcome Outcome;
        public float BossLifeBefore;
        public float BossLifeAfter;
        public float BossMaxLife;
        public long GoldReward;
        public bool LastHitByPlayer;
        public List<Item> Rewards = new List<Item>();
        public List<RaidMember> Ranking = new List<RaidMember>();
    }

    /// <summary>
    /// 4인 협동 레이드: 보스 한 마리에 집중. 제한 시간 안에 최대한 피해를 주고,
    /// 보스 체력은 다음 입장까지 그대로 남습니다 (처치할 때까지 누적). 기여도·막타 보상.
    /// </summary>
    public sealed class RaidSession : ICombatSession
    {
        public const float AttemptSeconds = 90f;

        readonly GameMeta _meta;
        readonly List<RaidMember> _members = new List<RaidMember>();
        readonly List<EnemyAgent> _adds = new List<EnemyAgent>();
        readonly EnemyFactory _factory = new EnemyFactory();
        readonly RaidBossBrain _brain;
        readonly float _lifeBefore;
        Combatant _lastHitter;

        public RaidSession(GameMeta meta, BalanceSettings balance, IRandom random, Rect arena, IRaidPartySource party)
        {
            _meta = meta;
            World = CombatSetup.CreateWorld(balance, random, arena);

            Player = meta.CreatePlayer(World);
            Player.Combatant.Position = arena.center + new Vector2(0f, -5f);
            World.Add(Player.Combatant);
            _members.Add(new RaidMember { Name = "나 (" + Player.Class.Current?.Name + ")", Character = Player, IsLocalPlayer = true });
            Pet = meta.CreatePet(Player, World);

            int level = Player.Progress.Level;
            var allies = party.CreateAllies(meta, World, level);
            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].Character.Combatant.Position = arena.center + new Vector2(-3f + 3f * i, -6.5f);
                World.Add(allies[i].Character.Combatant);
                _members.Add(allies[i]);
            }

            var state = meta.Profile.Raid;
            var minion = meta.Content.Enemies.First();
            Definition = RaidBossCatalog.ForLevel(RaidBossCatalog.CreateDefault(minion), state.BossLevel);
            Boss = CreateBoss(state, level);
            _lifeBefore = Boss.Health.Life;

            var context = new BossContext
            {
                Boss = Boss, World = World, Random = random,
                Party = _members.Select(m => m.Character.Combatant).ToList(),
                SummonAdd = SpawnAdd
            };
            _brain = new RaidBossBrain(_bossAgent, Definition, context);
            _brain.PatternStarted += name => Log?.Invoke($"<color=#FF8A5A>{Definition.Name}: {name}</color>");

            World.Damage.Resolved += OnDamage;
            World.CombatantRemoved += c => _adds.RemoveAll(a => a.Combatant == c);
            TimeRemaining = AttemptSeconds;
        }

        EnemyAgent _bossAgent;

        public CombatWorld World { get; }
        public PlayerCharacter Player { get; }
        public PetCompanion Pet { get; }
        public Combatant Boss { get; }
        public RaidBossDefinition Definition { get; }
        public IReadOnlyList<RaidMember> Members => _members;
        public float TimeRemaining { get; private set; }
        public RaidOutcome Outcome { get; private set; } = RaidOutcome.InProgress;
        public RaidResult Result { get; private set; }
        public string LastPattern => _brain.LastPattern;
        public bool Enraged => _brain.Enraged;
        public bool IsPlayerDead => !Player.Combatant.IsAlive;
        public float RespawnRemaining => 0f;
        public float TimeScale => 1f;

        public event Action<string> Log;
        public event Action<RaidResult> Finished;

        Combatant CreateBoss(Meta.RaidState state, int playerLevel)
        {
            float maxLife = Definition.LifePerPlayerLevel * (playerLevel + 5) * Mathf.Pow(1.5f, state.BossLevel - 1);
            var archetype = new EnemyArchetype
            {
                Id = Definition.Id, Name = Definition.Name, VisualKey = Definition.VisualKey,
                Life = 1f, Damage = 1f, MoveSpeed = 1.6f, Radius = 0.7f, AttackRange = 1.3f, AttackInterval = 1.6f,
                DamageType = Definition.Element
            };
            _bossAgent = _factory.Create(archetype, new MonsterScaling(1, 1f), EnemyRank.Boss, World.Bounds.center + new Vector2(0f, 4f));
            _bossAgent.Aggro = true;
            var boss = _bossAgent.Combatant;
            boss.Name = $"{Definition.Name} Lv.{state.BossLevel}";
            boss.Stats.SetBase(StatType.MaxLife, maxLife);
            boss.Stats.SetBase(StatType.WeaponDamage, (20f + Definition.DamagePerPlayerLevel * playerLevel) * Mathf.Pow(1.3f, state.BossLevel - 1));
            boss.Stats.SetBase(StatType.Armor, 50f * playerLevel);
            boss.Stats.SetBase(StatType.AllResistPct, 20f);
            boss.Health.ResetToFull();
            if (state.BossLife > 0f && state.BossLife < boss.Health.MaxLife)
                boss.Health.ApplyDamage(boss.Health.MaxLife - state.BossLife);
            World.Add(boss);
            return boss;
        }

        void SpawnAdd(EnemyArchetype archetype, Vector2 position)
        {
            var agent = _factory.Create(archetype, new MonsterScaling(Mathf.Max(1, Player.Progress.Level), 1f + 0.4f * (Mathf.Max(1, Player.Progress.Level) - 1)), EnemyRank.Normal, position);
            agent.Aggro = true;
            _adds.Add(agent);
            World.Add(agent.Combatant);
        }

        void OnDamage(DamageResult result)
        {
            if (result.Target != Boss || result.Attacker == null) return;
            var member = _members.FirstOrDefault(m => m.Character.Combatant == result.Attacker);
            if (member != null) member.Damage += result.Amount;
            if (result.Killed) _lastHitter = result.Attacker;
        }

        public void Tick(float dt)
        {
            if (Outcome != RaidOutcome.InProgress) return;
            dt = Mathf.Min(dt, 0.05f);
            TimeRemaining -= dt;

            foreach (var member in _members) member.Character.Tick(dt);
            Pet?.Tick(dt);
            _brain.Tick(dt);
            for (int i = 0; i < _adds.Count; i++)
                if (_adds[i].Combatant.IsAlive) _adds[i].Brain.Tick(_adds[i], World, dt);
            World.Tick(dt);

            if (!Boss.IsAlive) Finish(RaidOutcome.Victory);
            else if (_members.All(m => !m.Character.Combatant.IsAlive)) Finish(RaidOutcome.Wipe);
            else if (TimeRemaining <= 0f) Finish(RaidOutcome.TimeUp);
        }

        /// <summary>도중 포기 (남은 체력은 그대로 기록).</summary>
        public void Abandon()
        {
            if (Outcome == RaidOutcome.InProgress) Finish(RaidOutcome.TimeUp);
        }

        void Finish(RaidOutcome outcome)
        {
            Outcome = outcome;
            var state = _meta.Profile.Raid;
            var me = _members[0];
            var result = new RaidResult
            {
                Outcome = outcome,
                BossLifeBefore = _lifeBefore,
                BossLifeAfter = Mathf.Max(0f, Boss.Health.Life),
                BossMaxLife = Boss.Health.MaxLife,
                LastHitByPlayer = _lastHitter == Player.Combatant,
                Ranking = _members.OrderByDescending(m => m.Damage).ToList()
            };

            float goldFind = 1f + Player.Combatant.Stats.Get(StatType.GoldFindPct) / 100f;
            result.GoldReward = (long)Mathf.Round((me.Damage * 0.02f + 50f * state.BossLevel) * goldFind);
            state.LifetimeDamage += (long)me.Damage;

            if (outcome == RaidOutcome.Victory)
            {
                state.BossKills++;
                result.GoldReward += (long)Mathf.Round(500f * state.BossLevel * goldFind * (result.LastHitByPlayer ? 2f : 1f));
                for (int i = 0; i < 2; i++)
                {
                    var slot = SlotRules.All[_meta.Random.Range(0, SlotRules.All.Length)];
                    if (slot == EquipSlot.Ring2) slot = EquipSlot.Ring1;
                    var rarity = SlotRules.IsSetSlot(slot) && _meta.Random.Chance(40f) ? ItemRarity.Set : ItemRarity.Legendary;
                    var item = _meta.Loot.Create(slot, rarity, Player.Progress.Level + 2);
                    if (!_meta.Profile.Bag.TryAdd(item)) result.GoldReward += _meta.Pricing.SellValue(item, Player.Combatant.Stats);
                    else result.Rewards.Add(item);
                }
                state.BossLevel++;
                state.BossLife = -1f;
            }
            else
            {
                state.BossLife = Boss.Health.Life;
            }

            _meta.Wallet.Earn(result.GoldReward);
            _meta.Capture(Player);
            _meta.RequestSave();
            Result = result;
            Finished?.Invoke(result);
        }

        public void Dispose()
        {
            World.Damage.Resolved -= OnDamage;
            if (Outcome == RaidOutcome.InProgress) Abandon();
        }
    }
}
