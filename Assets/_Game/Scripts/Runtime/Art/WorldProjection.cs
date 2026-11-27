using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>
    /// 게임 규칙 좌표(격자, 위에서 본 평면) ↔ 화면 좌표.
    /// 아이소메트릭: 화면 = 45° 회전 후 세로를 sin(pitch)로 눌러 줌 → 바닥 한 칸이 마름모.
    /// 서 있는 것(캐릭터·이펙트·글자)은 위치만 변환하고, 바닥에 붙은 것(타일·장판)은 <see cref="CreateGroundRoot"/> 아래에 둡니다.
    /// </summary>
    public static class WorldProjection
    {
        const float C = 0.70710678f;
        static ProjectionSettings _settings;
        static bool _loaded;

        static ProjectionSettings Settings
        {
            get
            {
                if (_loaded) return _settings;
                _loaded = true;
                _settings = Resources.Load<ProjectionSettings>(ProjectionSettings.ResourcePath);
                return _settings;
            }
        }

        public static bool Isometric => Settings == null || Settings.isometric;
        public static float Pitch => Settings != null ? Settings.pitch : 30f;
        /// <summary>바닥 세로 눌림 비율.</summary>
        public static float GroundSquash => Isometric ? Mathf.Sin(Pitch * Mathf.Deg2Rad) : 1f;

        public static Vector3 ToView(Vector2 world)
        {
            if (!Isometric) return world;
            return new Vector3((world.x - world.y) * C, (world.x + world.y) * C * GroundSquash, 0f);
        }

        public static Vector2 DirToView(Vector2 worldDirection)
        {
            if (!Isometric) return worldDirection;
            return new Vector2((worldDirection.x - worldDirection.y) * C, (worldDirection.x + worldDirection.y) * C * GroundSquash);
        }

        /// <summary>화면에서 누른 방향(조이스틱·키보드) → 규칙 좌표 방향.</summary>
        public static Vector2 ViewDirToWorld(Vector2 view)
        {
            if (!Isometric) return view;
            float a = view.x / C;
            float b = view.y / (C * GroundSquash);
            var world = new Vector2((a + b) * 0.5f, (b - a) * 0.5f);
            return world.sqrMagnitude > 0.0001f ? world.normalized * view.magnitude : Vector2.zero;
        }

        /// <summary>
        /// 바닥 평면용 부모: 이 아래에 규칙 좌표 그대로 놓으면 화면에서 아이소메트릭 바닥이 됩니다
        /// (세로 눌림 부모 → 45° 회전 자식). 반환값이 자식.
        /// </summary>
        public static Transform CreateGroundRoot(Transform parent, string name)
        {
            var squash = new GameObject(name);
            squash.transform.SetParent(parent, false);
            squash.transform.localScale = new Vector3(1f, GroundSquash, 1f);
            var rotate = new GameObject(name + "_Rotated");
            rotate.transform.SetParent(squash.transform, false);
            if (Isometric) rotate.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return rotate.transform;
        }
    }
}
