using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Character;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Core.Growth;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.Tuning;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Game
{
    public enum FloorState
    {
        Fighting,
        /// <summary>마지막 몬스터 처치 직후 슬로우 모션.</summary>
        Clearing,
        /// <summary>결과창: 가져갈 장비 선택 + 계속/귀환 결정 (보스 층은 귀환만).</summary>
        Result,
        /// <summary>사망창: 잃을 아이템 + 사망 원인 → 광고 회수 / 그대로 귀환.</summary>
        Dead
    }

    /// <summary>층 결과 요약.</summary>
    public sealed class FloorSummary
    {
        public FloorPlan Plan;
        public string LayoutName;
        public int Kills;
        public float Seconds;
        public float Experience;
        public long Gold;
        public int ItemsFound;
        public bool IsBossFloor;
    }

    /// <summary>
    /// 던전 한 판 = 10층 (방향성 v1 §1~4).
    /// 층의 몬스터를 다 잡으면 슬로우 모션 → 결과창 (가방 칸에 맞춰 고르고 나머지 자동 판매).
    /// 결과창: 계속 / 마을 귀환 (옵션: 5초 뒤 자동 계속). 10층 보스 → 자동 귀환 + 다음 던전 개방.
    /// 사망: 가방 절반 손실 → 사망창에서 광고를 보면 전부 회수 (횟수 제한 없음).
    /// </summary>
    public sealed class DungeonSession : ICombatSession
    {
        readonly GameMeta _meta;
        readonly DungeonTuning _rules;
        readonly IRandom _random;
        readonly IFloorLayoutSelector _layouts;
        readonly int _seed;
        readonly DamageLedger _ledger;
        float _timer;
        float _floorTime;
        float _floorXp;
        long _floorGold;
        bool _returnRaised;
        int _floorsWithoutReturn;
        bool _diedThisRun;
        List<Item> _lostItems = new List<Item>();

        public DungeonSession(GameMeta meta, BalanceSettings balance, DungeonTuning rules, IRandom random, IFloorLayoutSelector layouts, int seed)
        {
            _meta = meta;
            _rules = rules ?? meta.Tuning.Dungeon;
            _random = random;
            _layouts = layouts;
            _seed = seed;

            World = CombatSetup.CreateWorld(balance, random, new Rect(-40f, -40f, 80f, 80f));
            Player = meta.CreatePlayer(World);
            Player.AutoMove = meta.Settings.AutoMove;
            World.Add(Player.Combatant);
            Pet = meta.CreatePet(Player, World);
            _ledger = new DamageLedger(World.Damage, Player.Combatant);

            Spawner = new FloorSpawner(World, new EnemyFactory(meta.Tuning.Monster), random, _rules);
            Spawner.BossSpawned += boss => Log?.Invoke($"<color=#FF6A4A>{boss.Combatant.Name}이(가) 가장 깊은 방에서 기다립니다</color>");
            World.CombatantDied += OnCombatantDied;
            Player.Progress.LeveledUp += level => Log?.Invoke($"레벨 업! Lv.{level} — 스탯 포인트 +{meta.Tuning.Growth.StatPointsPerLevel}");
            if (Pet != null) Pet.SkillUsed += skill => World.Emit(new VfxEvent { Kind = VfxKind.Text, Position = Pet.Position, Text = skill.Name, Duration = 0.8f });
            if (Player.Rhythm != null) Player.Rhythm.Judged += OnRhythmJudged;
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
        public DeathReport DeathReport { get; private set; }
        public IReadOnlyList<Item> LostItems => _lostItems;
        public Inventory Bag => Player.Inventory;
        public bool IsPlayerDead => !Player.Combatant.IsAlive;
        public float RespawnRemaining => 0f;
        public float TimeScale => State == FloorState.Clearing ? _rules.SlowMoScale : 1f;
        public int FloorsPerDungeon => _meta.Content.Acts.FloorsPerDungeon;
        public bool IsBossFloor => Plan.HasBoss;
        /// <summary>결과창 자동 진행까지 남은 시간 (꺼져 있으면 −1).</summary>
        public float AutoContinueRemaining => State == FloorState.Result && AutoContinueEnabled ? Mathf.Max(0f, _timer) : -1f;
        bool AutoContinueEnabled => _meta.Settings.AutoContinue || Plan.HasBoss;

        public ActDefinition Act => _meta.Content.Acts.Acts[Mathf.Clamp(Plan.ActIndex, 0, _meta.Content.Acts.Acts.Count - 1)];
        public string DungeonName => _meta.Content.Acts.DungeonName(Plan.ActIndex, Plan.DungeonIndex);

        public event Action<string> Log;
        public event Action<FloorPlan> FloorStarted;
        public event Action<FloorSummary> FloorResultReady;
        public event Action<DeathReport> PlayerDied;
        /// <summary>마을로 돌아가야 할 때 (true = 사망).</summary>
        public event Action<bool> ReturnToTownRequested;
        public event Action<Item> ItemFound;
        public event Action<Combatant> EnemyKilled;

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
                    if (!AutoContinueEnabled) break;
                    _timer -= dt;
                    if (_timer > 0f) break;
                    if (Plan.HasBoss) ReturnToTown();
                    else ContinueToNextFloor();
                    break;

                case FloorState.Dead:
                    World.Tick(dt);
                    break;
            }
        }

        // ---------- 결과창 ----------

        public bool ToggleLoot(Item item) => State == FloorState.Result && Loot.Toggle(item, Bag);

        public bool CanTake(Item item) => Loot.IsSelected(item) || Loot.CanAlsoTake(item, Bag);

        public void AutoSelectLoot() => Loot.AutoSelect(_meta.Scorer, Bag);

        public long PreviewSaleGold => Loot.PreviewSaleGold(_meta.Pricing, Player.Combatant.Stats);

        /// <summary>던전 안 가방 정리: 뺀 아이템은 바로 자동 판매.</summary>
        public long SellFromBag(Item item) => _meta.Sell(item, Player);

        /// <summary>결과창 자동 진행 멈춤 (플레이어가 고르는 중).</summary>
        public void HoldAutoContinue()
        {
            if (State == FloorState.Result && !Plan.HasBoss) _timer = float.PositiveInfinity;
        }

        /// <summary>다음 층으로 계속 (가방 안 아이템은 계속 위험에 노출).</summary>
        public void ContinueToNextFloor()
        {
            if (State != FloorState.Result || Plan.HasBoss) return;
            Settle();
            _floorsWithoutReturn++;
            _meta.Quests.ReportMax("streak", _floorsWithoutReturn);
            _meta.Profile.Progress.Advance(_meta.Content.Acts);
            _meta.RequestSave();
            StartFloor();
        }

        /// <summary>마을로 귀환 (가방 안 아이템 안전 확보). 보스 층이면 다음 던전 개방.</summary>
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
            _meta.NoteSale(gold);
            long clearBonus = (long)Mathf.Round(_rules.FloorClearGoldPerLevel * Plan.Level * (Plan.HasBoss ? _rules.BossClearGoldMultiplier : 1f));
            _meta.Wallet.Earn(clearBonus);
            Log?.Invoke($"가방에 {kept}개 · 자동 판매 +{gold:N0}G · 클리어 +{clearBonus:N0}G");

            if (Plan.HasBoss)
            {
                _meta.UnlockNextDungeon(_meta.Content.Acts.GlobalDungeon(_meta.Profile.Progress));
                if (!_diedThisRun) _meta.Quests.Report("deathless");
            }
            _meta.Capture(Player);
            _meta.RequestSave();
        }

        // ---------- 사망창 ----------

        /// <summary>보상형 광고 시청 완료 → 잃은 아이템 전부 회수 후 귀환 (횟수 제한 없음).</summary>
        public int RecoverWithAd()
        {
            if (State != FloorState.Dead || _returnRaised) return 0;
            int restored = 0;
            foreach (var item in _lostItems)
                if (Bag.TryAdd(item) || _meta.Profile.Stash.TryAdd(item)) restored++;
            _lostItems.Clear();
            Log?.Invoke($"광고 보상: 아이템 {restored}개 회수");
            ReturnAfterDeath();
            return restored;
        }

        /// <summary>그대로 귀환 (잃은 아이템 포기).</summary>
        public void ReturnAfterDeath()
        {
            if (State != FloorState.Dead || _returnRaised) return;
            _lostItems.Clear();
            _returnRaised = true;
            _meta.Capture(Player);
            _meta.RequestSave();
            ReturnToTownRequested?.Invoke(true);
        }

        // ---------- 층 진행 ----------

        void StartFloor()
        {
            World.ClearEffects();
            Plan = _meta.Content.Acts.Plan(_meta.Profile.Progress);
            Layout = _layouts.Build(Plan, _seed + Plan.ActIndex * 1000 + Plan.DungeonIndex * 50 + Plan.Floor);
            Loot = new FloorLoot(_meta.Tuning.Bag.MaxPicksPerFloor);
            _ledger.Reset();

            Player.Combatant.Position = Layout.StartPosition;
            if (Pet != null) Pet.SnapTo(Layout.StartPosition);
            World.SetNavigation(new GridNavigator(Layout.Map, () => World.Time));
            Spawner.Begin(Plan, Layout, _meta.Content.EnemiesFor(Act), _meta.Content.Elites, _meta.Content.Boss);

            Player.Context.Destination = null;
            _floorTime = 0f;
            _floorXp = 0f;
            _floorGold = 0;
            State = FloorState.Fighting;

            _meta.Quests.ReportMax("depth", Plan.Floor);
            _meta.State.Lifetime.DeepestFloor = Mathf.Max(_meta.State.Lifetime.DeepestFloor, _meta.Profile.Progress.GlobalFloor(_meta.Content.Acts) + 1);
            Log?.Invoke($"{DungeonName} {Plan.Floor}/{FloorsPerDungeon}층 — 몬스터 {Plan.TotalKills}");
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
            _timer = _rules.AutoContinueSeconds;
            Loot.AutoSelect(_meta.Scorer, Bag);
            Summary = new FloorSummary
            {
                Plan = Plan,
                LayoutName = Layout.Name,
                Kills = Spawner.Killed,
                Seconds = _floorTime,
                Experience = _floorXp,
                Gold = _floorGold,
                ItemsFound = Loot.Found.Count,
                IsBossFloor = Plan.HasBoss
            };
            FloorResultReady?.Invoke(Summary);
        }

        void OnCombatantDied(Combatant combatant)
        {
            if (combatant == Player.Combatant)
            {
                DeathReport = _ledger.Build(Player.Combatant.Stats);
                _lostItems = LoseFraction(_meta.Tuning.Bag.DeathLossFraction);
                Loot.Clear();
                State = FloorState.Dead;
                _diedThisRun = true;
                _meta.State.Lifetime.Deaths++;
                Log?.Invoke($"<color=#FF5A5A>쓰러졌습니다 — 가방 아이템 {_lostItems.Count}개를 잃었습니다</color>");
                _meta.Capture(Player);
                _meta.RequestSave();
                PlayerDied?.Invoke(DeathReport);
                return;
            }
            if (combatant.Team != Team.Enemy || State != FloorState.Fighting) return;

            Spawner.RegisterKill();
            var m = _meta.Tuning.Monster;
            float xp = m.XpPerLevel * Plan.Level * (combatant.Rank == EnemyRank.Boss ? m.BossXp : combatant.Rank == EnemyRank.Elite ? m.EliteXp : 1f);
            _floorXp += xp;
            Player.Progress.Gain(xp);
            CountKill(combatant);
            EnemyKilled?.Invoke(combatant);

            var drop = _meta.Tuning.Drop;
            int drops = combatant.Rank == EnemyRank.Boss ? drop.BossDrops : combatant.Rank == EnemyRank.Elite ? drop.EliteDrops : _random.Chance(drop.NormalDropChancePct) ? 1 : 0;
            for (int i = 0; i < drops; i++)
            {
                var item = _meta.Loot.Generate(new DropContext(Plan.ActIndex, Plan.DungeonIndex, Plan.Floor, combatant.Rank));
                Loot.Add(item);
                if (item.Rarity >= ItemRarity.Legendary) _meta.State.Lifetime.LegendariesFound++;
                World.Emit(new VfxEvent { Kind = VfxKind.Text, Position = combatant.Position, Text = item.Rarity.DisplayName(), Duration = 1f });
                ItemFound?.Invoke(item);
            }
        }

        void CountKill(Combatant combatant)
        {
            var life = _meta.State.Lifetime;
            life.Kills++;
            _meta.Quests.Report("kill");
            if (combatant.Rank == EnemyRank.Elite)
            {
                life.EliteKills++;
                _meta.Quests.Report("kill_elite");
            }
            if (combatant.Rank == EnemyRank.Boss)
            {
                life.BossKills++;
                _meta.Quests.Report("kill_boss");
            }
        }

        List<Item> LoseFraction(float fraction)
        {
            if (fraction >= 0.499f && fraction <= 0.501f) return Bag.LoseHalf(_random);
            var items = Bag.Items.ToList();
            int count = Mathf.RoundToInt(items.Count * Mathf.Clamp01(fraction));
            var lost = new List<Item>();
            for (int i = 0; i < count && items.Count > 0; i++)
            {
                int pick = _random.Range(0, items.Count);
                lost.Add(items[pick]);
                Bag.Remove(items[pick]);
                items.RemoveAt(pick);
            }
            return lost;
        }

        void OnRhythmJudged(Rhythm.RhythmJudgement judgement, int hits)
        {
            if (judgement == Rhythm.RhythmJudgement.Miss) return;
            var life = _meta.State.Lifetime;
            life.RhythmHits++;
            life.BestRhythmCombo = Math.Max(life.BestRhythmCombo, Player.Rhythm.Combo);
            _meta.Quests.Report("rhythm");
            _meta.Quests.ReportMax("rhythm_combo", Player.Rhythm.Combo);
        }

        void OnTreeChanged() => _meta.TreeService.ApplyTo(Player.Combatant);

        public void Dispose()
        {
            _meta.TreeService.Changed -= OnTreeChanged;
            _ledger.Dispose();
            _meta.Capture(Player);
        }
    }
}
