using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.Core.World
{
    /// <summary>이동 가능 지형 + 길찾기 (벽이 있는 던전). 열린 경기장은 null로 둡니다.</summary>
    public interface IWalkability
    {
        Rect Bounds { get; }
        bool IsWalkable(Vector2 position);
        bool HasLineOfSight(Vector2 from, Vector2 to);
        /// <summary>from에서 to로 가기 위한 이번 걸음 방향 (벽을 돌아감).</summary>
        Vector2 Steer(Vector2 from, Vector2 to);
    }

    public static class NavigationExtensions
    {
        /// <summary>벽이 있으면 돌아가는 방향, 없으면 직선 방향.</summary>
        public static Vector2 SteerTowards(this ICombatWorld world, Vector2 from, Vector2 to)
        {
            if (world.Navigation != null) return world.Navigation.Steer(from, to);
            var d = to - from;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.zero;
        }

        public static bool CanSee(this ICombatWorld world, Vector2 from, Vector2 to)
            => world.Navigation == null || world.Navigation.HasLineOfSight(from, to);
    }

    /// <summary>칸 단위 지도 (true = 걸을 수 있음). 1칸 = 1유닛.</summary>
    public sealed class GridMap
    {
        readonly bool[,] _walkable;

        public GridMap(int width, int height, Vector2 origin)
        {
            Width = width;
            Height = height;
            Origin = origin;
            _walkable = new bool[width, height];
        }

        public int Width { get; }
        public int Height { get; }
        public Vector2 Origin { get; }
        public Rect Bounds => new Rect(Origin, new Vector2(Width, Height));

        public bool InRange(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsWalkable(int x, int y) => InRange(x, y) && _walkable[x, y];
        public void Set(int x, int y, bool walkable) { if (InRange(x, y)) _walkable[x, y] = walkable; }

        public void Fill(RectInt rect, bool walkable)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++)
                Set(x, y, walkable);
        }

        public Vector2Int CellOf(Vector2 position)
            => new Vector2Int(Mathf.FloorToInt(position.x - Origin.x), Mathf.FloorToInt(position.y - Origin.y));

        public Vector2 CenterOf(Vector2Int cell) => Origin + new Vector2(cell.x + 0.5f, cell.y + 0.5f);

        public bool IsWalkable(Vector2 position)
        {
            var c = CellOf(position);
            return IsWalkable(c.x, c.y);
        }

        public int CountWalkable()
        {
            int n = 0;
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_walkable[x, y]) n++;
            return n;
        }
    }

    /// <summary>
    /// 격자 길찾기: 시야가 트였으면 직진, 막혔으면 목표 칸에서 퍼진 BFS 거리장을 따라 한 칸씩.
    /// 같은 목표(대개 플레이어)에 대한 거리장은 잠깐 캐시해 수십 마리가 함께 씁니다.
    /// </summary>
    public sealed class GridNavigator : IWalkability
    {
        const float FieldLifetime = 0.35f;

        static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        readonly GridMap _map;
        readonly Func<float> _clock;
        readonly Dictionary<int, (float time, int[] field)> _fields = new Dictionary<int, (float, int[])>();
        readonly Queue<int> _queue = new Queue<int>();

        public GridNavigator(GridMap map, Func<float> clock)
        {
            _map = map;
            _clock = clock;
        }

        public GridMap Map => _map;
        public Rect Bounds => _map.Bounds;

        public bool IsWalkable(Vector2 position) => _map.IsWalkable(position);

        public bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            float distance = delta.magnitude;
            int steps = Mathf.CeilToInt(distance / 0.3f);
            for (int i = 1; i < steps; i++)
                if (!_map.IsWalkable(from + delta * (i / (float)steps))) return false;
            return true;
        }

        public Vector2 Steer(Vector2 from, Vector2 to)
        {
            var direct = to - from;
            if (direct.sqrMagnitude < 0.0001f) return Vector2.zero;
            if (HasLineOfSight(from, to)) return direct.normalized;

            var target = _map.CellOf(to);
            var field = FieldTo(target);
            if (field == null) return direct.normalized;

            var cell = _map.CellOf(from);
            if (!_map.InRange(cell.x, cell.y)) return direct.normalized;
            int best = Distance(field, cell);
            var bestCell = cell;
            foreach (var n in Neighbours)
            {
                var next = cell + n;
                if (!_map.IsWalkable(next.x, next.y)) continue;
                if (n.x != 0 && n.y != 0 && (!_map.IsWalkable(cell.x + n.x, cell.y) || !_map.IsWalkable(cell.x, cell.y + n.y))) continue;
                int d = Distance(field, next);
                if (d >= best) continue;
                best = d;
                bestCell = next;
            }
            var step = _map.CenterOf(bestCell) - from;
            return step.sqrMagnitude > 0.0001f ? step.normalized : direct.normalized;
        }

        int Distance(int[] field, Vector2Int c) => _map.InRange(c.x, c.y) ? field[c.y * _map.Width + c.x] : int.MaxValue;

        int[] FieldTo(Vector2Int target)
        {
            if (!_map.IsWalkable(target.x, target.y)) return null;
            int key = target.y * _map.Width + target.x;
            float now = _clock();
            if (_fields.TryGetValue(key, out var cached) && now - cached.time < FieldLifetime) return cached.field;

            if (_fields.Count > 16) _fields.Clear();
            var field = new int[_map.Width * _map.Height];
            for (int i = 0; i < field.Length; i++) field[i] = int.MaxValue;
            field[key] = 0;
            _queue.Clear();
            _queue.Enqueue(key);
            while (_queue.Count > 0)
            {
                int index = _queue.Dequeue();
                int x = index % _map.Width;
                int y = index / _map.Width;
                int d = field[index] + 1;
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + Neighbours[i].x;
                    int ny = y + Neighbours[i].y;
                    if (!_map.IsWalkable(nx, ny)) continue;
                    int ni = ny * _map.Width + nx;
                    if (field[ni] <= d) continue;
                    field[ni] = d;
                    _queue.Enqueue(ni);
                }
            }
            _fields[key] = (now, field);
            return field;
        }
    }
}
