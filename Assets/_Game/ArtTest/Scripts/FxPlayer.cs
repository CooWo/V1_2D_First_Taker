using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.ArtTest
{
    /// <summary>타격 연출 조합 (베기·불꽃·충격파·사망 연기). 오브젝트는 풀링합니다.</summary>
    public sealed class FxPlayer : MonoBehaviour
    {
        const int EffectOrder = 5000;

        readonly Stack<FxSprite> _pool = new Stack<FxSprite>();
        Material _additive;
        Material _alpha;

        public void Initialize(Material additive, Material alpha)
        {
            _additive = additive;
            _alpha = alpha;
        }

        /// <summary>캐릭터 앞 초승달 베기.</summary>
        public void Slash(Vector2 origin, Vector2 direction, Color color, float scale, bool flip)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var at = origin + direction.normalized * 0.55f * scale + Vector2.up * 0.6f;
            Spawn(new FxSprite.Spec { Frames = FxLibrary.Slash, Color = color, Position = at, Rotation = angle, ScaleFrom = 1.5f * scale, ScaleTo = 1.9f * scale, AlphaFrom = 1f, AlphaTo = 0.2f, Duration = 0.22f, FlipX = flip, Order = EffectOrder }, _additive);
            Spawn(new FxSprite.Spec { Frames = new[] { FxLibrary.Glow }, Color = color * 0.7f, Position = at, ScaleFrom = 1.2f * scale, ScaleTo = 2.2f * scale, AlphaFrom = 0.6f, AlphaTo = 0f, Duration = 0.2f, Order = EffectOrder - 1 }, _additive);
        }

        /// <summary>피격 지점 불꽃 + 파편.</summary>
        public void Impact(Vector2 at, Color color, float power)
        {
            var p = (Vector3)at + Vector3.up * 0.7f;
            Spawn(new FxSprite.Spec { Frames = new[] { FxLibrary.Spark }, Color = Color.Lerp(color, Color.white, 0.6f), Position = p, Rotation = Random.Range(0f, 90f), ScaleFrom = 0.4f * power, ScaleTo = 1.3f * power, AlphaFrom = 1f, AlphaTo = 0f, Duration = 0.16f, Order = EffectOrder + 2 }, _additive);
            int debris = Mathf.RoundToInt(5 * power);
            for (int i = 0; i < debris; i++)
            {
                var v = Random.insideUnitCircle.normalized * Random.Range(3f, 7f) * power;
                Spawn(new FxSprite.Spec { Frames = new[] { FxLibrary.Glow }, Color = color, Position = p, ScaleFrom = 0.18f, ScaleTo = 0.05f, AlphaFrom = 1f, AlphaTo = 0f, Duration = Random.Range(0.18f, 0.32f), Velocity = v, Order = EffectOrder + 1 }, _additive);
            }
        }

        /// <summary>강타 충격파 (원 확산).</summary>
        public void Shockwave(Vector2 at, Color color, float radius)
        {
            var p = (Vector3)at + Vector3.up * 0.2f;
            Spawn(new FxSprite.Spec { Frames = new[] { FxLibrary.Ring }, Color = color, Position = p, ScaleFrom = 0.3f, ScaleTo = radius * 2f, AlphaFrom = 1f, AlphaTo = 0f, Duration = 0.35f, Order = EffectOrder }, _additive);
            Spawn(new FxSprite.Spec { Frames = new[] { FxLibrary.Glow }, Color = color, Position = p, ScaleFrom = radius, ScaleTo = radius * 1.8f, AlphaFrom = 0.8f, AlphaTo = 0f, Duration = 0.25f, Order = EffectOrder - 2 }, _additive);
        }

        /// <summary>사망 시 검은 연기 + 푸른 잔불.</summary>
        public void Death(Vector2 at)
        {
            var p = (Vector3)at + Vector3.up * 0.5f;
            for (int i = 0; i < 7; i++)
            {
                var v = Random.insideUnitCircle * 1.2f + Vector2.up * 0.8f;
                Spawn(new FxSprite.Spec { Frames = new[] { FxLibrary.Puff }, Color = new Color(0.05f, 0.04f, 0.08f, 0.9f), Position = p + (Vector3)Random.insideUnitCircle * 0.3f, ScaleFrom = 0.4f, ScaleTo = 1.3f, AlphaFrom = 0.9f, AlphaTo = 0f, Duration = Random.Range(0.6f, 0.9f), Velocity = v, Order = EffectOrder - 5 }, _alpha);
            }
            for (int i = 0; i < 10; i++)
            {
                var v = Random.insideUnitCircle * 2f + Vector2.up * 1.5f;
                Spawn(new FxSprite.Spec { Frames = new[] { FxLibrary.Glow }, Color = new Color(0.4f, 0.6f, 1f), Position = p, ScaleFrom = 0.12f, ScaleTo = 0.02f, AlphaFrom = 1f, AlphaTo = 0f, Duration = Random.Range(0.5f, 0.9f), Velocity = v, Order = EffectOrder }, _additive);
            }
        }

        void Spawn(FxSprite.Spec spec, Material material)
        {
            var fx = _pool.Count > 0 ? _pool.Pop() : Create();
            fx.Play(spec, material, f => _pool.Push(f));
        }

        FxSprite Create()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(transform, false);
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<FxSprite>();
        }
    }
}
