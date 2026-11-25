using ShadowRPG.Core.World;

namespace ShadowRPG.Core.Skills.Behaviors
{
    /// <summary>기본 근접 평타 (자원 생성기). 자원이 부족할 때 자동 전환됩니다.</summary>
    public sealed class ShadowStrikeBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var target = cast.Target;
            if (target == null) return;
            cast.Vfx(VfxKind.Slash, target.Position, 0.7f, 0.15f);
            var result = cast.Hit(target);
            if (result.Landed) cast.Caster.Resource.Gain(4f);
        }
    }

    /// <summary>기본 원거리 평타 (자원 생성기).</summary>
    public sealed class ShadowBoltBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var bolt = SkillPrimitives.CreateProjectile(cast, cast.Origin, cast.Direction, 13f, 9f, 1f, "bolt");
            var baseHit = bolt.OnHit;
            bolt.OnHit = (p, target) =>
            {
                baseHit(p, target);
                cast.Caster.Resource.Gain(3f);
            };
            cast.World.SpawnProjectile(bolt);
        }
    }
}
