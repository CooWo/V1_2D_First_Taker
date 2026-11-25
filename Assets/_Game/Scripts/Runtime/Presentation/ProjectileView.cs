using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>투사체 표시. VisualKey로 모양을 고르고 속성 색으로 칠합니다.</summary>
    public sealed class ProjectileView : MonoBehaviour
    {
        SpriteRenderer _renderer;
        Projectile _model;
        Vector3 _baseScale;
        float _spin;

        public Projectile Model => _model;

        public void Bind(Projectile model)
        {
            _model = model;
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = SortingOrders.Projectile;
            _renderer.color = Palette.For(model.Element);
            _spin = 0f;

            switch (model.VisualKey)
            {
                case "arrow":
                case "spectral":
                case "javelin":
                case "shard":
                case "ice":
                    _renderer.sprite = PixelArtFactory.Diamond;
                    _baseScale = model.VisualKey == "javelin" ? new Vector3(1.4f, 0.32f, 1f) : new Vector3(0.9f, 0.25f, 1f);
                    if (model.VisualKey == "spectral") { _baseScale = new Vector3(0.8f, 0.8f, 1f); _spin = 900f; }
                    break;
                case "wave":
                    _renderer.sprite = PixelArtFactory.Arc;
                    _baseScale = new Vector3(0.9f, 1.6f, 1f);
                    break;
                case "tornado":
                    _renderer.sprite = PixelArtFactory.Ring;
                    _baseScale = new Vector3(1f, 1f, 1f);
                    _spin = 720f;
                    break;
                case "orb":
                    _renderer.sprite = PixelArtFactory.Circle;
                    _baseScale = new Vector3(0.8f, 0.8f, 1f);
                    break;
                case "enemy_bolt":
                    _renderer.sprite = PixelArtFactory.Circle;
                    _renderer.color = Color.Lerp(Palette.For(model.Element), new Color(1f, 0.3f, 0.3f), 0.5f);
                    _baseScale = new Vector3(0.35f, 0.35f, 1f);
                    break;
                default:
                    _renderer.sprite = PixelArtFactory.Circle;
                    _baseScale = new Vector3(0.4f, 0.4f, 1f);
                    break;
            }
            Sync();
        }

        public void Sync()
        {
            if (_model == null) return;
            transform.position = new Vector3(_model.Position.x, _model.Position.y + 0.5f, 0f);
            transform.localScale = _baseScale * _model.VisualScale;
            if (_spin != 0f)
            {
                transform.Rotate(0f, 0f, _spin * Time.deltaTime);
                return;
            }
            float angle = Mathf.Atan2(_model.Direction.y, _model.Direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
