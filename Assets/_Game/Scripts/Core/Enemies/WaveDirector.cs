using System;
using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Enemies
{
    /// <summary>
    /// 무리 단위 몬스터 공급. 5웨이브마다 정예, 10웨이브마다 보스가 등장합니다.
    /// </summary>
    public sealed class WaveDirector
    {
        const float SpawnDelay = 1.2f;

        readonly CombatWorld _world;
        readonly IEnemyFactory _factory;
        readonly IRandom _random;
        readonly IReadOnlyList<EnemyArchetype> _normal;
        readonly IReadOnlyList<EnemyArchetype> _elites;
        readonly EnemyArchetype _boss;
        readonly List<EnemyAgent> _agents = new List<EnemyAgent>();
        float _cooldown = 0.5f;

        public WaveDirector(CombatWorld world, IEnemyFactory factory, IRandom random,
            IReadOnlyList<EnemyArchetype> normal, IReadOnlyList<EnemyArchetype> elites, EnemyArchetype boss)
        {
            _world = world;
            _factory = factory;
            _random = random;
            _normal = normal;
            _elites = elites;
            _boss = boss;
            world.CombatantRemoved += c => _agents.RemoveAll(a => a.Combatant == c);
        }

        public int Wave { get; private set; }
        public int AliveCount => _agents.Count;
        public int RespawnThreshold { get; set; } = 3;
        public IReadOnlyList<EnemyAgent> Agents => _agents;

        public event Action<int> WaveStarted;
        public event Action<EnemyAgent> BossSpawned;

        public int LevelForWave(int wave) => 1 + (wave - 1) / 2;

        public void Tick(float dt, Combatant player)
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                if (agent.Combatant.IsAlive) agent.Brain.Tick(agent, _world, dt);
            }

            if (_agents.Count > RespawnThreshold || !player.IsAlive) return;
            _cooldown -= dt;
            if (_cooldown > 0f) return;
            _cooldown = SpawnDelay;
            SpawnWave(player.Position);
        }

        public void SpawnWave(Vector2 around)
        {
            Wave++;
            int level = LevelForWave(Wave);
            int count = Mathf.Min(6 + Wave * 2, 30);

            for (int i = 0; i < count; i++)
                Spawn(_normal[_random.Range(0, _normal.Count)], level, EnemyRank.Normal, around);

            if (Wave % 5 == 0)
                for (int i = 0; i < 2; i++)
                    Spawn(_elites[_random.Range(0, _elites.Count)], level, EnemyRank.Elite, around);

            if (Wave % 10 == 0)
            {
                var boss = Spawn(_boss, level, EnemyRank.Boss, around);
                BossSpawned?.Invoke(boss);
            }

            WaveStarted?.Invoke(Wave);
        }

        public void Clear()
        {
            foreach (var agent in _agents.ToArray()) _world.Remove(agent.Combatant);
            _agents.Clear();
        }

        EnemyAgent Spawn(EnemyArchetype archetype, int level, EnemyRank rank, Vector2 around)
        {
            float angle = _random.Range(0f, 360f);
            float distance = _random.Range(7f, 11f);
            var position = around + CombatWorld.Rotate(Vector2.right, angle) * distance;
            var agent = _factory.Create(archetype, level, rank, _world.ClampToBounds(position));
            _agents.Add(agent);
            _world.Add(agent.Combatant);
            return agent;
        }
    }
}
