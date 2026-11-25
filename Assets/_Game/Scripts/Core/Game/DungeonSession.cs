using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>마지막 몬스터 처치 직후 슬로우 모션.</summary>
        Clearing,
        /// <summary>결과창: 가져갈 장비 선택 + 계속/귀환 결정.</summary>
        Result,
        Dead
    }

    /// <summary>던전 규칙 수치.</summary>
    [Serializable]
    public sealed class DungeonRules
    {
        [Tooltip("층당 가져갈 수 있는 최대 개수 (0 = 가방 칸으로만 제한)")]
        public int MaxPicksPerFloor;
        [Range(0f, 100f)] public float NormalDropChance = 12f;
        public float FloorClearGoldPerLevel = 12f;
        [Tooltip("클리어 슬로우 모션 길이 (게임 시간 초)")]
        public float ClearSlowMoSeconds = 0.6f;
        [Range(0.05f, 1f)] public float SlowMoScale = 0.25f;
        [Tooltip("사망 후 마을로 돌아가기까지 (초)")]
        public float DeathReturnDelay = 3f;
    }

    /// <summary>층 결과 요약.</summary>
    public sealed class FloorSummary
    {
        public FloorPlan Plan;
        public string LayoutName;
        public int Kills;
        public float Seconds;
        public float Experience;
        public int ItemsFound;
    }

    /// <summary>
    /// 던전 한 번의 진행(런). 층의 몬스터를 다 잡으면 슬로우 모션 → 결과창.
    /// 결과창에서 가방 칸에 맞춰 장비를 고르고(나머지 자동 판매) "다음 층" 또는 "마을 귀환"을 선택합니다.
    /// 계속 진행하다 죽으면 가방 아이템의 절반을 잃고 마을로 돌아갑니다.
    /// </summary>
    public sealed class DungeonSession : ICombatSession
    {
        readonly GameMeta _meta;
        readonly DungeonRules _rules;
        readonly IRandom _random;
        readonly IFloorLayoutSelector _layouts;
        readonly int _seed;
        float _timer;
        float _floorTime;
        float _floorXp;
        bool _returnRaised;

        public DungeonSession(GameMeta meta, BalanceSettings balance, DungeonRules rules, IRandom random, IFloorLayoutSelector layouts, int seed)
        {
            _meta = meta;
            _rules = rules;
            _random = random;
            _layouts = layouts;
            _seed = seed;

            World = CombatSetup.CreateWorld(balance, random, new Rect(-40f, -40f, 80f, 80f));
            Player = meta.CreatePlayer(World);
            World.Add(Player.Combatant);
            Pet = meta.CreatePet(Player, World);

            Spawner = new FloorSpawner(World, new EnemyFactory(), random);
            Spawner.BossSpawned += boss => Log?.Invoke($"<color=#FF6A4A>{boss.Combatant.Name}이(가) 가장 깊은 방에서 기다립니다</color>");
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
        public FloorLayout Layout { get; private set; }
        public FloorLoot Loot { get; private set; }
        public FloorSummary Summary { get; private set; }
        public Inventory Bag => Player.Inventory;
        public bool IsPlayerDead => !Player.Combatant.IsAlive;
        public float RespawnRemaining => State == FloorState.Dead ? _timer : 0f;
        public float TimeScale => State == FloorState.Clearing ? _rules.SlowMoScale : 1f;

        public ActDefinition Act => _meta.Content.Acts.Acts[Mathf.Clamp(Plan.ActIndex, 0, _meta.Content.Acts.Acts.Count - 1)];
        public string DungeonName => Act.DungeonNames.Length > Plan.DungeonIndex ? Act.DungeonNames[Plan.DungeonIndex] : $"던전 {Plan.DungeonIndex + 1}";

        public event Action<string> Log;
        public event Action<FloorPlan> FloorStarted;
        public event Action<FloorSummary> FloorResultReady;
        /// <summary>마을로 돌아가야 할 때 (true = 사망).</summary>
        public event Action<bool> ReturnToTownRequested;
        public event Action<Item> ItemFound;

        public void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);

            switch (State)
            {
                case FloorState.Fighting:
                    _floorTime += dt;
                    Player.Tick(dt);
                    Pet?.Tick(dt);
                    Spawner.Tick(dt, Player.Combatant);
                    World.Tick(dt);
                    if (State == FloorState.Fighting && Spawner.IsCleared) BeginClearing();
                    break;

                case FloorState.Clearing:
                    Pet?.Tick(dt);
                    World.Tick(dt);
                    _timer -= dt;
                    if (_timer <= 0f) EnterResult();
                    break;

                case FloorState.Result:
                    Pet?.Tick(dt);
                    World.Tick(dt);
                    break;

                case FloorState.Dead:
                    World.Tick(dt);
                    _timer -= dt;
                    if (_timer <= 0f && !_returnRaised)
                    {
                        _returnRaised = true;
                        ReturnToTownRequested?.Invoke(true);
                    }
                    break;
            }
        }

        // ---------- 결과창 ----------

        public bool ToggleLoot(Item item) => State == FloorState.Result && Loot.Toggle(item, Bag);

        public bool CanTake(Item item) => Loot.IsSelected(item) || Loot.CanAlsoTake(item, Bag);

        public void AutoSelectLoot() => Loot.AutoSelect(_meta.Scorer, Bag);

        public long PreviewSaleGold => Loot.PreviewSaleGold(_meta.Pricing, Player.Combatant.Stats);

        /// <summary>결과창에서 가방의 장비를 팔아 자리를 만듭니다.</summary>
        public long SellFromBag(Item item) => _meta.Sell(item, Player);

        /// <summary>다음 층으로 계속 (가방 안 아이템은 계속 위험에 노출).</summary>
        public void ContinueToNextFloor()
        {
            if (State != FloorState.Result) return;
            Settle();
            _meta.Profile.Progress.Advance(_meta.Content.Acts);
            _meta.RequestSave();
            StartFloor();
        }

        /// <summary>마을로 귀환 (가방 안 아이템 안전 확보).</summary>
        public void ReturnToTown()
        {
            if (State != FloorState.Result || _returnRaised) return;
            Settle();
            _meta.Profile.Progress.Advance(_meta.Content.Acts);
            _returnRaised = true;
            Log?.Invoke("마을로 귀환합니다.");
            ReturnToTownRequested?.Invoke(false);
        }

        void Settle()
        {
            var (kept, gold) = Loot.Resolve(Bag, _meta.Wallet, _meta.Pricing, Player.Combatant.Stats);
            long clearBonus = (long)Mathf.Round(_rules.FloorClearGoldPerLevel * Plan.Level * (Plan.HasBoss ? 3f : 1f));
            _meta.Wallet.Earn(clearBonus);
            Log?.Invoke($"가방에 {kept}개 · 자동 판매 +{gold:N0}G · 클리어 +{clearBonus:N0}G");

            if (Plan.HasBoss) _meta.UnlockNextDungeon(_meta.Content.Acts.GlobalDungeon(_meta.Profile.Progress));
            _meta.Capture(Player);
            _meta.RequestSave();
        }

        // ---------- 층 진행 ----------

        void StartFloor()
        {
            World.ClearEffects();
            Plan = _meta.Content.Acts.Plan(_meta.Profile.Progress);
            Layout = _layouts.Build(Plan, _seed + Plan.ActIndex * 1000 + Plan.DungeonIndex * 50 + Plan.Floor);
            Loot = new FloorLoot(_rules.MaxPicksPerFloor);

            Player.Combatant.Position = Layout.StartPosition;
            if (Pet != null) Pet.SnapTo(Layout.StartPosition);
            World.SetNavigation(new GridNavigator(Layout.Map, () => World.Time));
            Spawner.Begin(Plan, Layout, _meta.Content.EnemiesFor(Act), _meta.Content.Elites, _meta.Content.Boss);

            Player.Context.Destination = null;
            _floorTime = 0f;
            _floorXp = 0f;
            State = FloorState.Fighting;

            Log?.Invoke($"{DungeonName} {Plan.Floor}층 [{Layout.Name}] — 몬스터 {Plan.TotalKills}");
            FloorStarted?.Invoke(Plan);
        }

        void BeginClearing()
        {
            State = FloorState.Clearing;
            _timer = _rules.ClearSlowMoSeconds;
            World.Emit(VfxEvent.At(VfxKind.Nova, Player.Combatant.Position, 4f, DamageType.Light, 0.8f));
        }

        void EnterResult()
        {
            State = FloorState.Result;
            Loot.AutoSelect(_meta.Scorer, Bag);
            Summary = new FloorSummary
            {
                Plan = Plan,
                LayoutName = Layout.Name,
                Kills = Spawner.Killed,
                Seconds = _floorTime,
                Experience = _floorXp,
                ItemsFound = Loot.Found.Count
            };
            FloorResultReady?.Invoke(Summary);
        }

        void OnCombatantDied(Combatant combatant)
        {
            if (combatant == Player.Combatant)
            {
                var lost = Bag.LoseHalf(_random);
                Loot.Clear();
                State = FloorState.Dead;
                _timer = _rules.DeathReturnDelay;
                Log?.Invoke($"<color=#FF5A5A>쓰러졌습니다 — 가방 아이템 {lost.Count}개를 잃었습니다</color>");
                _meta.Capture(Player);
                _meta.RequestSave();
                return;
            }
            if (combatant.Team != Team.Enemy || State != FloorState.Fighting) return;

            Spawner.RegisterKill();
            float xp = 4f * Plan.Level * (combatant.Rank == EnemyRank.Boss ? 25f : combatant.Rank == EnemyRank.Elite ? 6f : 1f);
            _floorXp += xp;
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
