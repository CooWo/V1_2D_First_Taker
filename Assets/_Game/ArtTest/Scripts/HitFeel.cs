using UnityEngine;

namespace ShadowRPG.ArtTest
{
    /// <summary>
    /// 타격감 연출: 히트스톱(시간 정지), 화면 흔들림, 화면 번쩍임.
    /// 히트스톱은 실제 시간으로 재므로 정지 중에도 정확히 끝납니다.
    /// </summary>
    public sealed class HitFeel : MonoBehaviour
    {
        Camera _camera;
        Vector3 _basePosition;
        float _trauma;
        float _stopUntil;
        UnityEngine.UI.Image _flash;
        float _flashAlpha;
        Color _flashColor = Color.white;

        public void Initialize(Camera camera, UnityEngine.UI.Image flashOverlay)
        {
            _camera = camera;
            _basePosition = camera.transform.position;
            _flash = flashOverlay;
        }

        public void SetAnchor(Vector3 position) => _basePosition = position;

        /// <summary>히트스톱: 아주 짧게 시간을 멈춰 "맞았다"는 무게감을 줍니다.</summary>
        public void HitStop(float seconds)
        {
            _stopUntil = Mathf.Max(_stopUntil, Time.unscaledTime + seconds);
            Time.timeScale = 0.02f;
        }

        public void Shake(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        public void Flash(Color color, float alpha)
        {
            _flashColor = color;
            _flashAlpha = Mathf.Max(_flashAlpha, alpha);
        }

        void LateUpdate()
        {
            if (_stopUntil > 0f && Time.unscaledTime >= _stopUntil)
            {
                _stopUntil = 0f;
                Time.timeScale = 1f;
            }

            if (_camera != null)
            {
                float shake = _trauma * _trauma;
                float t = Time.unscaledTime * 40f;
                var offset = new Vector3(Mathf.PerlinNoise(t, 1.7f) - 0.5f, Mathf.PerlinNoise(3.1f, t) - 0.5f, 0f) * shake * 0.9f;
                _camera.transform.position = _basePosition + offset;
                _trauma = Mathf.MoveTowards(_trauma, 0f, Time.unscaledDeltaTime * 1.8f);
            }

            if (_flash != null)
            {
                _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, Time.unscaledDeltaTime * 4f);
                _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashAlpha);
            }
        }

        void OnDisable() => Time.timeScale = 1f;
    }
}
