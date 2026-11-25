using System.Collections.Generic;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>장판·공전체 표시.</summary>
    public sealed class ZoneView : MonoBehaviour
    {
        readonly List<SpriteRenderer> _blades = new List<SpriteRenderer>();
        Zone _model;
        SpriteRenderer _fill;
        SpriteRenderer _ring;
        float _time;
        int _bladeCount;

        public Zone Model => _model;

        public void Bind(Zone model)
        {
            _model = model;
            _time = 0f;
            var color = Palette.For(model.Element);

            _fill = Child("Fill", PixelArtFactory.Circle, new Color(color.r, color.g, color.b, 0f));
            _ring = Child("Ring", PixelArtFactory.Ring, new Color(color.r, color.g, color.b, 0.7f));

            switch (model.VisualKey)
            {
                case "lava":
                    _fill.color = new Color(1f, 0.4f, 0.1f, 0.35f);
                    break;
                case "void":
                    _fill.color = new Color(0.08f, 0.02f, 0.16f, 0.75f);
                    _ring.color = new Color(0.55f, 0.35f, 1f, 0.9f);
                    break;
                case "decoy":
                    _fill.sprite = PixelArtFactory.HeroIdle[0];
                    _fill.color = new Color(0.4f, 0.55f, 1f, 0.55f);
                    _ring.color = new Color(0.3f, 0.4f, 0.9f, 0.3f);
                    break;
                case "pull":
                    _ring.color = new Color(0.6f, 0.6f, 0.75f, 0.5f);
                    break;
                case "aegis":
                    _ring.color = new Color(0.4f, 0.8f, 1f, 0.8f);
                    _fill.color = new Color(0.4f, 0.8f, 1f, 0.12f);
                    break;
                case "blade_vortex":
                case "orbit_blades":
                    _ring.color = new Color(color.r, color.g, color.b, 0.2f);
                    break;
            }
            Sync(0f);
        }

        SpriteRenderer Child(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = SortingOrders.Zone;
            return renderer;
        }

        public void Sync(float dt)
        {
            if (_model == null) return;
            _time += dt;

            transform.position = new Vector3(_model.Position.x, _model.Position.y, 0f);
            float diameter = _model.Radius * 2f;

            if (_model.VisualKey == "decoy")
            {
                _fill.transform.localScale = Vector3.one;
                _fill.transform.localPosition = Vector3.zero;
                _fill.sortingOrder = SortingOrders.ForY(_model.Position.y);
                _ring.transform.localScale = Vector3.one * diameter * (0.6f + 0.4f * (_model.Elapsed / _model.Duration));
                return;
            }

            _fill.transform.localScale = Vector3.one * diameter;
            float pulse = _model.VisualKey == "pull" ? Mathf.Lerp(1.3f, 0.4f, Mathf.Repeat(_time * 3f, 1f)) : 1f + 0.04f * Mathf.Sin(_time * 6f);
            _ring.transform.localScale = Vector3.one * diameter * pulse;
            _ring.transform.Rotate(0f, 0f, (_model.VisualKey == "void" ? -240f : 90f) * dt);

            if (_model.VisualKey == "blade_vortex" || _model.VisualKey == "orbit_blades" || _model.VisualKey == "storm")
                SyncBlades(dt);
        }

        void SyncBlades(float dt)
        {
            int count = _model.VisualKey == "blade_vortex" ? _model.Stacks + 1 : 3;
            if (count != _bladeCount)
            {
                foreach (var b in _blades) Destroy(b.gameObject);
                _blades.Clear();
                var color = Palette.For(_model.Element);
                for (int i = 0; i < count; i++)
                {
                    var blade = Child("Blade", PixelArtFactory.Diamond, new Color(color.r, color.g, color.b, 0.9f));
                    blade.sortingOrder = SortingOrders.Projectile - 1;
                    _blades.Add(blade);
                }
                _bladeCount = count;
            }

            float speed = _model.VisualKey == "storm" ? 400f : 300f;
            for (int i = 0; i < _blades.Count; i++)
            {
                float angle = (_time * speed + 360f / _blades.Count * i) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _model.Radius * 0.85f;
                _blades[i].transform.localPosition = offset + Vector3.up * 0.5f;
                _blades[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
                _blades[i].transform.localScale = new Vector3(0.7f, 0.22f, 1f);
            }
        }
    }
}
