using System;
using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Character;
using ShadowRPG.Core.Classes;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Skills.Behaviors;
using ShadowRPG.Core.Supports;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Game
{
    /// <summary>게임 세션이 필요로 하는 데이터 묶음 (주입용).</summary>
    public sealed class GameContent
    {
        public ISkillCatalog Skills;
        public ISupportCatalog Supports;
        public ISetCatalog Sets;
        public IClassCatalog Classes;
        public List<EnemyArchetype> Enemies;
        public List<EnemyArchetype> Elites;
        public EnemyArchetype Boss;

        public static GameContent CreateDefault() => new GameContent
        {
            Skills = SkillCatalog.CreateDefault(),
            Supports = SupportCatalog.CreateDefault(),
            Sets = SetCatalog.CreateDefault(),
            Classes = ClassCatalog.CreateDefault(),
            Enemies = EnemyRoster.CreateDefault(),
            Elites = EnemyRoster.CreateElites(),
            Boss = EnemyRoster.CreateBoss()
        };
    }

    /// <summary>
    /// Core 조립 지점 (Composition Root). 규칙·서비스를 만들어 서로 연결하고,
    /// 표현 계층에는 이벤트와 읽기용 속성만 노출합니다.
    /// </summary>
    public sealed class GameSession
    {
        const float RespawnDelay = 3f;

        float _respawnTimer;

        public GameSession(GameContent content, BalanceSettings balance, IRandom random, Rect arena)
        {
            Content = content;
            Balance = balance;
            Random = random;

            var damage = new DamageService(balance,
                new IOutgoingDamageRule[]
                {
                    new TagScalingRule(), new RangeAsymmetryRule(balance), new TagMultiplierRule(),
                    new BossDamageRule(), new CriticalStrikeRule(balance, random)
                },
                new IIncomingDamageRule[]
                {
                    new EvasionRule(balance, random), new BlockRule(balance, random), new ArmorRule(),
                    new ResistanceRule(balance), new EliteDamageReductionRule(), new StatusModifierRule()
                });

            World = new CombatWorld(balance, random, damage, arena);
            World.Executor = new SkillExecutor(World, new ISkillModifierSource[] { new StatSkillModifierSource() });

            var fortify = new ShadowFortifyRule(balance);
            damage.AddListener(fortify);
            damage.AddListener(new LifeOnHitRule(balance));
            damage.AddListener(new MarkTransferListener(World, World));
            damage.AddListener(new DamageVfxReporter(World));
            World.Executor.AddListener(fortify);

            Loot = new LootGenerator(content.Skills, content.Supports, content.Sets, random);
            Scorer = new ItemScorer();
            AutoEquip = new AutoEquipPolicy(Scorer);

            Player = new PlayerCharacter(World, content.Skills, content.Supports, content.Sets);
            Player.Combatant.Position = arena.center;
            World.Add(Player.Combatant);

            Director = new WaveDirector(World, new EnemyFactory(), random, content.Enemies, content.Elites, content.Boss);
            World.CombatantDied += OnCombatantDied;
            Player.Progress.LeveledUp += level => Log?.Invoke($"레벨 업! Lv.{level}");
        }

        public GameContent Content { get; }
        public BalanceSettings Balance { get; }
        public IRandom Random { get; }
        public CombatWorld World { get; }
        public PlayerCharacter Player { get; }
        public WaveDirector Director { get; }
        public ILootGenerator Loot { get; }
        public IItemScorer Scorer { get; }
        public AutoEquipPolicy AutoEquip { get; }
        public int Kills { get; private set; }
        public bool AutoEquipEnabled { get; set; } = true;
        public bool IsPlayerDead => !Player.Combatant.IsAlive;
        public float RespawnRemaining => _respawnTimer;

        public event Action<Item, bool> ItemLooted;
        public event Action<string> Log;
        public event Action PlayerDied;
        public event Action PlayerRespawned;

        /// <summary>시작 장비 지급: 오른손·왼손·머리·몸·신발에 일반 등급 스킬 장비.</summary>
        public void GiveStarterKit(string classId)
        {
            var characterClass = Content.Classes.Get(classId) ?? Content.Classes.All[0];
            Player.ChangeClass(characterClass);

            var kit = StarterKit(characterClass.Id);
            foreach (var (slot, skill) in kit)
            {
                var item = Loot.Create(slot, ItemRarity.Normal, 1, skill);
                item.Requirements = default;
                Player.Equipment.ForceEquip(item, slot);
            }
            Player.Combatant.Health.ResetToFull();
            Player.Combatant.Resource.Fill();
        }

        static IEnumerable<(EquipSlot, string)> StarterKit(string classId)
        {
            switch (classId)
            {
                case "mage":
                    return new[] { (EquipSlot.RightHand, "INT-01"), (EquipSlot.LeftHand, "INT-08"), (EquipSlot.Head, "DEX-08"), (EquipSlot.Body, "INT-07"), (EquipSlot.Boots, "DEX-07") };
                case "archer":
                    return new[] { (EquipSlot.RightHand, "DEX-04"), (EquipSlot.LeftHand, "DEX-05"), (EquipSlot.Head, "DEX-08"), (EquipSlot.Body, "INT-07"), (EquipSlot.Boots, "DEX-07") };
                case "monk":
                    return new[] { (EquipSlot.RightHand, "DEX-01"), (EquipSlot.LeftHand, "DEX-02"), (EquipSlot.Head, "DEX-08"), (EquipSlot.Body, "STR-08"), (EquipSlot.Boots, "DEX-07") };
                default:
                    return new[] { (EquipSlot.RightHand, "STR-01"), (EquipSlot.LeftHand, "INT-05"), (EquipSlot.Head, "STR-07"), (EquipSlot.Body, "STR-08"), (EquipSlot.Boots, "DEX-07") };
            }
        }

        public void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);

            if (IsPlayerDead)
            {
                _respawnTimer -= dt;
                World.Tick(dt);
                if (_respawnTimer <= 0f) Respawn();
                return;
            }

            Player.Tick(dt);
            Director.Tick(dt, Player.Combatant);
            World.Tick(dt);
        }

        /// <summary>장비를 직접 장착 (인벤토리 UI용). 밀려난 장비는 인벤토리로 돌아갑니다.</summary>
        public EquipResult Equip(Item item, EquipSlot slot)
        {
            var result = Player.Equipment.TryEquip(item, slot, Player.Combatant.Stats);
            if (!result.Success) return result;
            Player.Inventory.Remove(item);
            if (result.Replaced != null) Player.Inventory.Add(result.Replaced, Scorer);
            return result;
        }

        void OnCombatantDied(Combatant combatant)
        {
            if (combatant == Player.Combatant)
            {
                _respawnTimer = RespawnDelay;
                Log?.Invoke("그림자가 흩어졌습니다… 3초 뒤 부활");
                PlayerDied?.Invoke();
                return;
            }

            Kills++;
            int level = Mathf.Max(1, Director.LevelForWave(Mathf.Max(1, Director.Wave)));
            float xp = 4f * level * (combatant.Rank == EnemyRank.Boss ? 25f : combatant.Rank == EnemyRank.Elite ? 6f : 1f);
            Player.Progress.Gain(xp);
            RollLoot(combatant, level);
        }

        void RollLoot(Combatant enemy, int level)
        {
            int drops = enemy.Rank == EnemyRank.Boss ? 3 : enemy.Rank == EnemyRank.Elite ? 1 : Random.Chance(14f) ? 1 : 0;
            for (int i = 0; i < drops; i++)
            {
                var item = Loot.Generate(level, enemy.Rank);
                bool equipped = false;
                if (AutoEquipEnabled && AutoEquip.TryAutoEquip(Player.Equipment, item, Player.Combatant.Stats, out _, out var replaced))
                {
                    equipped = true;
                    if (replaced != null) Player.Inventory.Add(replaced, Scorer);
                }
                else
                {
                    Player.Inventory.Add(item, Scorer);
                }
                World.Emit(new VfxEvent { Kind = VfxKind.Text, Position = enemy.Position, Text = item.Rarity.DisplayName(), Duration = 1f });
                ItemLooted?.Invoke(item, equipped);
            }
        }

        void Respawn()
        {
            Director.Clear();
            World.ClearEffects();
            World.Revive(Player.Combatant);
            Player.Combatant.Position = World.Bounds.center;
            Log?.Invoke("부활했습니다.");
            PlayerRespawned?.Invoke();
        }
    }
}
