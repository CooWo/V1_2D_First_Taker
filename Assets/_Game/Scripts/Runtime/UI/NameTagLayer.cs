using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>월드 위치를 따라다니는 이름표 (마을의 다른 캐릭터).</summary>
    public sealed class NameTagLayer : MonoBehaviour
    {
        sealed class Tag
        {
            public UnityEngine.UI.Text Label;
            public Func<Vector2> Position;
        }

        readonly List<Tag> _tags = new List<Tag>();
        RectTransform _root;
        Camera _camera;

        public static NameTagLayer Create(RectTransform parent, Camera camera)
        {
            var root = UiFactory.Stretch(parent, "NameTags");
            var layer = root.gameObject.AddComponent<NameTagLayer>();
            layer._root = root;
            layer._camera = camera;
            root.SetAsFirstSibling();
            return layer;
        }

        public void Add(string text, Func<Vector2> position)
        {
            var rect = UiFactory.Anchored(_root, "Tag", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 50f));
            var label = UiFactory.Label(rect, text, 17, TextAnchor.MiddleCenter, new Color(0.85f, 0.92f, 1f));
            _tags.Add(new Tag { Label = label, Position = position });
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            foreach (var tag in _tags)
            {
                var world = Art.WorldProjection.ToView(tag.Position()) + new Vector3(0f, 1.9f, 0f);
                var screen = _camera.WorldToScreenPoint(world);
                bool visible = screen.z > 0f;
                tag.Label.enabled = visible;
                if (!visible) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
                ((RectTransform)tag.Label.transform).anchoredPosition = local;
            }
        }
    }
}
