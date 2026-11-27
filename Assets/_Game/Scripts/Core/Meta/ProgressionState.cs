using System;
using System.Collections.Generic;

namespace ShadowRPG.Core.Meta
{
    /// <summary>플레이어 옵션 (설정 메뉴). 저장됩니다.</summary>
    [Serializable]
    public sealed class PlayerSettings
    {
        public bool AutoContinue;
        public bool AutoEquipOnReturn = true;
        public float Speed = 1f;
        public bool AutoMove = true;
        public float RhythmRingX = -1f;
        public float RhythmRingY = -1f;
        public float RhythmRingScale = 1f;
        public bool ScreenShake = true;
        public bool DamageNumbers = true;
        public bool Landscape;
        public float MusicVolume = 0.7f;
        public float SfxVolume = 0.9f;
        public bool Vibration = true;
        public int FrameRate = 60;
        public bool RecommendByPower = true;
    }

    [Serializable]
    public sealed class PresetSlotItem
    {
        public int Slot;
        public string ItemId;
    }

    /// <summary>장비 세팅 슬롯 하나 (리듬 빌드 ↔ 자동 빌드 전환, PvP 세팅 지정).</summary>
    [Serializable]
    public sealed class EquipmentPresetState
    {
        public string Name;
        public List<PresetSlotItem> Items = new List<PresetSlotItem>();
    }

    [Serializable]
    public sealed class QuestProgress
    {
        public string Id;
        public int Progress;
        public bool Claimed;
    }

    [Serializable]
    public sealed class QuestState
    {
        public string DayKey = string.Empty;
        public List<QuestProgress> Active = new List<QuestProgress>();
        public bool AllClearClaimed;
    }

    [Serializable]
    public sealed class AttendanceState
    {
        public string LastClaimDay = string.Empty;
        public int CycleDay;
        public int NewbieDay;
    }

    /// <summary>누적 기록 (업적·도감용).</summary>
    [Serializable]
    public sealed class LifetimeStats
    {
        public long Kills;
        public long EliteKills;
        public long BossKills;
        public long Deaths;
        public long RhythmHits;
        public int BestRhythmCombo;
        public long GoldEarnedBySelling;
        public int DeepestFloor;
        public int LegendariesFound;
    }

    /// <summary>
    /// 방향성 v1에서 새로 생긴 성장 상태 (스탯 분배·단련·템포·세팅 슬롯·퀘스트·출석·옵션).
    /// 단순 필드만 두어 그대로 JSON 저장됩니다.
    /// </summary>
    [Serializable]
    public sealed class ProgressionState
    {
        public int UnspentStatPoints;
        public int AllocatedStrength;
        public int AllocatedDexterity;
        public int AllocatedIntelligence;

        public int TrainAttack;
        public int TrainLife;
        public int TrainArmor;
        public int TrainResist;

        public long Tempo;
        public bool FirstTempoPurchaseDone;
        public long Speed3UntilUtcTicks;
        public bool MonthlyPass;
        public int StashExtraRows;

        public int PresetSlots = 3;
        public int ActivePreset;
        public int PvpPreset = -1;
        public List<EquipmentPresetState> Presets = new List<EquipmentPresetState>();

        public QuestState Quests = new QuestState();
        public AttendanceState Attendance = new AttendanceState();
        public LifetimeStats Lifetime = new LifetimeStats();
        public PlayerSettings Settings = new PlayerSettings();

        public int TotalAllocated => AllocatedStrength + AllocatedDexterity + AllocatedIntelligence;
    }
}
