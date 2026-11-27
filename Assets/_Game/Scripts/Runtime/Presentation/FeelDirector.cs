using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Tuning;
using ShadowRPG.Runtime.Art;
using Unity.Cinemachine;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 타격감 감독 (테일즈위버식 임팩트): 피해 결과를 받아 히트스톱·화면 흔들림·화면 번쩍임·넉백·불꽃·피해 숫자를 냅니다.
    /// 모든 세기는 PresentationTuning. 흔들림은 Cinemachine Impulse, 정지는 GameClock 한 곳에서.
    /// </summary>
    public sealed class FeelDirector : MonoBehaviour
    {
        PresentationTuning _t;
        CinemachineImpulseSource _impulse;
        Combatant _player;
        IFloatingText _text;
        System.Func<Combatant, CombatantView> _views;
        UnityEngine.UI.Image _flash;
        float _flashAlpha;
        Color _flashColor;
        float _lastStop;
        readonly Stack<TransientSprite> _pool = new Stack<TransientSprite>();

        public bool ShakeEnabled { get; set; } = true;
        public bool NumbersEnabled { get; set; } = true;

        public void Initialize(PresentationTuning tuning, Combatant player, IFloatingText text, System.Func<Combatant, CombatantView> views, UnityEngine.UI.Image flashOverlay)
        {
            _t = tuning;
            _player = player;
            _text = text;
            _views = views;
            _flash = flashOverlay;
            _impulse = gameObject.AddComponent<CinemachineImpulseSource>();
            _impulse.ImpulseDefinition.ImpulseDuration = 0.18f;
            _impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            _impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            _impulse.DefaultVelocity = new Vector3(0.6f, 1f, 0f);
        }

        public void Shake(float force)
        {
            if (!ShakeEnabled || force <= 0f || _impulse == null) return;
            var dir = Random.insideUnitCircle.normalized;
            _impulse.GenerateImpulseWithVelocity(new Vector3(dir.x, dir.y, 0f) * force);
        }

        public void Flash(Color color, float alpha)
        {
            _flashColor = color;
            _flashAlpha = Mathf.Max(_flashAlpha, alpha);
        }

        void Stop(float seconds)
        {
            // 연타에 히트스톱이 쌓여 게임이 끈적이지 않게 최소 간격
            if (Time.unscaledTime - _lastStop < _t.HitStopCooldown && seconds < _t.HitStopFinisher) return;
            _lastStop = Time.unscaledTime;
            GameClock.HitStop(seconds);
        }

        public void OnDamage(DamageResult r)
        {
            if (r.Target == null || _t == null) return;
            bool byPlayer = r.Attacker == _player;
            bool toPlayer = r.Target == _player;
            var targetView = _views?.Invoke(r.Target);

            if (r.Evaded)
            {
                if (NumbersEnabled) _text?.Show((Vector3)r.Target.Position + Vector3.up * 1.4f, "회피", new Color(0.75f, 0.85f, 1f), _t.DamageNumberSize * 0.8f, 0.5f);
                return;
            }
            if (r.Amount < 0.5f) return;

            var hitPoint = (Vector3)r.Target.Position + Vector3.up * 0.6f;
            if (byPlayer)
            {
                bool heavy = r.IsCrit || r.Killed && r.Target.IsEliteOrBoss;
                Stop(r.Target.Rank == EnemyRank.Boss && r.Killed ? _t.HitStopBoss : r.IsCrit ? _t.HitStopCrit : _t.HitStopNormal);
                Shake(r.Killed && r.Target.Rank == EnemyRank.Boss ? _t.ShakeBoss : r.IsCrit ? _t.ShakeCrit : _t.ShakeNormal);
                if (targetView != null)
                {
                    var away = r.Target.Position - _player.Position;
                    targetView.Kick(away, r.IsCrit ? _t.KnockbackCrit : _t.KnockbackNormal);
                }
                Spark(hitPoint, ElementColor(r.Type), heavy ? 1.5f : 1f);
                if (r.Killed && r.Target.Rank == EnemyRank.Boss) Flash(new Color(1f, 0.95f, 0.8f), _t.ScreenFlashFinisher * 1.5f);
            }
            else if (toPlayer)
            {
                Shake(_t.ShakePlayerHurt);
                Flash(new Color(0.8f, 0.05f, 0.05f), _t.HurtVignette * Mathf.Clamp01(r.Amount / Mathf.Max(1f, r.Target.Health.MaxLife) * 6f));
                Spark(hitPoint, new Color(1f, 0.35f, 0.3f), 0.8f);
            }

            if (!NumbersEnabled || _text == null) return;
            var color = toPlayer ? new Color(1f, 0.35f, 0.32f) : r.IsCrit ? new Color(1f, 0.62f, 0.15f) : Color.Lerp(Color.white, ElementColor(r.Type), 0.35f);
            float size = _t.DamageNumberSize * (r.IsCrit ? _t.CritNumberScale : 1f);
            string label = Mathf.RoundToInt(r.Amount).ToString("N0") + (r.IsCrit ? "!" : string.Empty);
            _text.Show((Vector3)r.Target.Position + new Vector3(Random.Range(-0.35f, 0.35f), 1.3f, 0f), label, color, size, _t.DamageNumberLifetime);
        }

        void Spark(Vector3 at, Color color, float power)
        {
            var fx = FxCatalog.Load();
            if (fx == null) return;
            var spark = FxCatalog.Pick(fx.spark, null);
            if (spark != null) Spawn(spark, Color.Lerp(color, Color.white, 0.5f), at, Random.Range(0f, 360f), 0.5f * power, 1.5f * power, 0.14f);
            var star = power > 1.2f ? FxCatalog.Pick(fx.star, null) : null;
            if (star != null) Spawn(star, color, at, Random.Range(0f, 90f), 0.8f, 2.4f, 0.2f);
        }

        void Spawn(Sprite sprite, Color color, Vector3 at, float rotation, float from, float to, float duration)
        {
            var fx = _pool.Count > 0 ? _pool.Pop() : CreateFx();
            fx.Renderer.sharedMaterial = FxMaterials.Additive;
            fx.Play(sprite, color, at, rotation, Vector3.one * from, Vector3.one * to, 1f, 0f, duration, SortingOrders.Vfx + 20, f => _pool.Push(f));
        }

        TransientSprite CreateFx()
        {
            var go = new GameObject("Spark");
            go.transform.SetParent(transform, false);
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<TransientSprite>();
        }

        void Update()
        {
            if (_flash == null) return;
            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, Time.unscaledDeltaTime * 2.5f);
            _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashAlpha);
            _flash.enabled = _flashAlpha > 0.005f;
        }

        public static Color ElementColor(DamageType type) => Palette.For(type);
    }
}
