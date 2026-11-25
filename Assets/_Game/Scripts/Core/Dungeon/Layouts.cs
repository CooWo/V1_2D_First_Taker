using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Dungeon
{
    /// <summary>생성된 층 지도: 걸을 수 있는 칸 + 방 목록 + 시작 칸.</summary>
    public sealed class FloorLayout
    {
        public string Name;
        public GridMap Map;
        public List<RectInt> Rooms = new List<RectInt>();
        public Vector2Int Start;

        public Vector2 StartPosition => Map.CenterOf(Start);
    }

    /// <summary>층 모양 생성기 (Strategy). 새 모양은 클래스를 추가해 선택기에 등록합니다.</summary>
    public interface IFloorLayoutGenerator
    {
        string Name { get; }
        FloorLayout Generate(System.Random random, int enemyCount);
    }

    /// <summary>층마다 어떤 모양을 쓸지 고릅니다.</summary>
    public interface IFloorLayoutSelector
    {
        FloorLayout Build(FloorPlan plan, int seed);
    }

    public sealed class FloorLayoutSelector : IFloorLayoutSelector
    {
        readonly IReadOnlyList<IFloorLayoutGenerator> _generators;

        public FloorLayoutSelector(IReadOnlyList<IFloorLayoutGenerator> generators) => _generators = generators;

        public static FloorLayoutSelector CreateDefault() => new FloorLayoutSelector(new IFloorLayoutGenerator[]
        {
            new RoomsAndCorridorsLayout(), new CaveLayout(), new CrossLayout(), new RingLayout(), new WindingHallLayout(), new PillarHallLayout()
        });

        public FloorLayout Build(FloorPlan plan, int seed)
        {
            var random = new System.Random(seed ^ (plan.ActIndex * 7919 + plan.DungeonIndex * 131 + plan.Floor * 17));
            // 보스 층은 넓은 홀, 그 외는 무작위 (연속으로 같은 모양이 덜 나오게 층 번호도 섞음)
            var generator = plan.HasBoss ? _generators.First(g => g is PillarHallLayout)
                : _generators[(random.Next(_generators.Count) + plan.Floor) % _generators.Count];
            return generator.Generate(random, plan.TotalKills);
        }
    }

    /// <summary>모든 칸이 열린 경기장 (레이드·테스트용).</summary>
    public sealed class OpenLayoutSelector : IFloorLayoutSelector
    {
        readonly int _width;
        readonly int _height;

        public OpenLayoutSelector(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public FloorLayout Build(FloorPlan plan, int seed)
        {
            var map = new GridMap(_width, _height, new Vector2(-(_width / 2), -(_height / 2)));
            map.Fill(new RectInt(1, 1, _width - 2, _height - 2), true);
            var room = new RectInt(1, 1, _width - 2, _height - 2);
            return new FloorLayout { Name = "광장", Map = map, Rooms = { room }, Start = new Vector2Int(_width / 2, _height / 2) };
        }
    }

    static class LayoutMath
    {
        /// <summary>몬스터 수에 맞춘 지도 한 변 길이.</summary>
        public static int SideFor(int enemyCount, int min = 34, int max = 72) => Mathf.Clamp(28 + enemyCount / 2, min, max);

        public static GridMap NewMap(int width, int height) => new GridMap(width, height, new Vector2(-(width / 2), -(height / 2)));

        public static void Corridor(GridMap map, Vector2Int a, Vector2Int b, int width, System.Random random)
        {
            bool horizontalFirst = random.Next(2) == 0;
            var corner = horizontalFirst ? new Vector2Int(b.x, a.y) : new Vector2Int(a.x, b.y);
            Line(map, a, corner, width);
            Line(map, corner, b, width);
        }

        public static void Line(GridMap map, Vector2Int a, Vector2Int b, int width)
        {
            int half = width / 2;
            int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
            int y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            map.Fill(new RectInt(x0 - half, y0 - half, x1 - x0 + width, y1 - y0 + width), true);
        }

        public static Vector2Int Center(RectInt r) => new Vector2Int(r.x + r.width / 2, r.y + r.height / 2);

        /// <summary>시작 칸과 이어진 영역만 남깁니다 (고립 지역 제거).</summary>
        public static void KeepConnected(GridMap map, Vector2Int start)
        {
            var seen = new bool[map.Width, map.Height];
            var queue = new Queue<Vector2Int>();
            if (!map.IsWalkable(start.x, start.y)) return;
            queue.Enqueue(start);
            seen[start.x, start.y] = true;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (!map.IsWalkable(n.x, n.y) || seen[n.x, n.y]) continue;
                    seen[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                if (!seen[x, y]) map.Set(x, y, false);
        }

        /// <summary>테두리 1칸은 항상 벽.</summary>
        public static void SealBorder(GridMap map)
        {
            for (int x = 0; x < map.Width; x++) { map.Set(x, 0, false); map.Set(x, map.Height - 1, false); }
            for (int y = 0; y < map.Height; y++) { map.Set(0, y, false); map.Set(map.Width - 1, y, false); }
        }
    }

    /// <summary>방과 복도: 크기가 다른 방들을 겹치지 않게 놓고 차례로 복도로 잇습니다.</summary>
    public sealed class RoomsAndCorridorsLayout : IFloorLayoutGenerator
    {
        public string Name => "방과 복도";

        public FloorLayout Generate(System.Random random, int enemyCount)
        {
            int side = LayoutMath.SideFor(enemyCount);
            var map = LayoutMath.NewMap(side, side);
            var rooms = new List<RectInt>();
            int wanted = Mathf.Clamp(4 + enemyCount / 10, 5, 14);

            for (int attempt = 0; attempt < 400 && rooms.Count < wanted; attempt++)
            {
                int w = random.Next(6, 12), h = random.Next(6, 11);
                var room = new RectInt(random.Next(2, side - w - 2), random.Next(2, side - h - 2), w, h);
                var padded = new RectInt(room.x - 2, room.y - 2, room.width + 4, room.height + 4);
                if (rooms.Any(r => r.Overlaps(padded))) continue;
                rooms.Add(room);
            }

            rooms = rooms.OrderBy(r => r.x + r.y).ToList();
            foreach (var room in rooms) map.Fill(room, true);
            for (int i = 1; i < rooms.Count; i++)
                LayoutMath.Corridor(map, LayoutMath.Center(rooms[i - 1]), LayoutMath.Center(rooms[i]), random.Next(2, 4), random);

            LayoutMath.SealBorder(map);
            var start = LayoutMath.Center(rooms[0]);
            LayoutMath.KeepConnected(map, start);
            return new FloorLayout { Name = Name, Map = map, Rooms = rooms, Start = start };
        }
    }

    /// <summary>동굴: 세포 자동자로 울퉁불퉁한 동굴을 만들고 가장 큰 영역만 남깁니다.</summary>
    public sealed class CaveLayout : IFloorLayoutGenerator
    {
        public string Name => "동굴";

        public FloorLayout Generate(System.Random random, int enemyCount)
        {
            int side = LayoutMath.SideFor(enemyCount, 36, 70);
            var map = LayoutMath.NewMap(side, side);
            var cells = new bool[side, side];
            for (int x = 0; x < side; x++)
            for (int y = 0; y < side; y++)
                cells[x, y] = random.NextDouble() > 0.44;

            for (int step = 0; step < 5; step++)
            {
                var next = new bool[side, side];
                for (int x = 1; x < side - 1; x++)
                for (int y = 1; y < side - 1; y++)
                {
                    int walls = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (!cells[x + dx, y + dy]) walls++;
                    next[x, y] = walls < 5;
                }
                cells = next;
            }
            for (int x = 0; x < side; x++)
            for (int y = 0; y < side; y++)
                map.Set(x, y, cells[x, y]);
            LayoutMath.SealBorder(map);

            var start = FindNearest(map, new Vector2Int(4, side / 2));
            LayoutMath.KeepConnected(map, start);

            var rooms = new List<RectInt>();
            for (int attempt = 0; attempt < 600 && rooms.Count < 6 + enemyCount / 8; attempt++)
            {
                var c = new Vector2Int(random.Next(3, side - 3), random.Next(3, side - 3));
                if (!map.IsWalkable(c.x, c.y) || (c - start).magnitude < 10f) continue;
                var r = new RectInt(c.x - 2, c.y - 2, 5, 5);
                if (rooms.Any(o => o.Overlaps(r))) continue;
                rooms.Add(r);
            }
            rooms.Insert(0, new RectInt(start.x - 2, start.y - 2, 5, 5));
            return new FloorLayout { Name = Name, Map = map, Rooms = rooms, Start = start };
        }

        static Vector2Int FindNearest(GridMap map, Vector2Int from)
        {
            for (int r = 0; r < map.Width; r++)
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                var c = from + new Vector2Int(dx, dy);
                if (map.IsWalkable(c.x, c.y)) return c;
            }
            map.Fill(new RectInt(from.x - 2, from.y - 2, 5, 5), true);
            return from;
        }
    }

    /// <summary>십자: 가운데 광장에서 네 갈래 통로 끝에 방이 달린 형태.</summary>
    public sealed class CrossLayout : IFloorLayoutGenerator
    {
        public string Name => "십자 회랑";

        public FloorLayout Generate(System.Random random, int enemyCount)
        {
            int side = LayoutMath.SideFor(enemyCount, 40, 70);
            var map = LayoutMath.NewMap(side, side);
            int mid = side / 2;
            int hub = 9;
            var center = new RectInt(mid - hub / 2, mid - hub / 2, hub, hub);
            map.Fill(center, true);

            var rooms = new List<RectInt>();
            var ends = new[] { new Vector2Int(mid, 5), new Vector2Int(mid, side - 6), new Vector2Int(5, mid), new Vector2Int(side - 6, mid) };
            foreach (var end in ends)
            {
                LayoutMath.Line(map, LayoutMath.Center(center), end, random.Next(3, 5));
                var room = new RectInt(end.x - 4, end.y - 4, 9, 9);
                map.Fill(room, true);
                rooms.Add(room);
                // 곁가지 작은 방
                var side1 = new Vector2Int((end.x + mid) / 2 + random.Next(-1, 2) * 6, (end.y + mid) / 2 + random.Next(-1, 2) * 6);
                LayoutMath.Line(map, (end + new Vector2Int(mid, mid)) / 2, side1, 2);
                var small = new RectInt(side1.x - 3, side1.y - 3, 6, 6);
                map.Fill(small, true);
                rooms.Add(small);
            }
            rooms.Add(center);
            LayoutMath.SealBorder(map);
            var start = LayoutMath.Center(rooms[0]);
            LayoutMath.KeepConnected(map, start);
            return new FloorLayout { Name = Name, Map = map, Rooms = rooms, Start = start };
        }
    }

    /// <summary>고리: 가운데가 막힌 사각 순환 통로와 모서리 방.</summary>
    public sealed class RingLayout : IFloorLayoutGenerator
    {
        public string Name => "순환 회랑";

        public FloorLayout Generate(System.Random random, int enemyCount)
        {
            int side = LayoutMath.SideFor(enemyCount, 36, 66);
            var map = LayoutMath.NewMap(side, side);
            int margin = 5;
            int width = random.Next(4, 7);
            var outer = new RectInt(margin, margin, side - margin * 2, side - margin * 2);
            map.Fill(outer, true);
            map.Fill(new RectInt(outer.x + width, outer.y + width, outer.width - width * 2, outer.height - width * 2), false);

            var rooms = new List<RectInt>();
            var corners = new[] { new Vector2Int(margin, margin), new Vector2Int(side - margin, margin), new Vector2Int(margin, side - margin), new Vector2Int(side - margin, side - margin) };
            foreach (var c in corners)
            {
                var room = new RectInt(c.x - 4, c.y - 4, 9, 9);
                map.Fill(room, true);
                rooms.Add(room);
            }
            // 안쪽 성소: 한 곳만 뚫린 방
            int mid = side / 2;
            var sanctum = new RectInt(mid - 4, mid - 4, 9, 9);
            map.Fill(sanctum, true);
            LayoutMath.Line(map, new Vector2Int(mid, mid), new Vector2Int(mid, outer.y + 1), 3);
            rooms.Add(sanctum);

            LayoutMath.SealBorder(map);
            var start = LayoutMath.Center(rooms[0]);
            LayoutMath.KeepConnected(map, start);
            return new FloorLayout { Name = Name, Map = map, Rooms = rooms, Start = start };
        }
    }

    /// <summary>구불구불한 긴 회랑과 중간중간 방.</summary>
    public sealed class WindingHallLayout : IFloorLayoutGenerator
    {
        public string Name => "구불 회랑";

        public FloorLayout Generate(System.Random random, int enemyCount)
        {
            int width = LayoutMath.SideFor(enemyCount, 34, 64);
            int height = width + 10;
            var map = LayoutMath.NewMap(width, height);
            var rooms = new List<RectInt>();
            int lanes = Mathf.Clamp(3 + enemyCount / 30, 3, 6);
            int laneGap = (height - 8) / lanes;

            var previous = new Vector2Int(5, 5);
            for (int lane = 0; lane < lanes; lane++)
            {
                int y = 5 + lane * laneGap;
                bool leftToRight = lane % 2 == 0;
                var a = new Vector2Int(leftToRight ? 5 : width - 6, y);
                var b = new Vector2Int(leftToRight ? width - 6 : 5, y);
                LayoutMath.Line(map, previous, a, 3);
                LayoutMath.Line(map, a, b, 3);
                previous = b;

                var room = new RectInt(random.Next(8, width - 16), y - 4, random.Next(7, 11), 8);
                map.Fill(room, true);
                rooms.Add(room);
            }
            rooms.Insert(0, new RectInt(2, 2, 7, 7));
            map.Fill(rooms[0], true);
            LayoutMath.SealBorder(map);
            var start = LayoutMath.Center(rooms[0]);
            LayoutMath.KeepConnected(map, start);
            return new FloorLayout { Name = Name, Map = map, Rooms = rooms, Start = start };
        }
    }

    /// <summary>기둥 홀: 넓은 홀에 기둥이 늘어선 형태 (보스 층).</summary>
    public sealed class PillarHallLayout : IFloorLayoutGenerator
    {
        public string Name => "기둥 홀";

        public FloorLayout Generate(System.Random random, int enemyCount)
        {
            int width = Mathf.Clamp(26 + enemyCount / 4, 28, 50);
            int height = width + 6;
            var map = LayoutMath.NewMap(width, height);
            var hall = new RectInt(2, 2, width - 4, height - 4);
            map.Fill(hall, true);

            int spacing = random.Next(5, 7);
            for (int x = 5; x < width - 5; x += spacing)
            for (int y = 8; y < height - 6; y += spacing)
                map.Fill(new RectInt(x, y, 2, 2), false);

            var rooms = new List<RectInt>
            {
                new RectInt(width / 2 - 3, 3, 6, 5),
                new RectInt(3, height / 2 - 3, 6, 6),
                new RectInt(width - 9, height / 2 - 3, 6, 6),
                new RectInt(width / 2 - 4, height - 10, 8, 7)
            };
            foreach (var r in rooms) map.Fill(r, true);
            LayoutMath.SealBorder(map);
            var start = LayoutMath.Center(rooms[0]);
            LayoutMath.KeepConnected(map, start);
            return new FloorLayout { Name = Name, Map = map, Rooms = rooms, Start = start };
        }
    }
}
