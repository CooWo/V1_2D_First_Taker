using ShadowRPG.Core.Combat;
using UnityEngine;

namespace ShadowRPG.Core.World
{
    public enum VfxKind
    {
        Slash,
        Cone,
        Nova,
        Explosion,
        Beam,
        Shield,
        DashTrail,
        Blink,
        Warcry,
        Pillar,
        Meteor,
        Mark,
        Text
    }

    /// <summary>
    /// 연출 요청. Core는 "무엇이 일어났는지"만 알리고, 어떻게 그릴지는 표현 계층이 정합니다 (DIP).
    /// </summary>
    public struct VfxEvent
    {
        public VfxKind Kind;
        public Vector2 Position;
        public Vector2 To;
        public float Radius;
        public float AngleDegrees;
        public float Duration;
        public DamageType Element;
        public string Text;

        public static VfxEvent At(VfxKind kind, Vector2 position, float radius, DamageType element, float duration = 0.25f)
            => new VfxEvent { Kind = kind, Position = position, To = position, Radius = radius, Element = element, Duration = duration };

        public static VfxEvent Line(VfxKind kind, Vector2 from, Vector2 to, DamageType element, float duration = 0.15f)
            => new VfxEvent { Kind = kind, Position = from, To = to, Element = element, Duration = duration };
    }
}
