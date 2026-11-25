using System.Collections.Generic;
using UnityEngine;

namespace LootHunters
{
    public enum Tile : byte { Wall, Floor, Stairs }

    /// <summary>
    /// 한 층의 지도. 타일 (x, y)의 중심이 월드 좌표 (x, y)이고 크기는 1칸 = 1유닛입니다.
    /// 방을 랜덤으로 놓고 L자 복도로 이어 붙이는 간단한 로그라이크 방식입니다.
    /// </summary>
    public class DungeonMap
    {
        public readonly int Width;
        public readonly int Height;
        public readonly List<RectInt> Rooms = new List<RectInt>();
        public Vector2Int Start { get; private set; }
        public Vector2Int Stairs { get; private set; }
        public readonly bool[,] Explored;

        readonly Tile[,] tiles;

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public DungeonMap(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new Tile[width, height];      // 기본값 Wall
            Explored = new bool[width, height];
        }

        // ───────── 조회 ─────────

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public Tile Get(int x, int y) => InBounds(x, y) ? tiles[x, y] : Tile.Wall;
        public bool IsWalkable(int x, int y) => Get(x, y) != Tile.Wall;
        public bool IsWalkable(Vector2Int p) => IsWalkable(p.x, p.y);

        public static Vector2Int TileOf(Vector2 world) => new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));

        // ───────── 생성 ─────────

        public static DungeonMap Generate(DungeonData d, System.Random rng)
        {
            var map = new DungeonMap(Mathf.Max(20, d.mapWidth), Mathf.Max(16, d.mapHeight));
            int minS = Mathf.Max(3, d.roomMinSize), maxS = Mathf.Max(minS, d.roomMaxSize);

            for (int attempt = 0; attempt < 300 && map.Rooms.Count < d.roomCount; attempt++)
            {
                int w = rng.Next(minS, maxS + 1), h = rng.Next(minS, maxS + 1);
                int x = rng.Next(1, map.Width - w - 1), y = rng.Next(1, map.Height - h - 1);
                var room = new RectInt(x, y, w, h);
                var padded = new RectInt(x - 1, y - 1, w + 2, h + 2);

                bool overlaps = false;
                foreach (var r in map.Rooms)
                    if (padded.Overlaps(r)) { overlaps = true; break; }
                if (overlaps) continue;

                map.Carve(room);
                if (map.Rooms.Count > 0)
                    map.Corridor(Center(map.Rooms[map.Rooms.Count - 1]), Center(room), rng.Next(2) == 0);
                map.Rooms.Add(room);
            }

            if (map.Rooms.Count == 0)   // 극단적인 설정값 대비
            {
                var fallback = new RectInt(map.Width / 2 - 3, map.Height / 2 - 3, 6, 6);
                map.Carve(fallback);
                map.Rooms.Add(fallback);
            }

            map.Start = Center(map.Rooms[0]);

            // 계단은 시작 방에서 가장 먼 방 가운데
            var far = map.Rooms[0];
            int best = -1;
            foreach (var r in map.Rooms)
            {
                var c = Center(r);
                int dist = Mathf.Abs(c.x - map.Start.x) + Mathf.Abs(c.y - map.Start.y);
                if (dist > best) { best = dist; far = r; }
            }
            // 계단 칸은 더 이상 그리지 않음 (몹을 다 잡으면 결과창 → 다음 층). 위치만 남겨 둠.
            map.Stairs = Center(far);
            return map;
        }

        public static Vector2Int Center(RectInt r) => new Vector2Int(r.x + r.width / 2, r.y + r.height / 2);

        void Carve(RectInt r)
        {
            for (int x = r.xMin; x < r.xMax; x++)
                for (int y = r.yMin; y < r.yMax; y++)
                    tiles[x, y] = Tile.Floor;
        }

        void Corridor(Vector2Int a, Vector2Int b, bool horizontalFirst)
        {
            var corner = horizontalFirst ? new Vector2Int(b.x, a.y) : new Vector2Int(a.x, b.y);
            Line(a, corner);
            Line(corner, b);
        }

        void Line(Vector2Int a, Vector2Int b)
        {
            int x = a.x, y = a.y;
            while (true)
            {
                // 통로 폭 2칸: 몬스터 무리가 한 줄로 막히지 않게
                for (int dx = 0; dx <= 1; dx++)
                    for (int dy = 0; dy <= 1; dy++)
                    {
                        int cx = x + dx, cy = y + dy;
                        if (cx > 0 && cy > 0 && cx < Width - 1 && cy < Height - 1 && tiles[cx, cy] == Tile.Wall)
                            tiles[cx, cy] = Tile.Floor;
                    }
                if (x == b.x && y == b.y) break;
                x += System.Math.Sign(b.x - x);
                if (x == b.x) y += System.Math.Sign(b.y - y);
            }
        }

        // ───────── 시야 / 길찾기 / 충돌 ─────────

        public void Reveal(Vector2 center, float radius)
        {
            int r = Mathf.CeilToInt(radius);
            var c = TileOf(center);
            for (int x = c.x - r; x <= c.x + r; x++)
                for (int y = c.y - r; y <= c.y + r; y++)
                    if (InBounds(x, y) && (new Vector2(x, y) - center).sqrMagnitude <= radius * radius)
                        Explored[x, y] = true;
        }

        /// <summary>두 지점 사이에 벽이 없는지 (0.25칸 간격으로 검사).</summary>
        public bool LineClear(Vector2 a, Vector2 b)
        {
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.CeilToInt(dist / 0.25f);
            for (int i = 1; i < steps; i++)
                if (!IsWalkable(TileOf(Vector2.Lerp(a, b, (float)i / steps)))) return false;
            return true;
        }

        /// <summary>from 타일에서 각 타일까지 걸음 수 (못 가면 -1). 몬스터 추적과 자동 이동에 씁니다.</summary>
        public int[,] DistanceField(Vector2Int from)
        {
            var dist = new int[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    dist[x, y] = -1;
            if (!IsWalkable(from)) return dist;

            var q = new Queue<Vector2Int>();
            dist[from.x, from.y] = 0;
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = p + d;
                    if (!IsWalkable(n) || dist[n.x, n.y] >= 0) continue;
                    dist[n.x, n.y] = dist[p.x, p.y] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>거리장에서 값이 더 작은 옆 칸 (목표 쪽으로 한 칸). 없으면 자기 자신.</summary>
        public Vector2Int StepDown(int[,] field, Vector2Int at)
        {
            var best = at;
            int bestVal = InBounds(at.x, at.y) ? field[at.x, at.y] : -1;
            foreach (var d in Dirs)
            {
                var n = at + d;
                if (!InBounds(n.x, n.y)) continue;
                int v = field[n.x, n.y];
                if (v >= 0 && (bestVal < 0 || v < bestVal)) { best = n; bestVal = v; }
            }
            return best;
        }

        /// <summary>
        /// 원(반지름 radius)이 벽에 막히지 않게 이동. x와 y를 따로 처리해서 벽을 따라 미끄러집니다.
        /// </summary>
        public Vector2 Move(Vector2 pos, Vector2 delta, float radius)
        {
            var nx = new Vector2(pos.x + delta.x, pos.y);
            if (!Blocked(nx, radius)) pos = nx;
            var ny = new Vector2(pos.x, pos.y + delta.y);
            if (!Blocked(ny, radius)) pos = ny;
            return pos;
        }

        /// <summary>반지름 r인 몸이 p에 서 있을 수 있는지.</summary>
        public bool Fits(Vector2 p, float r) => !Blocked(p, r);

        /// <summary>
        /// 반지름 r인 몸이 a에서 b까지 직선으로 갈 수 있는지 (몸 크기까지 고려).
        /// LineClear는 중심선만 보기 때문에 모서리에 몸이 걸리는 경우를 못 잡습니다.
        /// </summary>
        public bool PathClear(Vector2 a, Vector2 b, float r)
        {
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.15f));
            for (int i = 1; i <= steps; i++)
                if (Blocked(Vector2.Lerp(a, b, (float)i / steps), r)) return false;
            return true;
        }

        bool Blocked(Vector2 p, float r)
        {
            return !IsWalkable(TileOf(new Vector2(p.x - r, p.y - r))) ||
                   !IsWalkable(TileOf(new Vector2(p.x + r, p.y - r))) ||
                   !IsWalkable(TileOf(new Vector2(p.x - r, p.y + r))) ||
                   !IsWalkable(TileOf(new Vector2(p.x + r, p.y + r)));
        }
    }
}
