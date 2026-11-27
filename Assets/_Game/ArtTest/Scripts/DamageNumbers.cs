using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.ArtTest
{
    /// <summary>튀어 오르며 쌓이는 피해 숫자 (연속 타격 연출). 화면 공간 UI로 그립니다.</summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        sealed class Entry
        {
            public UnityEngine.UI.Text Label;
            public Vector3 World;
            public Vector2 Drift;
            public float Time;
            public float Duration;
            public float Size;
            public Color Color;
        }

        readonly List<Entry> _active = new List<Entry>();
        readonly Stack<Entry> _pool = new Stack<Entry>();
        RectTransform _root;
        Camera _camera;
        Font _font;

        public void Initialize(RectTransform root, Camera camera, Font font)
        {
            _root = root;
            _camera = camera;
            _font = font;
        }

        public void Show(Vector3 world, int amount, bool crit, int stackIndex = 0)
        {
            var e = _pool.Count > 0 ? _pool.Pop() : Create();
            e.World = world + new Vector3(Random.Range(-0.15f, 0.15f), 0.35f * stackIndex, 0f);
            e.Drift = new Vector2(Random.Range(-0.2f, 0.2f), 0.9f);
            e.Time = 0f;
            e.Duration = crit ? 0.9f : 0.65f;
            e.Size = crit ? 64f : 40f;
            e.Color = crit ? new Color(1f, 0.72f, 0.2f) : new Color(0.95f, 0.95f, 1f);
            e.Label.text = crit ? amount + "!" : amount.ToString();
            e.Label.gameObject.SetActive(true);
            _active.Add(e);
        }

        Entry Create()
        {
            var go = new GameObject("Dmg", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var label = go.AddComponent<UnityEngine.UI.Text>();
            label.font = _font;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.05f, 0.02f, 0.08f, 1f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            ((RectTransform)go.transform).sizeDelta = new Vector2(300f, 100f);
            return new Entry { Label = label };
        }

        void LateUpdate()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                e.Time += Time.unscaledDeltaTime;
                float t = e.Time / e.Duration;
                if (t >= 1f)
                {
                    e.Label.gameObject.SetActive(false);
                    _active.RemoveAt(i);
                    _pool.Push(e);
                    continue;
                }
                var world = e.World + (Vector3)(e.Drift * Mathf.Sqrt(t));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, _camera.WorldToScreenPoint(world), null, out var local);
                var rt = (RectTransform)e.Label.transform;
                rt.anchoredPosition = local;
                // 튀어나왔다가 제자리 (팝)
                float pop = t < 0.12f ? Mathf.Lerp(1.8f, 1f, t / 0.12f) : 1f;
                e.Label.fontSize = Mathf.RoundToInt(e.Size * pop);
                var c = e.Color;
                c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                e.Label.color = c;
            }
        }
    }
}
