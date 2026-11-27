using System;
using UnityEngine;

namespace ShadowRPG.ArtTest
{
    /// <summary>8방향 스프라이트 재생 + 피격 플래시 겹침. 타격 프레임에 이벤트를 보냅니다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DirectionalSpriteAnimator : MonoBehaviour
    {
        DirectionalSpriteSet _set;
        SpriteRenderer _body;
        SpriteRenderer _flash;
        Sprite[] _frames;
        DirectionalSpriteSet.Clip _clip;
        string _clipName;
        float _time;
        int _lastFrame = -1;
        float _flashAmount;
        Color _flashColor = Color.white;
        Action _onImpact;
        Action _onComplete;

        public int Direction { get; private set; } = 6;
        public string CurrentClip => _clipName;
        public bool IsPlaying { get; private set; }
        public SpriteRenderer Body => _body;
        public float Speed { get; set; } = 1f;

        public void Initialize(DirectionalSpriteSet set, Material bodyMaterial, Material flashMaterial)
        {
            _set = set;
            _body = GetComponent<SpriteRenderer>();
            if (bodyMaterial != null) _body.sharedMaterial = bodyMaterial;

            var flashObject = new GameObject("Flash");
            flashObject.transform.SetParent(transform, false);
            _flash = flashObject.AddComponent<SpriteRenderer>();
            _flash.sharedMaterial = flashMaterial;
            _flash.color = new Color(1f, 1f, 1f, 0f);
        }

        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            int d = DirectionalSpriteSet.DirectionOf(direction);
            if (d == Direction) return;
            Direction = d;
            if (_clipName != null) _frames = _set.Frames(_clipName, Direction);
        }

        public void Play(string clipName, Action onImpact = null, Action onComplete = null, bool restart = false)
        {
            if (!restart && clipName == _clipName && IsPlaying) return;
            _clipName = clipName;
            _clip = _set.Find(clipName);
            _frames = _set.Frames(clipName, Direction);
            _time = 0f;
            _lastFrame = -1;
            _onImpact = onImpact;
            _onComplete = onComplete;
            IsPlaying = true;
            ApplyFrame(0);
        }

        public void Flash(Color color, float amount = 1f)
        {
            _flashColor = color;
            _flashAmount = amount;
        }

        void Update()
        {
            if (_clip != null && _frames != null && _frames.Length > 0 && IsPlaying)
            {
                _time += Time.deltaTime * Speed;
                int frame = Mathf.FloorToInt(_time * _clip.framesPerSecond);
                if (frame >= _frames.Length)
                {
                    if (_clip.loop) frame %= _frames.Length;
                    else
                    {
                        frame = _frames.Length - 1;
                        IsPlaying = false;
                        var done = _onComplete;
                        _onComplete = null;
                        done?.Invoke();
                    }
                }
                if (frame != _lastFrame)
                {
                    if (_clip.impactFrame >= 0 && _lastFrame < _clip.impactFrame && frame >= _clip.impactFrame)
                    {
                        var impact = _onImpact;
                        _onImpact = null;
                        impact?.Invoke();
                    }
                    ApplyFrame(frame);
                }
            }

            _flashAmount = Mathf.MoveTowards(_flashAmount, 0f, Time.unscaledDeltaTime * 8f);
            if (_flash != null)
            {
                _flash.sprite = _body.sprite;
                _flash.sortingOrder = _body.sortingOrder + 1;
                _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashAmount);
            }
        }

        void ApplyFrame(int frame)
        {
            _lastFrame = frame;
            if (_frames == null || _frames.Length == 0) return;
            _body.sprite = _frames[Mathf.Clamp(frame, 0, _frames.Length - 1)];
        }
    }
}
