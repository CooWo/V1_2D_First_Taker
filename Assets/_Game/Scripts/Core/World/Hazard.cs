using System;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using UnityEngine;

namespace ShadowRPG.Core.World
{
    public enum HazardShape
    {
        Circle,
        Line,
        Donut
    }

    /// <summary>
    /// 예고 장판 (보스 패턴). 바닥에 범위가 먼저 표시되고 Delay 뒤에 범위 안 대상에게 피해를 줍니다.
    /// 자동 사냥 AI는 이 범위를 보고 피합니다.
    /// </summary>
    public sealed class Hazard
    {
        public Combatant Source;
        public Team Team;
        public HazardShape Shape;
        public Vector2 Center;
        public Vector2 Direction = Vector2.right;
        public float Radius = 2f;
        public float InnerRadius;
        public float Length = 10f;
        public float Width = 2f;
        public float Delay = 1.2f;
        public float Elapsed;
        public DamageType Element = DamageType.Fire;
        public string Label;

        /// <summary>발동 시 범위 안의 각 대상에게 호출됩니다.</summary>
        public Action<Hazard, Combatant> OnHit;

        public bool IsResolved { get; private set; }
        public float Progress => Mathf.Clamp01(Elapsed / Delay);

        public bool Contains(Vector2 point, float padding = 0f)
        {
            switch (Shape)
            {
                case HazardShape.Circle:
                    return (point - Center).sqrMagnitude <= (Radius + padding) * (Radius + padding);
                case HazardShape.Donut:
                {
                    float d = (point - Center).magnitude;
                    return d <= Radius + padding && d >= InnerRadius - padding;
                }
                default:
                {
                    var dir = Direction.normalized;
                    var local = point - Center;
                    float along = Vector2.Dot(local, dir);
                    float across = Mathf.Abs(local.x * dir.y - local.y * dir.x);
                    return along >= -padding && along <= Length + padding && across <= Width * 0.5f + padding;
                }
            }
        }

        /// <summary>범위를 빠져나가는 가장 짧은 방향.</summary>
        public Vector2 EscapeDirection(Vector2 point)
        {
            switch (Shape)
            {
                case HazardShape.Circle:
                {
                    var away = point - Center;
                    return away.sqrMagnitude > 0.001f ? away.normalized : Vector2.right;
                }
                case HazardShape.Donut:
                {
                    var toCenter = Center - point;
                    return toCenter.sqrMagnitude > 0.001f ? toCenter.normalized : Vector2.up;
                }
                default:
                {
                    var dir = Direction.normalized;
                    var normal = new Vector2(-dir.y, dir.x);
                    return Vector2.Dot(point - Center, normal) >= 0f ? normal : -normal;
                }
            }
        }

        public void MarkResolved() => IsResolved = true;
    }
}
