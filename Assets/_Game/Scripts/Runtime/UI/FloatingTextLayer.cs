using System.Collections.Generic;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>피해 숫자 등 월드 위치에 떠오르는 글자 (화면 공간 UI, 풀링).</summary>
    public sealed class FloatingTextLayer : MonoBehaviour, IFloatingText
    {
        const int MaxActive = 70;

        sealed class Entry
        {
            public UnityEngine.UI.Text Label;
            public Vector3 World;
            public float Time;
            public float Duration;
            public Color Color;
        }

        readonly List<Entry> _active = new List<Entry>();
        readonly Stack<Entry> _pool = new Stack<Entry>();
        Camera _camera;
        RectTransform _root;

        public void Initialize(RectTransform root, Camera camera)
        {
            _root = root;
            _camera = camera;
        }

        public void Show(Vector3 worldPosition, string text, Color color, float size, float duration)
        {
            if (_root == null || string.IsNullOrEmpty(text)) return;
            if (_active.Count >= MaxActive) Recycle(0);

            var entry = _pool.Count > 0 ? _pool.Pop() : Create();
            entry.Label.gameObject.SetActive(true);
            entry.Label.text = text;
            entry.Label.fontSize = Mathf.RoundToInt(size);
            entry.World = worldPosition;
            entry.Time = 0f;
            entry.Duration = duration;
            entry.Color = color;
            _active.Add(entry);
            Place(entry);
        }

        Entry Create()
        {
            var rect = UiFactory.Anchored(_root, "Float", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 50f));
            var label = UiFactory.Label(rect, string.Empty, 24, TextAnchor.MiddleCenter, Color.white);
            return new Entry { Label = label };
        }

        void LateUpdate()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var entry = _active[i];
                entry.Time += Time.deltaTime;
                if (entry.Time >= entry.Duration)
                {
                    Recycle(i);
                    continue;
                }
                Place(entry);
            }
        }

        void Place(Entry entry)
        {
            if (_camera == null) return;
            float t = entry.Time / entry.Duration;
            var world = entry.World + Vector3.up * (t * 0.8f);
            var screen = _camera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            var rect = (RectTransform)entry.Label.transform;
            rect.anchoredPosition = local;
            float pop = t < 0.15f ? Mathf.Lerp(1.4f, 1f, t / 0.15f) : 1f;
            rect.localScale = Vector3.one * pop;
            var c = entry.Color;
            c.a = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            entry.Label.color = c;
        }

        void Recycle(int index)
        {
            var entry = _active[index];
            _active.RemoveAt(index);
            entry.Label.gameObject.SetActive(false);
            _pool.Push(entry);
        }
    }
}
