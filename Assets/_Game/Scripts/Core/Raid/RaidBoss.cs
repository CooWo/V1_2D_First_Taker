using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Raid
{
    /// <summary>레이드 보스 패턴 실행 문맥.</summary>
    public sealed class BossContext
    {
        public Combatant Boss;
        public ICombatWorld World;
        public IRandom Random;
        public IReadOnlyList<Combatant> Party;
        public Action<EnemyArchetype, Vector2> SummonAdd;
        public bool Enraged;

        public float Damage => Boss.Stats.Get(StatType.WeaponDamage) * (Enraged ? 1.5f : 1f);

        public Combatant RandomTarget()
        {
            var alive = Party.Where(p => p.IsAlive).ToList();
            return alive.Count == 0 ? null : alive[Random.Range(0, alive.Count)];
        }

        public void HitTarget(Hazard hazard, Combatant target, float multiplier)
        {
            World.Damage.Deal(new DamageRequest
            {
                Attacker = Boss, Target = target, Tags = SkillTag.Area, Type = hazard.Element,
                BaseAmount = Damage * multiplier, CanCrit = false
            });
        }
    }

    /// <summary>보스 패턴 (Strategy). 새 패턴은 클래스를 추가해 보스 정의에 넣습니다.</summary>
    public interface IBossPattern
    {
        string Name { get; }
        void Execute(BossContext context);
    }

    /// <summary>대상 발밑 원형 강타.</summary>
    public sealed class SlamPattern : IBossPattern
    {
        public string Name => "대지 강타";

        public void Execute(BossContext c)
        {
            foreach (var target in c.Party.Where(p => p.IsAlive).Take(2))
            {
                c.World.SpawnHazard(new Hazard
                {
                    Source = c.Boss, Team = c.Boss.Team, Shape = HazardShape.Circle, Center = target.Position,
                    Radius = 2.4f, Delay = 1.3f, Element = DamageType.Earth, Label = Name,
                    OnHit = (h, victim) => c.HitTarget(h, victim, 2.2f)
                });
            }
        }
    }

    /// <summary>보스에서 대상 방향 직선 휩쓸기.</summary>
    public sealed class LineSweepPattern : IBossPattern
    {
        public string Name => "어둠의 일섬";

        public void Execute(BossContext c)
        {
            var target = c.RandomTarget();
            if (target == null) return;
            c.World.SpawnHazard(new Hazard
            {
                Source = c.Boss, Team = c.Boss.Team, Shape = HazardShape.Line, Center = c.Boss.Position,
                Direction = (target.Position - c.Boss.Position).normalized, Length = 16f, Width = 2.6f, Delay = 1.4f,
                Element = DamageType.Shadow, Label = Name, OnHit = (h, victim) => c.HitTarget(h, victim, 2.6f)
            });
        }
    }

    /// <summary>보스 주변만 안전한 고리 폭발 (보스 품으로 파고들어야 삽니다).</summary>
    public sealed class DonutPattern : IBossPattern
    {
        public string Name => "화염 고리";

        public void Execute(BossContext c)
        {
            c.World.SpawnHazard(new Hazard
            {
                Source = c.Boss, Team = c.Boss.Team, Shape = HazardShape.Donut, Center = c.Boss.Position,
                InnerRadius = 3.2f, Radius = 13f, Delay = 2f, Element = DamageType.Fire, Label = Name,
                OnHit = (h, victim) => c.HitTarget(h, victim, 2f)
            });
        }
    }

    /// <summary>무작위 낙하 운석 다발.</summary>
    public sealed class MeteorRainPattern : IBossPattern
    {
        public string Name => "운석 비";

        public void Execute(BossContext c)
        {
            for (int i = 0; i < 6; i++)
            {
                var anchor = c.RandomTarget();
                if (anchor == null) return;
                var center = c.World.ClampToBounds(anchor.Position + Skills.SkillPrimitives.RandomOffset(c.Random, 3f));
                c.World.SpawnHazard(new Hazard
                {
                    Source = c.Boss, Team = c.Boss.Team, Shape = HazardShape.Circle, Center = center, Radius = 1.6f,
                    Delay = 1.2f + 0.15f * i, Element = DamageType.Fire, Label = Name, OnHit = (h, victim) => c.HitTarget(h, victim, 1.4f)
                });
            }
        }
    }

    /// <summary>졸개 소환.</summary>
    public sealed class SummonPattern : IBossPattern
    {
        readonly EnemyArchetype _minion;
        public SummonPattern(EnemyArchetype minion) => _minion = minion;
        public string Name => "망령 소환";

        public void Execute(BossContext c)
        {
            for (int i = 0; i < 4; i++)
                c.SummonAdd(_minion, c.World.ClampToBounds(c.Boss.Position + Skills.SkillPrimitives.RandomOffset(c.Random, 3.5f)));
        }
    }

    public sealed class RaidBossDefinition
    {
        public string Id;
        public string Name;
        public string VisualKey;
        public DamageType Element;
        public float LifePerPlayerLevel = 8000f;
        public float DamagePerPlayerLevel = 4f;
        public float PatternInterval = 3.4f;
        public List<IBossPattern> Patterns = new List<IBossPattern>();
    }

    public static class RaidBossCatalog
    {
        public static IReadOnlyList<RaidBossDefinition> CreateDefault(EnemyArchetype minion) => new[]
        {
            new RaidBossDefinition
            {
                Id = "abyss_lord", Name = "심연의 군주", VisualKey = "big_demon", Element = DamageType.Fire,
                Patterns = { new SlamPattern(), new LineSweepPattern(), new DonutPattern(), new SummonPattern(minion), new MeteorRainPattern() }
            },
            new RaidBossDefinition
            {
                Id = "frost_giant", Name = "서리 거인", VisualKey = "ogre", Element = DamageType.Cold, LifePerPlayerLevel = 9000f,
                Patterns = { new SlamPattern(), new DonutPattern(), new LineSweepPattern(), new MeteorRainPattern() }
            },
            new RaidBossDefinition
            {
                Id = "king_of_dead", Name = "망자의 왕", VisualKey = "big_zombie", Element = DamageType.Shadow, LifePerPlayerLevel = 8500f,
                Patterns = { new LineSweepPattern(), new SummonPattern(minion), new SlamPattern(), new DonutPattern() }
            }
        };

        /// <summary>보스 레벨마다 순환합니다.</summary>
        public static RaidBossDefinition ForLevel(IReadOnlyList<RaidBossDefinition> bosses, int level)
            => bosses[(Mathf.Max(1, level) - 1) % bosses.Count];
    }

    /// <summary>
    /// 레이드 보스 두뇌: 기본은 근접 추적·공격, 일정 간격으로 예고 패턴을 순환. 체력 30% 이하 격노.
    /// </summary>
    public sealed class RaidBossBrain
    {
        readonly EnemyAgent _agent;
        readonly RaidBossDefinition _definition;
        readonly BossContext _context;
        readonly IEnemyBrain _melee = new MeleeEnemyBrain();
        int _patternIndex;
        float _patternTimer = 2.5f;

        public RaidBossBrain(EnemyAgent agent, RaidBossDefinition definition, BossContext context)
        {
            _agent = agent;
            _definition = definition;
            _context = context;
        }

        public string LastPattern { get; private set; } = "-";
        public bool Enraged => _context.Enraged;

        public event Action<string> PatternStarted;

        public void Tick(float dt)
        {
            var boss = _agent.Combatant;
            if (!boss.IsAlive) return;

            if (!_context.Enraged && boss.Health.LifeFraction <= 0.3f)
            {
                _context.Enraged = true;
                PatternStarted?.Invoke("격노!");
            }

            _melee.Tick(_agent, _context.World, dt);

            _patternTimer -= dt;
            if (_patternTimer > 0f || _definition.Patterns.Count == 0) return;
            _patternTimer = _definition.PatternInterval * (_context.Enraged ? 0.65f : 1f);

            var pattern = _definition.Patterns[_patternIndex++ % _definition.Patterns.Count];
            LastPattern = pattern.Name;
            pattern.Execute(_context);
            PatternStarted?.Invoke(pattern.Name);
        }
    }
}
