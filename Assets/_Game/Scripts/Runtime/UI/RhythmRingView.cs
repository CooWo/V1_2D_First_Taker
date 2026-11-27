using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Rhythm;
using ShadowRPG.Core.Tuning;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>
    /// 리듬 원 (방향성 v1 §7): 화면 한쪽 고정. 바깥 원이 줄어들어 판정 원에 맞을 때 터치.
    /// 판정: 완벽(PERFECT)/좋음(GOOD)/놓침 — 놓쳐도 불이익 없음. 위치는 설정에서 좌/우.
    /// 리듬 무기를 들었을 때만 보입니다.
    /// </summary>
    public sealed class RhythmRingView : MonoBehaviour, IPointerDownHandler
    {
        RhythmController _rhythm;
        RhythmTuning _t;
        PlayerSettings _settings;
        RectTransform _root;
        RectTransform _outer;
        UnityEngine.UI.Image _outerImage;
        UnityEngine.UI.Image _target;
        UnityEngine.UI.Image _core;
        UnityEngine.UI.Text _judgement;
        UnityEngine.UI.Text _combo;
        float _judgementTimer;
        float _pulse;
        System.Func<bool> _visible;

        public static RhythmRingView Create(RectTransform parent, RhythmController rhythm, RhythmTuning tuning, PlayerSettings settings, System.Func<bool> visible = null)
        {
            var root = UiFactory.Anchored(parent, "RhythmRing", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * tuning.RingSizeDp * 1.6f);
            var view = root.gameObject.AddComponent<RhythmRingView>();
            view._visible = visible;
            view.Build(root, rhythm, tuning, settings);
            return view;
        }

        void Build(RectTransform root, RhythmController rhythm, RhythmTuning tuning, PlayerSettings settings)
        {
            _root = root;
            _rhythm = rhythm;
            _t = tuning;
            _settings = settings;
            var hit = UiFactory.Panel(root, new Color(0f, 0f, 0f, 0.001f), true);
            hit.sprite = UiShapes.Disc;

            float size = tuning.RingSizeDp;
            _core = Image(root, "Core", UiShapes.Disc, size * 0.82f, new Color(0.15f, 0.1f, 0.3f, 0.45f));
            _target = Image(root, "Target", UiShapes.Ring, size, new Color(1f, 0.82f, 0.4f, 0.95f));
            _outerImage = Image(root, "Outer", UiShapes.ThinRing, size, new Color(0.6f, 0.8f, 1f, 0.9f));
            _outer = _outerImage.rectTransform;
            _judgement = UiFactory.Label(UiFactory.Anchored(root, "Judgement", new Vector2(0.5f, 1f), new Vector2(0f, 20f), new Vector2(360f, 60f)), string.Empty, 34, TextAnchor.MiddleCenter, Color.white);
            _combo = UiFactory.Label(UiFactory.Anchored(root, "Combo", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 80f)), string.Empty, 30, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            rhythm.Judged += OnJudged;
            rhythm.Beat += () => _pulse = 1f;
        }

        static UnityEngine.UI.Image Image(RectTransform parent, string name, Sprite sprite, float size, Color color)
        {
            var image = UiFactory.Panel(UiFactory.Anchored(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size), color);
            image.sprite = sprite;
            return image;
        }

        public void OnPointerDown(PointerEventData eventData) => _rhythm.Tap();

        void OnJudged(RhythmJudgement judgement, int hits)
        {
            _judgementTimer = 0.45f;
            switch (judgement)
            {
                case RhythmJudgement.Perfect:
                    _judgement.text = "<b>PERFECT</b>";
                    _judgement.color = new Color(1f, 0.85f, 0.35f);
                    if (_settings.Vibration && _t.VibrateOnPerfect) Handheld.Vibrate();
                    break;
                case RhythmJudgement.Good:
                    _judgement.text = "GOOD";
                    _judgement.color = new Color(0.6f, 0.85f, 1f);
                    break;
                default:
                    _judgement.text = "MISS";
                    _judgement.color = new Color(0.6f, 0.6f, 0.7f);
                    break;
            }
        }

        void Update()
        {
            bool active = _rhythm != null && _rhythm.IsActive && (_visible == null || _visible());
            foreach (Transform child in _root) child.gameObject.SetActive(active);
            if (!active) return;

            // 위치: 설정(좌/우) 또는 기본값
            float x = _settings.RhythmRingX >= 0f ? _settings.RhythmRingX : _t.RingScreenX;
            float y = _settings.RhythmRingY >= 0f ? _settings.RhythmRingY : _t.RingScreenY;
            _root.anchorMin = _root.anchorMax = new Vector2(x, y);
            _root.anchoredPosition = Vector2.zero;
            _root.localScale = Vector3.one * Mathf.Max(0.5f, _settings.RhythmRingScale);

            // 바깥 원: 박 시작 RingStartScale → 판정 순간 1.0
            float phase = _rhythm.Phase;
            float scale = Mathf.Lerp(_t.RingStartScale, 1f, phase);
            _outer.localScale = Vector3.one * scale;
            _outerImage.color = new Color(0.6f, 0.8f, 1f, Mathf.Lerp(0.25f, 1f, phase));

            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.unscaledDeltaTime * 4f);
            _target.rectTransform.localScale = Vector3.one * (1f + _pulse * 0.12f);
            _core.color = new Color(0.15f + _rhythm.Stacks * 0.05f, 0.1f, 0.3f + _rhythm.Stacks * 0.04f, 0.45f + _pulse * 0.2f);

            _combo.text = _rhythm.Combo > 1 ? $"<b>{_rhythm.Combo}</b>\n<size=16>콤보 · 공속 +{_rhythm.Stacks * _t.AttackSpeedPctPerStack:0}%</size>" : string.Empty;
            _judgementTimer -= Time.unscaledDeltaTime;
            if (_judgementTimer <= 0f) _judgement.text = string.Empty;
        }

        void OnDestroy()
        {
            if (_rhythm != null) _rhythm.Judged -= OnJudged;
        }
    }
}
