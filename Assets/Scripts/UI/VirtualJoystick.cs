using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LootHunters
{
    /// <summary>
    /// 세로 모바일 레이아웃의 크기. GameUI가 매 프레임 실제 UI 높이로 갱신하고,
    /// DungeonView가 이 값으로 영웅을 가운데 보이는 영역에 맞춥니다.
    /// </summary>
    public static class MobileLayout
    {
        /// <summary>화면 위쪽에서 상단 UI가 덮는 비율 (0~1).</summary>
        public static float TopFraction = 0.12f;
        /// <summary>화면 아래쪽에서 하단 UI가 덮는 비율 (0~1).</summary>
        public static float BottomFraction = 0.32f;
    }

    /// <summary>
    /// 떠오르는 가상 조이스틱. 맵 영역 아무 곳이나 누르면 그 자리에 나타나고,
    /// 끌면 그 방향으로 이동합니다 (수동 모드로 전환). 손을 떼면 사라집니다.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>현재 입력 (-1~1). 누르고 있지 않으면 0.</summary>
        public static Vector2 Value { get; private set; }

        RectTransform area, baseRt, knobRt;
        float radius = 70f;

        public void Init(RectTransform baseCircle, RectTransform knob, float radiusUnits)
        {
            area = (RectTransform)transform;
            baseRt = baseCircle;
            knobRt = knob;
            radius = radiusUnits;
            baseRt.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local)) return;
            baseRt.anchoredPosition = local;
            knobRt.anchoredPosition = Vector2.zero;
            baseRt.gameObject.SetActive(true);
            Value = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local)) return;
            var delta = Vector2.ClampMagnitude(local - baseRt.anchoredPosition, radius);
            knobRt.anchoredPosition = delta;
            var v = delta / radius;
            Value = v.magnitude < 0.15f ? Vector2.zero : v;   // 살짝 건드린 것은 무시
        }

        public void OnPointerUp(PointerEventData e)
        {
            Value = Vector2.zero;
            baseRt.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            Value = Vector2.zero;
            if (baseRt != null) baseRt.gameObject.SetActive(false);
        }

        /// <summary>조이스틱용 원 스프라이트 (코드로 생성).</summary>
        public static Sprite CircleSprite(bool filled)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    float dx = x - (n - 1) / 2f, dy = y - (n - 1) / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (n / 2f);
                    float a = filled
                        ? Mathf.Clamp01((1f - d) * 30f)
                        : Mathf.Clamp01((1f - d) * 30f) * Mathf.Clamp01((d - 0.86f) * 30f) + Mathf.Clamp01((1f - d) * 30f) * 0.25f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
