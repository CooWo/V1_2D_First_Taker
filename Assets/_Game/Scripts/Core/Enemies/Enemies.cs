using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Enemies
{
    /// <summary>몬스터 원형 데이터.</summary>
    public sealed class EnemyArchetype
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string VisualKey { get; set; }
        public float Life { get; set; } = 40f;
        public float Damage { get; set; } = 6f;
        public float MoveSpeed { get; set; } = 2f;
        public float AttackRange { get; set; } = 0.9f;
        public float AttackInterval { get; set; } = 1.2f;
        public bool IsRanged { get; set; }
        public float Radius { get; set; } = 0.35f;
        public float Armor { get; set; }
        public DamageType DamageType { get; set; } = DamageType.Physical;
    }

    /// <summary>몬스터 행동 결정 (Strategy).</summary>
    public interface IEnemyBrain
    {
        void Tick(EnemyAgent agent, ICombatWorld world, float dt);
    }

    /// <summary>몬스터 = 전투원 상태 + 원형 + 두뇌.</summary>
    public sealed class EnemyAgent
    {
        public EnemyAgent(Combatant combatant, EnemyArchetype archetype, IEnemyBrain brain)
        {
            Combatant = combatant;
            Archetype = archetype;
            Brain = brain;
        }

        public Combatant Combatant { get; }
        public EnemyArchetype Archetype { get; }
        public IEnemyBrain Brain { get; }
        public float AttackTimer { get; set; }
        public int Level { get; set; }
    }

    /// <summary>근접 몬스터: 추적 후 근접 공격.</summary>
    public sealed class MeleeEnemyBrain : IEnemyBrain
    {
        public void Tick(EnemyAgent agent, ICombatWorld world, float dt)
        {
            var self = agent.Combatant;
            if (!self.CanAct) return;

            var target = world.FindNearestEnemy(self.Team, self.Position, 40f);
            if (target == null) return;

            agent.AttackTimer -= dt;
            float reach = agent.Archetype.AttackRange + target.Radius + self.Radius;
            var delta = target.Position - self.Position;

            if (delta.magnitude > reach)
            {
                self.Move(delta + EnemyMotion.Separation(self, world), dt);
                return;
            }

            self.Facing = delta.normalized;
            if (agent.AttackTimer > 0f) return;
            agent.AttackTimer = agent.Archetype.AttackInterval;
            self.LockActions(0.35f);
            world.Emit(VfxEvent.At(VfxKind.Slash, target.Position, 0.4f, agent.Archetype.DamageType, 0.12f));
            world.Damage.Deal(new DamageRequest
            {
                Attacker = self, Target = target, Tags = SkillTag.Attack | SkillTag.Melee,
                Type = agent.Archetype.DamageType, BaseAmount = self.Stats.Get(StatType.WeaponDamage), CanCrit = false
            });
        }
    }

    /// <summary>원거리 몬스터: 거리를 유지하며 투사체 발사.</summary>
    public sealed class RangedEnemyBrain : IEnemyBrain
    {
        const float PreferredDistance = 5f;

        public void Tick(EnemyAgent agent, ICombatWorld world, float dt)
        {
            var self = agent.Combatant;
            if (!self.CanAct) return;

            var target = world.FindNearestEnemy(self.Team, self.Position, 40f);
            if (target == null) return;

            agent.AttackTimer -= dt;
            var delta = target.Position - self.Position;
            float distance = delta.magnitude;

            if (distance > agent.Archetype.AttackRange) self.Move(delta + EnemyMotion.Separation(self, world), dt);
            else if (distance < PreferredDistance * 0.6f) self.Move(-delta + EnemyMotion.Separation(self, world), dt);

            if (distance > agent.Archetype.AttackRange || agent.AttackTimer > 0f) return;
            agent.AttackTimer = agent.Archetype.AttackInterval;
            self.LockActions(0.4f);

            var attacker = self;
            var archetype = agent.Archetype;
            world.SpawnProjectile(new Projectile
            {
                Owner = self, Team = self.Team, Position = self.Position, Direction = delta.normalized,
                Speed = 6.5f, MaxDistance = archetype.AttackRange + 2f, Radius = 0.2f, Element = archetype.DamageType,
                VisualKey = "enemy_bolt",
                OnHit = (p, victim) => world.Damage.Deal(new DamageRequest
                {
                    Attacker = attacker, Target = victim, Tags = SkillTag.Attack | SkillTag.Ranged | SkillTag.Projectile,
                    Type = archetype.DamageType, BaseAmount = attacker.Stats.Get(StatType.WeaponDamage), CanCrit = false
                })
            });
        }
    }

    public static class EnemyMotion
    {
        /// <summary>몬스터끼리 겹치지 않도록 밀어내는 방향.</summary>
        public static Vector2 Separation(Combatant self, ITargetQuery world)
        {
            var push = Vector2.zero;
            var all = world.Combatants;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == self || other.Team != self.Team || !other.IsAlive) continue;
                var away = self.Position - other.Position;
                float min = self.Radius + other.Radius;
                float sqr = away.sqrMagnitude;
                if (sqr >= min * min || sqr < 0.0001f) continue;
                push += away.normalized * (min - Mathf.Sqrt(sqr)) * 4f;
            }
            return push;
        }
    }

    public interface IEnemyFactory
    {
        EnemyAgent Create(EnemyArchetype archetype, int level, EnemyRank rank, Vector2 position);
    }

    public sealed class EnemyFactory : IEnemyFactory
    {
        readonly IEnemyBrain _melee = new MeleeEnemyBrain();
        readonly IEnemyBrain _ranged = new RangedEnemyBrain();

        public EnemyAgent Create(EnemyArchetype archetype, int level, EnemyRank rank, Vector2 position)
        {
            float lifeScale = (1f + 0.4f * (level - 1)) * (rank == EnemyRank.Boss ? 18f : rank == EnemyRank.Elite ? 4f : 1f);
            float damageScale = (1f + 0.24f * (level - 1)) * (rank == EnemyRank.Boss ? 2.2f : rank == EnemyRank.Elite ? 1.5f : 1f);

            var combatant = new Combatant(RankPrefix(rank) + archetype.Name, Team.Enemy)
            {
                Rank = rank,
                VisualKey = archetype.VisualKey,
                Radius = archetype.Radius * (rank == EnemyRank.Boss ? 1.8f : rank == EnemyRank.Elite ? 1.3f : 1f),
                BaseMoveSpeed = archetype.MoveSpeed * (rank == EnemyRank.Boss ? 0.85f : 1f),
                Position = position
            };
            combatant.Stats.SetBase(StatType.MaxLife, archetype.Life * lifeScale);
            combatant.Stats.SetBase(StatType.WeaponDamage, archetype.Damage * damageScale);
            combatant.Stats.SetBase(StatType.Armor, archetype.Armor * (1f + 0.2f * level));
            combatant.Stats.SetBase(StatType.AllResistPct, Mathf.Min(40f, level * 1.5f));
            combatant.Health.ResetToFull();

            var agent = new EnemyAgent(combatant, archetype, archetype.IsRanged ? _ranged : _melee) { Level = level };
            agent.AttackTimer = archetype.AttackInterval * 0.5f;
            return agent;
        }

        static string RankPrefix(EnemyRank rank) => rank == EnemyRank.Boss ? "[보스] " : rank == EnemyRank.Elite ? "[정예] " : string.Empty;
    }

    public static class EnemyRoster
    {
        public static List<EnemyArchetype> CreateDefault() => new List<EnemyArchetype>
        {
            new EnemyArchetype { Id = "goblin", Name = "고블린", VisualKey = "goblin", Life = 30f, Damage = 5f, MoveSpeed = 2.6f },
            new EnemyArchetype { Id = "imp", Name = "임프", VisualKey = "imp", Life = 24f, Damage = 4f, MoveSpeed = 3.1f, DamageType = DamageType.Fire },
            new EnemyArchetype { Id = "skelet", Name = "해골", VisualKey = "skelet", Life = 36f, Damage = 6f, MoveSpeed = 2.2f, Armor = 20f },
            new EnemyArchetype { Id = "tiny_zombie", Name = "꼬마 좀비", VisualKey = "tiny_zombie", Life = 28f, Damage = 5f, MoveSpeed = 2.4f },
            new EnemyArchetype { Id = "orc_warrior", Name = "오크 전사", VisualKey = "orc_warrior", Life = 55f, Damage = 8f, MoveSpeed = 2.1f, Armor = 40f, Radius = 0.4f },
            new EnemyArchetype { Id = "masked_orc", Name = "가면 오크", VisualKey = "masked_orc", Life = 50f, Damage = 9f, MoveSpeed = 2.3f, Radius = 0.4f },
            new EnemyArchetype { Id = "chort", Name = "초르트", VisualKey = "chort", Life = 40f, Damage = 7f, MoveSpeed = 2.8f, DamageType = DamageType.Shadow },
            new EnemyArchetype { Id = "orc_shaman", Name = "오크 주술사", VisualKey = "orc_shaman", Life = 32f, Damage = 7f, MoveSpeed = 1.9f, AttackRange = 6f, AttackInterval = 1.8f, IsRanged = true, DamageType = DamageType.Fire },
            new EnemyArchetype { Id = "necromancer", Name = "강령술사", VisualKey = "necromancer", Life = 30f, Damage = 8f, MoveSpeed = 1.8f, AttackRange = 6.5f, AttackInterval = 2f, IsRanged = true, DamageType = DamageType.Shadow }
        };

        public static List<EnemyArchetype> CreateElites() => new List<EnemyArchetype>
        {
            new EnemyArchetype { Id = "big_zombie", Name = "거대 좀비", VisualKey = "big_zombie", Life = 60f, Damage = 10f, MoveSpeed = 1.8f, Radius = 0.55f, Armor = 60f },
            new EnemyArchetype { Id = "ogre", Name = "오우거", VisualKey = "ogre", Life = 70f, Damage = 12f, MoveSpeed = 1.7f, Radius = 0.55f, Armor = 80f }
        };

        public static EnemyArchetype CreateBoss() => new EnemyArchetype
        {
            Id = "big_demon", Name = "심연의 대악마", VisualKey = "big_demon", Life = 80f, Damage = 14f, MoveSpeed = 1.7f,
            Radius = 0.6f, Armor = 100f, AttackRange = 1.2f, AttackInterval = 1.4f, DamageType = DamageType.Fire
        };
    }
}
