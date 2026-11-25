using ShadowRPG.Core.AutoBattle;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>화면 왼쪽 아래 가상 조이스틱. 손을 떼면 자동 이동으로 돌아갑니다.</summary>
    public sealed class VirtualJoystick : MonoBehaviour, IMoveInput, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float Radius = 110f;

        RectTransform _base;
        RectTransform _knob;
        Vector2 _direction;

        public Vector2 Direction => _direction;

        public static VirtualJoystick Create(Transform parent)
        {
            var area = UiFactory.Anchored(parent, "Joystick", new Vector2(0f, 0f), new Vector2(30f, 210f), new Vector2(300f, 300f));
            var hit = UiFactory.Panel(area, new Color(0f, 0f, 0f, 0.001f), true);
            hit.raycastTarget = true;
            var joystick = area.gameObject.AddComponent<VirtualJoystick>();

            joystick._base = UiFactory.Anchored(area, "Base", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Radius * 2f, Radius * 2f));
            var baseImage = UiFactory.Panel(joystick._base, new Color(0.3f, 0.5f, 1f, 0.18f));
            baseImage.sprite = PixelArtFactory.Circle;

            joystick._knob = UiFactory.Anchored(joystick._base, "Knob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));
            var knobImage = UiFactory.Panel(joystick._knob, new Color(0.45f, 0.7f, 1f, 0.55f));
            knobImage.sprite = PixelArtFactory.Circle;
            return joystick;
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_base, eventData.position, eventData.pressEventCamera, out var local)) return;
            var clamped = Vector2.ClampMagnitude(local, Radius);
            _knob.anchoredPosition = clamped;
            _direction = clamped.magnitude > Radius * 0.15f ? clamped / Radius : Vector2.zero;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _direction = Vector2.zero;
            _knob.anchoredPosition = Vector2.zero;
        }
    }
}
