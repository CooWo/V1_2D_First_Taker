using System;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>모델 위치를 따라가는 단순 스프라이트 (펫, 포탈 표식 등).</summary>
    public sealed class FollowerView : MonoBehaviour
    {
        SpriteRenderer _renderer;
        Sprite[] _frames;
        Func<Vector2> _position;
        Func<bool> _visible;
        float _fps;
        float _time;
        float _bob;

        public void Bind(Sprite[] frames, float fps, Func<Vector2> position, Func<bool> visible, Color tint, float scale, float bob = 0f)
        {
            _frames = frames;
            _fps = fps;
            _position = position;
            _visible = visible;
            _bob = bob;
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = frames[0];
            _renderer.color = tint;
            transform.localScale = Vector3.one * scale;
        }

        void LateUpdate()
        {
            if (_position == null) return;
            bool visible = _visible == null || _visible();
            _renderer.enabled = visible;
            if (!visible) return;

            _time += Time.deltaTime;
            var p = _position();
            transform.position = new Vector3(p.x, p.y + Mathf.Sin(_time * 3f) * _bob, 0f);
            _renderer.sprite = _frames[Mathf.FloorToInt(_time * _fps) % _frames.Length];
            _renderer.sortingOrder = SortingOrders.ForY(p.y) + 1;
        }
    }
}
