using System;
using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>
    /// 8방향 스프라이트 재생 + 피격 플래시 겹침. 타격 프레임에 이벤트를 보냅니다.
    /// 반전 방향(W·NW·SW)은 flipX로 그립니다.
    /// </summary>
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
        float _flashFade = 8f;
        Color _flashColor = Color.white;
        Action _onImpact;
        Action _onComplete;
        bool _flip;

        public int Direction { get; private set; } = 6;
        public string CurrentClip => _clipName;
        public bool IsPlaying { get; private set; }
        public SpriteRenderer Body => _body;
        public DirectionalSpriteSet Set => _set;
        public float Speed { get; set; } = 1f;
        /// <summary>현재 동작 진행도 (0~1).</summary>
        public float Progress => _clip == null || _frames == null || _frames.Length == 0 ? 1f : Mathf.Clamp01(_time * _clip.framesPerSecond / _frames.Length);

        public void Initialize(DirectionalSpriteSet set, Material bodyMaterial, Material flashMaterial)
        {
            _set = set;
            _body = GetComponent<SpriteRenderer>();
            if (bodyMaterial != null) _body.sharedMaterial = bodyMaterial;

            if (flashMaterial != null)
            {
                var flashObject = new GameObject("Flash");
                flashObject.transform.SetParent(transform, false);
                _flash = flashObject.AddComponent<SpriteRenderer>();
                _flash.sharedMaterial = flashMaterial;
                _flash.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            int d = DirectionalSpriteSet.DirectionOf(direction);
            if (d == Direction) return;
            Direction = d;
            RefreshFrames();
            ApplyFrame(Mathf.Max(0, _lastFrame));
        }

        public bool Has(string clipName) => _set != null && _set.Has(clipName);

        public void Play(string clipName, Action onImpact = null, Action onComplete = null, bool restart = false, float startNormalized = 0f)
        {
            if (_set == null) return;
            if (!_set.Has(clipName)) clipName = _set.Has("idle") ? "idle" : _set.clips.Count > 0 ? _set.clips[0].name : null;
            if (clipName == null) return;
            if (!restart && clipName == _clipName && IsPlaying) return;
            _clipName = clipName;
            _clip = _set.Find(clipName);
            RefreshFrames();
            _time = _clip != null && _frames != null && _frames.Length > 0 ? Mathf.Clamp01(startNormalized) * _frames.Length / Mathf.Max(0.01f, _clip.framesPerSecond) : 0f;
            _lastFrame = -1;
            _onImpact = onImpact;
            _onComplete = onComplete;
            IsPlaying = true;
            ApplyFrame(Mathf.FloorToInt(_time * (_clip?.framesPerSecond ?? 0f)));
        }

        /// <summary>피격 플래시. fade = 초당 감소량.</summary>
        public void Flash(Color color, float amount = 1f, float fade = 8f)
        {
            _flashColor = color;
            _flashAmount = Mathf.Max(_flashAmount, amount);
            _flashFade = fade;
        }

        void RefreshFrames()
        {
            if (_clipName == null || _set == null) return;
            var (row, flip) = _set.Resolve(Direction);
            _frames = _set.FramesForRow(_clipName, row);
            _flip = flip;
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
                        FireImpactIfSkipped();
                        var done = _onComplete;
                        _onComplete = null;
                        done?.Invoke();
                    }
                }
                if (frame != _lastFrame)
                {
                    if (_clip.impactFrame >= 0 && _lastFrame < _clip.impactFrame && frame >= _clip.impactFrame) FireImpactIfSkipped();
                    ApplyFrame(frame);
                }
            }

            _flashAmount = Mathf.MoveTowards(_flashAmount, 0f, Time.unscaledDeltaTime * _flashFade);
            if (_flash != null)
            {
                _flash.sprite = _body.sprite;
                _flash.flipX = _body.flipX;
                _flash.sortingLayerID = _body.sortingLayerID;
                _flash.sortingOrder = _body.sortingOrder + 1;
                _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashAmount);
            }
        }

        void FireImpactIfSkipped()
        {
            var impact = _onImpact;
            _onImpact = null;
            impact?.Invoke();
        }

        void ApplyFrame(int frame)
        {
            _lastFrame = frame;
            if (_frames == null || _frames.Length == 0) return;
            _body.sprite = _frames[Mathf.Clamp(frame, 0, _frames.Length - 1)];
            _body.flipX = _flip;
        }
    }
}
