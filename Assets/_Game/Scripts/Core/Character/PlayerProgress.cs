using System;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tuning;
using UnityEngine;

namespace ShadowRPG.Core.Character
{
    /// <summary>
    /// 경험치·레벨. 레벨업 = 스탯 포인트(기본 1)를 받아 힘/민첩/지능에 직접 분배합니다 (방향성 v1 §6).
    /// 레벨 자체는 생명력만 조금 올려 줍니다. 분배 상태는 프로필이 들고, 여기서는 적용만 합니다.
    /// </summary>
    public sealed class PlayerProgress
    {
        readonly Combatant _owner;
        readonly object _source = new object();
        readonly GrowthTuning _tuning;

        public PlayerProgress(Combatant owner, GrowthTuning tuning = null)
        {
            _owner = owner;
            _tuning = tuning ?? GameTuning.Current.Growth;
        }

        public int Level { get; private set; } = 1;
        public float Experience { get; private set; }
        public float ExperienceToNext => Mathf.Round(_tuning.ExpBase * Mathf.Pow(Level, _tuning.ExpExponent));

        public int AllocatedStrength { get; private set; }
        public int AllocatedDexterity { get; private set; }
        public int AllocatedIntelligence { get; private set; }

        /// <summary>호환용 (예전 친화 스탯). 지금은 쓰이지 않습니다.</summary>
        public CoreStat Affinity { get; set; } = CoreStat.Strength;

        /// <summary>새 레벨, 이번에 오른 단계 수.</summary>
        public event Action<int, int> LeveledUpBy;
        public event Action<int> LeveledUp;

        public void Restore(int level, float experience)
        {
            Level = Mathf.Max(1, level);
            Experience = Mathf.Max(0f, experience);
            ApplyLevelBonus();
        }

        public void SetAllocation(int strength, int dexterity, int intelligence)
        {
            AllocatedStrength = Mathf.Max(0, strength);
            AllocatedDexterity = Mathf.Max(0, dexterity);
            AllocatedIntelligence = Mathf.Max(0, intelligence);
            ApplyLevelBonus();
        }

        /// <summary>경험치 획득 (트리의 경험치 보너스 % 적용).</summary>
        public void Gain(float amount)
        {
            Experience += amount * (1f + _owner.Stats.Get(StatType.ExperiencePct) / 100f);
            int gained = 0;
            while (Experience >= ExperienceToNext)
            {
                Experience -= ExperienceToNext;
                Level++;
                gained++;
            }
            if (gained == 0) return;

            ApplyLevelBonus();
            _owner.Health.Heal(_owner.Health.MaxLife);
            LeveledUpBy?.Invoke(Level, gained);
            LeveledUp?.Invoke(Level);
        }

        public void ApplyLevelBonus()
        {
            _owner.Stats.RemoveSource(_source);
            _owner.Stats.AddModifier(new StatModifier(StatType.Strength, AllocatedStrength, _source));
            _owner.Stats.AddModifier(new StatModifier(StatType.Dexterity, AllocatedDexterity, _source));
            _owner.Stats.AddModifier(new StatModifier(StatType.Intelligence, AllocatedIntelligence, _source));
            _owner.Stats.AddModifier(new StatModifier(StatType.MaxLife, (Level - 1) * _tuning.LifePerLevel, _source));
        }
    }
}
