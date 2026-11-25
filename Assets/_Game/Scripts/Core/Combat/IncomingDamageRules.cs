using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Combat
{
    /// <summary>[공격] 태그 피해만 회피할 수 있습니다.</summary>
    public sealed class EvasionRule : IIncomingDamageRule
    {
        readonly BalanceSettings _balance;
        readonly IRandom _random;

        public EvasionRule(BalanceSettings balance, IRandom random)
        {
            _balance = balance;
            _random = random;
        }

        public void Apply(ref IncomingDamage damage)
        {
            if (!damage.Tags.HasAny(SkillTag.Attack)) return;
            float chance = Mathf.Min(damage.Target.Stats.Get(StatType.EvasionPct), _balance.MaxEvasionPct);
            if (_random.Chance(chance)) damage.Evaded = true;
        }
    }

    /// <summary>방패 막기: 막으면 피해를 무효화합니다 (복수의 가시 갑주 트리거 조건).</summary>
    public sealed class BlockRule : IIncomingDamageRule
    {
        readonly BalanceSettings _balance;
        readonly IRandom _random;

        public BlockRule(BalanceSettings balance, IRandom random)
        {
            _balance = balance;
            _random = random;
        }

        public void Apply(ref IncomingDamage damage)
        {
            float chance = Mathf.Min(damage.Target.Stats.Get(StatType.BlockChancePct), _balance.MaxBlockPct);
            if (chance <= 0f || !_random.Chance(chance)) return;
            damage.Blocked = true;
            damage.Amount = 0f;
        }
    }

    /// <summary>방어도: 물리 피해 감소. 큰 한 방일수록 효율이 떨어지는 PoE식 공식입니다.</summary>
    public sealed class ArmorRule : IIncomingDamageRule
    {
        public void Apply(ref IncomingDamage damage)
        {
            if (damage.Type != DamageType.Physical || damage.Amount <= 0f) return;
            var stats = damage.Target.Stats;
            float armor = stats.Get(StatType.Armor) * (1f + stats.Get(StatType.ArmorEffectPct) / 100f);
            if (armor <= 0f) return;
            float reduction = Mathf.Clamp(armor / (armor + 10f * damage.Amount), 0f, 0.9f);
            damage.Amount *= 1f - reduction;
        }
    }

    public sealed class ResistanceRule : IIncomingDamageRule
    {
        readonly BalanceSettings _balance;

        public ResistanceRule(BalanceSettings balance) => _balance = balance;

        public void Apply(ref IncomingDamage damage)
        {
            if (damage.Type == DamageType.Physical) return;
            float resist = Mathf.Min(damage.Target.Stats.Get(StatType.AllResistPct), _balance.MaxResistPct);
            damage.Amount *= 1f - resist / 100f;
        }
    }

    /// <summary>정예/보스가 주는 피해 감소 (몸 방어구 옵션).</summary>
    public sealed class EliteDamageReductionRule : IIncomingDamageRule
    {
        public void Apply(ref IncomingDamage damage)
        {
            if (damage.Attacker == null || !damage.Attacker.IsEliteOrBoss) return;
            float pct = Mathf.Clamp(damage.Target.Stats.Get(StatType.EliteDamageReductionPct), 0f, 75f);
            damage.Amount *= 1f - pct / 100f;
        }
    }

    /// <summary>상태 효과(그림자 갑주, 감전, 무적 등)에 의한 최종 조정.</summary>
    public sealed class StatusModifierRule : IIncomingDamageRule
    {
        public void Apply(ref IncomingDamage damage) => damage.Amount = damage.Target.Statuses.ModifyIncoming(damage.Amount);
    }
}
