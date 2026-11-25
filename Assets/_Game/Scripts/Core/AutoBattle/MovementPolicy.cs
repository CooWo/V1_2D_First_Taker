using ShadowRPG.Core.Combat;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.AutoBattle
{
    /// <summary>수동 이동 입력 (가상 조이스틱, 키보드 등). 입력 장치를 Core가 모르도록 분리합니다.</summary>
    public interface IMoveInput
    {
        Vector2 Direction { get; }
    }

    public sealed class NoMoveInput : IMoveInput
    {
        public Vector2 Direction => Vector2.zero;
    }

    /// <summary>자동 사냥 이동 결정.</summary>
    public interface IMovementPolicy
    {
        /// <summary>이번 프레임 이동 방향 (정지면 zero).</summary>
        Vector2 Decide(AutoBattleContext context, float dt);
    }

    /// <summary>
    /// 근접: 사거리까지 접근, 3.5m 이내면 그림자 추적 대시(0.15초)로 즉시 파고듭니다 (GDD 4.1-②).
    /// 원거리: 사거리 안에서 거리를 유지하고, 너무 가까우면 물러섭니다 (카이팅).
    /// </summary>
    public sealed class AutoMovementPolicy : IMovementPolicy
    {
        float _shadowStepCooldown;

        public Vector2 Decide(AutoBattleContext context, float dt)
        {
            _shadowStepCooldown -= dt;

            var self = context.Self;
            if (!self.CanAct && !self.IsDashing) return Vector2.zero;

            // 1) 예고 장판 회피가 최우선
            var hazards = context.World.Hazards;
            for (int i = 0; i < hazards.Count; i++)
            {
                var hazard = hazards[i];
                if (hazard.Team != self.Team && hazard.Contains(self.Position, self.Radius + 0.35f))
                    return hazard.EscapeDirection(self.Position);
            }

            var world = context.World;
            var enemy = context.NearestEnemy;
            if (enemy == null)
            {
                // 2) 주변에 적이 없으면: 목적지 → 없으면 층 어딘가 남은 적을 찾아 탐색 (벽을 돌아감)
                if (context.Destination.HasValue)
                {
                    var goal = context.Destination.Value;
                    return (goal - self.Position).sqrMagnitude > 0.04f ? world.SteerTowards(self.Position, goal) : Vector2.zero;
                }
                var far = world.FindNearestEnemy(self.Team, self.Position, 999f);
                return far != null ? world.SteerTowards(self.Position, far.Position) : Vector2.zero;
            }

            float range = EngageRange(context);
            float gap = context.NearestDistance - enemy.Radius;
            bool visible = world.CanSee(self.Position, enemy.Position);
            var toward = world.SteerTowards(self.Position, enemy.Position);

            // 벽 너머의 적은 보일 때까지 길을 따라 접근
            if (!visible) return toward;

            return context.PrimaryIsMelee
                ? Melee(context, enemy.Position, toward, gap, range)
                : Ranged(toward, gap, range);
        }

        Vector2 Melee(AutoBattleContext context, Vector2 enemyPosition, Vector2 toward, float gap, float range)
        {
            if (gap <= range * 0.85f) return Vector2.zero;

            var balance = context.Balance;
            var self = context.Self;
            if (gap <= balance.ShadowStepRadius && _shadowStepCooldown <= 0f && self.CanAct && context.World.IsWalkable(enemyPosition))
            {
                var destination = enemyPosition - toward * Mathf.Max(0.5f, range * 0.6f);
                destination = context.World.ClampToBounds(destination);
                context.World.Emit(VfxEvent.Line(VfxKind.DashTrail, self.Position, destination, DamageType.Shadow, 0.15f));
                self.BeginDash(destination, balance.ShadowStepDuration);
                _shadowStepCooldown = balance.ShadowStepCooldown;
                return Vector2.zero;
            }
            return toward;
        }

        static Vector2 Ranged(Vector2 toward, float gap, float range)
        {
            float preferred = Mathf.Min(range * 0.75f, 6f);
            if (gap < preferred * 0.5f) return -toward;
            if (gap > range * 0.95f) return toward;
            return Vector2.zero;
        }

        static float EngageRange(AutoBattleContext context)
        {
            var loadout = context.Loadout;
            if (loadout == null) return 1.5f;
            var primary = loadout.Primary;
            if (primary != null && context.Self.Resource.CanAfford(primary.GetCost(context.Self.Stats)))
                return Mathf.Min(primary.Definition.Range, 9f);
            return loadout.BasicAttack != null ? loadout.BasicAttack.Definition.Range : 1.5f;
        }
    }
}
