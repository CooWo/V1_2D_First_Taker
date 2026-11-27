using System.Collections.Generic;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>떠오르는 글자 출력 창구 (UI 계층이 구현).</summary>
    public interface IFloatingText
    {
        void Show(Vector3 worldPosition, string text, Color color, float size, float duration);
    }

    /// <summary>Core의 VfxEvent를 스프라이트 연출로 바꿉니다. 오브젝트는 풀링합니다.</summary>
    public sealed class VfxPlayer : MonoBehaviour
    {
        readonly Stack<TransientSprite> _pool = new Stack<TransientSprite>();
        IFloatingText _text;

        public void Initialize(IFloatingText text) => _text = text;

        static Art.FxCatalog F => Art.FxCatalog.Load() ?? ScriptableObject.CreateInstance<Art.FxCatalog>();

        static Sprite Fx(Sprite[] list, Sprite fallback) => Art.FxCatalog.Pick(list, fallback);

        public void Play(VfxEvent e)
        {
            var color = Palette.For(e.Element);
            var p = (Vector3)e.Position;
            switch (e.Kind)
            {
                case VfxKind.Slash:
                    Spawn(Fx(F.slash, PixelArtFactory.Arc), color, p + Vector3.up * 0.5f, Random.Range(0f, 360f), e.Radius * 1.2f, e.Radius * 1.6f, 1f, 0f, Mathf.Max(0.12f, e.Duration));
                    break;
                case VfxKind.Cone:
                {
                    var dir = e.To - e.Position;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    Spawn(Fx(F.slash, PixelArtFactory.Arc), color, p + (Vector3)(dir.normalized * e.Radius * 0.35f), angle, e.Radius * 0.6f, e.Radius * 1.9f, 0.9f, 0f, e.Duration);
                    break;
                }
                case VfxKind.Nova:
                    Spawn(Fx(F.ring, PixelArtFactory.Ring), color, p, 0f, e.Radius * 0.4f, e.Radius * 2f, 1f, 0f, e.Duration);
                    break;
                case VfxKind.Explosion:
                    Spawn(Fx(F.glow, PixelArtFactory.Circle), color, p, 0f, e.Radius * 0.5f, e.Radius * 2f, 0.8f, 0f, e.Duration);
                    Spawn(Fx(F.ring, PixelArtFactory.Ring), Color.Lerp(color, Color.white, 0.4f), p, 0f, e.Radius * 0.8f, e.Radius * 2.2f, 1f, 0f, e.Duration);
                    break;
                case VfxKind.Beam:
                    Beam(e.Position, e.To, color, e.Duration, e.Element == DamageType.Lightning ? 3 : 1, 0.12f);
                    break;
                case VfxKind.DashTrail:
                    Beam(e.Position, e.To, new Color(0.15f, 0.2f, 0.4f, 0.8f), e.Duration, 1, 0.45f);
                    break;
                case VfxKind.Blink:
                    Spawn(PixelArtFactory.HeroIdle[0], new Color(0.3f, 0.5f, 1f, 0.6f), p, 0f, 1f, 1f, 0.7f, 0f, Mathf.Max(0.2f, e.Duration), SortingOrders.ForY(e.Position.y) + 1);
                    break;
                case VfxKind.Shield:
                    Spawn(Fx(F.ring, PixelArtFactory.Ring), Palette.EnergyShield, p + Vector3.up * 0.6f, 0f, e.Radius * 2.2f, e.Radius * 2.5f, 0.9f, 0f, Mathf.Min(1f, e.Duration));
                    break;
                case VfxKind.Warcry:
                    Spawn(Fx(F.ring, PixelArtFactory.Ring), new Color(1f, 0.85f, 0.5f), p, 0f, 0.5f, e.Radius * 2f, 1f, 0f, e.Duration);
                    break;
                case VfxKind.Pillar:
                    Spawn(PixelArtFactory.Square, new Color(0.08f, 0.08f, 0.14f), p + Vector3.up * 0.5f, 0f, new Vector3(0.5f, 0.2f, 1f), new Vector3(0.5f, 1.4f, 1f), 1f, 0.6f, e.Duration);
                    break;
                case VfxKind.Meteor:
                    Spawn(Fx(F.glow, PixelArtFactory.Circle), color, p, 0f, 1.2f, 1.6f, 1f, 1f, e.Duration, SortingOrders.Vfx + 10, e.To);
                    break;
                case VfxKind.Mark:
                    Spawn(Fx(F.ring, PixelArtFactory.Ring), color, p, 0f, e.Radius * 2.2f, e.Radius * 1.6f, 0.4f, 0.9f, e.Duration);
                    break;
                case VfxKind.Text:
                    _text?.Show(p + Vector3.up * 1.2f, e.Text, Color.white, 26f, e.Duration);
                    break;
            }
        }

        void Beam(Vector2 from, Vector2 to, Color color, float duration, int segments, float width)
        {
            var previous = from;
            for (int i = 1; i <= segments; i++)
            {
                var next = i == segments ? to : Vector2.Lerp(from, to, i / (float)segments) + Random.insideUnitCircle * 0.35f;
                var delta = next - previous;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                var mid = (Vector3)((previous + next) * 0.5f) + Vector3.up * 0.5f;
                var scale = new Vector3(delta.magnitude, width, 1f);
                Spawn(PixelArtFactory.Square, color, mid, angle, scale, new Vector3(delta.magnitude, width * 0.3f, 1f), 1f, 0f, duration);
                previous = next;
            }
        }

        void Spawn(Sprite sprite, Color color, Vector3 position, float rotation, float scaleFrom, float scaleTo,
            float alphaFrom, float alphaTo, float duration, int order = SortingOrders.Vfx, Vector2? moveTo = null)
            => Spawn(sprite, color, position, rotation, Vector3.one * scaleFrom, Vector3.one * scaleTo, alphaFrom, alphaTo, duration, order, moveTo);

        void Spawn(Sprite sprite, Color color, Vector3 position, float rotation, Vector3 scaleFrom, Vector3 scaleTo,
            float alphaFrom, float alphaTo, float duration, int order = SortingOrders.Vfx, Vector2? moveTo = null)
        {
            var effect = _pool.Count > 0 ? _pool.Pop() : CreateEffect();
            effect.Renderer.sharedMaterial = sprite == PixelArtFactory.Square || sprite == PixelArtFactory.HeroIdle[0] ? effect.DefaultMaterial : Art.FxMaterials.Additive;
            effect.Play(sprite, color, position, rotation, scaleFrom, scaleTo, alphaFrom, alphaTo, duration, order, Release,
                moveTo.HasValue ? (Vector3)moveTo.Value : (Vector3?)null);
        }

        TransientSprite CreateEffect()
        {
            var go = new GameObject("Vfx");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            var t = go.AddComponent<TransientSprite>();
            t.DefaultMaterial = r.sharedMaterial;
            return t;
        }

        void Release(TransientSprite effect) => _pool.Push(effect);
    }
}
