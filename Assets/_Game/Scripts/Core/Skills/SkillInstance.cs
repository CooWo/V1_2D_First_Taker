using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Skills
{
    /// <summary>
    /// 장비에 내장된 스킬 한 개의 런타임 상태 (정의 + 등급 + 쿨타임 + 스킬별 상태).
    /// </summary>
    public sealed class SkillInstance
    {
        public SkillInstance(SkillDefinition definition, SkillRarity rarity, object source = null)
        {
            Definition = definition;
            Rarity = rarity;
            Profile = SkillRarityProfile.For(rarity);
            Source = source;
        }

        public SkillDefinition Definition { get; }
        public SkillRarity Rarity { get; }
        public SkillRarityProfile Profile { get; }
        /// <summary>이 스킬을 부여한 장비 (기본 공격은 null).</summary>
        public object Source { get; }

        public float CooldownRemaining { get; private set; }
        public float LastCooldown { get; private set; }
        public bool IsReady => CooldownRemaining <= 0f;

        /// <summary>오라/공전체 등 켜져 있는 지속형 스킬 여부.</summary>
        public bool IsActive { get; set; }
        /// <summary>N번째 타격 같은 스킬 고유 카운터.</summary>
        public int Counter { get; set; }
        /// <summary>스킬 동작이 보관하는 상태 (예: 칼날 소용돌이 장판 참조).</summary>
        public object State { get; set; }

        public SkillTag Tags => Definition.Tags;

        public float GetCooldown(IStatReader stats)
        {
            float cdr = Mathf.Clamp(stats.Get(StatType.CooldownReductionPct), 0f, 60f);
            return Definition.Cooldown * Profile.CooldownMultiplier * (1f - cdr / 100f);
        }

        public float GetCost(IStatReader stats)
        {
            float reduction = Mathf.Clamp(stats.Get(StatType.ResourceCostReductionPct), 0f, 80f);
            return Definition.ResourceCost * Profile.CostMultiplier * (1f - reduction / 100f);
        }

        public void StartCooldown(float seconds)
        {
            CooldownRemaining = seconds;
            LastCooldown = seconds;
        }

        public void ResetCooldown() => CooldownRemaining = 0f;

        public void ReduceCooldown(float seconds) => CooldownRemaining = Mathf.Max(0f, CooldownRemaining - seconds);

        public float CooldownFraction => LastCooldown <= 0f ? 0f : Mathf.Clamp01(CooldownRemaining / LastCooldown);

        public void Tick(float dt)
        {
            if (CooldownRemaining > 0f) CooldownRemaining -= dt;
        }

        public override string ToString() => $"{Definition.Name} [{Rarity}]";
    }
}
