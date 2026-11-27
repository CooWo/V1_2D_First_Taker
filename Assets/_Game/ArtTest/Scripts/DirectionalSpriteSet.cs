using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.ArtTest
{
    /// <summary>
    /// 3D 모델에서 구운 8방향 애니메이션 묶음. 동작마다 텍스처 한 장 (행 = 방향 0~7, 열 = 프레임).
    /// 방향 0 = 오른쪽(E)에서 반시계 방향으로 45°씩: 0 E, 1 NE, 2 N, 3 NW, 4 W, 5 SW, 6 S, 7 SE.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art Test/Directional Sprite Set", fileName = "DirectionalSpriteSet")]
    public sealed class DirectionalSpriteSet : ScriptableObject
    {
        public const int Directions = 8;

        [Serializable]
        public sealed class Clip
        {
            public string name;
            public Texture2D texture;
            public int frames;
            public float framesPerSecond = 12f;
            public bool loop = true;
            [Tooltip("타격 판정이 나는 프레임 (공격 동작용, -1 = 없음)")]
            public int impactFrame = -1;
        }

        public string characterName;
        public int cellSize = 256;
        public float pixelsPerUnit = 112f;
        [Tooltip("셀 안 발 위치 (0~1)")]
        public Vector2 pivot = new Vector2(0.5f, 0.2f);
        public List<Clip> clips = new List<Clip>();

        readonly Dictionary<string, Sprite[][]> _cache = new Dictionary<string, Sprite[][]>();

        public Clip Find(string clipName) => clips.Find(c => c.name == clipName);

        /// <summary>동작·방향별 프레임 (처음 요청할 때 잘라 만듭니다).</summary>
        public Sprite[] Frames(string clipName, int direction)
        {
            if (!_cache.TryGetValue(clipName, out var byDirection) || byDirection[0] == null || byDirection[0].Length == 0 || byDirection[0][0] == null)
            {
                var clip = Find(clipName);
                if (clip == null || clip.texture == null) return Array.Empty<Sprite>();
                byDirection = new Sprite[Directions][];
                for (int d = 0; d < Directions; d++)
                {
                    byDirection[d] = new Sprite[clip.frames];
                    for (int f = 0; f < clip.frames; f++)
                    {
                        // 텍스처 위쪽이 방향 0
                        var rect = new Rect(f * cellSize, clip.texture.height - (d + 1) * cellSize, cellSize, cellSize);
                        byDirection[d][f] = Sprite.Create(clip.texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    }
                }
                _cache[clipName] = byDirection;
            }
            return byDirection[Mathf.Clamp(direction, 0, Directions - 1)];
        }

        public static int DirectionOf(Vector2 facing)
        {
            if (facing.sqrMagnitude < 0.0001f) return 6;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            return ((Mathf.RoundToInt(angle / 45f) % Directions) + Directions) % Directions;
        }
    }
}
