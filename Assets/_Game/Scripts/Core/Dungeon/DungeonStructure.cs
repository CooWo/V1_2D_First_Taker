using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Enemies;
using UnityEngine;

namespace ShadowRPG.Core.Dungeon
{
    /// <summary>
    /// 상위 구역 (ACT). 층당 몬스터 수는 ACT 안에서 점점 늘고, ACT가 오를수록 범위 자체가 커집니다 (10 → 100).
    /// </summary>
    public sealed class ActDefinition
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public int MinEnemiesPerFloor { get; set; }
        public int MaxEnemiesPerFloor { get; set; }
        public string[] DungeonNames { get; set; } = Array.Empty<string>();
        public string[] EnemyIds { get; set; } = Array.Empty<string>();
        public Color FloorTint { get; set; } = Color.white;
    }

    /// <summary>한 층의 구성 (몬스터 수, 레벨, 보스 여부).</summary>
    public readonly struct FloorPlan
    {
        public readonly int ActIndex;
        public readonly int DungeonIndex;
        public readonly int Floor;
        public readonly int EnemyCount;
        public readonly int Level;
        public readonly int EliteCount;
        public readonly bool HasBoss;

        public FloorPlan(int actIndex, int dungeonIndex, int floor, int enemyCount, int level, int eliteCount, bool hasBoss)
        {
            ActIndex = actIndex;
            DungeonIndex = dungeonIndex;
            Floor = floor;
            EnemyCount = enemyCount;
            Level = level;
            EliteCount = eliteCount;
            HasBoss = hasBoss;
        }

        /// <summary>층 클리어에 필요한 총 처치 수 (일반 + 정예 + 보스).</summary>
        public int TotalKills => EnemyCount + EliteCount + (HasBoss ? 1 : 0);
    }

    /// <summary>현재 위치: ACT → 던전 → 층. 클리어할 때마다 다음 층으로 진행합니다.</summary>
    [Serializable]
    public sealed class DungeonProgress
    {
        public int ActIndex;
        public int DungeonIndex;
        public int Floor = 1;
        public int HighestGlobalFloor;

        public int GlobalFloor(IActCatalog catalog)
            => ActIndex * catalog.DungeonsPerAct * catalog.FloorsPerDungeon + DungeonIndex * catalog.FloorsPerDungeon + (Floor - 1);

        public void Advance(IActCatalog catalog)
        {
            HighestGlobalFloor = Mathf.Max(HighestGlobalFloor, GlobalFloor(catalog) + 1);
            Floor++;
            if (Floor <= catalog.FloorsPerDungeon) return;
            Floor = 1;
            DungeonIndex++;
            if (DungeonIndex < catalog.DungeonsPerAct) return;
            DungeonIndex = 0;
            ActIndex = Mathf.Min(ActIndex + 1, catalog.Acts.Count - 1);
        }
    }

    public interface IActCatalog
    {
        IReadOnlyList<ActDefinition> Acts { get; }
        int DungeonsPerAct { get; }
        int FloorsPerDungeon { get; }
        FloorPlan Plan(DungeonProgress progress);
    }

    public sealed class ActCatalog : IActCatalog
    {
        public ActCatalog(IEnumerable<ActDefinition> acts, int dungeonsPerAct, int floorsPerDungeon)
        {
            Acts = acts.ToList();
            DungeonsPerAct = dungeonsPerAct;
            FloorsPerDungeon = floorsPerDungeon;
        }

        public IReadOnlyList<ActDefinition> Acts { get; }
        public int DungeonsPerAct { get; }
        public int FloorsPerDungeon { get; }

        /// <summary>
        /// ACT 안 진행도(0~1)에 따라 최소~최대 몬스터 수 사이를 보간합니다.
        /// 던전의 마지막 층은 같은 수의 몬스터 + 보스.
        /// </summary>
        public FloorPlan Plan(DungeonProgress progress)
        {
            var act = Acts[Mathf.Clamp(progress.ActIndex, 0, Acts.Count - 1)];
            int floorsInAct = DungeonsPerAct * FloorsPerDungeon;
            int indexInAct = progress.DungeonIndex * FloorsPerDungeon + (progress.Floor - 1);
            float t = floorsInAct <= 1 ? 1f : indexInAct / (float)(floorsInAct - 1);

            int count = Mathf.RoundToInt(Mathf.Lerp(act.MinEnemiesPerFloor, act.MaxEnemiesPerFloor, t));
            count = Mathf.Clamp(count, 10, 100);

            bool bossFloor = progress.Floor == FloorsPerDungeon;
            int elites = bossFloor ? 0 : progress.Floor >= 3 ? 1 + progress.ActIndex / 2 : 0;

            int level = 1 + Mathf.RoundToInt(progress.GlobalFloor(this) * 0.6f);
            return new FloorPlan(progress.ActIndex, progress.DungeonIndex, progress.Floor, count, level, elites, bossFloor);
        }

        public static ActCatalog CreateDefault() => new ActCatalog(new[]
        {
            new ActDefinition
            {
                Index = 0, Name = "그림자 변경", MinEnemiesPerFloor = 10, MaxEnemiesPerFloor = 20,
                DungeonNames = new[] { "버려진 초소", "속삭이는 하수도", "무너진 망루" },
                EnemyIds = new[] { "goblin", "tiny_zombie", "imp", "skelet" }, FloorTint = new Color(0.55f, 0.58f, 0.7f)
            },
            new ActDefinition
            {
                Index = 1, Name = "잿빛 지하묘지", MinEnemiesPerFloor = 20, MaxEnemiesPerFloor = 40,
                DungeonNames = new[] { "납골당 입구", "망자의 회랑", "검은 성소" },
                EnemyIds = new[] { "skelet", "tiny_zombie", "necromancer", "chort" }, FloorTint = new Color(0.5f, 0.5f, 0.55f)
            },
            new ActDefinition
            {
                Index = 2, Name = "얼어붙은 심연", MinEnemiesPerFloor = 35, MaxEnemiesPerFloor = 60,
                DungeonNames = new[] { "서리 동굴", "빙결 협곡", "푸른 무덤" },
                EnemyIds = new[] { "orc_warrior", "masked_orc", "orc_shaman", "chort" }, FloorTint = new Color(0.5f, 0.62f, 0.8f)
            },
            new ActDefinition
            {
                Index = 3, Name = "불타는 성채", MinEnemiesPerFloor = 55, MaxEnemiesPerFloor = 80,
                DungeonNames = new[] { "재의 성문", "용암 대전", "화염 감옥" },
                EnemyIds = new[] { "imp", "orc_warrior", "masked_orc", "orc_shaman" }, FloorTint = new Color(0.75f, 0.52f, 0.45f)
            },
            new ActDefinition
            {
                Index = 4, Name = "공허의 왕좌", MinEnemiesPerFloor = 75, MaxEnemiesPerFloor = 100,
                DungeonNames = new[] { "공허의 문", "별 없는 회랑", "왕좌의 방" },
                EnemyIds = new[] { "chort", "necromancer", "masked_orc", "skelet", "orc_shaman" }, FloorTint = new Color(0.55f, 0.45f, 0.75f)
            }
        }, dungeonsPerAct: 3, floorsPerDungeon: 5);
    }

    /// <summary>
    /// 한 층의 몬스터 공급. 총 수는 고정이고, 동시에 살아 있는 수만 제한해 무리 단위로 내보냅니다.
    /// </summary>
    public sealed class FloorSpawner
    {
        const int MaxAlive = 16;
        const int PackSize = 6;
        const float PackInterval = 1.5f;

        readonly World.CombatWorld _world;
        readonly IEnemyFactory _factory;
        readonly IRandom _random;
        readonly List<EnemyAgent> _agents = new List<EnemyAgent>();
        IReadOnlyList<EnemyArchetype> _pool;
        IReadOnlyList<EnemyArchetype> _elites;
        EnemyArchetype _boss;
        FloorPlan _plan;
        int _spawnedNormal;
        int _spawnedElite;
        bool _spawnedBoss;
        float _cooldown;

        public FloorSpawner(World.CombatWorld world, IEnemyFactory factory, IRandom random)
        {
            _world = world;
            _factory = factory;
            _random = random;
            world.CombatantRemoved += c => _agents.RemoveAll(a => a.Combatant == c);
        }

        public FloorPlan Plan => _plan;
        public int Killed { get; private set; }
        public int Remaining => Mathf.Max(0, _plan.TotalKills - Killed);
        public bool IsCleared => Killed >= _plan.TotalKills && _plan.TotalKills > 0;
        public IReadOnlyList<EnemyAgent> Agents => _agents;
        public EnemyAgent Boss { get; private set; }

        public event Action<EnemyAgent> BossSpawned;

        public void Begin(FloorPlan plan, IReadOnlyList<EnemyArchetype> pool, IReadOnlyList<EnemyArchetype> elites, EnemyArchetype boss)
        {
            Clear();
            _plan = plan;
            _pool = pool;
            _elites = elites;
            _boss = boss;
            _spawnedNormal = 0;
            _spawnedElite = 0;
            _spawnedBoss = false;
            Killed = 0;
            Boss = null;
            _cooldown = 0.4f;
        }

        public void RegisterKill() => Killed++;

        public void Tick(float dt, Actors.Combatant player)
        {
            for (int i = 0; i < _agents.Count; i++)
                if (_agents[i].Combatant.IsAlive) _agents[i].Brain.Tick(_agents[i], _world, dt);

            if (!player.IsAlive) return;
            _cooldown -= dt;
            if (_cooldown > 0f || _agents.Count >= MaxAlive) return;
            _cooldown = PackInterval;
            SpawnPack(player.Position);
        }

        void SpawnPack(Vector2 around)
        {
            var anchor = PackAnchor(around);
            int room = MaxAlive - _agents.Count;
            for (int i = 0; i < PackSize && room > 0 && _spawnedNormal < _plan.EnemyCount; i++, room--)
            {
                Spawn(_pool[_random.Range(0, _pool.Count)], Actors.EnemyRank.Normal, anchor);
                _spawnedNormal++;
            }

            if (_spawnedElite < _plan.EliteCount && _spawnedNormal >= _plan.EnemyCount / 2 && room > 0)
            {
                Spawn(_elites[_random.Range(0, _elites.Count)], Actors.EnemyRank.Elite, anchor);
                _spawnedElite++;
            }

            if (_plan.HasBoss && !_spawnedBoss && _spawnedNormal >= _plan.EnemyCount / 2)
            {
                _spawnedBoss = true;
                Boss = Spawn(_boss, Actors.EnemyRank.Boss, anchor);
                BossSpawned?.Invoke(Boss);
            }
        }

        Vector2 PackAnchor(Vector2 around)
        {
            float angle = _random.Range(0f, 360f);
            return _world.ClampToBounds(around + World.CombatWorld.Rotate(Vector2.right, angle) * _random.Range(7f, 11f));
        }

        EnemyAgent Spawn(EnemyArchetype archetype, Actors.EnemyRank rank, Vector2 anchor)
        {
            var position = _world.ClampToBounds(anchor + Skills.SkillPrimitives.RandomOffset(_random, 1.8f));
            var agent = _factory.Create(archetype, _plan.Level, rank, position);
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
