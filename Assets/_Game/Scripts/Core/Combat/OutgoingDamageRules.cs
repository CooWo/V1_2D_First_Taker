using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Combat
{
    /// <summary>태그별 "피해 증가 %" 합산. [주문] 태그가 붙은 근접 공격(비전 마검격)은 주문 피해 증가도 그대로 받습니다.</summary>
    public sealed class TagScalingRule : IOutgoingDamageRule
    {
        public void Apply(ref OutgoingDamage damage)
        {
            var attacker = damage.Request.Attacker;
            if (attacker == null) return;

            var stats = attacker.Stats;
            var tags = damage.Request.Tags;
            var type = damage.Request.Type;

            float pct = stats.Get(StatType.AllDamagePct);
            if (tags.IsMelee()) pct += stats.Get(StatType.MeleeDamagePct);
            if (tags.HasAny(SkillTag.Projectile)) pct += stats.Get(StatType.ProjectileDamagePct);
            if (tags.HasAny(SkillTag.Spell)) pct += stats.Get(StatType.SpellDamagePct);
            if (type.IsElemental()) pct += stats.Get(StatType.ElementalDamagePct);
            pct += stats.Get(type.DamageStat());

            damage.IncreasedPct += pct;
        }
    }

    /// <summary>GDD 4.1-③: 근접 1.4배 / 원거리 1.0배 최종 계수.</summary>
    public sealed class RangeAsymmetryRule : IOutgoingDamageRule
    {
        readonly BalanceSettings _balance;

        public RangeAsymmetryRule(BalanceSettings balance) => _balance = balance;

        public void Apply(ref OutgoingDamage damage)
        {
            var tags = damage.Request.Tags;
            if (tags.IsMelee()) damage.More *= _balance.MeleeDamageMultiplier;
            else if (tags.IsRanged()) damage.More *= _balance.RangedDamageMultiplier;
        }
    }

    /// <summary>세트·전설 권능의 태그 조건부 최종 곱연산.</summary>
    public sealed class TagMultiplierRule : IOutgoingDamageRule
    {
        public void Apply(ref OutgoingDamage damage)
        {
            var attacker = damage.Request.Attacker;
            if (attacker == null) return;
            damage.More *= attacker.TagMultipliers.For(damage.Request.Tags);
        }
    }

    public sealed class BossDamageRule : IOutgoingDamageRule
    {
        public void Apply(ref OutgoingDamage damage)
        {
            var attacker = damage.Request.Attacker;
            var target = damage.Request.Target;
            if (attacker == null || target == null || !target.IsEliteOrBoss) return;
            damage.More *= 1f + attacker.Stats.Get(StatType.BossDamagePct) / 100f;
        }
    }

    /// <summary>치명타 판정. 대상의 표식(받는 치명타 증가)까지 반영합니다.</summary>
    public sealed class CriticalStrikeRule : IOutgoingDamageRule
    {
        readonly BalanceSettings _balance;
        readonly IRandom _random;

        public CriticalStrikeRule(BalanceSettings balance, IRandom random)
        {
            _balance = balance;
            _random = random;
        }

        public void Apply(ref OutgoingDamage damage)
        {
            var request = damage.Request;
            if (!request.CanCrit || request.Attacker == null) return;

            var attacker = request.Attacker;
            var target = request.Target;

            float chance = attacker.Stats.Get(StatType.CritChancePct) + request.CritChanceBonus;
            float critDamage = attacker.Stats.Get(StatType.CritDamagePct);
            if (target != null)
            {
                chance += target.Statuses.CritChanceTakenBonus;
                critDamage += target.Statuses.CritDamageTakenBonus;
            }

            damage.CritChancePct = Mathf.Min(chance, _balance.MaxCritChancePct);
            damage.CritMultiplier = _balance.BaseCritMultiplier + critDamage / 100f;
            damage.IsCrit = _random.Chance(damage.CritChancePct);
        }
    }
}
