using System;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Character
{
    /// <summary>경험치·레벨. 레벨업 시 3대 코어 스탯이 오릅니다 (친화 스탯은 더 많이).</summary>
    public sealed class PlayerProgress
    {
        const float PointsPerLevel = 2f;
        const float AffinityBonus = 3f;

        readonly Combatant _owner;
        readonly object _source = new object();

        public PlayerProgress(Combatant owner) => _owner = owner;

        public int Level { get; private set; } = 1;
        public float Experience { get; private set; }
        public float ExperienceToNext => Mathf.Round(25f * Mathf.Pow(Level, 1.35f));

        /// <summary>레벨업 보너스를 몰아줄 스탯 (현재 직업 친화 스탯).</summary>
        public CoreStat Affinity { get; set; } = CoreStat.Strength;

        public event Action<int> LeveledUp;

        /// <summary>저장된 레벨/경험치 복원.</summary>
        public void Restore(int level, float experience)
        {
            Level = Mathf.Max(1, level);
            Experience = Mathf.Max(0f, experience);
            ApplyLevelBonus();
        }

        /// <summary>경험치 획득 (트리의 경험치 보너스 % 적용).</summary>
        public void Gain(float amount)
        {
            Experience += amount * (1f + _owner.Stats.Get(StatType.ExperiencePct) / 100f);
            bool leveled = false;
            while (Experience >= ExperienceToNext)
            {
                Experience -= ExperienceToNext;
                Level++;
                leveled = true;
            }
            if (!leveled) return;

            ApplyLevelBonus();
            _owner.Health.Heal(_owner.Health.MaxLife);
            LeveledUp?.Invoke(Level);
        }

        public void ApplyLevelBonus()
        {
            _owner.Stats.RemoveSource(_source);
            float gained = (Level - 1) * PointsPerLevel;
            float affinity = (Level - 1) * AffinityBonus;
            _owner.Stats.AddModifier(new StatModifier(StatType.Strength, gained + (Affinity == CoreStat.Strength ? affinity : 0f), _source));
            _owner.Stats.AddModifier(new StatModifier(StatType.Dexterity, gained + (Affinity == CoreStat.Dexterity ? affinity : 0f), _source));
            _owner.Stats.AddModifier(new StatModifier(StatType.Intelligence, gained + (Affinity == CoreStat.Intelligence ? affinity : 0f), _source));
            _owner.Stats.AddModifier(new StatModifier(StatType.MaxLife, (Level - 1) * 12f, _source));
        }
    }
}
