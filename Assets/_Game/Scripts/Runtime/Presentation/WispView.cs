using System;
using ShadowRPG.Runtime.Art;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 펫 임시 외형: 떠다니는 빛 정령 (이펙트 그림 + 2D 조명). 정식 펫 모델을 구우면 CombatantView 방식으로 교체합니다.
    /// </summary>
    public sealed class WispView : MonoBehaviour
    {
        Func<Vector2> _position;
        SpriteRenderer _core;
        SpriteRenderer _swirl;
        float _seed;

        public static WispView Create(Transform parent, Func<Vector2> position, Color color)
        {
            var go = new GameObject("PetWisp");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<WispView>();
            view.Build(position, color);
            return view;
        }

        void Build(Func<Vector2> position, Color color)
        {
            _position = position;
            _seed = UnityEngine.Random.value * 10f;
            var fx = FxCatalog.Load();
            _core = Layer("Core", fx != null ? FxCatalog.Pick(fx.glow, null) : null, color, 0.9f);
            _swirl = Layer("Swirl", fx != null && fx.magic != null && fx.magic.Length > 0 ? fx.magic[0] : null, color * 0.8f, 0.7f);
            var light = new GameObject("Light").AddComponent<Light2D>();
            light.transform.SetParent(transform, false);
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = 0.8f;
            light.pointLightOuterRadius = 2.5f;
            transform.position = position();
        }

        SpriteRenderer Layer(string name, Sprite sprite, Color color, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sharedMaterial = FxMaterials.Additive;
            r.sortingOrder = SortingOrders.Vfx;
            return r;
        }

        void LateUpdate()
        {
            if (_position == null) return;
            var target = (Vector3)_position() + new Vector3(0f, 1.1f + Mathf.Sin(Time.time * 2.4f + _seed) * 0.15f, 0f);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            if (_swirl != null) _swirl.transform.Rotate(0f, 0f, 120f * Time.deltaTime);
            if (_core != null) _core.transform.localScale = Vector3.one * (0.85f + Mathf.Sin(Time.time * 5f + _seed) * 0.08f);
        }
    }
}
