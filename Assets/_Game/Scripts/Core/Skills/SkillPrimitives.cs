using System;
using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Status;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Skills
{
    /// <summary>여러 스킬이 공유하는 판정·투사체 생성 도우미. 스킬 동작은 이것을 조합해 만듭니다.</summary>
    public static class SkillPrimitives
    {
        public static int HitCircle(SkillCast cast, Vector2 center, float radius, float coefficientScale,
            Action<Combatant, DamageResult> onHit = null, SkillTag extraTags = SkillTag.None)
        {
            var targets = ListPool<Combatant>.Get();
            cast.World.QueryEnemies(cast.Team, center, radius, targets);
            int hits = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var result = cast.Hit(targets[i], coefficientScale, extraTags);
                if (!result.Landed) continue;
                hits++;
                onHit?.Invoke(targets[i], result);
            }
            ListPool<Combatant>.Release(targets);
            return hits;
        }

        public static int HitCone(SkillCast cast, Vector2 origin, Vector2 direction, float radius, float angleDegrees,
            float coefficientScale, Action<Combatant, DamageResult> onHit = null)
        {
            var targets = ListPool<Combatant>.Get();
            cast.World.QueryEnemies(cast.Team, origin, radius, targets);
            float halfAngle = angleDegrees * 0.5f;
            int hits = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var to = targets[i].Position - origin;
                bool inside = to.sqrMagnitude < 0.36f || Vector2.Angle(direction, to) <= halfAngle;
                if (!inside) continue;
                var result = cast.Hit(targets[i], coefficientScale);
                if (!result.Landed) continue;
                hits++;
                onHit?.Invoke(targets[i], result);
            }
            ListPool<Combatant>.Release(targets);
            return hits;
        }

        /// <summary>
        /// GDD 4.1-④: 기본 투사체는 첫 적중 시 소멸합니다. 관통·연쇄·갈래는 보조 권능/능력치에서만 얻습니다.
        /// </summary>
        public static Projectile CreateProjectile(SkillCast cast, Vector2 origin, Vector2 direction, float speed,
            float maxDistance, float coefficientScale, string visualKey)
        {
            var projectile = new Projectile
            {
                Owner = cast.Caster,
                Team = cast.Team,
                Position = origin,
                Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right,
                Speed = speed,
                MaxDistance = maxDistance,
                Element = cast.Element,
                VisualKey = visualKey,
                PierceRemaining = cast.Mods.ExtraPierce,
                ChainRemaining = cast.Mods.ExtraChains,
                CanFork = cast.Mods.Fork
            };
            projectile.OnHit = (p, target) => cast.Hit(target, coefficientScale, SkillTag.Projectile);
            return projectile;
        }

        /// <summary>기본 개수 + 추가 투사체 수만큼 부채꼴로 발사합니다.</summary>
        public static int FireSpread(SkillCast cast, int baseCount, float spreadDegrees, Vector2 direction, Func<Vector2, Projectile> factory)
        {
            int count = Mathf.Max(1, baseCount + cast.Mods.ExtraProjectiles);
            float spread = count > 1 ? Mathf.Max(spreadDegrees, 8f * (count - 1)) : 0f;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                var dir = CombatWorld.Rotate(direction, Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t));
                cast.World.SpawnProjectile(factory(dir));
            }
            return count;
        }

        /// <summary>주변 적이 가장 많이 몰린 지점 (밀집 지역 우선 조준).</summary>
        public static Vector2 DensestPoint(ITargetQuery query, Team team, Vector2 around, float searchRadius, float clusterRadius)
        {
            var candidates = ListPool<Combatant>.Get();
            var neighbours = ListPool<Combatant>.Get();
            query.QueryEnemies(team, around, searchRadius, candidates);

            var best = around;
            int bestCount = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                neighbours.Clear();
                query.QueryEnemies(team, candidates[i].Position, clusterRadius, neighbours);
                if (neighbours.Count <= bestCount) continue;
                bestCount = neighbours.Count;
                best = candidates[i].Position;
            }

            ListPool<Combatant>.Release(candidates);
            ListPool<Combatant>.Release(neighbours);
            return best;
        }

        public static int CountEnemies(ITargetQuery query, Team team, Vector2 center, float radius)
        {
            var list = ListPool<Combatant>.Get();
            query.QueryEnemies(team, center, radius, list);
            int count = list.Count;
            ListPool<Combatant>.Release(list);
            return count;
        }

        public static void Knockback(Combatant target, Vector2 from, float distance)
        {
            if (target.Statuses.IsStaggerImmune || target.Rank == EnemyRank.Boss) return;
            var dir = target.Position - from;
            if (dir.sqrMagnitude < 0.0001f) return;
            target.Position += dir.normalized * distance;
        }

        public static Vector2 RandomOffset(IRandom random, float radius)
        {
            float angle = random.Range(0f, 360f);
            float distance = Mathf.Sqrt(random.Value()) * radius;
            return CombatWorld.Rotate(Vector2.right, angle) * distance;
        }

        public static DamageOverTimeStatus Ignite(SkillCast cast, float duration, float dpsCoefficient)
            => new DamageOverTimeStatus(StatusIds.Ignite, duration, cast.ScaledDamage(dpsCoefficient));
    }
}
