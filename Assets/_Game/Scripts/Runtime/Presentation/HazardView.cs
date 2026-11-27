using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>예고 장판 표시: 범위 윤곽 + 시간에 따라 차오르는 안쪽 채움 (도넛은 안전 지대를 초록으로).</summary>
    public sealed class HazardView : MonoBehaviour
    {
        Hazard _model;
        SpriteRenderer _outline;
        SpriteRenderer _fill;
        SpriteRenderer _safe;

        public void Bind(Hazard model)
        {
            _model = model;
            var color = Palette.For(model.Element);
            var danger = Color.Lerp(color, new Color(1f, 0.2f, 0.15f), 0.5f);

            switch (model.Shape)
            {
                case HazardShape.Line:
                    _outline = Child("Outline", PixelArtFactory.Square, new Color(danger.r, danger.g, danger.b, 0.22f), SortingOrders.Zone + 5);
                    _fill = Child("Fill", PixelArtFactory.Square, new Color(danger.r, danger.g, danger.b, 0.45f), SortingOrders.Zone + 6);
                    float angle = Mathf.Atan2(model.Direction.y, model.Direction.x) * Mathf.Rad2Deg;
                    transform.localPosition = model.Center + model.Direction.normalized * model.Length * 0.5f;
                    transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    _outline.transform.localScale = new Vector3(model.Length, model.Width, 1f);
                    break;
                default:
                    transform.localPosition = model.Center;
                    _outline = Child("Outline", PixelArtFactory.Ring, new Color(danger.r, danger.g, danger.b, 0.9f), SortingOrders.Zone + 5);
                    _fill = Child("Fill", PixelArtFactory.Circle, new Color(danger.r, danger.g, danger.b, 0.35f), SortingOrders.Zone + 6);
                    _outline.transform.localScale = Vector3.one * model.Radius * 2f;
                    if (model.Shape == HazardShape.Donut)
                    {
                        _safe = Child("Safe", PixelArtFactory.Circle, new Color(0.2f, 0.9f, 0.45f, 0.35f), SortingOrders.Zone + 7);
                        _safe.transform.localScale = Vector3.one * model.InnerRadius * 2f;
                    }
                    break;
            }
            Sync();
        }

        SpriteRenderer Child(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        public void Sync()
        {
            if (_model == null) return;
            float p = _model.Progress;
            if (_model.Shape == HazardShape.Line)
            {
                _fill.transform.localScale = new Vector3(_model.Length, _model.Width * p, 1f);
                return;
            }
            _fill.transform.localScale = Vector3.one * _model.Radius * 2f * p;
        }
    }
}
