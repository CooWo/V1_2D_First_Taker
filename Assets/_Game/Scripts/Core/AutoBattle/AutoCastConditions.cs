using ShadowRPG.Core.Skills;

namespace ShadowRPG.Core.AutoBattle
{
    /// <summary>조건 조립용 팩토리. 정의 데이터에서 읽기 쉽게 쓰기 위한 정적 생성자 모음입니다.</summary>
    public static class When
    {
        public static IAutoCastCondition ResourceAtLeast(float fraction) => new ResourceAtLeastCondition(fraction);
        public static IAutoCastCondition EnemiesWithin(float radius, int count) => new EnemiesWithinCondition(radius, count);
        public static IAutoCastCondition LifeBelow(float fraction) => new LifeBelowCondition(fraction);
        public static IAutoCastCondition EliteOrBossWithin(float radius) => new EliteOrBossCondition(radius);
        public static IAutoCastCondition Any(params IAutoCastCondition[] conditions) => new AnyCondition(conditions);
    }

    public sealed class ResourceAtLeastCondition : IAutoCastCondition
    {
        readonly float _fraction;
        public ResourceAtLeastCondition(float fraction) => _fraction = fraction;
        public bool IsMet(AutoBattleContext context, SkillInstance skill) => context.Self.Resource.Fraction >= _fraction;
    }

    public sealed class EnemiesWithinCondition : IAutoCastCondition
    {
        readonly float _radius;
        readonly int _count;

        public EnemiesWithinCondition(float radius, int count)
        {
            _radius = radius;
            _count = count;
        }

        public bool IsMet(AutoBattleContext context, SkillInstance skill) => context.CountEnemiesWithin(_radius) >= _count;
    }

    public sealed class LifeBelowCondition : IAutoCastCondition
    {
        readonly float _fraction;
        public LifeBelowCondition(float fraction) => _fraction = fraction;
        public bool IsMet(AutoBattleContext context, SkillInstance skill) => context.Self.Health.LifeFraction <= _fraction;
    }

    public sealed class EliteOrBossCondition : IAutoCastCondition
    {
        readonly float _radius;
        public EliteOrBossCondition(float radius) => _radius = radius;
        public bool IsMet(AutoBattleContext context, SkillInstance skill) => context.FindEliteOrBoss(_radius) != null;
    }

    public sealed class AnyCondition : IAutoCastCondition
    {
        readonly IAutoCastCondition[] _conditions;
        public AnyCondition(IAutoCastCondition[] conditions) => _conditions = conditions;

        public bool IsMet(AutoBattleContext context, SkillInstance skill)
        {
            foreach (var condition in _conditions)
                if (condition.IsMet(context, skill)) return true;
            return false;
        }
    }
}
