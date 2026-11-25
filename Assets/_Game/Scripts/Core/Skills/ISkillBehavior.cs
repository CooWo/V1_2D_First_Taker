using ShadowRPG.Core.Actors;
using ShadowRPG.Core.World;

namespace ShadowRPG.Core.Skills
{
    /// <summary>스킬 실제 동작 (Strategy). 정의와 분리되어 있어 동작만 교체·추가할 수 있습니다.</summary>
    public interface ISkillBehavior
    {
        void Execute(SkillCast cast);
    }

    /// <summary>켜 두는 형태의 스킬 (오라, 공전체). 장비를 벗으면 꺼야 합니다.</summary>
    public interface IPersistentSkillBehavior
    {
        void Deactivate(SkillInstance skill, Combatant caster, ICombatWorld world);
    }

    /// <summary>스킬이 시전된 직후 알림을 받는 관찰자 (그림자 갑주 감소, 트리거 등).</summary>
    public interface ICastListener
    {
        void OnSkillCast(SkillCast cast);
    }
}
