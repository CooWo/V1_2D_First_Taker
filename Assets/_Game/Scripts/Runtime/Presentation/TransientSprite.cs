using System;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 짧게 나타났다 사라지는 연출 조각 (확대·페이드·이동·회전). 끝나면 풀로 돌아갑니다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TransientSprite : MonoBehaviour
    {
        SpriteRenderer _renderer;
        float _time;
        float _duration;
        Vector3 _scaleFrom;
        Vector3 _scaleTo;
        Vector3 _positionFrom;
        Vector3 _positionTo;
        float _alphaFrom;
        float _alphaTo;
        float _spin;
        Color _color;
        Action<TransientSprite> _release;

        public Material DefaultMaterial;

        public SpriteRenderer Renderer => _renderer != null ? _renderer : (_renderer = GetComponent<SpriteRenderer>());

        public void Play(Sprite sprite, Color color, Vector3 position, float rotation, Vector3 scaleFrom, Vector3 scaleTo,
            float alphaFrom, float alphaTo, float duration, int sortingOrder, Action<TransientSprite> release,
            Vector3? moveTo = null, float spin = 0f)
        {
            gameObject.SetActive(true);
            Renderer.sprite = sprite;
            Renderer.sortingOrder = sortingOrder;
            _color = color;
            _positionFrom = position;
            _positionTo = moveTo ?? position;
            _scaleFrom = scaleFrom;
            _scaleTo = scaleTo;
            _alphaFrom = alphaFrom;
            _alphaTo = alphaTo;
            _duration = Mathf.Max(0.02f, duration);
            _spin = spin;
            _time = 0f;
            _release = release;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotation));
            Apply(0f);
        }

        void Update()
        {
            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / _duration);
            Apply(t);
            if (_spin != 0f) transform.Rotate(0f, 0f, _spin * Time.deltaTime);
            if (t < 1f) return;

            gameObject.SetActive(false);
            _release?.Invoke(this);
        }

        void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.LerpUnclamped(_scaleFrom, _scaleTo, eased);
            transform.position = Vector3.LerpUnclamped(_positionFrom, _positionTo, t);
            var c = _color;
            c.a *= Mathf.Lerp(_alphaFrom, _alphaTo, t);
            Renderer.color = c;
        }
    }
}
