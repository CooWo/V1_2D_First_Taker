using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Status;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>전투원 한 명의 표시 담당. 모델을 읽기만 하고 절대 바꾸지 않습니다.</summary>
    public sealed class CombatantView : MonoBehaviour
    {
        static readonly Color FrozenTint = new Color(0.55f, 0.85f, 1f);
        static readonly Color FlashTint = new Color(1f, 0.45f, 0.45f);

        Combatant _model;
        SpriteRenderer _body;
        SpriteRenderer _shadow;
        SpriteRenderer _barBack;
        SpriteRenderer _barFill;
        SpriteRenderer _fortify;
        Sprite[] _idle;
        Sprite[] _run;
        float _fps;
        float _animTime;
        float _flash;
        float _deathFade = -1f;
        Vector2 _lastPosition;
        float _barWidth;
        Color _baseTint = Color.white;

        public Combatant Model => _model;
        public bool IsFinished => _deathFade > 0.6f;

        public void Bind(Combatant model, Sprite[] idle, Sprite[] run, float fps, float scale, bool showHealthBar, bool glowEyes)
        {
            _model = model;
            _idle = idle;
            _run = run != null && run.Length > 0 ? run : idle;
            _fps = fps;
            _lastPosition = model.Position;

            transform.position = model.Position;
            transform.localScale = Vector3.one * scale;

            _shadow = CreateRenderer("Shadow", PixelArtFactory.Shadow, new Color(0f, 0f, 0f, 0.55f));
            _shadow.transform.localScale = new Vector3(model.Radius * 2.4f / scale, model.Radius * 1.1f / scale, 1f);
            _shadow.sortingOrder = SortingOrders.GroundShadow;

            _fortify = CreateRenderer("Fortify", PixelArtFactory.Ring, new Color(0.35f, 0.6f, 1f, 0f));
            _fortify.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            _fortify.transform.localScale = Vector3.one * 1.4f;

            _body = CreateRenderer("Body", _idle[0], Color.white);

            if (model.Rank == EnemyRank.Elite) _baseTint = new Color(1f, 0.85f, 0.7f);
            if (model.Rank == EnemyRank.Boss) _baseTint = new Color(1f, 0.7f, 0.7f);

            if (showHealthBar)
            {
                float top = _body.sprite.bounds.size.y + 0.15f;
                _barWidth = 0.9f;
                _barBack = CreateRenderer("BarBack", PixelArtFactory.Square, new Color(0f, 0f, 0f, 0.7f));
                _barBack.transform.localPosition = new Vector3(0f, top, 0f);
                _barBack.transform.localScale = new Vector3(_barWidth, 0.1f, 1f);
                _barFill = CreateRenderer("BarFill", PixelArtFactory.Square, model.IsEliteOrBoss ? new Color(1f, 0.6f, 0.15f) : Palette.Life);
                _barFill.transform.localPosition = new Vector3(0f, top, 0f);
            }

            if (glowEyes) AddEyeGlow();

            model.Health.Damaged += OnDamaged;
        }

        void AddEyeGlow()
        {
            var lightObject = new GameObject("EyeGlow");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = Palette.ShadowBlue;
            light.intensity = 0.9f;
            light.pointLightOuterRadius = 2.2f;
            light.pointLightInnerRadius = 0.1f;
        }

        SpriteRenderer CreateRenderer(string name, Sprite sprite, Color color)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            return renderer;
        }

        void OnDamaged(float amount) => _flash = 0.12f;

        public void Sync(float dt)
        {
            if (_model == null) return;

            if (!_model.IsAlive)
            {
                if (_deathFade < 0f) _deathFade = 0f;
                _deathFade += dt;
                float alpha = Mathf.Clamp01(1f - _deathFade / 0.6f);
                _body.color = new Color(0.2f, 0.2f, 0.3f, alpha);
                _shadow.color = new Color(0f, 0f, 0f, 0.55f * alpha);
                if (_barBack != null) _barBack.enabled = _barFill.enabled = false;
                _fortify.color = Color.clear;
                return;
            }
            _deathFade = -1f;
            _shadow.color = new Color(0f, 0f, 0f, 0.55f);

            var position = _model.Position;
            bool moving = (position - _lastPosition).sqrMagnitude > 0.00002f;
            _lastPosition = position;
            transform.position = new Vector3(position.x, position.y, 0f);

            _animTime += dt;
            var frames = moving ? _run : _idle;
            int frame = Mathf.FloorToInt(_animTime * (moving ? _fps * 1.4f : _fps * 0.5f)) % frames.Length;
            _body.sprite = frames[frame];
            if (Mathf.Abs(_model.Facing.x) > 0.05f) _body.flipX = _model.Facing.x < 0f;

            int order = SortingOrders.ForY(position.y);
            _body.sortingOrder = order;
            _fortify.sortingOrder = order - 1;

            _flash -= dt;
            var tint = _model.Statuses.Has(StatusIds.Freeze) ? FrozenTint : _baseTint;
            _body.color = _flash > 0f ? FlashTint : tint;

            bool fortified = _model.Statuses.Has(StatusIds.ShadowFortify);
            _fortify.color = new Color(0.35f, 0.6f, 1f, fortified ? 0.35f + 0.15f * Mathf.Sin(_animTime * 8f) : 0f);

            if (_barFill != null)
            {
                float fraction = Mathf.Clamp01(_model.Health.LifeFraction);
                _barFill.transform.localScale = new Vector3(_barWidth * fraction, 0.1f, 1f);
                _barFill.transform.localPosition = new Vector3(-_barWidth * (1f - fraction) * 0.5f, _barBack.transform.localPosition.y, 0f);
                _barBack.sortingOrder = order + 1;
                _barFill.sortingOrder = order + 2;
                bool show = fraction < 0.999f;
                _barBack.enabled = _barFill.enabled = show;
            }
        }

        void OnDestroy()
        {
            if (_model != null) _model.Health.Damaged -= OnDamaged;
        }
    }
}
