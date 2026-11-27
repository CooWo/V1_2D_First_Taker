using System;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Status;
using ShadowRPG.Runtime.Art;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 전투원 한 명의 표시: 8방향 구운 스프라이트 + 동작(대기·달리기·공격·시전·피격·사망) + 피격 플래시·넉백 흔들림.
    /// 모델은 읽기만 합니다. 외형 키가 바뀌면(장비 교체) 스프라이트 묶음을 갈아 끼웁니다.
    /// </summary>
    public sealed class CombatantView : MonoBehaviour
    {
        static readonly Color FrozenTint = new Color(0.6f, 0.85f, 1f);

        Combatant _model;
        DirectionalSpriteAnimator _animator;
        SpriteRenderer _shadow;
        SpriteRenderer _barBack;
        SpriteRenderer _barFill;
        Func<Combatant, string> _appearanceKey;
        string _currentKey;
        float _keyCheckTimer;
        float _scale = 1f;
        Color _tint = Color.white;
        Vector2 _lastPosition;
        float _actionLock;
        int _attackToggle;
        float _hitAnimCooldown;
        float _deathTime = -1f;
        Vector2 _kick;
        float _barTop;
        bool _showBar;

        public Combatant Model => _model;
        public bool IsFinished => _deathTime >= 0f && _deathTime > 1.4f;
        public DirectionalSpriteAnimator Animator => _animator;

        /// <summary>피격 순간 (연출 감독이 히트스톱·숫자에 사용).</summary>
        public event Action<CombatantView, float> Hurt;

        public void Bind(Combatant model, Func<Combatant, string> appearanceKey, bool showHealthBar, bool isPlayer)
        {
            _model = model;
            _appearanceKey = appearanceKey;
            _lastPosition = model.Position;
            _showBar = showHealthBar;
            transform.position = WorldProjection.ToView(model.Position);

            var fx = FxCatalog.Load();
            _shadow = CreateRenderer("Shadow", PixelArtFactory.Shadow, new Color(0f, 0f, 0f, 0.45f));
            _shadow.sortingOrder = SortingOrders.GroundShadow;

            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            body.AddComponent<SpriteRenderer>();
            _animator = body.AddComponent<DirectionalSpriteAnimator>();
            ApplyAppearance(true);

            if (showHealthBar)
            {
                _barBack = CreateRenderer("BarBack", PixelArtFactory.Square, new Color(0f, 0f, 0f, 0.75f));
                _barFill = CreateRenderer("BarFill", PixelArtFactory.Square, model.IsEliteOrBoss ? new Color(1f, 0.55f, 0.15f) : new Color(0.85f, 0.18f, 0.16f));
            }

            if (isPlayer) AddHeroLight();
            if (model.Rank != EnemyRank.Normal) AddEliteAura(model.Rank == EnemyRank.Boss);

            model.Health.Damaged += OnDamaged;
            model.ActionStarted += OnAction;

            // 해골은 땅에서 솟아오르며 등장
            if (!isPlayer && _animator.Has("spawn")) PlayAction("spawn", 0.8f);
        }

        void ApplyAppearance(bool force)
        {
            string key = _appearanceKey?.Invoke(_model) ?? _model.VisualKey;
            if (!force && key == _currentKey) return;
            _currentKey = key;
            var catalog = AppearanceCatalog.Load();
            var entry = catalog != null ? catalog.GetOrFallback(key) : null;
            if (entry == null || entry.set == null) return;

            _scale = entry.scale;
            _tint = entry.tint;
            if (_model.Rank == EnemyRank.Elite) _tint *= new Color(1f, 0.82f, 0.72f);
            transform.localScale = Vector3.one * _scale;
            _animator.Initialize(entry.set, null, FxMaterials.Flash);
            _animator.Body.color = _tint;
            _animator.Play(_animator.CurrentClip ?? "idle", restart: true);
            _barTop = entry.set.heightUnits + 0.25f;
            if (_shadow != null) _shadow.transform.localScale = new Vector3(_model.Radius * 2.6f / _scale, _model.Radius * 1.1f / _scale, 1f);
        }

        void AddHeroLight()
        {
            var lightObject = new GameObject("HeroLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.62f, 0.75f, 1f);
            light.intensity = Core.Tuning.GameTuning.Current.Presentation.HeroLightIntensity;
            light.pointLightOuterRadius = 4.5f;
            light.pointLightInnerRadius = 0.4f;
            light.falloffIntensity = 0.6f;
        }

        void AddEliteAura(bool boss)
        {
            var lightObject = new GameObject("Aura");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = boss ? new Color(1f, 0.25f, 0.18f) : new Color(1f, 0.55f, 0.2f);
            light.intensity = boss ? 1.2f : 0.7f;
            light.pointLightOuterRadius = boss ? 3.5f : 2f;
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

        void OnAction(CombatAction action)
        {
            if (!_model.IsAlive) return;
            switch (action)
            {
                case CombatAction.Cast:
                    PlayAction(_animator.Has("cast") ? "cast" : "attack", 0.25f);
                    break;
                case CombatAction.Movement:
                    PlayAction(_animator.Has("dodge") ? "dodge" : "run", 0f);
                    break;
                case CombatAction.Heavy:
                    PlayAction("attack2", 0.3f);
                    break;
                default:
                    _attackToggle = 1 - _attackToggle;
                    PlayAction(_attackToggle == 0 ? "attack" : "attack2", 0.32f);
                    break;
            }
        }

        /// <summary>공격 동작은 타격 직전부터 재생 → 판정(즉시)과 칼 휘두름이 맞아 보이게.</summary>
        void PlayAction(string clip, float startAt)
        {
            if (!_animator.Has(clip)) clip = "attack";
            var data = _animator.Set != null ? _animator.Set.Find(clip) : null;
            if (data == null) return;
            if (data.impactFrame > 0 && startAt > 0f)
                startAt = Mathf.Clamp01((data.impactFrame - 1f) / data.frames);
            _animator.Play(clip, restart: true, startNormalized: startAt);
            float length = data.frames / Mathf.Max(1f, data.framesPerSecond);
            _actionLock = length * (1f - startAt);
        }

        void OnDamaged(float amount)
        {
            _animator.Flash(Color.white, 0.9f, 7f);
            if (_model.IsAlive && _actionLock <= 0f && _hitAnimCooldown <= 0f && _animator.Has("hit"))
            {
                _animator.Play("hit", restart: true);
                _actionLock = 0.18f;
                _hitAnimCooldown = 0.6f;
            }
            Hurt?.Invoke(this, amount);
        }

        /// <summary>연출용 밀림 (모델 위치는 바꾸지 않음).</summary>
        public void Kick(Vector2 worldDirection, float distance) => _kick += WorldProjection.DirToView(worldDirection).normalized * distance;

        public void Sync(float dt)
        {
            if (_model == null) return;

            _keyCheckTimer -= dt;
            if (_keyCheckTimer <= 0f)
            {
                _keyCheckTimer = 0.5f;
                if (_model.IsAlive) ApplyAppearance(false);
            }

            _kick = Vector2.MoveTowards(_kick, Vector2.zero, dt * 4f);
            var position = _model.Position;
            var view = WorldProjection.ToView(position);
            transform.position = view + (Vector3)_kick;
            int order = SortingOrders.ForY(view.y);
            _animator.Body.sortingOrder = order;

            if (!_model.IsAlive)
            {
                if (_deathTime < 0f)
                {
                    _deathTime = 0f;
                    _animator.Play("death", restart: true);
                    if (_barBack != null) _barBack.enabled = _barFill.enabled = false;
                }
                _deathTime += dt;
                float fade = Mathf.Clamp01((_deathTime - 0.9f) / 0.5f);
                var c = _animator.Body.color;
                _animator.Body.color = new Color(c.r, c.g, c.b, 1f - fade);
                _shadow.color = new Color(0f, 0f, 0f, 0.5f * (1f - fade));
                return;
            }

            bool moving = (position - _lastPosition).sqrMagnitude > 0.00002f;
            var velocity = (position - _lastPosition) / Mathf.Max(0.0001f, dt);
            _lastPosition = position;
            _actionLock -= dt;
            _hitAnimCooldown -= dt;

            _animator.Face(WorldProjection.DirToView(_actionLock > 0f || !moving ? _model.Facing : velocity));
            if (_actionLock <= 0f)
            {
                _animator.Play(moving ? "run" : "idle");
                _animator.Speed = moving ? Mathf.Clamp(velocity.magnitude / Mathf.Max(0.5f, _model.BaseMoveSpeed), 0.7f, 1.6f) : 1f;
            }
            else _animator.Speed = 1f;

            _animator.Body.color = _model.Statuses.Has(StatusIds.Freeze) ? _tint * FrozenTint : _tint;

            if (_barFill != null)
            {
                float fraction = Mathf.Clamp01(_model.Health.LifeFraction);
                bool show = _showBar && fraction < 0.999f;
                _barBack.enabled = _barFill.enabled = show;
                if (show)
                {
                    float width = _model.IsEliteOrBoss ? 1.1f : 0.75f;
                    _barBack.transform.localPosition = new Vector3(0f, _barTop / _scale, 0f);
                    _barBack.transform.localScale = new Vector3(width / _scale, 0.09f / _scale, 1f);
                    _barFill.transform.localScale = new Vector3(width * fraction / _scale, 0.07f / _scale, 1f);
                    _barFill.transform.localPosition = new Vector3(-width * (1f - fraction) * 0.5f / _scale, _barTop / _scale, 0f);
                    _barBack.sortingOrder = order + 5;
                    _barFill.sortingOrder = order + 6;
                }
            }
        }

        void OnDestroy()
        {
            if (_model == null) return;
            _model.Health.Damaged -= OnDamaged;
            _model.ActionStarted -= OnAction;
        }
    }
}
