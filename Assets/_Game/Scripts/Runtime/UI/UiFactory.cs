using System;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>uGUI 요소 생성 도우미. 모든 UI 타입은 이름 충돌을 피하려고 정규화된 이름을 씁니다.</summary>
    public static class UiFactory
    {
        static Font _font;

        /// <summary>세로 화면 여부 (HUD 배치 기준).</summary>
        public static bool IsPortrait => Screen.height > Screen.width;

        /// <summary>
        /// 기본 동적 폰트. 한글 글리프는 OS 폰트 폴백(Windows 맑은 고딕, Android Noto CJK)으로 그려집니다.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = UiSkin.Current != null && UiSkin.Current.font != null ? UiSkin.Current.font : Resources.Load<Font>("Fonts/NotoSansKR");
                if (_font != null) return _font;
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Arial" }, 28);
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            bool portrait = Screen.height > Screen.width;
            scaler.referenceResolution = portrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = portrait ? 0f : 0.5f;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return canvas;
        }

        /// <summary>한 점에 고정된 요소 (anchor = pivot).</summary>
        public static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>부모를 가득 채우는 요소.</summary>
        public static RectTransform Stretch(Transform parent, string name, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static UnityEngine.UI.Image Panel(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = PixelArtFactory.Square;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static UnityEngine.UI.Text Label(RectTransform rect, string text, int size, TextAnchor alignment, Color color)
        {
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            var outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return label;
        }

        public static UnityEngine.UI.Text Label(Transform parent, string name, string text, int size, TextAnchor alignment, Color color, float inset = 6f)
            => Label(Stretch(parent, name, inset), text, size, alignment, color);

        public static UnityEngine.UI.Button Button(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, string text, Action onClick)
        {
            var rect = Anchored(parent, name, anchor, position, size);
            var skin = UiSkin.Current;
            var background = Panel(rect, new Color(0.1f, 0.13f, 0.2f, 0.92f), true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            if (skin != null && skin.button != null)
            {
                background.sprite = skin.button;
                background.type = UnityEngine.UI.Image.Type.Sliced;
                background.color = skin.buttonTint;
                button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            }
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.12f, 1.2f);
            colors.pressedColor = new Color(0.7f, 0.68f, 0.8f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.55f, 0.6f);
            colors.colorMultiplier = 1.3f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());
            if (skin == null || skin.button == null) Border(rect, Palette.PanelBorder);
            Label(rect, "Label", text, 26, TextAnchor.MiddleCenter, Color.white, 4f);
            return button;
        }

        /// <summary>큰 창 배경 (스킨의 9-slice 패널, 없으면 단색 + 테두리).</summary>
        public static UnityEngine.UI.Image Frame(RectTransform rect, bool inset = false, bool raycast = true)
        {
            var skin = UiSkin.Current;
            var image = Panel(rect, new Color(0.04f, 0.05f, 0.08f, 0.96f), raycast);
            var sprite = skin == null ? null : inset ? skin.inset : skin.panel;
            if (sprite == null)
            {
                Border(rect, Palette.PanelBorder);
                return image;
            }
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = inset ? skin.insetTint : skin.panelTint;
            return image;
        }

        /// <summary>얇은 테두리 (4변).</summary>
        public static void Border(RectTransform rect, Color color, float thickness = 2f)
        {
            Edge(rect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, color);
            Edge(rect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color);
            Edge(rect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color);
            Edge(rect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, color);
        }

        static void Edge(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Panel(rect, color);
        }

        /// <summary>가로 채움 막대.</summary>
        public static UnityEngine.UI.Image Fill(RectTransform rect, Color color, UnityEngine.UI.Image.FillMethod method = UnityEngine.UI.Image.FillMethod.Horizontal)
        {
            var image = Panel(rect, color);
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = method;
            image.fillOrigin = 0;
            image.fillAmount = 1f;
            return image;
        }
    }
}
