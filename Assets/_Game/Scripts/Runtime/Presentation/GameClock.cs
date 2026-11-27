using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 시간 배율을 한 곳에서 계산합니다: 세션(층 클리어 슬로우) × 배속 × 히트스톱.
    /// 여러 연출이 Time.timeScale을 서로 덮어쓰지 않게 하는 단일 창구입니다.
    /// </summary>
    public static class GameClock
    {
        static float _hitStopUntil;
        static float _hitStopScale = 0.05f;

        public static float SessionScale { get; set; } = 1f;
        public static float Speed { get; set; } = 1f;
        public static bool Paused { get; set; }

        public static bool InHitStop => Time.unscaledTime < _hitStopUntil;

        public static void HitStop(float seconds, float scale = 0.05f)
        {
            if (seconds <= 0f) return;
            _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + seconds);
            _hitStopScale = scale;
        }

        public static void Apply()
        {
            float scale = Paused ? 0f : SessionScale * Speed * (InHitStop ? _hitStopScale : 1f);
            Time.timeScale = scale;
        }

        public static void Reset()
        {
            _hitStopUntil = 0f;
            SessionScale = 1f;
            Paused = false;
            Time.timeScale = 1f;
        }
    }
}
