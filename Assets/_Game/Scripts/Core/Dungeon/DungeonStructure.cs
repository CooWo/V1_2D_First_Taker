using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Core.Tuning;
using UnityEngine;

namespace ShadowRPG.Core.Dungeon
{
    /// <summary>상위 구역 (ACT). ACT마다 컨셉이 있는 던전 4~6개 (난이도 순).</summary>
    public sealed class ActDefinition
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string[] DungeonNames { get; set; } = Array.Empty<string>();
        public string[] EnemyIds { get; set; } = Array.Empty<string>();
        public int DungeonCount => DungeonNames.Length;
    }

    /// <summary>한 층의 구성. 몹은 "무리(팩)" 단위로 배치됩니다.</summary>
    public readonly struct FloorPlan
    {
        public readonly int ActIndex;
        public readonly int DungeonIndex;
        public readonly int Floor;
        public readonly int Packs;
        public readonly int MobsPerPack;
        public readonly int Level;
        public readonly int EliteCount;
        public readonly bool HasBoss;
        /// <summary>몬스터 공격·생명·방어 배율 (층 소폭 × 던전 × ACT).</summary>
        public readonly float Power;

        public FloorPlan(int actIndex, int dungeonIndex, int floor, int packs, int mobsPerPack, int level, int eliteCount, bool hasBoss, float power)
        {
            ActIndex = actIndex;
            DungeonIndex = dungeonIndex;
            Floor = floor;
            Packs = packs;
            MobsPerPack = mobsPerPack;
            Level = level;
            EliteCount = eliteCount;
            HasBoss = hasBoss;
            Power = power;
        }

        public int EnemyCount => Packs * MobsPerPack;

        /// <summary>층 클리어에 필요한 총 처치 수 (일반 + 정예 + 보스).</summary>
        public int TotalKills => EnemyCount + EliteCount + (HasBoss ? 1 : 0);
    }

    /// <summary>현재 위치: ACT → 던전 → 층.</summary>
    [Serializable]
    public sealed class DungeonProgress
    {
        public int ActIndex;
        public int DungeonIndex;
        public int Floor = 1;
        public int HighestGlobalFloor;

        public int GlobalDungeon(IActCatalog catalog)
        {
            int index = 0;
            for (int a = 0; a < ActIndex && a < catalog.Acts.Count; a++) index += catalog.Acts[a].DungeonCount;
            return index + DungeonIndex;
        }

        public int GlobalFloor(IActCatalog catalog) => GlobalDungeon(catalog) * catalog.FloorsPerDungeon + (Floor - 1);

        public bool IsLastFloor(IActCatalog catalog) => Floor >= catalog.FloorsPerDungeon;

        /// <summary>다음 층으로. 던전 마지막 층이면 그대로 (보스 후 자동 귀환).</summary>
        public void Advance(IActCatalog catalog)
        {
            HighestGlobalFloor = Mathf.Max(HighestGlobalFloor, GlobalFloor(catalog) + 1);
            if (Floor < catalog.FloorsPerDungeon) Floor++;
        }
    }

    public interface IActCatalog
    {
        IReadOnlyList<ActDefinition> Acts { get; }
        int FloorsPerDungeon { get; }
        FloorPlan Plan(DungeonProgress progress);
    }

    /// <summary>ACT 구분 없이 던전을 0부터 번호로 다루는 도우미 (마을의 던전 선택용).</summary>
    public static class ActCatalogExtensions
    {
        public static int TotalDungeons(this IActCatalog catalog) => catalog.Acts.Sum(a => a.DungeonCount);

        public static int GlobalDungeon(this IActCatalog catalog, DungeonProgress progress) => progress.GlobalDungeon(catalog);

        public static (int act, int dungeon) Locate(this IActCatalog catalog, int globalDungeon)
        {
            globalDungeon = Mathf.Clamp(globalDungeon, 0, catalog.TotalDungeons() - 1);
            for (int a = 0; a < catalog.Acts.Count; a++)
            {
                int count = catalog.Acts[a].DungeonCount;
                if (globalDungeon < count) return (a, globalDungeon);
                globalDungeon -= count;
            }
            return (catalog.Acts.Count - 1, catalog.Acts[catalog.Acts.Count - 1].DungeonCount - 1);
        }

        public static DungeonProgress StartOf(this IActCatalog catalog, int globalDungeon, int highestFloor)
        {
            var (act, dungeon) = catalog.Locate(globalDungeon);
            return new DungeonProgress { ActIndex = act, DungeonIndex = dungeon, Floor = 1, HighestGlobalFloor = highestFloor };
        }

        public static string DungeonName(this IActCatalog catalog, int act, int dungeon)
        {
            var a = catalog.Acts[Mathf.Clamp(act, 0, catalog.Acts.Count - 1)];
            return a.DungeonNames.Length > dungeon ? a.DungeonNames[dungeon] : $"던전 {dungeon + 1}";
        }

        public static string DungeonLabel(this IActCatalog catalog, int globalDungeon)
        {
            var (act, dungeon) = catalog.Locate(globalDungeon);
            return $"ACT {act + 1} {catalog.Acts[act].Name} · {catalog.DungeonName(act, dungeon)}";
        }

        /// <summary>전역 던전 번호로 몇 번째 ACT까지 열렸는지 (1부터).</summary>
        public static int ActsOpened(this IActCatalog catalog, int unlockedDungeons)
        {
            var (act, _) = catalog.Locate(Mathf.Max(0, unlockedDungeons - 1));
            return act + 1;
        }
    }

    /// <summary>
    /// 수치는 <see cref="DungeonTuning"/>·<see cref="MonsterTuning"/>에서 읽습니다.
    /// 난이도 = 던전 안의 깊이 (층마다 소폭), 던전 순번·ACT 경계에서 크게 뜀.
    /// </summary>
    public sealed class ActCatalog : IActCatalog
    {
        readonly DungeonTuning _dungeon;
        readonly MonsterTuning _monster;

        public ActCatalog(DungeonTuning dungeon, MonsterTuning monster)
        {
            _dungeon = dungeon;
            _monster = monster;
            Acts = dungeon.Acts.Select((a, i) => new ActDefinition
            {
                Index = i,
                Name = a.Name,
                DungeonNames = a.Dungeons ?? Array.Empty<string>(),
                EnemyIds = a.EnemyIds ?? Array.Empty<string>()
            }).ToList();
        }

        public IReadOnlyList<ActDefinition> Acts { get; }
        public int FloorsPerDungeon => Mathf.Max(1, _dungeon.FloorsPerDungeon);

        public FloorPlan Plan(DungeonProgress progress)
        {
            int act = Mathf.Clamp(progress.ActIndex, 0, Acts.Count - 1);
            int dungeon = Mathf.Max(0, progress.DungeonIndex);
            int floor = Mathf.Clamp(progress.Floor, 1, FloorsPerDungeon);

            int packs = _dungeon.StartPacks
                        + (floor - 1) / Mathf.Max(1, _dungeon.FloorsPerExtraPack)
                        + dungeon * _dungeon.ExtraPacksPerDungeon;
            packs = Mathf.Clamp(packs, 1, Mathf.Max(1, _dungeon.MaxPacks));

            bool bossFloor = floor == FloorsPerDungeon;
            int elites = bossFloor || floor < _dungeon.EliteFromFloor ? 0 : _dungeon.ElitesPerFloor + act / 2 * _dungeon.ExtraElitesPerTwoActs;

            float power = (1f + _monster.FloorStatPct / 100f * (floor - 1))
                          * Mathf.Pow(_monster.DungeonMultiplier, dungeon)
                          * Mathf.Pow(_monster.ActMultiplier, act);

            int level = 1 + Mathf.RoundToInt(progress.GlobalFloor(this) * _dungeon.LevelPerFloor);
            return new FloorPlan(act, dungeon, floor, packs, _dungeon.MobsPerPack, level, elites, bossFloor, power);
        }

        public static ActCatalog CreateDefault() => new ActCatalog(GameTuning.Current.Dungeon, GameTuning.Current.Monster);
    }

    /// <summary>
    /// 한 층의 몬스터 배치. 무리(팩)마다 방 하나에 모아 두고 (시작 방 제외),
    /// 몬스터는 플레이어를 보거나 맞을 때까지 방에서 대기합니다. 정예는 먼 방, 보스는 가장 먼 방.
    /// </summary>
    public sealed class FloorSpawner
    {
        readonly World.CombatWorld _world;
        readonly IEnemyFactory _factory;
        readonly IRandom _random;
        readonly DungeonTuning _tuning;
        readonly List<EnemyAgent> _agents = new List<EnemyAgent>();
        FloorPlan _plan;

        public FloorSpawner(World.CombatWorld world, IEnemyFactory factory, IRandom random, DungeonTuning tuning = null)
        {
            _world = world;
            _factory = factory;
            _random = random;
            _tuning = tuning ?? GameTuning.Current.Dungeon;
            world.CombatantRemoved += c => _agents.RemoveAll(a => a.Combatant == c);
        }

        public FloorPlan Plan => _plan;
        public int Killed { get; private set; }
        public int Remaining => Mathf.Max(0, _plan.TotalKills - Killed);
        public bool IsCleared => Killed >= _plan.TotalKills && _plan.TotalKills > 0;
        public IReadOnlyList<EnemyAgent> Agents => _agents;
        public EnemyAgent Boss { get; private set; }

        public event Action<EnemyAgent> BossSpawned;

        public void Begin(FloorPlan plan, FloorLayout layout, IReadOnlyList<EnemyArchetype> pool, IReadOnlyList<EnemyArchetype> elites, EnemyArchetype boss)
        {
            Clear();
            _plan = plan;
            Killed = 0;
            Boss = null;

            var start = layout.StartPosition;
            var rooms = layout.Rooms
                .Select(r => layout.Map.CenterOf(new Vector2Int(r.x + r.width / 2, r.y + r.height / 2)))
                .Where(c => Vector2.Distance(c, start) >= _tuning.SafeRadiusFromStart && layout.Map.IsWalkable(c))
                .OrderBy(c => Vector2.Distance(c, start))
                .ToList();
            if (rooms.Count == 0) rooms.Add(FarthestWalkable(layout, start));

            var scaling = new MonsterScaling(plan.Level, plan.Power);

            // 무리: 한 무리 = 같은 방에 MobsPerPack마리 (무리마다 종류 1~2가지)
            for (int p = 0; p < plan.Packs; p++)
            {
                var anchor = rooms[p % rooms.Count];
                var main = pool[_random.Range(0, pool.Count)];
                var second = pool[_random.Range(0, pool.Count)];
                for (int i = 0; i < plan.MobsPerPack; i++)
                    Spawn(i % 3 == 2 ? second : main, Actors.EnemyRank.Normal, anchor, layout, scaling);
            }

            for (int i = 0; i < plan.EliteCount; i++)
                Spawn(elites[_random.Range(0, elites.Count)], Actors.EnemyRank.Elite, rooms[Mathf.Max(0, rooms.Count - 1 - i % Mathf.Max(1, rooms.Count / 2))], layout, scaling);

            if (plan.HasBoss)
            {
                Boss = Spawn(boss, Actors.EnemyRank.Boss, rooms[rooms.Count - 1], layout, scaling);
                BossSpawned?.Invoke(Boss);
            }
        }

        public void RegisterKill() => Killed++;

        public void Tick(float dt, Actors.Combatant player)
        {
            for (int i = 0; i < _agents.Count; i++)
                if (_agents[i].Combatant.IsAlive) _agents[i].Brain.Tick(_agents[i], _world, dt);
        }

        Vector2 FarthestWalkable(FloorLayout layout, Vector2 from)
        {
            var best = from;
            float bestDistance = 0f;
            for (int x = 0; x < layout.Map.Width; x += 2)
            for (int y = 0; y < layout.Map.Height; y += 2)
            {
                if (!layout.Map.IsWalkable(x, y)) continue;
                var c = layout.Map.CenterOf(new Vector2Int(x, y));
                float d = Vector2.Distance(c, from);
                if (d <= bestDistance) continue;
                bestDistance = d;
                best = c;
            }
            return best;
        }

        EnemyAgent Spawn(EnemyArchetype archetype, Actors.EnemyRank rank, Vector2 anchor, FloorLayout layout, MonsterScaling scaling)
        {
            var position = anchor;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var candidate = anchor + Skills.SkillPrimitives.RandomOffset(_random, _tuning.PackSpread);
                if (!layout.Map.IsWalkable(candidate)) continue;
                position = candidate;
                break;
            }
            var agent = _factory.Create(archetype, scaling, rank, position);
            _agents.Add(agent);
            _world.Add(agent.Combatant);
            return agent;
        }

        public void Clear()
        {
            foreach (var agent in _agents.ToArray()) _world.Remove(agent.Combatant);
            _agents.Clear();
        }
    }
}
