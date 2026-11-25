using ShadowRPG.Core.Actors;

namespace ShadowRPG.Core.Status
{
    /// <summary>버프/디버프/상태이상 공통 계약.</summary>
    public interface IStatusEffect
    {
        string Id { get; }
        float Remaining { get; }
        bool IsExpired { get; }
        void OnApplied(Combatant owner);
        void OnRemoved(Combatant owner);
        void Tick(Combatant owner, float dt);

        /// <summary>같은 Id 효과가 다시 걸렸을 때 (갱신·중첩 규칙은 효과가 스스로 정합니다).</summary>
        void Refresh(IStatusEffect incoming);
    }

    // 아래 역할 인터페이스는 필요한 효과만 골라 구현합니다 (ISP).

    /// <summary>받는 피해를 조정 (그림자 갑주 피해 감소, 감전 피해 증가 등).</summary>
    public interface IIncomingDamageModifier
    {
        float ModifyIncoming(float amount);
    }

    public interface IMoveSpeedModifier
    {
        float MoveSpeedMultiplier { get; }
    }

    /// <summary>넉백/히트스톱 면역.</summary>
    public interface IStaggerImmunity { }

    /// <summary>행동 불가 (빙결 등).</summary>
    public interface IActionDisabler { }

    /// <summary>대상이 받는 치명타 보정 (암살자의 표식).</summary>
    public interface ICritVulnerability
    {
        float CritChanceBonus { get; }
        float CritDamageBonus { get; }
    }

    /// <summary>상태이상(Ailment) 표시. 그림자 전령의 처치 폭발 조건에 쓰입니다.</summary>
    public interface IAilment { }
}
