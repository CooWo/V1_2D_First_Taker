using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Status;
using ShadowRPG.Core.World;

namespace ShadowRPG.Core.Combat
{
    /// <summary>
    /// GDD 4.1-① 그림자 갑주: [근접] 직접 타격 적중 시 4초 피해 30% 감소 + 경직 면역.
    /// [원거리] 스킬 시전 시 남은 지속시간이 절반으로 줄어듭니다.
    /// </summary>
    public sealed class ShadowFortifyRule : IDamageListener, ICastListener
    {
        readonly BalanceSettings _balance;

        public ShadowFortifyRule(BalanceSettings balance) => _balance = balance;

        public void OnDamageResolved(in DamageResult result)
        {
            var attacker = result.Attacker;
            if (attacker == null || !attacker.IsAlive || !result.Landed) return;
            if (!result.Tags.IsMelee() || result.Tags.HasAny(SkillTag.Trigger)) return;
            attacker.Statuses.Apply(new ShadowFortifyStatus(_balance.FortifyDuration, _balance.FortifyDamageReduction));
        }

        public void OnSkillCast(SkillCast cast)
        {
            if (!cast.Tags.IsRanged()) return;
            cast.Caster.Statuses.Get<ShadowFortifyStatus>()?.HalveRemaining();
        }
    }

    /// <summary>GDD 4.1-③ 적중 시 회복: 근접 100% / 원거리 45% 효율.</summary>
    public sealed class LifeOnHitRule : IDamageListener
    {
        readonly BalanceSettings _balance;

        public LifeOnHitRule(BalanceSettings balance) => _balance = balance;

        public void OnDamageResolved(in DamageResult result)
        {
            var attacker = result.Attacker;
            if (attacker == null || !attacker.IsAlive || !result.Landed || result.Amount <= 0f) return;

            float lifeOnHit = attacker.Stats.Get(StatType.LifeOnHit);
            if (lifeOnHit <= 0f) return;

            float efficiency = result.Tags.IsMelee() ? _balance.MeleeLifeOnHitEfficiency : _balance.RangedLifeOnHitEfficiency;
            attacker.Health.Heal(lifeOnHit * efficiency);
        }
    }

    /// <summary>치명타·막기 같은 결과를 연출 이벤트로 내보냅니다.</summary>
    public sealed class DamageVfxReporter : IDamageListener
    {
        readonly IEffectSpawner _effects;

        public DamageVfxReporter(IEffectSpawner effects) => _effects = effects;

        public void OnDamageResolved(in DamageResult result)
        {
            if (result.Target == null) return;
            string text = result.Evaded ? "회피" : result.Blocked ? "막기" : null;
            if (text == null) return;
            _effects.Emit(new VfxEvent { Kind = VfxKind.Text, Position = result.Target.Position, Text = text, Duration = 0.6f });
        }
    }
}
