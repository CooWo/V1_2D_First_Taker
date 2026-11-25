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
            var enemy = context.NearestEnemy;
            if (enemy == null || !self.CanAct && !self.IsDashing) return Vector2.zero;

            float range = EngageRange(context);
            float gap = context.NearestDistance - enemy.Radius;
            var toward = (enemy.Position - self.Position).normalized;

            return context.PrimaryIsMelee
                ? Melee(context, enemy.Position, toward, gap, range)
                : Ranged(toward, gap, range);
        }

        Vector2 Melee(AutoBattleContext context, Vector2 enemyPosition, Vector2 toward, float gap, float range)
        {
            if (gap <= range * 0.85f) return Vector2.zero;

            var balance = context.Balance;
            var self = context.Self;
            if (gap <= balance.ShadowStepRadius && _shadowStepCooldown <= 0f && self.CanAct)
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
