using System;
using UnityEngine;

namespace ShadowRPG.ArtTest
{
    /// <summary>짧은 이펙트 하나: 프레임 재생 + 크기·투명도·이동·회전 변화. 끝나면 풀로 돌아갑니다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FxSprite : MonoBehaviour
    {
        public struct Spec
        {
            public Sprite[] Frames;
            public Color Color;
            public Vector3 Position;
            public float Rotation;
            public float ScaleFrom, ScaleTo;
            public float AlphaFrom, AlphaTo;
            public float Duration;
            public Vector2 Velocity;
            public float Spin;
            public bool FlipX;
            public int Order;
            public bool Unscaled;
        }

        SpriteRenderer _renderer;
        Spec _spec;
        float _time;
        Action<FxSprite> _release;

        public void Play(Spec spec, Material material, Action<FxSprite> release)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.sortingOrder = spec.Order;
            _renderer.flipX = spec.FlipX;
            _spec = spec;
            _time = 0f;
            _release = release;
            transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(0f, 0f, spec.Rotation));
            gameObject.SetActive(true);
            Apply(0f);
        }

        void Update()
        {
            _time += _spec.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(_time / Mathf.Max(0.01f, _spec.Duration));
            transform.position += (Vector3)(_spec.Velocity * (_spec.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime));
            if (_spec.Spin != 0f) transform.Rotate(0f, 0f, _spec.Spin * Time.deltaTime);
            Apply(t);
            if (t < 1f) return;
            gameObject.SetActive(false);
            _release?.Invoke(this);
        }

        void Apply(float t)
        {
            var frames = _spec.Frames;
            _renderer.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * frames.Length))];
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(_spec.ScaleFrom, _spec.ScaleTo, eased);
            var c = _spec.Color;
            c.a *= Mathf.Lerp(_spec.AlphaFrom, _spec.AlphaTo, t);
            _renderer.color = c;
        }
    }
}
