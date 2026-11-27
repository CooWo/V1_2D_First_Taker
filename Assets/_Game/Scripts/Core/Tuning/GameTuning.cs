using System;
using ShadowRPG.Core.Combat;

namespace ShadowRPG.Core.Tuning
{
    /// <summary>
    /// 게임의 조정 가능한 모든 수치 (기획 문서: Docs/Tuning_Guide.md).
    /// 탭(섹션)마다 JSON 파일 하나: Assets/_Game/Resources/Tuning/{섹션}.json
    /// 파일에 없는 항목은 여기 적힌 기본값을 씁니다. 수치만 바꾸면 되고 코드는 건드릴 필요 없습니다.
    /// </summary>
    [Serializable]
    public sealed class GameTuning
    {
        public DungeonTuning Dungeon = new DungeonTuning();
        public MonsterTuning Monster = new MonsterTuning();
        public DropTuning Drop = new DropTuning();
        public ItemTuning Item = new ItemTuning();
        public GrowthTuning Growth = new GrowthTuning();
        public TrainingTuning Training = new TrainingTuning();
        public EnhanceTuning Enhance = new EnhanceTuning();
        public TreeTuning Tree = new TreeTuning();
        public PetTuning Pet = new PetTuning();
        public BalanceSettings Combat = new BalanceSettings();
        public RhythmTuning Rhythm = new RhythmTuning();
        public BagTuning Bag = new BagTuning();
        public ConvenienceTuning Convenience = new ConvenienceTuning();
        public QuestTuning Quest = new QuestTuning();
        public AttendanceTuning Attendance = new AttendanceTuning();
        public ShopTuning Shop = new ShopTuning();
        public RaidTuning Raid = new RaidTuning();
        public PresentationTuning Presentation = new PresentationTuning();

        /// <summary>섹션 이름 = JSON 파일 이름. 문서의 탭 순서와 같습니다.</summary>
        public static readonly string[] SectionNames =
        {
            "Dungeon", "Monster", "Drop", "Item", "Growth", "Training", "Enhance", "Tree", "Pet", "Combat",
            "Rhythm", "Bag", "Convenience", "Quest", "Attendance", "Shop", "Raid", "Presentation"
        };

        public object Section(string name)
        {
            var field = typeof(GameTuning).GetField(name);
            return field?.GetValue(this);
        }

        /// <summary>
        /// 현재 적용 중인 수치. 게임 시작 시 Runtime이 JSON을 읽어 교체합니다 (테스트는 기본값).
        /// 정적 도우미(아이템 크기 등)가 쓰는 마지막 수단이며, 서비스는 생성자로 받는 것을 우선합니다.
        /// </summary>
        public static GameTuning Current { get; set; } = new GameTuning();
    }

    // =====================================================================
    // 던전
    // =====================================================================

    [Serializable]
    public sealed class ActEntry
    {
        public string Name;
        public string[] Dungeons;
        public string[] EnemyIds;
    }

    [Serializable]
    public sealed class DungeonTuning
    {
        public int FloorsPerDungeon = 10;
        public ActEntry[] Acts =
        {
            new ActEntry { Name = "버려진 지하묘지", Dungeons = new[] { "무너진 입구", "납골 회랑", "속삭이는 성소", "검은 제단" }, EnemyIds = new[] { "skeleton_minion", "skeleton_rogue" } },
            new ActEntry { Name = "잿빛 감옥", Dungeons = new[] { "녹슨 문", "고문실", "죄수의 길", "간수장의 방", "잿빛 심장" }, EnemyIds = new[] { "skeleton_minion", "skeleton_rogue", "skeleton_mage" } },
            new ActEntry { Name = "얼어붙은 심연", Dungeons = new[] { "서리 계단", "빙결 회랑", "푸른 무덤", "침묵의 홀", "얼음 왕관" }, EnemyIds = new[] { "skeleton_minion", "skeleton_mage", "skeleton_warrior" } },
            new ActEntry { Name = "불타는 성채", Dungeons = new[] { "재의 성문", "용암 대전", "화염 감옥", "타는 회랑", "불꽃 왕좌", "종말의 탑" }, EnemyIds = new[] { "skeleton_minion", "skeleton_rogue", "skeleton_mage", "skeleton_warrior" } }
        };

        public int StartPacks = 3;
        public int MobsPerPack = 8;
        public int FloorsPerExtraPack = 3;
        public int ExtraPacksPerDungeon = 1;
        public int MaxPacks = 7;
        public float PackSpread = 2.4f;

        public int EliteFromFloor = 3;
        public int ElitesPerFloor = 1;
        public int ExtraElitesPerTwoActs = 1;

        public float LevelPerFloor = 0.6f;

        public float AutoContinueSeconds = 5f;
        public float ClearSlowMoSeconds = 0.6f;
        public float SlowMoScale = 0.25f;
        public float FloorClearGoldPerLevel = 12f;
        public float BossClearGoldMultiplier = 3f;

        public int RoomCountMin = 5;
        public int RoomCountMax = 8;
        public int RoomSizeMin = 7;
        public int RoomSizeMax = 11;
        public int CorridorWidth = 2;
        public int CorridorLengthMin = 3;
        public float SafeRadiusFromStart = 7f;
    }

    // =====================================================================
    // 몬스터
    // =====================================================================

    [Serializable]
    public sealed class MonsterTuning
    {
        public float GlobalLife = 1.6f;
        public float GlobalDamage = 1.7f;
        public float FloorStatPct = 4f;
        public float DungeonMultiplier = 1.15f;
        public float ActMultiplier = 1.8f;

        public float EliteLife = 4f;
        public float EliteDamage = 1.5f;
        public float EliteSize = 1.3f;
        public float BossLife = 18f;
        public float BossDamage = 2.2f;
        public float BossSize = 1.8f;
        public float BossSpeed = 0.85f;

        public float ArmorPerLevel = 0.2f;
        public float ResistPerLevel = 1.5f;
        public float ResistCap = 40f;

        public float XpPerLevel = 4f;
        public float EliteXp = 6f;
        public float BossXp = 25f;

        public float AggroRange = 8f;
        public float AlertRadius = 5f;
    }

    // =====================================================================
    // 드랍
    // =====================================================================

    [Serializable]
    public sealed class RarityBand
    {
        public int FromFloor;
        public float Normal;
        public float Magic;
        public float Rare;
        public float Legendary;
        public float Set;
    }

    [Serializable]
    public sealed class AffixLineRow
    {
        public int Act;
        public int Normal;
        public int Magic;
        public int Rare;
        public int Legendary;
    }

    [Serializable]
    public sealed class AffixUnlock
    {
        public int Act;
        public string[] Stats;
    }

    [Serializable]
    public sealed class DropTuning
    {
        public float NormalDropChancePct = 12f;
        public int EliteDrops = 1;
        public int BossDrops = 3;
        public bool BossGuaranteesRare = true;
        public float BossRarityBonus = 2f;

        public RarityBand[] Bands =
        {
            new RarityBand { FromFloor = 1, Normal = 70f, Magic = 30f },
            new RarityBand { FromFloor = 4, Normal = 50f, Magic = 35f, Rare = 15f },
            new RarityBand { FromFloor = 7, Normal = 35f, Magic = 37f, Rare = 24f, Legendary = 3f, Set = 1f },
            new RarityBand { FromFloor = 10, Normal = 0f, Magic = 30f, Rare = 55f, Legendary = 10f, Set = 5f }
        };

        public string[] ActRarityCap = { "Rare", "Legendary", "Set", "Set" };
        public float Act1BossLegendaryChancePct = 3f;

        public int ItemLevelPerAct = 25;
        public int ItemLevelPerDungeon = 4;
        public int FloorsPerItemLevel = 2;

        public AffixLineRow[] AffixLines =
        {
            new AffixLineRow { Act = 1, Normal = 1, Magic = 2, Rare = 3, Legendary = 3 },
            new AffixLineRow { Act = 2, Normal = 1, Magic = 2, Rare = 4, Legendary = 4 },
            new AffixLineRow { Act = 3, Normal = 1, Magic = 3, Rare = 5, Legendary = 5 },
            new AffixLineRow { Act = 4, Normal = 2, Magic = 3, Rare = 6, Legendary = 6 }
        };

        public AffixUnlock[] AffixPools =
        {
            new AffixUnlock { Act = 1, Stats = new[] { "Strength", "Dexterity", "Intelligence", "MaxLife", "WeaponDamage", "AllDamagePct", "Armor", "AttackSpeedPct", "CritChancePct", "LifeOnHit", "MoveSpeedPct", "GoldFindPct" } },
            new AffixUnlock { Act = 2, Stats = new[] { "FireDamagePct", "ColdDamagePct", "LightningDamagePct", "EarthDamagePct", "LightDamagePct", "DarkDamagePct", "FireResistPct", "ColdResistPct", "LightningResistPct", "EarthResistPct", "LightResistPct", "DarkResistPct", "AllResistPct" } },
            new AffixUnlock { Act = 3, Stats = new[] { "CooldownReductionPct", "AreaPct", "ExtraProjectiles", "ExtraChains", "CritDamagePct", "EvasionPct", "BlockChancePct" } },
            new AffixUnlock { Act = 4, Stats = new[] { "BossDamagePct", "EliteDamageReductionPct", "LifeRegen", "ResourceRegen", "ResourceCostReductionPct", "ExperiencePct", "PetPowerPct" } }
        };
    }

    // =====================================================================
    // 아이템
    // =====================================================================

    [Serializable]
    public sealed class ItemTuning
    {
        public float SellNormal = 5f;
        public float SellMagic = 15f;
        public float SellRare = 45f;
        public float SellLegendary = 150f;
        public float SellSet = 120f;
        public float SellPerItemLevel = 0.15f;
        public float SellPerEnhance = 0.25f;

        public float PowerPerStatPoint = 1f;
        public float PowerPerLife = 0.2f;
        public float PowerPerDamage = 3f;
        public float PowerPerPct = 2f;
        public float PowerPerArmor = 0.25f;
        public float PowerPerSkillRarity = 40f;

        public bool AutoEquipOnReturnDefault = true;
    }

    // =====================================================================
    // 성장: 레벨·스탯
    // =====================================================================

    [Serializable]
    public sealed class GrowthTuning
    {
        public float ExpBase = 25f;
        public float ExpExponent = 1.35f;
        public int StatPointsPerLevel = 1;
        public float LifePerLevel = 12f;
        public long RespecGoldPerLevel = 10;
        public long StartingGold = 200;
        public int StartingStatPoints = 0;
    }

    // =====================================================================
    // 단련 (골드 → 기본 체급, ACT별 한도)
    // =====================================================================

    [Serializable]
    public sealed class TrainingTuning
    {
        public float CostBase = 50f;
        public float CostGrowth = 1.12f;
        public int LevelsPerAct = 20;
        public float AttackPctPerLevel = 1f;
        public float LifePctPerLevel = 1f;
        public float ArmorPctPerLevel = 1f;
        public float ResistPctPerLevel = 0.5f;
    }

    // =====================================================================
    // 강화
    // =====================================================================

    [Serializable]
    public sealed class EnhanceTuning
    {
        public int MaxLevel = 15;
        public float BonusPerLevel = 0.08f;
        public float CostBase = 30f;
        public float CostGrowth = 1.32f;
        public float CostPerItemLevel = 0.1f;
        public float RarityNormal = 1f;
        public float RarityMagic = 1.5f;
        public float RarityRare = 2.5f;
        public float RarityLegendary = 4f;
        public float RaritySet = 3.5f;
    }

    // =====================================================================
    // 성장 트리
    // =====================================================================

    [Serializable]
    public sealed class TreeTuning
    {
        public int NodeCount = 3000;
        public int Seed = 20261126;
        public float CostBase = 25f;
        public float CostExponent = 1.35f;
        public long ResetGoldPerNode = 5;
    }

    // =====================================================================
    // 펫
    // =====================================================================

    [Serializable]
    public sealed class PetTuning
    {
        public float LevelCostBase = 60f;
        public float LevelCostGrowth = 1.2f;
        public float GradeCostBase = 800f;
        public float GradeCostGrowth = 5f;
        public int MaxGrade = 5;
        public int[] MaxGradeByAct = { 2, 3, 4, 5 };
    }

    // =====================================================================
    // 리듬 무기
    // =====================================================================

    [Serializable]
    public sealed class RhythmTuning
    {
        public float DefaultBeatSeconds = 0.9f;
        public float RingStartScale = 2.6f;
        public float PerfectWindowSeconds = 0.08f;
        public float GoodWindowSeconds = 0.17f;
        public int PerfectExtraHits = 2;
        public int GoodExtraHits = 1;
        public float ExtraHitDamagePct = 60f;
        public float AttackSpeedPctPerStack = 6f;
        public int MaxStacks = 8;
        public float StackDurationSeconds = 3f;
        public float RingScreenX = 0.8f;
        public float RingScreenY = 0.3f;
        public float RingSizeDp = 190f;
        public bool VibrateOnPerfect = true;
    }

    // =====================================================================
    // 가방 · 사망
    // =====================================================================

    [Serializable]
    public sealed class BagTuning
    {
        public int BagWidth = 8;
        public int BagHeight = 5;
        public int StashWidth = 10;
        public int StashHeight = 8;
        public int StashExpandRows = 2;
        public int StashMaxHeight = 16;
        public int MaxPicksPerFloor = 0;
        public float DeathLossFraction = 0.5f;
        public bool AdRecoveryUnlimited = true;
        public int AdRecoveryPerDay = 0;
    }

    // =====================================================================
    // 편의 (배속 · 세팅 슬롯)
    // =====================================================================

    [Serializable]
    public sealed class ConvenienceTuning
    {
        public float[] Speeds = { 1f, 2f, 3f };
        public float FreeMaxSpeed = 2f;
        public int PresetSlotsBase = 3;
        public int PresetSlotsMax = 6;
        public long PresetSlotTempoPrice = 300;
    }

    // =====================================================================
    // 일일 퀘스트
    // =====================================================================

    [Serializable]
    public sealed class QuestTemplate
    {
        public string Id;
        public string Category;
        public string Text;
        public int Target;
        public string Param;
        public long RewardGold;
        public long RewardTempo;
    }

    [Serializable]
    public sealed class QuestTuning
    {
        public int DailyCount = 5;
        public long AllClearGold = 2000;
        public long AllClearTempo = 30;
        public QuestTemplate[] Pool =
        {
            new QuestTemplate { Id = "turnin_rare", Category = "반납", Text = "희귀 등급 장비 {0}개 반납", Target = 1, Param = "Rare", RewardGold = 800, RewardTempo = 5 },
            new QuestTemplate { Id = "turnin_big", Category = "반납", Text = "6칸 이상 장비 {0}개 반납", Target = 2, Param = "6", RewardGold = 600, RewardTempo = 5 },
            new QuestTemplate { Id = "turnin_fire", Category = "반납", Text = "불 속성 옵션 장비 {0}개 반납", Target = 1, Param = "FireDamagePct", RewardGold = 700, RewardTempo = 5 },
            new QuestTemplate { Id = "kill", Category = "처치", Text = "몬스터 {0}마리 처치", Target = 300, RewardGold = 500, RewardTempo = 3 },
            new QuestTemplate { Id = "kill_elite", Category = "처치", Text = "정예 {0}마리 처치", Target = 10, RewardGold = 600, RewardTempo = 3 },
            new QuestTemplate { Id = "kill_boss", Category = "처치", Text = "던전 보스 {0}회 처치", Target = 1, RewardGold = 800, RewardTempo = 5 },
            new QuestTemplate { Id = "depth", Category = "깊이", Text = "아무 던전 {0}층 이상 도달", Target = 7, RewardGold = 700, RewardTempo = 5 },
            new QuestTemplate { Id = "streak", Category = "깊이", Text = "귀환 없이 {0}층 연속 클리어", Target = 5, RewardGold = 900, RewardTempo = 5 },
            new QuestTemplate { Id = "deathless", Category = "생존", Text = "사망 없이 10층 클리어 {0}회", Target = 1, RewardGold = 1200, RewardTempo = 8 },
            new QuestTemplate { Id = "rhythm", Category = "리듬", Text = "리듬 판정 성공 {0}회", Target = 200, RewardGold = 500, RewardTempo = 3 },
            new QuestTemplate { Id = "rhythm_combo", Category = "리듬", Text = "리듬 연속 성공 {0}회", Target = 30, RewardGold = 700, RewardTempo = 5 },
            new QuestTemplate { Id = "train", Category = "성장", Text = "단련 {0}회", Target = 3, RewardGold = 400, RewardTempo = 2 },
            new QuestTemplate { Id = "tree", Category = "성장", Text = "트리 노드 {0}개 구매", Target = 2, RewardGold = 400, RewardTempo = 2 },
            new QuestTemplate { Id = "enhance", Category = "성장", Text = "장비 강화 {0}회", Target = 1, RewardGold = 400, RewardTempo = 2 },
            new QuestTemplate { Id = "sell_gold", Category = "경제", Text = "판매로 골드 {0} 획득", Target = 3000, RewardGold = 500, RewardTempo = 3 },
            new QuestTemplate { Id = "pvp", Category = "콘텐츠", Text = "PvP {0}회", Target = 3, RewardGold = 500, RewardTempo = 5 },
            new QuestTemplate { Id = "raid", Category = "콘텐츠", Text = "레이드 {0}회", Target = 1, RewardGold = 800, RewardTempo = 5 }
        };
    }

    // =====================================================================
    // 출석
    // =====================================================================

    [Serializable]
    public sealed class AttendanceReward
    {
        public int Day;
        public long Gold;
        public long Tempo;
        public string Item;
    }

    [Serializable]
    public sealed class AttendanceTuning
    {
        public int CycleDays = 28;
        public long DailyGold = 1000;
        public long DailyGoldPerAct = 500;
        public AttendanceReward[] Special =
        {
            new AttendanceReward { Day = 7, Tempo = 50, Item = "장비 상자" },
            new AttendanceReward { Day = 14, Tempo = 80, Item = "꾸미기 상자" },
            new AttendanceReward { Day = 21, Tempo = 100, Item = "장비 상자" },
            new AttendanceReward { Day = 28, Tempo = 200, Item = "꾸미기 상자" }
        };
        public AttendanceReward[] Newbie =
        {
            new AttendanceReward { Day = 1, Gold = 3000 },
            new AttendanceReward { Day = 2, Tempo = 50 },
            new AttendanceReward { Day = 3, Gold = 5000, Item = "세팅 슬롯 체험(3일)" },
            new AttendanceReward { Day = 4, Tempo = 50 },
            new AttendanceReward { Day = 5, Gold = 8000, Item = "펫 등급업 재료" },
            new AttendanceReward { Day = 6, Tempo = 80 },
            new AttendanceReward { Day = 7, Tempo = 150, Item = "희귀 장비 상자" }
        };
    }

    // =====================================================================
    // 상점
    // =====================================================================

    [Serializable]
    public sealed class ShopProduct
    {
        public string Id;
        public string Name;
        public string Currency;
        public long Price;
        public long Tempo;
        public long Gold;
        public string Note;
    }

    [Serializable]
    public sealed class ShopTuning
    {
        public ShopProduct[] TempoPacks =
        {
            new ShopProduct { Id = "tempo_60", Name = "템포 60", Currency = "KRW", Price = 1200, Tempo = 60, Note = "첫 구매 2배" },
            new ShopProduct { Id = "tempo_300", Name = "템포 300", Currency = "KRW", Price = 5900, Tempo = 300, Note = "첫 구매 2배" },
            new ShopProduct { Id = "tempo_980", Name = "템포 980", Currency = "KRW", Price = 19000, Tempo = 980, Note = "첫 구매 2배" },
            new ShopProduct { Id = "tempo_3280", Name = "템포 3280", Currency = "KRW", Price = 59000, Tempo = 3280, Note = "첫 구매 2배" }
        };
        public ShopProduct[] Packages =
        {
            new ShopProduct { Id = "starter", Name = "스타터 패키지", Currency = "KRW", Price = 4900, Tempo = 300, Gold = 30000, Note = "계정당 1회" },
            new ShopProduct { Id = "monthly", Name = "월정액", Currency = "KRW", Price = 5900, Tempo = 30, Note = "30일간 매일 템포 30 + 배속 ×3" },
            new ShopProduct { Id = "season_pass", Name = "시즌 패스", Currency = "KRW", Price = 12000, Note = "유료 보상 트랙 개방" }
        };
        public ShopProduct[] Convenience =
        {
            new ShopProduct { Id = "preset_slot", Name = "세팅 슬롯 +1", Currency = "Tempo", Price = 300, Note = "최대 6" },
            new ShopProduct { Id = "stash_rows", Name = "창고 확장 (2줄)", Currency = "Tempo", Price = 200 },
            new ShopProduct { Id = "speed3_day", Name = "배속 ×3 (1일)", Currency = "Tempo", Price = 50 }
        };
        public int GoldShopItems = 6;
        public float GoldShopPriceMultiplier = 8f;
        public int GoldShopRefreshHours = 24;
        public long GoldShopRerollGold = 500;
    }

    // =====================================================================
    // 레이드
    // =====================================================================

    [Serializable]
    public sealed class RaidTuning
    {
        public int EntriesPerDay = 3;
        public long[] PowerRequirementByTier = { 500, 1500, 4000, 10000 };
        public int PartySize = 4;
        public float MatchmakingSeconds = 5f;
    }

    // =====================================================================
    // 연출 · 카메라
    // =====================================================================

    [Serializable]
    public sealed class PresentationTuning
    {
        public float CameraViewWidth = 10f;
        public float CameraViewWidthLandscape = 20f;
        public float CameraFollowDamping = 0.25f;
        public float CameraLookAhead = 0.8f;
        public float CameraVerticalOffset = 0.8f;

        public float HitStopNormal = 0.035f;
        public float HitStopCrit = 0.07f;
        public float HitStopFinisher = 0.12f;
        public float HitStopBoss = 0.16f;
        public float HitStopCooldown = 0.12f;

        public float ShakeNormal = 0.06f;
        public float ShakeCrit = 0.16f;
        public float ShakePlayerHurt = 0.22f;
        public float ShakeBoss = 0.45f;

        public float ScreenFlashFinisher = 0.22f;
        public float HurtVignette = 0.35f;

        public float KnockbackNormal = 0.18f;
        public float KnockbackCrit = 0.35f;

        public bool ShowDamageNumbers = true;
        public float DamageNumberSize = 26f;
        public float CritNumberScale = 1.45f;
        public float DamageNumberLifetime = 0.75f;

        public float AmbientIntensity = 0.85f;
        public float HeroLightIntensity = 0.7f;
        public float BloomIntensity = 0.9f;
        public float VignetteIntensity = 0.38f;
    }
}
