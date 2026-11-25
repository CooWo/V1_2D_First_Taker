using ShadowRPG.Core.Actors;

namespace ShadowRPG.Core.Combat
{
    public enum DamageType
    {
        Physical,
        Fire,
        Cold,
        Lightning,
        Shadow,
        Arcane
    }

    public static class DamageTypeExtensions
    {
        public static SkillTag ToTag(this DamageType type)
        {
            switch (type)
            {
                case DamageType.Fire: return SkillTag.Fire;
                case DamageType.Cold: return SkillTag.Cold;
                case DamageType.Lightning: return SkillTag.Lightning;
                case DamageType.Shadow: return SkillTag.Shadow;
                case DamageType.Arcane: return SkillTag.Arcane;
                default: return SkillTag.Physical;
            }
        }

        public static bool IsElemental(this DamageType type)
            => type == DamageType.Fire || type == DamageType.Cold || type == DamageType.Lightning;
    }

    /// <summary>공격자가 보내는 피해 요청 (배율 적용 전 기본 피해).</summary>
    public struct DamageRequest
    {
        public Combatant Attacker;
        public Combatant Target;
        public SkillTag Tags;
        public DamageType Type;
        public float BaseAmount;
        public bool CanCrit;
        public float CritChanceBonus;
    }

    /// <summary>공격 측 규칙들이 차례로 채워 나가는 계산 상태.</summary>
    public struct OutgoingDamage
    {
        public DamageRequest Request;
        /// <summary>합연산 증가 % 합계.</summary>
        public float IncreasedPct;
        /// <summary>곱연산 배율 누적.</summary>
        public float More;
        public float CritChancePct;
        public float CritMultiplier;
        public bool IsCrit;

        public float Total => Request.BaseAmount * (1f + IncreasedPct / 100f) * More * (IsCrit ? CritMultiplier : 1f);
    }

    /// <summary>방어 측 규칙들이 차례로 깎아 나가는 계산 상태.</summary>
    public struct IncomingDamage
    {
        public Combatant Attacker;
        public Combatant Target;
        public SkillTag Tags;
        public DamageType Type;
        public float Amount;
        public bool IsCrit;
        public bool Blocked;
        public bool Evaded;
    }

    public struct DamageResult
    {
        public Combatant Attacker;
        public Combatant Target;
        public SkillTag Tags;
        public DamageType Type;
        public float Amount;
        public bool IsCrit;
        public bool Blocked;
        public bool Evaded;
        public bool Killed;

        public bool Landed => !Evaded && Target != null;
    }

    /// <summary>공격 측 피해 규칙 (OCP: 새 규칙은 클래스를 추가해 DamageService에 등록).</summary>
    public interface IOutgoingDamageRule
    {
        void Apply(ref OutgoingDamage damage);
    }

    /// <summary>방어 측 피해 규칙.</summary>
    public interface IIncomingDamageRule
    {
        void Apply(ref IncomingDamage damage);
    }

    /// <summary>피해가 확정된 뒤 반응하는 관찰자 (적중 시 회복, 그림자 갑주, 트리거 등).</summary>
    public interface IDamageListener
    {
        void OnDamageResolved(in DamageResult result);
    }
}
