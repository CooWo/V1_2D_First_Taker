using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>
    /// 3D 모델에서 구운 8방향 애니메이션 묶음. 동작마다 텍스처 한 장 (행 = 구운 방향, 열 = 프레임).
    /// 게임 방향: 0 E, 1 NE, 2 N, 3 NW, 4 W, 5 SW, 6 S, 7 SE (오른쪽에서 반시계 45°씩).
    /// 용량을 줄이려고 5방향(E·NE·N·S·SE)만 굽고 나머지 3방향은 좌우 반전으로 만듭니다 (<see cref="mirrored"/>).
    /// AI·손그림 스프라이트로 바꿀 때도 같은 행·열 규칙으로 텍스처만 넣으면 됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Directional Sprite Set", fileName = "SpriteSet")]
    public sealed class DirectionalSpriteSet : ScriptableObject
    {
        public const int Directions = 8;

        /// <summary>반전 사용 시 텍스처에 실제로 들어 있는 방향 (행 순서).</summary>
        public static readonly int[] MirroredRows = { 0, 1, 2, 6, 7 };

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
        public int cellSize = 224;
        [Tooltip("셀 픽셀 / 게임 1유닛. 모든 굽기 결과가 같은 밀도를 쓰면 크기 비율이 3D 모델 그대로 유지됩니다.")]
        public float pixelsPerUnit = 128f;
        [Tooltip("셀 안 발 위치 (0~1)")]
        public Vector2 pivot = new Vector2(0.5f, 0.2f);
        [Tooltip("켜면 5방향만 들어 있고 W·NW·SW는 좌우 반전")]
        public bool mirrored = true;
        [Tooltip("캐릭터 키 (게임 유닛, 정보용)")]
        public float heightUnits = 1.3f;
        public List<Clip> clips = new List<Clip>();

        readonly Dictionary<string, Sprite[][]> _cache = new Dictionary<string, Sprite[][]>();

        public Clip Find(string clipName) => clips.Find(c => c.name == clipName);
        public bool Has(string clipName) => Find(clipName) != null;

        int RowCount => mirrored ? MirroredRows.Length : Directions;

        /// <summary>게임 방향 → (텍스처 행, 반전 여부).</summary>
        public (int row, bool flip) Resolve(int direction)
        {
            direction = ((direction % Directions) + Directions) % Directions;
            if (!mirrored) return (direction, false);
            switch (direction)
            {
                case 3: return (Array.IndexOf(MirroredRows, 1), true); // NW = NE 반전
                case 4: return (Array.IndexOf(MirroredRows, 0), true); // W = E 반전
                case 5: return (Array.IndexOf(MirroredRows, 7), true); // SW = SE 반전
                default: return (Array.IndexOf(MirroredRows, direction), false);
            }
        }

        /// <summary>동작·텍스처 행별 프레임 (처음 요청할 때 잘라 만듭니다).</summary>
        public Sprite[] FramesForRow(string clipName, int row)
        {
            if (!_cache.TryGetValue(clipName, out var byRow) || byRow[0] == null || byRow[0].Length == 0 || byRow[0][0] == null)
            {
                var clip = Find(clipName);
                if (clip == null || clip.texture == null) return Array.Empty<Sprite>();
                byRow = new Sprite[RowCount][];
                for (int r = 0; r < RowCount; r++)
                {
                    byRow[r] = new Sprite[clip.frames];
                    for (int f = 0; f < clip.frames; f++)
                    {
                        // 텍스처 위쪽이 첫 행
                        var rect = new Rect(f * cellSize, clip.texture.height - (r + 1) * cellSize, cellSize, cellSize);
                        byRow[r][f] = Sprite.Create(clip.texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    }
                }
                _cache[clipName] = byRow;
            }
            return byRow[Mathf.Clamp(row, 0, byRow.Length - 1)];
        }

        public static int DirectionOf(Vector2 facing)
        {
            if (facing.sqrMagnitude < 0.0001f) return 6;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            return ((Mathf.RoundToInt(angle / 45f) % Directions) + Directions) % Directions;
        }
    }
}
