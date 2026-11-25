using System;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Character;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Game
{
    public enum FloorState
    {
        Fighting,
        LootChoice,
        Portal
    }

    /// <summary>던전 규칙 수치 (층당 반출 개수 등).</summary>
    [Serializable]
    public sealed class DungeonRules
    {
        [Tooltip("한 층에서 가지고 나갈 수 있는 장비 수")]
        public int KeepLimitPerFloor = 3;
        [Tooltip("보스 층 추가 반출")]
        public int BossFloorBonusKeep = 1;
        [Tooltip("층 클리어 후 이 시간(초)이 지나면 자동 선택대로 확정 (0이면 직접 확정할 때까지 대기)")]
        public float LootAutoConfirmSeconds = 10f;
        [Range(0f, 100f)] public float NormalDropChance = 12f;
        public float FloorClearGoldPerLevel = 12f;
        public float RespawnDelay = 3f;
    }

    /// <summary>
    /// 던전 진행: ACT → 던전 → 층. 층의 몬스터를 모두 잡으면 전리품을 고르고(나머지는 자동 판매)
    /// 포탈로 다음 층에 내려갑니다. 죽으면 그 층의 전리품을 잃고 층을 다시 시작합니다.
    /// </summary>
    public sealed class DungeonSession : ICombatSession
    {
        readonly GameMeta _meta;
        readonly DungeonRules _rules;
        readonly IRandom _random;
        float _respawnTimer;

        public DungeonSession(GameMeta meta, BalanceSettings balance, DungeonRules rules, IRandom random, Rect arena)
        {
            _meta = meta;
            _rules = rules;
            _random = random;

            World = CombatSetup.CreateWorld(balance, random, arena);
            Player = meta.CreatePlayer(World);
            World.Add(Player.Combatant);
            Pet = meta.CreatePet(Player, World);

            Spawner = new FloorSpawner(World, new EnemyFactory(), random);
            Spawner.BossSpawned += boss => Log?.Invoke($"<color=#FF6A4A>{boss.Combatant.Name} 등장!</color>");
            World.CombatantDied += OnCombatantDied;
            Player.Progress.LeveledUp += level => Log?.Invoke($"레벨 업! Lv.{level}");
            if (Pet != null) Pet.SkillUsed += skill => World.Emit(new VfxEvent { Kind = VfxKind.Text, Position = Pet.Position, Text = skill.Name, Duration = 0.8f });
            meta.TreeService.Changed += OnTreeChanged;

            StartFloor();
        }

        public CombatWorld World { get; }
        public PlayerCharacter Player { get; }
        public PetCompanion Pet { get; }
        public FloorSpawner Spawner { get; }
        public GameMeta Meta => _meta;
        public FloorState State { get; private set; }
        public FloorPlan Plan { get; private set; }
        public FloorLoot Loot { get; private set; }
        public Vector2 PortalPosition { get; private set; }
        public float LootTimer { get; private set; }
        public bool IsPlayerDead => !Player.Combatant.IsAlive;
        public float RespawnRemaining => _respawnTimer;

        public ActDefinition Act => _meta.Content.Acts.Acts[Mathf.Clamp(Plan.ActIndex, 0, _meta.Content.Acts.Acts.Count - 1)];
        public string DungeonName => Act.DungeonNames.Length > Plan.DungeonIndex ? Act.DungeonNames[Plan.DungeonIndex] : $"던전 {Plan.DungeonIndex + 1}";
        public int KeepLimit => _rules.KeepLimitPerFloor + (Plan.HasBoss ? _rules.BossFloorBonusKeep : 0);

        public event Action<string> Log;
        public event Action<FloorPlan> FloorStarted;
        public event Action<FloorLoot> FloorCleared;
        public event Action<Item> ItemFound;

        public void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);

            if (IsPlayerDead)
            {
                _respawnTimer -= dt;
                World.Tick(dt);
                if (_respawnTimer <= 0f) RestartFloorAfterDeath();
                return;
            }

            switch (State)
            {
                case FloorState.Fighting:
                    Player.Tick(dt);
                    Pet?.Tick(dt);
                    Spawner.Tick(dt, Player.Combatant);
                    World.Tick(dt);
                    if (Spawner.IsCleared) EnterLootChoice();
                    break;

                case FloorState.LootChoice:
                    Pet?.Tick(dt);
                    World.Tick(dt);
                    if (_rules.LootAutoConfirmSeconds <= 0f) break;
                    LootTimer -= dt;
                    if (LootTimer <= 0f) ConfirmLoot();
                    break;

                case FloorState.Portal:
                    Player.Tick(dt);
                    Pet?.Tick(dt);
                    World.Tick(dt);
                    if (Vector2.Distance(Player.Combatant.Position, PortalPosition) < 0.9f) NextFloor();
                    break;
            }
        }

        // ---------- 전리품 선택 ----------

        public bool ToggleLoot(Item item) => State == FloorState.LootChoice && Loot.Toggle(item, Player.Inventory.FreeSlots);

        public long PreviewSaleGold => Loot.PreviewSaleGold(_meta.Pricing, Player.Combatant.Stats);

        public void ConfirmLoot()
        {
            if (State != FloorState.LootChoice) return;
            var (kept, gold) = Loot.Resolve(Player.Inventory, _meta.Wallet, _meta.Pricing, Player.Combatant.Stats);
            long clearBonus = (long)Mathf.Round(_rules.FloorClearGoldPerLevel * Plan.Level * (Plan.HasBoss ? 3f : 1f));
            _meta.Wallet.Earn(clearBonus);
            Log?.Invoke($"반출 {kept}개 · 자동 판매 +{gold:N0}G · 층 클리어 +{clearBonus:N0}G");

            State = FloorState.Portal;
            Player.Context.Destination = PortalPosition;
            World.Emit(VfxEvent.At(VfxKind.Nova, PortalPosition, 1.2f, DamageType.Light, 0.8f));
            _meta.Capture(Player);
            _meta.RequestSave();
        }

        // ---------- 층 진행 ----------

        void StartFloor()
        {
            World.ClearEffects();
            Plan = _meta.Content.Acts.Plan(_meta.Profile.Progress);
            Loot = new FloorLoot(KeepLimit);
            Spawner.Begin(Plan, _meta.Content.EnemiesFor(Act), _meta.Content.Elites, _meta.Content.Boss);

            var bounds = World.Bounds;
            Player.Combatant.Position = new Vector2(bounds.center.x, bounds.yMin + 2f);
            PortalPosition = new Vector2(bounds.center.x, bounds.yMax - 2f);
            Player.Context.Destination = null;
            State = FloorState.Fighting;

            Log?.Invoke($"ACT {Plan.ActIndex + 1} {Act.Name} · {DungeonName} {Plan.Floor}층 — 몬스터 {Plan.TotalKills}");
            FloorStarted?.Invoke(Plan);
        }

        void EnterLootChoice()
        {
            State = FloorState.LootChoice;
            LootTimer = _rules.LootAutoConfirmSeconds;
            Loot.AutoSelect(_meta.Scorer, Player.Inventory.FreeSlots);
            Log?.Invoke($"층 클리어! 획득 {Loot.Found.Count}개 중 {Loot.AllowedPicks(Player.Inventory.FreeSlots)}개까지 반출");
            FloorCleared?.Invoke(Loot);
            if (Loot.Found.Count == 0) ConfirmLoot();
        }

        void NextFloor()
        {
            _meta.Profile.Progress.Advance(_meta.Content.Acts);
            _meta.RequestSave();
            StartFloor();
        }

        void RestartFloorAfterDeath()
        {
            World.Revive(Player.Combatant);
            Log?.Invoke("부활 — 이 층의 전리품을 잃고 다시 시작합니다.");
            StartFloor();
        }

        void OnCombatantDied(Combatant combatant)
        {
            if (combatant == Player.Combatant)
            {
                _respawnTimer = _rules.RespawnDelay;
                Loot.Clear();
                Log?.Invoke("그림자가 흩어졌습니다…");
                return;
            }
            if (combatant.Team != Team.Enemy) return;

            Spawner.RegisterKill();
            float xp = 4f * Plan.Level * (combatant.Rank == EnemyRank.Boss ? 25f : combatant.Rank == EnemyRank.Elite ? 6f : 1f);
            Player.Progress.Gain(xp);

            int drops = combatant.Rank == EnemyRank.Boss ? 3 : combatant.Rank == EnemyRank.Elite ? 1 : _random.Chance(_rules.NormalDropChance) ? 1 : 0;
            for (int i = 0; i < drops; i++)
            {
                var item = _meta.Loot.Generate(Plan.Level, combatant.Rank);
                Loot.Add(item);
                World.Emit(new VfxEvent { Kind = VfxKind.Text, Position = combatant.Position, Text = item.Rarity.DisplayName(), Duration = 1f });
                ItemFound?.Invoke(item);
            }
        }

        void OnTreeChanged() => _meta.TreeService.ApplyTo(Player.Combatant);

        public void Dispose()
        {
            _meta.TreeService.Changed -= OnTreeChanged;
            _meta.Capture(Player);
        }
    }
}
