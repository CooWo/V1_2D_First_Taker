using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Tuning;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Dungeon
{
    /// <summary>
    /// 방 + 좁은 통로 지도 (방향성 v1 §3): 광장형 금지, 방과 방 사이 통로에서 1:1 구도가 나오게.
    /// 함정·기믹·숨은 방은 없음. 크기·개수·통로 폭은 DungeonTuning.
    /// </summary>
    public abstract class ChamberLayoutBase : IFloorLayoutGenerator
    {
        protected readonly DungeonTuning Tuning;

        protected ChamberLayoutBase(DungeonTuning tuning) => Tuning = tuning ?? GameTuning.Current.Dungeon;

        public abstract string Name { get; }

        public FloorLayout Generate(System.Random random, int enemyCount) => Generate(random, enemyCount, false);

        public FloorLayout Generate(System.Random random, int enemyCount, bool bossArena)
        {
            int roomCount = random.Next(Tuning.RoomCountMin, Tuning.RoomCountMax + 1);
            var rooms = PlaceRooms(random, roomCount, bossArena);
            int minX = rooms.Min(r => r.xMin) - 3, minY = rooms.Min(r => r.yMin) - 3;
            int maxX = rooms.Max(r => r.xMax) + 3, maxY = rooms.Max(r => r.yMax) + 3;
            var map = new GridMap(maxX - minX, maxY - minY, new Vector2(-(maxX - minX) / 2, -(maxY - minY) / 2));
            var shift = new Vector2Int(-minX, -minY);
            rooms = rooms.Select(r => new RectInt(r.position + shift, r.size)).ToList();

            foreach (var room in rooms) map.Fill(room, true);
            Connect(map, rooms, random);
            LayoutMath.SealBorder(map);
            var start = LayoutMath.Center(rooms[0]);
            LayoutMath.KeepConnected(map, start);
            return new FloorLayout { Name = Name, Map = map, Rooms = rooms, Start = start };
        }

        protected abstract List<RectInt> PlaceRooms(System.Random random, int count, bool bossArena);

        protected virtual void Connect(GridMap map, List<RectInt> rooms, System.Random random)
        {
            for (int i = 1; i < rooms.Count; i++) Passage(map, rooms[i - 1], rooms[i], random);
        }

        protected RectInt RandomRoom(System.Random random, Vector2Int at, bool big = false)
        {
            int min = Tuning.RoomSizeMin, max = Tuning.RoomSizeMax;
            int w = big ? max + 4 : random.Next(min, max + 1);
            int h = big ? max + 2 : random.Next(min, max + 1);
            return new RectInt(at.x - w / 2, at.y - h / 2, w, h);
        }

        /// <summary>두 방 중심을 ㄱ자 통로로 잇습니다 (폭 = CorridorWidth).</summary>
        protected void Passage(GridMap map, RectInt a, RectInt b, System.Random random)
            => LayoutMath.Corridor(map, LayoutMath.Center(a), LayoutMath.Center(b), Mathf.Max(1, Tuning.CorridorWidth), random);

        protected static bool Overlaps(List<RectInt> rooms, RectInt candidate, int gap)
        {
            var padded = new RectInt(candidate.x - gap, candidate.y - gap, candidate.width + gap * 2, candidate.height + gap * 2);
            return rooms.Any(r => r.Overlaps(padded));
        }
    }

    /// <summary>사슬형: 방이 한 줄로 이어지며 굽이칩니다 (가장 먼 방 = 보스 방).</summary>
    public sealed class ChamberChainLayout : ChamberLayoutBase
    {
        public ChamberChainLayout(DungeonTuning tuning = null) : base(tuning) { }

        public override string Name => "이어진 방";

        protected override List<RectInt> PlaceRooms(System.Random random, int count, bool bossArena)
        {
            var rooms = new List<RectInt>();
            var cursor = Vector2Int.zero;
            var dirs = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            rooms.Add(RandomRoom(random, cursor));
            for (int i = 1; i < count; i++)
            {
                bool last = i == count - 1;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    var dir = dirs[random.Next(dirs.Length)];
                    int step = Tuning.RoomSizeMax + Tuning.CorridorLengthMin + random.Next(0, 4);
                    var candidate = RandomRoom(random, cursor + dir * step, bossArena && last);
                    if (Overlaps(rooms, candidate, Tuning.CorridorLengthMin)) continue;
                    rooms.Add(candidate);
                    cursor = LayoutMath.Center(candidate);
                    break;
                }
            }
            return rooms;
        }
    }

    /// <summary>가지형: 중앙 방에서 가지가 뻗고, 가지 끝끼리 한 번 더 이어 순환로를 만듭니다.</summary>
    public sealed class ChamberBranchLayout : ChamberLayoutBase
    {
        public ChamberBranchLayout(DungeonTuning tuning = null) : base(tuning) { }

        public override string Name => "갈래 방";

        protected override List<RectInt> PlaceRooms(System.Random random, int count, bool bossArena)
        {
            var rooms = new List<RectInt> { RandomRoom(random, Vector2Int.zero) };
            var dirs = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.left, Vector2Int.down };
            for (int i = 1; i < count; i++)
            {
                bool last = i == count - 1;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    var from = rooms[random.Next(rooms.Count)];
                    var dir = dirs[random.Next(dirs.Length)];
                    int step = Tuning.RoomSizeMax + Tuning.CorridorLengthMin + random.Next(0, 3);
                    var candidate = RandomRoom(random, LayoutMath.Center(from) + dir * step, bossArena && last);
                    if (Overlaps(rooms, candidate, Tuning.CorridorLengthMin)) continue;
                    rooms.Add(candidate);
                    break;
                }
            }
            // 시작 방에서 먼 순서로 정렬 → 마지막 방이 가장 먼 방
            var start = LayoutMath.Center(rooms[0]);
            rooms = new[] { rooms[0] }.Concat(rooms.Skip(1).OrderBy(r => (LayoutMath.Center(r) - start).sqrMagnitude)).ToList();
            return rooms;
        }

        protected override void Connect(GridMap map, List<RectInt> rooms, System.Random random)
        {
            // 각 방을 이미 연결된 방 중 가장 가까운 방과 이음 (트리)
            for (int i = 1; i < rooms.Count; i++)
            {
                var c = LayoutMath.Center(rooms[i]);
                var nearest = rooms.Take(i).OrderBy(r => (LayoutMath.Center(r) - c).sqrMagnitude).First();
                Passage(map, nearest, rooms[i], random);
            }
            if (rooms.Count >= 4) Passage(map, rooms[rooms.Count - 2], rooms[rooms.Count - 3], random);
        }
    }

    /// <summary>방향성 v1 지도 선택: 일반 층은 사슬/가지 무작위, 보스 층은 마지막 방을 넓은 보스 방으로.</summary>
    public sealed class ChamberLayoutSelector : IFloorLayoutSelector
    {
        readonly ChamberLayoutBase[] _generators;

        public ChamberLayoutSelector(DungeonTuning tuning = null)
        {
            _generators = new ChamberLayoutBase[] { new ChamberChainLayout(tuning), new ChamberBranchLayout(tuning) };
        }

        public FloorLayout Build(FloorPlan plan, int seed)
        {
            var random = new System.Random(seed ^ (plan.ActIndex * 7919 + plan.DungeonIndex * 131 + plan.Floor * 17));
            var generator = _generators[(random.Next(_generators.Length) + plan.Floor) % _generators.Length];
            return generator.Generate(random, plan.TotalKills, plan.HasBoss);
        }
    }
}
