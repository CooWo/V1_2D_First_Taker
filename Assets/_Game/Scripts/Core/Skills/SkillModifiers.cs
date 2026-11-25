using ShadowRPG.Core.Combat;

namespace ShadowRPG.Core.Skills
{
    /// <summary>
    /// 한 번의 시전에 적용되는 형태 변화 묶음. 보조 권능·능력치·세트가 채워 넣고 스킬 동작이 읽습니다.
    /// </summary>
    public sealed class SkillModifiers
    {
        public int ExtraProjectiles;
        public int ExtraChains;
        public int ExtraPierce;
        public bool Fork;
        public bool Echo;
        public float DamageMultiplier = 1f;
        public float AreaMultiplier = 1f;
        /// <summary>물리 피해를 이 속성으로 100% 전환 (SUP-06).</summary>
        public DamageType? ConvertPhysicalTo;

        public void Reset()
        {
            ExtraProjectiles = 0;
            ExtraChains = 0;
            ExtraPierce = 0;
            Fork = false;
            Echo = false;
            DamageMultiplier = 1f;
            AreaMultiplier = 1f;
            ConvertPhysicalTo = null;
        }
    }

    /// <summary>시전 직전에 SkillModifiers를 채우는 공급자 (보조 권능, 능력치 등).</summary>
    public interface ISkillModifierSource
    {
        void Modify(Actors.Combatant caster, SkillInstance skill, SkillModifiers modifiers);
    }

    /// <summary>능력치(추가 투사체·연쇄·관통·범위)를 시전 형태로 옮깁니다.</summary>
    public sealed class StatSkillModifierSource : ISkillModifierSource
    {
        public void Modify(Actors.Combatant caster, SkillInstance skill, SkillModifiers modifiers)
        {
            var stats = caster.Stats;
            var tags = skill.Tags;
            if (tags.HasAny(SkillTag.Projectile))
            {
                modifiers.ExtraProjectiles += UnityEngine.Mathf.RoundToInt(stats.Get(Stats.StatType.ExtraProjectiles));
                modifiers.ExtraPierce += UnityEngine.Mathf.RoundToInt(stats.Get(Stats.StatType.ExtraPierce));
            }
            if (tags.HasAny(SkillTag.Chain | SkillTag.Projectile))
                modifiers.ExtraChains += UnityEngine.Mathf.RoundToInt(stats.Get(Stats.StatType.ExtraChains));
            if (tags.HasAny(SkillTag.Area))
                modifiers.AreaMultiplier *= 1f + stats.Get(Stats.StatType.AreaPct) / 100f;
        }
    }
}
