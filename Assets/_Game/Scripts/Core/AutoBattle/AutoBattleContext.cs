using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.AutoBattle
{
    /// <summary>자동 사냥 규칙이 판단에 쓰는 정보와 시전 창구.</summary>
    public sealed class AutoBattleContext
    {
        public AutoBattleContext(Combatant self, ICombatWorld world)
        {
            Self = self;
            World = world;
        }

        public Combatant Self { get; }
        public ICombatWorld World { get; }
        public SkillLoadout Loadout => Self.Loadout;
        public SkillExecutor Executor => World.Executor;
        public BalanceSettings Balance => World.Balance;

        /// <summary>싸울 적이 없을 때 걸어갈 목적지 (다음 층 포탈 등).</summary>
        public Vector2? Destination { get; set; }

        public Combatant NearestEnemy { get; private set; }
        public float NearestDistance { get; private set; } = float.MaxValue;
        public bool PrimaryIsMelee => Loadout == null || Loadout.PrimaryIsMelee;

        public void Refresh()
        {
            NearestEnemy = World.FindNearestEnemy(Self.Team, Self.Position, Balance.EnemySearchRadius);
            NearestDistance = NearestEnemy != null ? Self.DistanceTo(NearestEnemy) : float.MaxValue;
        }

        public int CountEnemiesWithin(float radius) => SkillPrimitives.CountEnemies(World, Self.Team, Self.Position, radius);

        public Combatant FindEliteOrBoss(float range)
        {
            var list = ListPool<Combatant>.Get();
            World.QueryEnemies(Self.Team, Self.Position, range, list);
            Combatant best = null;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (!c.IsEliteOrBoss) continue;
                if (best == null || c.Rank > best.Rank) best = c;
            }
            ListPool<Combatant>.Release(list);
            return best;
        }

        public Combatant LowestLifeEnemy(float range)
        {
            var list = ListPool<Combatant>.Get();
            World.QueryEnemies(Self.Team, Self.Position, range, list);
            Combatant best = null;
            for (int i = 0; i < list.Count; i++)
                if (best == null || list[i].Health.LifeFraction < best.Health.LifeFraction) best = list[i];
            ListPool<Combatant>.Release(list);
            return best;
        }

        public Vector2 DensestPoint(float range, float clusterRadius)
            => SkillPrimitives.DensestPoint(World, Self.Team, Self.Position, range, clusterRadius);

        /// <summary>
        /// 등급별 조준: 일반은 가장 가까운 적, 마법 이상은 스마트 타겟팅
        /// (범위기는 밀집 지역, 단일기는 체력 낮은 적).
        /// </summary>
        public Combatant ChooseTarget(SkillInstance skill)
        {
            float range = Mathf.Max(skill.Definition.Range, 1f);
            if (!skill.Profile.SmartTargeting) return World.FindNearestEnemy(Self.Team, Self.Position, range);

            if (skill.Tags.HasAny(SkillTag.Area | SkillTag.Chain))
            {
                var dense = DensestPoint(range, 2.5f);
                return World.FindNearestEnemy(Self.Team, dense, 2.5f) ?? World.FindNearestEnemy(Self.Team, Self.Position, range);
            }
            return LowestLifeEnemy(range) ?? World.FindNearestEnemy(Self.Team, Self.Position, range);
        }

        public bool TryCast(SkillInstance skill, Combatant target)
        {
            var point = target != null ? target.Position : Self.Position + Self.Facing * 2f;
            return Executor.TryCast(Self, skill, target, point);
        }

        public bool TryCastAtPoint(SkillInstance skill, Vector2 point, Combatant target = null)
            => Executor.TryCast(Self, skill, target, point);

        public bool ConditionMet(SkillInstance skill)
            => skill.Definition.Condition == null || skill.Definition.Condition.IsMet(this, skill);

        public IEnumerable<SkillInstance> Skills(AutoCastCategory category)
            => Loadout != null ? Loadout.ByCategory(category) : System.Array.Empty<SkillInstance>();
    }

    /// <summary>스킬별 추가 자동 시전 조건 (GDD 5장 "자동 사냥 AI 조건" 열).</summary>
    public interface IAutoCastCondition
    {
        bool IsMet(AutoBattleContext context, SkillInstance skill);
    }
}
