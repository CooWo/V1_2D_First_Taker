using System;
using System.Collections.Generic;
using UnityEngine;

namespace LootHunters
{
    public enum RunState { Town, InDungeon, Returning }

    public enum FxKind { MonsterHit, Crit, HeroHit, Heal, Kill, LevelUp, Skill, Gold, Shielded, Alert, Stairs, FloorClear }

    /// <summary>연출 한 건. pos는 월드 좌표, value는 숫자(피해량 등), text는 표시할 글자.</summary>
    public struct FxEvent
    {
        public FxKind kind;
        public Vector2 pos;
        public int value;
        public string text;
    }

    /// <summary>
    /// 게임 진행의 중심. 실시간 던전(맵 이동, 자동/수동 전투, 귀환/사망)과
    /// 마을 행동(장착, 판매, 강화)을 모두 여기서 호출합니다. UI는 이 클래스만 바라봅니다.
    /// </summary>
    public class DungeonRun : MonoBehaviour
    {
        [Tooltip("비워 두면 SampleData의 예제 던전들을 자동 생성합니다.")]
        [SerializeField] List<DungeonData> dungeons = new List<DungeonData>();
        [SerializeField] Stats heroBaseStats = new Stats(60, 6, 2);
        [Header("실시간 이동 / 전투 (4단계)")]
        [Tooltip("영웅 이동 속도 (초당 칸)")]
        [SerializeField] float heroMoveSpeed = 4f;
        [Tooltip("영웅 공격 간격(초)")]
        [SerializeField] float heroAttackInterval = 0.6f;
        [Tooltip("근접 공격 거리 (칸)")]
        [SerializeField] float attackRange = 1.1f;
        [Tooltip("몬스터가 영웅을 알아채는 거리 (칸)")]
        [SerializeField] float aggroRange = 6f;
        [Tooltip("시야 반경 (칸). 지도 밝히기와 두루마리 사거리")]
        [SerializeField] float sightRadius = 7f;
        [Tooltip("귀환 주문 시전 시간(초)")]
        [SerializeField] float returnCastTime = 3f;
        [Tooltip("전체 진행 속도 배율. 이동, 공격, 귀환이 이만큼 빨라집니다.")]
        [SerializeField] float pace = 1.6f;
        [Tooltip("몬스터가 벽 너머로도 영웅을 찾아오는 거리 (걸음 수)")]
        [SerializeField] int huntSteps = 14;
        [Tooltip("전투 소리를 듣고 몬스터가 깨어나는 거리 (걸음 수)")]
        [SerializeField] int noiseSteps = 9;
        [Tooltip("층에 들어오고 이 시간(초)이 지나면 남은 몬스터가 모두 영웅을 찾아옴")]
        [SerializeField] float allHuntAfter = 20f;
        [Header("몬스터 무리 (층마다)")]
        [Tooltip("층의 몬스터 수 범위. 층에 들어갈 때 이 사이에서 정해집니다.")]
        [SerializeField] int populationMin = 40;
        [SerializeField] int populationMax = 60;
        [Tooltip("살아 있는 몬스터가 이 수 이하가 되면 새 무리가 나타나 영웅을 찾아옵니다.")]
        [SerializeField] int respawnAtOrBelow = 5;
        [Tooltip("한 층의 무리 수 (첫 무리 포함). 마지막 무리까지 잡으면 층 정리.")]
        [SerializeField] int wavesPerFloor = 3;
        [Tooltip("무리 몬스터 한 마리의 HP / 공격력 / 보상 배율 (많이 나오는 대신 약하게)")]
        [SerializeField] float hordeHp = 0.55f;
        [SerializeField] float hordeAttack = 0.45f;
        [SerializeField] float hordeReward = 0.35f;
        [Tooltip("몬스터 한 마리가 아이템을 떨어뜨릴 확률 (드랍 테이블 굴리기 전)")]
        [Range(0f, 1f)] [SerializeField] float lootChance = 0.15f;
        [SerializeField] ConsumableSettings consumables = new ConsumableSettings();

        [Header("저장 / 방치 보상")]
        [SerializeField] bool enableSave = true;
        [Tooltip("오프라인 상태에서 몬스터 한 마리 잡는 데 걸린다고 보는 시간(초)")]
        [SerializeField] float offlineSecondsPerKill = 20f;
        [Tooltip("오프라인 보상 최대 인정 시간(시간)")]
        [SerializeField] float offlineMaxHours = 8f;
        [Tooltip("오프라인 보상 중 아이템 드랍을 굴리는 최대 횟수 (창고가 넘치지 않게)")]
        [SerializeField] int offlineItemRollLimit = 20;
        [Tooltip("이 시간(초)보다 짧게 껐다 켜면 보상 없음")]
        [SerializeField] float offlineMinSeconds = 60f;

        public Hero Hero { get; private set; }
        public IReadOnlyList<DungeonData> Dungeons => dungeons;
        public int SelectedIndex { get; private set; }
        public DungeonData Dungeon => dungeons[SelectedIndex];
        public RunState State { get; private set; } = RunState.Town;
        public int Depth { get; private set; }
        public Combatant CurrentMonster { get; private set; }
        public ConsumableSettings Consumables => consumables;

        /// <summary>로그 한 줄이 추가될 때.</summary>
        public event Action<string> OnLog;
        /// <summary>골드/아이템/상태 등 화면을 다시 그려야 할 변화가 생겼을 때.</summary>
        public event Action OnChanged;
        /// <summary>데미지 숫자, 효과음, 화면 흔들림 같은 연출용 신호.</summary>
        public event Action<FxEvent> OnFx;

        int[] bestFloor;
        string offlineReport;
        Dictionary<string, ItemData> itemRegistry;

        void Awake()
        {
            dungeons.RemoveAll(d => d == null);
            if (dungeons.Count == 0) dungeons = SampleData.CreateDungeons();
            itemRegistry = SaveSystem.BuildItemRegistry(dungeons);
            NewGame();
        }

        /// <summary>저장 불러오기는 Start에서 합니다 (GameUI가 먼저 이벤트를 구독할 수 있도록).</summary>
        void Start()
        {
            if (enableSave) LoadGame();
        }

        void NewGame()
        {
            bestFloor = new int[dungeons.Count];
            SelectedIndex = 0;
            Hero = new Hero("Hero", heroBaseStats);
            Hero.weapon = new ItemInstance(SampleData.StarterWeapon);
            Hero.potions = 2;
            Hero.RefreshStats(fullHeal: true);
        }

        // ───────── 던전 선택 ─────────

        public bool IsUnlocked(int index) => Hero.Power >= dungeons[index].requiredPower;

        /// <summary>해당 던전에서 도달한 가장 깊은 층 (1부터, 0은 미입장).</summary>
        public int BestFloor(int index) => bestFloor[index];

        public void SelectDungeon(int delta)
        {
            if (State != RunState.Town) return;
            SelectedIndex = (SelectedIndex + delta + dungeons.Count) % dungeons.Count;
            Changed();
        }

        // ───────── 실시간 던전 ─────────

        public DungeonMap Map { get; private set; }
        public readonly List<MapMonster> Monsters = new List<MapMonster>();
        public Vector2 HeroPos { get; private set; }
        public Vector2 HeroFacing { get; private set; } = Vector2.right;
        /// <summary>true면 영웅이 알아서 이동/사냥, false면 키보드로 직접 이동.</summary>
        public bool AutoMode { get; private set; } = true;
        /// <summary>층이 새로 만들어지거나 마을로 돌아올 때마다 1씩 증가 (화면이 맵을 다시 그리는 신호).</summary>
        public int MapVersion { get; private set; }
        public float ReturnProgress => State == RunState.Returning ? Mathf.Clamp01(returnTimer / ReturnCastTime) : 0f;
        float ReturnCastTime => returnCastTime / Mathf.Max(0.1f, pace);
        public float SightRadius => sightRadius;
        public int AliveMonsters
        {
            get
            {
                int n = 0;
                foreach (var m in Monsters) if (!m.IsDead) n++;
                return n;
            }
        }
        /// <summary>현재 무리 번호 (1부터)와 층의 무리 수.</summary>
        public int Wave { get; private set; }
        public int WavesPerFloor => Mathf.Max(1, wavesPerFloor);
        public int Population { get; private set; }
        public int FloorKills { get; private set; }
        /// <summary>마지막 무리까지 다 잡았는지.</summary>
        public bool FloorCleared => Wave >= WavesPerFloor && AliveMonsters == 0;

        readonly System.Random rng = new System.Random();
        Vector2 moveInput;
        float heroAttackTimer;
        float returnTimer;
        Vector2Int heroTile = new Vector2Int(-999, -999);
        int[,] heroField;      // 영웅 칸까지의 거리 (몬스터 추적용)
        int[,] goalField;      // 자동 이동 목표 칸까지의 거리 (영웅 길찾기용)
        Vector2Int goalTile = new Vector2Int(-999, -999);
        // 끼임 감지: 움직이려는데 위치가 거의 안 바뀌면 칸 중앙으로 빠져나옴
        Vector2 stuckCheckPos;
        float stuckCheckTimer;
        float unstickTimer;

        float floorTime;                                    // 이번 층에 들어온 뒤 지난 시간
        float clearTimer = -1f;                             // 층 클리어 슬로우 모션 남은 실제 시간 (-1 = 없음)
        int floorStartGold, floorStartItems, floorExp;

        [Header("층 클리어 연출")]
        [SerializeField] float clearSlowMoSeconds = 1.3f;
        [Range(0.05f, 1f)] [SerializeField] float clearSlowMoScale = 0.2f;
        [Tooltip("결과창에서 아무것도 안 누르면 이 시간(초) 뒤 자동으로 다음 층. 0이면 자동 진행 안 함.")]
        [SerializeField] float autoContinueSeconds = 10f;

        /// <summary>결과창 자동 진행까지 남은 실제 시간(초).</summary>
        public float ResultCountdown { get; private set; }
        public bool AutoContinueEnabled => autoContinueSeconds > 0f;

        /// <summary>층 결과창에 보여 줄 내용.</summary>
        public class FloorResultInfo
        {
            public int floor, kills, gold, items, exp, totalGold, totalItems;
            public float seconds;
        }

        /// <summary>true면 결과창을 띄우고 플레이어의 선택(다음 층 / 복귀)을 기다리는 중.</summary>
        public bool FloorResultPending { get; private set; }
        public FloorResultInfo FloorResult { get; private set; }
        /// <summary>마지막 몹을 잡고 슬로우 모션 중인지.</summary>
        public bool ClearingFloor => clearTimer >= 0f;
        readonly float[] skillCd = new float[SkillDef.All.Length];
        float frenzyTimer, guardTimer;

        // ───────── 게임 속도 / 스킬 / 스탯 (UI가 읽는 값) ─────────

        /// <summary>게임 배속 (1, 2, 3). Time.timeScale로 적용됩니다.</summary>
        public int GameSpeed { get; private set; } = 1;
        public bool FrenzyActive => frenzyTimer > 0f;
        public bool GuardActive => guardTimer > 0f && Hero.Combatant.Shield > 0;

        public void CycleSpeed()
        {
            GameSpeed = GameSpeed >= 3 ? 1 : GameSpeed + 1;
            if (!ClearingFloor) Time.timeScale = GameSpeed;   // 클리어 슬로우 모션 중에는 끝난 뒤 적용
            Log($"게임 속도 x{GameSpeed}");
            OnChanged?.Invoke();
        }

        public bool SkillUnlocked(int i) => Hero.progress.level >= SkillDef.All[i].unlockLevel;
        public float SkillCooldown(int i) => skillCd[i];
        /// <summary>쿨타임 남은 비율 (1 = 방금 씀, 0 = 사용 가능).</summary>
        public float SkillCooldownRatio(int i) => skillCd[i] <= 0f ? 0f : skillCd[i] / SkillDef.All[i].cooldown;

        public bool CanUseSkill(int i)
        {
            if (State != RunState.InDungeon || Map == null || Hero.Combatant.IsDead) return false;
            if (!SkillUnlocked(i) || skillCd[i] > 0f) return false;
            switch ((SkillId)i)
            {
                case SkillId.Whirlwind: return CountInRange(WhirlwindRadius) > 0;
                case SkillId.Dash: return DashTarget() != null;
                default: return true;
            }
        }

        /// <summary>스탯 포인트 1개를 분배. 던전 안에서도 바로 쓸 수 있습니다.</summary>
        public void AddStat(StatKind kind)
        {
            int beforeMax = Hero.Combatant.Stats.maxHp;
            if (!Hero.progress.Spend(kind)) return;
            Hero.RefreshStats(fullHeal: false);
            int gain = Hero.Combatant.Stats.maxHp - beforeMax;
            if (gain > 0) Hero.Combatant.Heal(gain);
            Log($"{HeroProgress.Name(kind)} +1  ({HeroProgress.Effect(kind)})  남은 포인트 {Hero.progress.points}");
            Changed();
        }

        const float HeroRadius = 0.3f;
        const float MonsterRadius = 0.3f;

        public void EnterDungeon()
        {
            if (State != RunState.Town) return;
            if (!IsUnlocked(SelectedIndex))
            {
                Log($"전투력이 부족합니다. (필요 {Dungeon.requiredPower}, 현재 {Hero.Power})");
                return;
            }

            Hero.RefreshStats(fullHeal: true);
            Hero.Combatant.Shield = 0;
            frenzyTimer = guardTimer = 0f;
            for (int i = 0; i < skillCd.Length; i++) skillCd[i] = 0f;
            Depth = 0;
            State = RunState.InDungeon;
            Log($"=== {Dungeon.dungeonName} 입장 ===  ({(AutoMode ? "자동" : "수동")} 모드, Tab으로 전환)");
            BuildFloor();
        }

        void BuildFloor()
        {
            var floor = Dungeon.GetFloor(Depth);
            float mult = Dungeon.GetMultiplier(Depth);
            Map = DungeonMap.Generate(Dungeon, rng);
            Monsters.Clear();

            HeroPos = Map.Start;
            Population = rng.Next(Mathf.Min(populationMin, populationMax), Mathf.Max(populationMin, populationMax) + 1);
            Wave = 1;
            FloorKills = 0;
            // 첫 무리: 시작 방 밖에 흩어져 있다가, 가까운 것부터 냄새를 맡고 찾아옴
            SpawnMonsters(Population, minDistance: 7f, awake: false);

            heroTile = new Vector2Int(-999, -999);
            goalTile = new Vector2Int(-999, -999);
            goalField = null;
            stuckCheckPos = HeroPos;
            stuckCheckTimer = unstickTimer = 0f;
            floorTime = 0f;
            clearTimer = -1f;
            FloorResultPending = false;
            floorStartGold = Hero.carriedGold;
            floorStartItems = Hero.carriedLoot.Count;
            floorExp = 0;
            heroAttackTimer = 0f;
            CurrentMonster = null;
            UpdateHeroTile();

            bestFloor[SelectedIndex] = Mathf.Max(bestFloor[SelectedIndex], Depth + 1);
            MapVersion++;
            Log($"-- {Depth + 1}층 ({floor.floorName}) 몬스터 {Population}마리 x {WavesPerFloor}무리, 배율 x{mult:0.00} --");
            Changed();
        }

        /// <summary>귀환 주문 시전 시작. returnCastTime초 버티면 마을로 돌아갑니다.</summary>
        public void RequestReturn()
        {
            if (State != RunState.InDungeon) return;
            State = RunState.Returning;
            returnTimer = 0f;
            Log($"귀환 주문 시전 중... ({ReturnCastTime:0.0}초)");
            Changed();
        }

        // ───────── 조작 (PlayerController가 호출) ─────────

        /// <summary>키보드 이동 입력. 방향키를 누르면 자동으로 수동 모드가 됩니다.</summary>
        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
            if (input != Vector2.zero && AutoMode && State != RunState.Town)
            {
                AutoMode = false;
                Log("수동 조작으로 전환 (Tab: 자동)");
                Changed();
            }
        }

        public void ToggleAuto()
        {
            AutoMode = !AutoMode;
            Log(AutoMode ? "자동 전투 켜짐" : "자동 전투 꺼짐 (WASD/방향키로 이동)");
            Changed();
        }

        // ───────── 매 프레임 시뮬레이션 ─────────

        void Update()
        {
            if (State == RunState.Town || Map == null || FloorResultPending) return;
            float dt = Time.deltaTime;
            floorTime += dt;

            // 스킬 쿨타임 / 버프 시간
            for (int i = 0; i < skillCd.Length; i++) if (skillCd[i] > 0f) skillCd[i] -= dt;
            if (frenzyTimer > 0f) frenzyTimer -= dt;
            if (guardTimer > 0f)
            {
                guardTimer -= dt;
                if (guardTimer <= 0f) Hero.Combatant.Shield = 0;
            }
            if (AutoMode && State == RunState.InDungeon) AutoSkills();

            MoveHero(dt);
            HeroAttack(dt);
            MonstersAct(dt);
            if (State == RunState.Town) return;   // (안전장치)

            var hc = Hero.Combatant;
            if (consumables.autoPotion && Hero.potions > 0 && !hc.IsDead &&
                hc.CurrentHp <= hc.Stats.maxHp * consumables.autoPotionHpRatio)
                DrinkPotion(auto: true);

            if (hc.IsDead) { Die(); return; }

            if (State == RunState.Returning)
            {
                returnTimer += dt;
                if (returnTimer >= ReturnCastTime) { ReturnToTown(); return; }
            }

            // 계단: 수동이면 밟는 즉시, 자동이면 몬스터를 다 잡은 뒤 내려감
            // 무리가 거의 다 잡히면 다음 무리가 맵 가장자리에서 나타나 영웅을 찾아옴
            if (State == RunState.InDungeon && Wave < WavesPerFloor && AliveMonsters <= respawnAtOrBelow)
            {
                Wave++;
                Monsters.RemoveAll(x => x.IsDead);
                int add = Mathf.Max(0, Population - AliveMonsters);
                SpawnMonsters(add, minDistance: 9f, awake: true);
                Fx(FxKind.Alert, HeroPos, Wave, "!");
                Log($"<color=#FF8080>몬스터 무리가 몰려옵니다! ({Wave}/{WavesPerFloor}무리, {add}마리)</color>");
                Changed();
            }

            // 마지막 몹까지 잡으면: 잠깐 슬로우 모션 → 결과창 (계단 없음)
            if (State == RunState.InDungeon && FloorCleared && clearTimer < 0f)
            {
                clearTimer = clearSlowMoSeconds;
                Time.timeScale = clearSlowMoScale;
                Fx(FxKind.FloorClear, HeroPos, Depth + 1, $"{Depth + 1}층 클리어!");
                Log($"<color=#F2CC4D>{Depth + 1}층 클리어!</color>");
            }
        }

        /// <summary>슬로우 모션 시간을 실제 시간으로 세다가 끝나면 결과창을 띄웁니다.</summary>
        void LateUpdate()
        {
            // 결과창: autoContinueSeconds 동안 고르지 않으면 자동으로 다음 층
            if (FloorResultPending)
            {
                if (autoContinueSeconds > 0f)
                {
                    ResultCountdown -= Time.unscaledDeltaTime;
                    if (ResultCountdown <= 0f) ContinueNextFloor();
                }
                return;
            }
            if (clearTimer < 0f) return;
            clearTimer -= Time.unscaledDeltaTime;
            if (clearTimer > 0f) return;

            Time.timeScale = GameSpeed;
            FloorResult = new FloorResultInfo
            {
                floor = Depth + 1,
                kills = FloorKills,
                gold = Hero.carriedGold - floorStartGold,
                items = Hero.carriedLoot.Count - floorStartItems,
                exp = floorExp,
                seconds = floorTime,
                totalGold = Hero.carriedGold,
                totalItems = Hero.carriedLoot.Count,
            };
            FloorResultPending = true;
            ResultCountdown = autoContinueSeconds;
            Changed();
        }

        /// <summary>결과창에서 다음 층으로.</summary>
        public void ContinueNextFloor()
        {
            if (!FloorResultPending) return;
            FloorResultPending = false;
            clearTimer = -1f;
            Depth++;
            BuildFloor();
        }

        /// <summary>결과창에서 마을로 복귀 (귀환 주문 없이 전리품 모두 보관).</summary>
        public void ReturnFromResult()
        {
            if (!FloorResultPending) return;
            FloorResultPending = false;
            clearTimer = -1f;
            ReturnToTown();
        }

        void MoveHero(float dt)
        {
            Vector2 dir;
            if (AutoMode) dir = State == RunState.Returning ? Vector2.zero : AutoDirection();
            else dir = moveInput;

            if (dir.sqrMagnitude > 0.0001f)
            {
                HeroFacing = dir;
                float speed = heroMoveSpeed * pace * (FrenzyActive ? 1.3f : 1f);
                HeroPos = Map.Move(HeroPos, dir.normalized * Mathf.Min(1f, dir.magnitude) * speed * dt, HeroRadius);
            }
            UpdateHeroTile();
            if (AutoMode) CheckStuck(dir, dt);
        }

        /// <summary>자동 이동 중 0.5초 동안 거의 못 움직였으면, 잠깐 칸 중앙으로 되돌아가 다시 길을 잡습니다.</summary>
        void CheckStuck(Vector2 dir, float dt)
        {
            if (unstickTimer > 0f) unstickTimer -= dt;
            if (dir.sqrMagnitude < 0.0001f) { stuckCheckTimer = 0f; stuckCheckPos = HeroPos; return; }

            stuckCheckTimer += dt;
            if (stuckCheckTimer < 0.5f) return;
            if (Vector2.Distance(HeroPos, stuckCheckPos) < heroMoveSpeed * pace * 0.5f * 0.15f) unstickTimer = 0.35f;
            stuckCheckTimer = 0f;
            stuckCheckPos = HeroPos;
        }

        void UpdateHeroTile()
        {
            var t = DungeonMap.TileOf(HeroPos);
            if (t == heroTile) return;
            heroTile = t;
            heroField = Map.DistanceField(heroTile);
            Map.Reveal(HeroPos, sightRadius);
        }

        /// <summary>자동 모드: 가장 가까운(걸어서) 몬스터 → 다 잡으면 계단으로.</summary>
        Vector2 AutoDirection()
        {
            MapMonster target = null;
            int best = int.MaxValue;
            foreach (var m in Monsters)
            {
                if (m.IsDead) continue;
                var t = DungeonMap.TileOf(m.Pos);
                int d = heroField[t.x, t.y];
                if (d >= 0 && d < best) { best = d; target = m; }
            }

            Vector2 goal;
            if (target != null)
            {
                float dist = Vector2.Distance(HeroPos, target.Pos);
                if (dist <= attackRange * 0.85f && Map.LineClear(HeroPos, target.Pos)) return Vector2.zero;
                goal = target.Pos;
            }
            else
            {
                return Vector2.zero;   // 남은 몹이 없으면 제자리 (곧 다음 무리 또는 결과창)
            }

            return SteerTo(goal);
        }

        /// <summary>
        /// 몸 크기를 고려한 길찾기. 몸이 통째로 지나갈 수 있으면 직선으로,
        /// 아니면 목표까지의 거리장을 따라 칸 중앙을 하나씩 밟아 갑니다.
        /// </summary>
        Vector2 SteerTo(Vector2 goal)
        {
            // 끼였다고 판단되면 잠깐 현재 칸 중앙으로 복귀
            if (unstickTimer > 0f) return ToPoint(heroTile);

            if (Map.PathClear(HeroPos, goal, HeroRadius)) return ToPoint(goal);

            var gt = DungeonMap.TileOf(goal);
            if (!Map.IsWalkable(gt)) return Vector2.zero;
            if (gt != goalTile || goalField == null)
            {
                goalTile = gt;
                goalField = Map.DistanceField(gt);
            }
            if (goalField[heroTile.x, heroTile.y] < 0) return Vector2.zero;   // 갈 수 없는 곳

            var next = Map.StepDown(goalField, heroTile);
            var after = Map.StepDown(goalField, next);
            // 두 칸 앞까지 몸이 지나갈 수 있으면 모서리를 부드럽게 질러감
            if (after != next && Map.PathClear(HeroPos, after, HeroRadius)) return ToPoint(after);
            if (Map.PathClear(HeroPos, next, HeroRadius)) return ToPoint(next);
            // 모서리에 걸린 상태: 현재 칸 중앙으로 먼저 이동
            return ToPoint(heroTile);
        }

        Vector2 ToPoint(Vector2 p)
        {
            var d = p - HeroPos;
            // 거의 도착했으면 속도를 줄여 떨림 방지
            return d.sqrMagnitude < 0.0004f ? Vector2.zero : (d.magnitude < 0.2f ? d / 0.2f : d.normalized);
        }

        void HeroAttack(float dt)
        {
            heroAttackTimer -= dt;

            MapMonster target = null;
            float best = attackRange;
            foreach (var m in Monsters)
            {
                if (m.IsDead) continue;
                float d = Vector2.Distance(HeroPos, m.Pos);
                if (d <= best && Map.LineClear(HeroPos, m.Pos)) { best = d; target = m; }
            }
            CurrentMonster = target?.Combatant;
            if (target == null) return;

            // 강한 몬스터와 붙으면 두루마리 자동 사용 (몬스터당 한 번만 판단)
            if (!target.ScrollChecked)
            {
                target.ScrollChecked = true;
                if (consumables.autoScroll && Hero.scrolls > 0)
                {
                    int perHit = Mathf.Max(1, Hero.Combatant.Stats.attack - target.Combatant.Stats.defense);
                    int hits = Mathf.CeilToInt((float)target.Combatant.CurrentHp / perHit);
                    if (hits >= consumables.autoScrollMinHits) CastScroll(target, auto: true);
                }
            }

            if (!target.IsDead && heroAttackTimer <= 0f)
            {
                heroAttackTimer = HeroAttackInterval;
                HeroStrike(target, 1f);
            }
            if (target.IsDead) OnKill(target);
        }

        /// <summary>민첩, 광폭화, 진행 속도를 반영한 공격 간격.</summary>
        float HeroAttackInterval =>
            heroAttackInterval / (pace * Hero.progress.AttackSpeedMult * (FrenzyActive ? 2f : 1f));

        /// <summary>영웅의 한 방. 치명타 판정, 연출, 주변 몬스터 깨우기까지 처리합니다.</summary>
        int HeroStrike(MapMonster target, float multiplier)
        {
            bool crit = UnityEngine.Random.value < Hero.progress.CritChance;
            int dmg = Hero.Combatant.Hit(target.Combatant, multiplier, crit);
            target.Awake = true;
            Fx(crit ? FxKind.Crit : FxKind.MonsterHit, target.Pos, dmg);
            MakeNoise(target.Pos);
            return dmg;
        }

        /// <summary>전투 소리: noiseSteps 걸음 안의 잠든 몬스터가 깨어나 영웅에게 옵니다.</summary>
        void MakeNoise(Vector2 at)
        {
            if (heroField == null) return;
            foreach (var m in Monsters)
            {
                if (m.IsDead || m.Awake) continue;
                var t = DungeonMap.TileOf(m.Pos);
                int steps = heroField[t.x, t.y];
                if (steps >= 0 && steps <= noiseSteps)
                {
                    m.Awake = true;
                    Fx(FxKind.Alert, m.Pos, 0, "!");
                }
            }
        }

        // ───────── 스킬 ─────────

        const float WhirlwindRadius = 1.9f;
        const float DashRange = 5f;

        int CountInRange(float radius)
        {
            int n = 0;
            foreach (var m in Monsters)
                if (!m.IsDead && Vector2.Distance(HeroPos, m.Pos) <= radius && Map.LineClear(HeroPos, m.Pos)) n++;
            return n;
        }

        MapMonster DashTarget()
        {
            MapMonster best = null;
            float bestDist = DashRange;
            foreach (var m in Monsters)
            {
                if (m.IsDead) continue;
                float d = Vector2.Distance(HeroPos, m.Pos);
                if (d > 1.6f && d <= bestDist && Map.PathClear(HeroPos, m.Pos, HeroRadius)) { bestDist = d; best = m; }
            }
            return best;
        }

        /// <summary>단축키 1~4 / 스킬 버튼. index는 SkillDef.All 순서.</summary>
        public void UseSkill(int index)
        {
            if (index < 0 || index >= skillCd.Length) return;
            var def = SkillDef.All[index];
            if (!SkillUnlocked(index))
            {
                if (State != RunState.Town) Log($"{def.name}은(는) Lv {def.unlockLevel}에 배웁니다.");
                return;
            }
            if (!CanUseSkill(index)) return;

            skillCd[index] = def.cooldown;
            var hc = Hero.Combatant;
            switch (def.id)
            {
                case SkillId.Whirlwind:
                    Fx(FxKind.Skill, HeroPos, (int)def.id, def.name);
                    foreach (var m in Monsters.ToArray())
                    {
                        if (m.IsDead || Vector2.Distance(HeroPos, m.Pos) > WhirlwindRadius || !Map.LineClear(HeroPos, m.Pos)) continue;
                        HeroStrike(m, 1.6f);
                        if (m.IsDead) OnKill(m);
                    }
                    break;

                case SkillId.Dash:
                {
                    var t = DashTarget();
                    if (t == null) { skillCd[index] = 0f; return; }
                    var dir = (t.Pos - HeroPos).normalized;
                    var land = t.Pos - dir * 0.8f;
                    if (Map.Fits(land, HeroRadius)) HeroPos = land;
                    HeroFacing = dir;
                    UpdateHeroTile();
                    Fx(FxKind.Skill, HeroPos, (int)def.id, def.name);
                    HeroStrike(t, 2.5f);
                    if (t.IsDead) OnKill(t);
                    break;
                }

                case SkillId.Guard:
                    hc.Shield = Mathf.RoundToInt(hc.Stats.maxHp * 0.35f);
                    guardTimer = 6f;
                    Fx(FxKind.Skill, HeroPos, (int)def.id, def.name);
                    break;

                case SkillId.Frenzy:
                    frenzyTimer = 6f;
                    heroAttackTimer = 0f;
                    Fx(FxKind.Skill, HeroPos, (int)def.id, def.name);
                    break;
            }
            Log($"[스킬] {def.name}");
        }

        /// <summary>자동 모드에서 스킬을 알아서 씁니다.</summary>
        void AutoSkills()
        {
            var hc = Hero.Combatant;
            bool fighting = CurrentMonster != null;
            int near = CountInRange(WhirlwindRadius);
            float hpRatio = hc.Stats.maxHp > 0 ? (float)hc.CurrentHp / hc.Stats.maxHp : 1f;

            if (CanUseSkill((int)SkillId.Guard) && fighting && hpRatio < 0.6f) UseSkill((int)SkillId.Guard);
            if (CanUseSkill((int)SkillId.Whirlwind) && (near >= 2 || (fighting && near >= 1))) UseSkill((int)SkillId.Whirlwind);
            // 앞의 스킬로 대상이 죽었을 수 있으니 CurrentMonster를 다시 확인
            var cur = CurrentMonster;
            if (CanUseSkill((int)SkillId.Frenzy) && (near >= 3 || (cur != null && cur.CurrentHp > hc.Stats.attack * 4)))
                UseSkill((int)SkillId.Frenzy);
            if (CanUseSkill((int)SkillId.Dash) && CurrentMonster == null) UseSkill((int)SkillId.Dash);
        }

        void MonstersAct(float dt)
        {
            foreach (var m in Monsters)
            {
                if (m.IsDead) continue;
                float dist = Vector2.Distance(m.Pos, HeroPos);

                // 발견: 눈에 보이거나, 벽 너머라도 huntSteps 걸음 안이면 냄새를 맡고 찾아옴.
                // 층에 오래 머물면 남은 몬스터가 전부 몰려옴.
                if (!m.Awake)
                {
                    var mt = DungeonMap.TileOf(m.Pos);
                    int steps = heroField != null ? heroField[mt.x, mt.y] : -1;
                    bool sees = dist <= aggroRange && Map.LineClear(m.Pos, HeroPos);
                    bool smells = steps >= 0 && steps <= huntSteps;
                    bool lateHunt = floorTime >= allHuntAfter && steps >= 0;
                    if (sees || smells || lateHunt)
                    {
                        m.Awake = true;
                        Fx(FxKind.Alert, m.Pos, 0, "!");
                    }
                }
                if (!m.Awake)
                {
                    Wander(m, dt);
                    continue;
                }

                // 이동: 보이면 직선으로, 벽이 가리면 거리장을 따라 한 칸씩
                if (dist > attackRange * 0.8f)
                {
                    Vector2 dir;
                    // 가까울 때만 몸 크기 직선 검사 (몬스터가 많아서 먼 놈은 거리장만 따라감)
                    if (dist <= 8f && Map.PathClear(m.Pos, HeroPos, MonsterRadius)) dir = (HeroPos - m.Pos).normalized;
                    else
                    {
                        // 몸 크기까지 고려해서 다음 칸으로, 모서리에 걸리면 자기 칸 중앙으로 먼저
                        var here = DungeonMap.TileOf(m.Pos);
                        var next = Map.StepDown(heroField, here);
                        Vector2 aim = Map.PathClear(m.Pos, next, MonsterRadius) ? (Vector2)next : (Vector2)here;
                        var d = aim - m.Pos;
                        dir = d.sqrMagnitude < 0.0004f ? Vector2.zero : d.normalized;
                    }
                    m.Pos = Map.Move(m.Pos, dir * MonsterSpeed(m) * dt, MonsterRadius);
                }

                // 공격
                m.AttackTimer -= dt;
                if (dist <= attackRange && m.AttackTimer <= 0f && Map.LineClear(m.Pos, HeroPos))
                {
                    m.AttackTimer = m.Data.attackInterval / Mathf.Sqrt(Mathf.Max(0.1f, pace));
                    int shieldBefore = Hero.Combatant.Shield;
                    int dmg = m.Combatant.AttackTarget(Hero.Combatant);
                    bool fullyBlocked = shieldBefore >= dmg && dmg > 0;
                    Fx(fullyBlocked ? FxKind.Shielded : FxKind.HeroHit, HeroPos, dmg);
                }
            }

            // 몬스터끼리 겹치지 않게 살짝 밀어냄
            for (int i = 0; i < Monsters.Count; i++)
            {
                for (int j = i + 1; j < Monsters.Count; j++)
                {
                    var a = Monsters[i];
                    var b = Monsters[j];
                    if (a.IsDead || b.IsDead) continue;
                    var diff = a.Pos - b.Pos;
                    float d = diff.magnitude;
                    if (d < 0.6f && d > 0.0001f)
                    {
                        var push = diff / d * (0.6f - d) * 0.5f;
                        a.Pos = Map.Move(a.Pos, push, MonsterRadius);
                        b.Pos = Map.Move(b.Pos, -push, MonsterRadius);
                    }
                }
            }
        }

        /// <summary>
        /// 몬스터 count마리를 영웅에게서 minDistance 이상 떨어진 빈 바닥에 소환.
        /// awake=true면 나오자마자 영웅을 쫓아옵니다.
        /// </summary>
        void SpawnMonsters(int count, float minDistance, bool awake)
        {
            var floor = Dungeon.GetFloor(Depth);
            float mult = Dungeon.GetMultiplier(Depth);
            for (int i = 0; i < count; i++)
            {
                var tile = PickSpawnTile(minDistance);
                var data = floor.monsterPool[rng.Next(floor.monsterPool.Count)];
                var s = data.baseStats.Scaled(mult);
                s.maxHp = Mathf.Max(1, Mathf.RoundToInt(s.maxHp * hordeHp));
                s.attack = Mathf.Max(1, Mathf.RoundToInt(s.attack * hordeAttack));
                var m = new MapMonster(data, s, tile) { Awake = awake };
                // 같은 칸에 겹쳐 나오지 않게 살짝 흩뿌림
                m.Pos += new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.5f;
                if (!Map.Fits(m.Pos, MonsterRadius)) m.Pos = tile;
                Monsters.Add(m);
            }
        }

        Vector2Int PickSpawnTile(float minDistance)
        {
            Vector2Int best = Map.Start;
            float bestDist = -1f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var room = Map.Rooms[rng.Next(Map.Rooms.Count)];
                var t = new Vector2Int(rng.Next(room.xMin, room.xMax), rng.Next(room.yMin, room.yMax));
                if (!Map.IsWalkable(t) || t == Map.Stairs) continue;
                float d = Vector2.Distance(t, HeroPos);
                if (d >= minDistance) return t;
                if (d > bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        /// <summary>몬스터 이동 속도. 진행 속도 배율을 일부 반영합니다.</summary>
        float MonsterSpeed(MapMonster m) => m.Data.moveSpeed * (1f + (pace - 1f) * 0.6f);

        /// <summary>아직 영웅을 못 찾은 몬스터는 자기 근처를 천천히 어슬렁거립니다.</summary>
        void Wander(MapMonster m, float dt)
        {
            m.WanderTimer -= dt;
            if (m.WanderTimer <= 0f || Vector2.Distance(m.Pos, m.WanderTarget) < 0.15f)
            {
                m.WanderTimer = 1.5f + (float)rng.NextDouble() * 2.5f;
                var here = DungeonMap.TileOf(m.Pos);
                var pick = here + new Vector2Int(rng.Next(-2, 3), rng.Next(-2, 3));
                m.WanderTarget = Map.IsWalkable(pick) && Map.PathClear(m.Pos, pick, MonsterRadius) ? (Vector2)pick : m.Pos;
            }
            var d = m.WanderTarget - m.Pos;
            if (d.sqrMagnitude > 0.0004f)
                m.Pos = Map.Move(m.Pos, d.normalized * MonsterSpeed(m) * 0.35f * dt, MonsterRadius);
        }

        void OnKill(MapMonster m)
        {
            FloorKills++;
            int gold = Mathf.Max(1, Mathf.RoundToInt(rng.Next(m.Data.goldMin, m.Data.goldMax + 1) * hordeReward));
            Hero.carriedGold += gold;
            Fx(FxKind.Kill, m.Pos, gold);
            GainExp(Mathf.Max(1, Mathf.RoundToInt(m.Combatant.Stats.Power * 0.45f * Mathf.Max(0.5f, hordeReward))), m.Pos);
            string lootText = "";
            if (m.Data.lootTable != null && rng.NextDouble() < lootChance)
            {
                foreach (var item in m.Data.lootTable.Roll())
                {
                    var inst = new ItemInstance(item);
                    Hero.carriedLoot.Add(inst);
                    lootText += $" {inst}";
                }
            }
            if (CurrentMonster == m.Combatant) CurrentMonster = null;
            // 무리 사냥이라 처치 로그는 전리품이 나왔을 때만
            if (lootText.Length > 0) Log($"{m.Combatant.Name} 처치! 전리품:{lootText}");
            Changed();
        }

        void ReturnToTown()
        {
            int count = Hero.carriedLoot.Count;
            Hero.stash.AddRange(Hero.carriedLoot);
            Hero.gold += Hero.carriedGold;
            Log($"귀환 성공! 아이템 {count}개, {Hero.carriedGold}G 보관.");
            ClearCarried();
            BackToTown();
        }

        void Die()
        {
            float loss = Dungeon.deathLossRatio;
            var kept = new List<ItemInstance>();
            int lost = 0;
            foreach (var item in Hero.carriedLoot)
            {
                if (UnityEngine.Random.value < loss) lost++;
                else kept.Add(item);
            }
            int keptGold = Mathf.RoundToInt(Hero.carriedGold * (1f - loss));

            Hero.stash.AddRange(kept);
            Hero.gold += keptGold;
            Log($"사망... {Depth + 1}층에서 쓰러짐. 아이템 {lost}개 손실, {kept.Count}개와 {keptGold}G만 건짐.");
            ClearCarried();
            BackToTown();
        }

        void ClearCarried()
        {
            Hero.carriedLoot.Clear();
            Hero.carriedGold = 0;
        }

        void BackToTown()
        {
            FloorResultPending = false;
            clearTimer = -1f;
            Time.timeScale = GameSpeed;
            CurrentMonster = null;
            State = RunState.Town;
            Map = null;
            Monsters.Clear();
            moveInput = Vector2.zero;
            MapVersion++;
            Hero.Combatant.Shield = 0;
            frenzyTimer = guardTimer = 0f;
            Hero.RefreshStats(fullHeal: true);
            Changed();
        }

        // ───────── 마을 행동 ─────────

        bool InTown()
        {
            if (State == RunState.Town) return true;
            Log("마을에서만 할 수 있습니다.");
            return false;
        }

        public void Equip(ItemInstance item)
        {
            if (!InTown()) return;
            if (Hero.Equip(item)) Log($"{item} 장착. 전투력 {Hero.Power}");
            Changed();
        }

        public void EquipBest()
        {
            if (!InTown()) return;
            int changed = Hero.EquipBestFromStash();
            Log(changed > 0 ? $"장비 교체 완료. 전투력 {Hero.Power}" : "더 좋은 장비가 없습니다.");
            Changed();
        }

        public void Sell(ItemInstance item)
        {
            if (!InTown()) return;
            int price = item.SellPrice;
            if (Shop.Sell(Hero, item)) Log($"{item} 판매 +{price}G");
            Changed();
        }

        public void SellJunk()
        {
            if (!InTown()) return;
            var (count, gold) = Shop.SellJunk(Hero);
            Log(count > 0 ? $"잡동사니 {count}개 판매 +{gold}G" : "팔 잡동사니가 없습니다.");
            Changed();
        }

        public void Upgrade(ItemSlot slot)
        {
            if (!InTown()) return;
            var item = Hero.GetEquipped(slot);
            if (item == null) { Log("장착한 장비가 없습니다."); return; }

            int cost = item.UpgradeCost;
            if (Shop.Upgrade(Hero, item)) Log($"강화 성공! {item} (-{cost}G) 전투력 {Hero.Power}");
            else if (!item.CanUpgrade) Log("이미 최대 강화입니다.");
            else Log($"골드가 부족합니다. ({cost}G 필요)");
            Changed();
        }

        public void Merge(ItemData data)
        {
            if (!InTown()) return;
            var made = Crafting.Merge(Hero, data);
            Log(made != null ? $"합성 성공! {data.displayName} x{data.mergeCount} → {made}" : "재료가 부족합니다.");
            Changed();
        }

        public void BuyPotion() => Buy(ref Hero.potions, consumables.potionPrice, "회복 포션");
        public void BuyScroll() => Buy(ref Hero.scrolls, consumables.scrollPrice, "화염 두루마리");

        void Buy(ref int stock, int price, string name)
        {
            if (!InTown()) return;
            if (Hero.gold < price) { Log($"골드가 부족합니다. ({price}G 필요)"); return; }
            Hero.gold -= price;
            stock++;
            Log($"{name} 구매 (-{price}G)");
            Changed();
        }

        // ───────── 소모품 사용 ─────────

        public bool CanUsePotion => Hero.potions > 0 && State != RunState.Town && !Hero.Combatant.IsDead
                                    && Hero.Combatant.CurrentHp < Hero.Combatant.Stats.maxHp;
        public bool CanUseScroll => Hero.scrolls > 0 && State != RunState.Town && ScrollTarget() != null;

        /// <summary>버튼/키로 직접 사용.</summary>
        public void UsePotion() { if (CanUsePotion) DrinkPotion(auto: false); }
        public void UseScroll()
        {
            if (!CanUseScroll) return;
            var t = ScrollTarget();
            CastScroll(t, auto: false);
            if (t.IsDead) OnKill(t);
        }

        /// <summary>두루마리 대상: 시야 안에서 가장 가까운 살아 있는 몬스터.</summary>
        MapMonster ScrollTarget()
        {
            if (Map == null) return null;
            MapMonster best = null;
            float bestDist = sightRadius;
            foreach (var m in Monsters)
            {
                if (m.IsDead) continue;
                float d = Vector2.Distance(HeroPos, m.Pos);
                if (d <= bestDist && Map.LineClear(HeroPos, m.Pos)) { bestDist = d; best = m; }
            }
            return best;
        }

        void DrinkPotion(bool auto)
        {
            Hero.potions--;
            int healed = Hero.Combatant.Heal(Mathf.RoundToInt(Hero.Combatant.Stats.maxHp * consumables.potionHealRatio));
            Fx(FxKind.Heal, HeroPos, healed);
            Log($"{(auto ? "[자동] " : "")}회복 포션 사용 +{healed} HP (남은 {Hero.potions}개)");
            Changed();
        }

        void CastScroll(MapMonster target, bool auto)
        {
            Hero.scrolls--;
            target.Awake = true;
            int dmg = target.Combatant.TakeTrueDamage(Mathf.RoundToInt(Hero.Combatant.Stats.attack * consumables.scrollDamageMultiplier));
            Fx(FxKind.Crit, target.Pos, dmg);
            Log($"{(auto ? "[자동] " : "")}화염 두루마리! {target.Combatant.Name}에게 {dmg} 피해 (남은 {Hero.scrolls}개)");
            Changed();
        }

        // ───────── 저장 / 불러오기 / 방치 보상 ─────────

        public void SaveGame()
        {
            if (!enableSave || Hero == null) return;

            var s = new SaveData
            {
                gold = Hero.gold,
                potions = Hero.potions,
                scrolls = Hero.scrolls,
                selectedIndex = SelectedIndex,
                weapon = SaveSystem.ToSave(Hero.weapon),
                armor = SaveSystem.ToSave(Hero.armor),
                bestFloor = (int[])bestFloor.Clone(),
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                level = Hero.progress.level,
                exp = Hero.progress.exp,
                statPoints = Hero.progress.points,
                str = Hero.progress.str,
                vit = Hero.progress.vit,
                agi = Hero.progress.agi,
            };
            foreach (var item in Hero.stash) s.stash.Add(SaveSystem.ToSave(item));

            // 던전 도중에 끄면 들고 있던 전리품은 귀환한 것으로 처리
            if (State != RunState.Town)
            {
                foreach (var item in Hero.carriedLoot) s.stash.Add(SaveSystem.ToSave(item));
                s.gold += Hero.carriedGold;
            }
            SaveSystem.Save(s);
        }

        void LoadGame()
        {
            if (!SaveSystem.TryLoad(out var s)) return;

            Hero.gold = s.gold;
            Hero.potions = s.potions;
            Hero.scrolls = s.scrolls;
            Hero.weapon = SaveSystem.FromSave(s.weapon, itemRegistry);
            Hero.armor = SaveSystem.FromSave(s.armor, itemRegistry);
            Hero.stash.Clear();
            foreach (var save in s.stash)
            {
                var item = SaveSystem.FromSave(save, itemRegistry);
                if (item != null) Hero.stash.Add(item);
            }
            for (int i = 0; i < bestFloor.Length && s.bestFloor != null && i < s.bestFloor.Length; i++)
                bestFloor[i] = s.bestFloor[i];
            SelectedIndex = Mathf.Clamp(s.selectedIndex, 0, dungeons.Count - 1);
            var p = Hero.progress;
            p.level = Mathf.Max(1, s.level);
            p.exp = Mathf.Max(0, s.exp);
            p.points = Mathf.Max(0, s.statPoints);
            p.str = Mathf.Max(0, s.str);
            p.vit = Mathf.Max(0, s.vit);
            p.agi = Mathf.Max(0, s.agi);
            Hero.RefreshStats(fullHeal: true);

            Log("저장된 진행 상황을 불러왔습니다.");
            if (s.savedAtUnix > 0)
                GrantOfflineReward(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - s.savedAtUnix);
            Changed();
        }

        /// <summary>
        /// 꺼져 있던 시간만큼 보상. 가장 깊이 간 층의 절반 정도 깊이에서
        /// 안전하게 사냥했다고 가정하고, 골드는 전부 / 아이템은 일부 횟수만 굴립니다.
        /// </summary>
        void GrantOfflineReward(long seconds)
        {
            if (seconds < offlineMinSeconds) return;

            int idx = SelectedIndex;
            if (bestFloor[idx] == 0 || !IsUnlocked(idx)) idx = 0;
            if (bestFloor[idx] == 0) return;   // 한 번도 사냥한 적 없음

            double counted = Math.Min(seconds, offlineMaxHours * 3600.0);
            int kills = (int)(counted / Mathf.Max(1f, offlineSecondsPerKill));
            if (kills <= 0) return;

            var d = dungeons[idx];
            int floorIndex = Mathf.Max(0, (bestFloor[idx] + 1) / 2 - 1);
            var floor = d.GetFloor(floorIndex);

            int gold = 0, items = 0;
            for (int k = 0; k < kills; k++)
            {
                var m = floor.monsterPool[UnityEngine.Random.Range(0, floor.monsterPool.Count)];
                gold += UnityEngine.Random.Range(m.goldMin, m.goldMax + 1);
                if (k < offlineItemRollLimit && m.lootTable != null)
                {
                    foreach (var item in m.lootTable.Roll())
                    {
                        Hero.stash.Add(new ItemInstance(item));
                        items++;
                    }
                }
            }
            Hero.gold += gold;

            var span = TimeSpan.FromSeconds(counted);
            offlineReport =
                $"자리를 비운 동안 영웅이 사냥을 계속했습니다.\n\n" +
                $"{(int)span.TotalHours}시간 {span.Minutes}분 · {d.dungeonName} {floorIndex + 1}층\n" +
                $"몬스터 {kills}마리 처치\n+{gold}G, 아이템 {items}개 (창고에 보관됨)";
            Log($"오프라인 보상: {kills}마리 처치, +{gold}G, 아이템 {items}개");
        }

        /// <summary>오프라인 보상 안내문을 한 번만 꺼내 갑니다 (없으면 null).</summary>
        public string ConsumeOfflineReport()
        {
            var r = offlineReport;
            offlineReport = null;
            return r;
        }

        /// <summary>저장 파일을 지우고 처음부터 다시 시작 (마을에서만).</summary>
        public void ResetProgress()
        {
            if (!InTown()) return;
            SaveSystem.Delete();
            NewGame();
            Log("진행 상황을 초기화했습니다.");
            Changed();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveGame();
        }

        void OnApplicationQuit() => SaveGame();

        void OnDestroy() => Time.timeScale = 1f;   // 배속이 다른 씬에 남지 않게

        // ───────── 공통 ─────────

        /// <summary>화면 갱신 알림. 마을에 있을 때 바뀐 내용은 바로 자동 저장합니다.</summary>
        void Changed()
        {
            if (State == RunState.Town) SaveGame();
            OnChanged?.Invoke();
        }

        void GainExp(int amount, Vector2 at)
        {
            var p = Hero.progress;
            floorExp += amount;
            int levels = p.AddExp(amount);
            if (levels <= 0) return;

            Hero.RefreshStats(fullHeal: false);
            Hero.Combatant.Heal(Mathf.RoundToInt(Hero.Combatant.Stats.maxHp * 0.3f));
            Fx(FxKind.LevelUp, HeroPos, p.level, $"LEVEL UP! Lv {p.level}");
            Log($"레벨 업! Lv {p.level}  스탯 포인트 +{levels * HeroProgress.PointsPerLevel} (보유 {p.points})");
            foreach (var s in SkillDef.All)
                if (s.unlockLevel > p.level - levels && s.unlockLevel <= p.level)
                    Log($"새 스킬 습득: {s.name}  ({s.desc})");
        }

        void Fx(FxKind kind, Vector2 pos, int value, string text = null)
            => OnFx?.Invoke(new FxEvent { kind = kind, pos = pos, value = value, text = text });

        void Log(string msg)
        {
            Debug.Log($"[LootHunters] {msg}");
            OnLog?.Invoke(msg);
        }
    }
}
