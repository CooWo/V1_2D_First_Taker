using ShadowRPG.Core.AutoBattle;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;

namespace ShadowRPG.Core.Skills
{
    /// <summary>
    /// GDD 4.2 자동 사냥 발동 우선순위 분류. 숫자가 작을수록 먼저 검사합니다.
    /// Reactive는 큐가 아니라 피격/막기 트리거로만 발동합니다.
    /// </summary>
    public enum AutoCastCategory
    {
        Aura = 1,
        Survival = 2,
        Mobility = 3,
        Tactical = 4,
        Burst = 5,
        Primary = 6,
        Reactive = 99
    }

    /// <summary>
    /// 스킬 정의 (불변 데이터 + 동작 전략). 동작은 ISkillBehavior로 분리되어 있어
    /// 새 스킬은 정의 하나와 동작 클래스 하나만 추가하면 됩니다 (OCP).
    /// </summary>
    public sealed class SkillDefinition
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string EnglishName { get; set; }
        public SkillTag Tags { get; set; }
        public CoreStat Attribute { get; set; }
        public AutoCastCategory Category { get; set; }
        public DamageType DamageType { get; set; }

        /// <summary>기본 피해 대비 계수.</summary>
        public float Coefficient { get; set; } = 1f;
        public float Cooldown { get; set; }
        public float ResourceCost { get; set; }
        /// <summary>자동 사냥이 시전을 시도하는 사거리.</summary>
        public float Range { get; set; } = 1.5f;
        /// <summary>기준 행동 시간(초). 공격은 공격 속도, 주문은 시전 속도로 나뉩니다.</summary>
        public float CastTime { get; set; } = 0.6f;
        /// <summary>오라가 점유하는 최대 자원 비율.</summary>
        public float Reservation { get; set; }

        public ISkillBehavior Behavior { get; set; }
        /// <summary>분류 기본 규칙 외에 추가로 만족해야 하는 자동 시전 조건 (없으면 null).</summary>
        public IAutoCastCondition Condition { get; set; }

        public string Summary { get; set; }
        public string RareGimmick { get; set; }
        public string LegendaryAwakening { get; set; }

        public bool IsMelee => Tags.IsMelee();
        public bool IsAttack => Tags.HasAny(SkillTag.Attack);
        public bool IsSpell => Tags.HasAny(SkillTag.Spell);

        public override string ToString() => $"{Code} {Name}";
    }
}
