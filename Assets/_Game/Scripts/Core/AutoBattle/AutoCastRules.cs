using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Skills;
using UnityEngine;

namespace ShadowRPG.Core.AutoBattle
{
    /// <summary>자동 시전 우선순위 큐의 한 단계 (GDD 4.2). 새 단계는 규칙 클래스를 추가합니다 (OCP).</summary>
    public interface IAutoCastRule
    {
        int Priority { get; }
        string Name { get; }
        bool TryExecute(AutoBattleContext context);
    }

    /// <summary>1순위: 상시 오라 / 패시브 트리거 — 전투 진입 시 자동 활성화.</summary>
    public sealed class AuraRule : IAutoCastRule
    {
        public int Priority => 1;
        public string Name => "오라";

        public bool TryExecute(AutoBattleContext context)
        {
            if (context.NearestEnemy == null) return false;
            foreach (var skill in context.Skills(AutoCastCategory.Aura))
                if (!skill.IsActive && context.TryCast(skill, null)) return true;
            return false;
        }
    }

    /// <summary>2순위: 생존 반응기 — 생명력 50% 이하 또는 보호막 파괴 시 즉시.</summary>
    public sealed class SurvivalRule : IAutoCastRule
    {
        public int Priority => 2;
        public string Name => "생존";

        public bool TryExecute(AutoBattleContext context)
        {
            var health = context.Self.Health;
            bool danger = health.LifeFraction <= context.Balance.SurvivalLifeThreshold || health.SinceEnergyShieldBroken < 0.3f;
            if (!danger) return false;

            foreach (var skill in context.Skills(AutoCastCategory.Survival))
                if (context.ConditionMet(skill) && context.TryCast(skill, context.NearestEnemy)) return true;
            return false;
        }
    }

    /// <summary>
    /// 3순위: 회피/포지셔닝 — 원거리 주력이면 포위 시 이탈, 근접 주력이면 4m 밖 다음 무리로 돌진.
    /// </summary>
    public sealed class MobilityRule : IAutoCastRule
    {
        public int Priority => 3;
        public string Name => "이동기";

        public bool TryExecute(AutoBattleContext context)
        {
            if (context.NearestEnemy == null) return false;
            var balance = context.Balance;
            var self = context.Self;

            foreach (var skill in context.Skills(AutoCastCategory.Mobility))
            {
                if (!context.ConditionMet(skill)) continue;

                if (!context.PrimaryIsMelee)
                {
                    if (context.CountEnemiesWithin(balance.RangedThreatRadius) < balance.RangedThreatCount) continue;
                    var away = self.Position - ThreatCentroid(context, 3f);
                    if (away.sqrMagnitude < 0.01f) away = -self.Facing;
                    var escape = self.Position + away.normalized * 6f;
                    if (context.TryCastAtPoint(skill, context.World.ClampToBounds(escape))) return true;
                }
                else
                {
                    if (context.NearestDistance <= balance.MeleeEngageDistance) continue;
                    var pack = context.DensestPoint(balance.EnemySearchRadius, 2.5f);
                    if (Vector2.Distance(self.Position, pack) <= balance.MeleeEngageDistance) continue;
                    if (context.TryCastAtPoint(skill, pack)) return true;
                }
            }
            return false;
        }

        static Vector2 ThreatCentroid(AutoBattleContext context, float radius)
        {
            var list = ListPool<Combatant>.Get();
            context.World.QueryEnemies(context.Self.Team, context.Self.Position, radius, list);
            var sum = Vector2.zero;
            foreach (var c in list) sum += c.Position;
            var centroid = list.Count > 0 ? sum / list.Count : context.Self.Position;
            ListPool<Combatant>.Release(list);
            return centroid;
        }
    }

    /// <summary>4순위: 전술 버프/표식 — 정예·보스 감지 또는 버프 만료 시 갱신.</summary>
    public sealed class TacticalRule : IAutoCastRule
    {
        public int Priority => 4;
        public string Name => "전술";

        public bool TryExecute(AutoBattleContext context)
        {
            foreach (var skill in context.Skills(AutoCastCategory.Tactical))
            {
                float range = Mathf.Max(skill.Definition.Range, 4f);
                var target = context.FindEliteOrBoss(range) ?? context.World.FindNearestEnemy(context.Self.Team, context.Self.Position, range);
                if (target == null || !context.ConditionMet(skill)) continue;
                if (context.TryCast(skill, target)) return true;
            }
            return false;
        }
    }

    /// <summary>5순위: 고화력 쿨타임 / 조건부 폭발기.</summary>
    public sealed class BurstRule : IAutoCastRule
    {
        public int Priority => 5;
        public string Name => "폭발기";

        public bool TryExecute(AutoBattleContext context)
        {
            foreach (var skill in context.Skills(AutoCastCategory.Burst))
            {
                if (!skill.IsReady || !context.ConditionMet(skill)) continue;
                var target = context.ChooseTarget(skill);
                if (target == null) continue;

                var point = skill.Tags.HasAny(SkillTag.Area)
                    ? context.DensestPoint(skill.Definition.Range, 2.5f)
                    : target.Position;
                if (context.TryCastAtPoint(skill, point, target)) return true;
            }
            return false;
        }
    }

    /// <summary>6순위: 주력 자원 소모기 → 자원 부족 시 기본 평타로 즉시 전환.</summary>
    public sealed class PrimaryRule : IAutoCastRule
    {
        public int Priority => 6;
        public string Name => "주력기";

        readonly List<SkillInstance> _ordered = new List<SkillInstance>();

        public bool TryExecute(AutoBattleContext context)
        {
            var loadout = context.Loadout;
            if (loadout == null || context.NearestEnemy == null) return false;

            _ordered.Clear();
            if (loadout.Primary != null) _ordered.Add(loadout.Primary);
            foreach (var skill in loadout.ByCategory(AutoCastCategory.Primary))
                if (skill != loadout.Primary) _ordered.Add(skill);

            foreach (var skill in _ordered)
            {
                if (!context.Executor.CanCast(context.Self, skill) || !context.ConditionMet(skill)) continue;
                if (TryCastInRange(context, skill)) return true;
            }

            var basic = loadout.BasicAttack;
            return basic != null && TryCastInRange(context, basic);
        }

        static bool TryCastInRange(AutoBattleContext context, SkillInstance skill)
        {
            var target = context.ChooseTarget(skill);
            if (target == null) return false;
            float reach = skill.Definition.Range + target.Radius;
            if (context.Self.DistanceTo(target) > reach) return false;
            return context.TryCast(skill, target);
        }
    }
}
