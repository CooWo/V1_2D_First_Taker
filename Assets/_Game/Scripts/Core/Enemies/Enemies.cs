using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tuning;
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
        public string BossVisualKey { get; set; }
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

        /// <summary>플레이어를 알아챘는지. 던전 몬스터는 방에서 대기하다 보이거나 맞으면 추적합니다.</summary>
        public bool Aggro { get; set; }

        public static float AggroRange => GameTuning.Current.Monster.AggroRange;
        public static float AlertRadius => GameTuning.Current.Monster.AlertRadius;

        /// <summary>대기 중이면 시야 안의 적을 확인해 깨어나고, 주변 동료도 깨웁니다.</summary>
        public Combatant AcquireTarget(ICombatWorld world)
        {
            var self = Combatant;
            var target = world.FindNearestEnemy(self.Team, self.Position, Aggro ? 60f : AggroRange);
            if (target == null) return null;
            if (Aggro) return target;
            if (!world.CanSee(self.Position, target.Position)) return null;
            Aggro = true;
            return target;
        }
    }

    /// <summary>근접 몬스터: 추적 후 근접 공격.</summary>
    public sealed class MeleeEnemyBrain : IEnemyBrain
    {
        public void Tick(EnemyAgent agent, ICombatWorld world, float dt)
        {
            var self = agent.Combatant;
            if (!self.CanAct) return;

            var target = agent.AcquireTarget(world);
            if (target == null) return;

            agent.AttackTimer -= dt;
            float reach = agent.Archetype.AttackRange + target.Radius + self.Radius;
            var delta = target.Position - self.Position;

            if (delta.magnitude > reach || !world.CanSee(self.Position, target.Position))
            {
                self.Move(world.SteerTowards(self.Position, target.Position) + EnemyMotion.Separation(self, world), dt);
                return;
            }

            self.Facing = delta.normalized;
            if (agent.AttackTimer > 0f) return;
            agent.AttackTimer = agent.Archetype.AttackInterval;
            self.LockActions(0.35f);
            self.NotifyAction(CombatAction.Attack);
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

            var target = agent.AcquireTarget(world);
            if (target == null) return;

            agent.AttackTimer -= dt;
            var delta = target.Position - self.Position;
            float distance = delta.magnitude;
            bool visible = world.CanSee(self.Position, target.Position);

            if (distance > agent.Archetype.AttackRange || !visible) self.Move(world.SteerTowards(self.Position, target.Position) + EnemyMotion.Separation(self, world), dt);
            else if (distance < PreferredDistance * 0.6f) self.Move(-delta + EnemyMotion.Separation(self, world), dt);

            if (distance > agent.Archetype.AttackRange || !visible || agent.AttackTimer > 0f) return;
            agent.AttackTimer = agent.Archetype.AttackInterval;
            self.LockActions(0.4f);
            self.NotifyAction(agent.Archetype.DamageType == DamageType.Physical ? CombatAction.RangedAttack : CombatAction.Cast);

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

        /// <summary>층 난이도: 레벨(경험치·드랍용)과 능력치 배율.</summary>
    public readonly struct MonsterScaling
    {
        public readonly int Level;
        public readonly float Power;

        public MonsterScaling(int level, float power)
        {
            Level = level;
            Power = power;
        }
    }

    public interface IEnemyFactory
    {
        EnemyAgent Create(EnemyArchetype archetype, MonsterScaling scaling, EnemyRank rank, Vector2 position);
    }

    /// <summary>원형 × 층 배율 × 등급(정예·보스) 배율. 수치는 MonsterTuning.</summary>
    public sealed class EnemyFactory : IEnemyFactory
    {
        readonly IEnemyBrain _melee = new MeleeEnemyBrain();
        readonly IEnemyBrain _ranged = new RangedEnemyBrain();
        readonly MonsterTuning _tuning;

        public EnemyFactory(MonsterTuning tuning = null) => _tuning = tuning ?? GameTuning.Current.Monster;

        public EnemyAgent Create(EnemyArchetype archetype, MonsterScaling scaling, EnemyRank rank, Vector2 position)
        {
            var t = _tuning;
            float lifeScale = t.GlobalLife * scaling.Power * (rank == EnemyRank.Boss ? t.BossLife : rank == EnemyRank.Elite ? t.EliteLife : 1f);
            float damageScale = t.GlobalDamage * scaling.Power * (rank == EnemyRank.Boss ? t.BossDamage : rank == EnemyRank.Elite ? t.EliteDamage : 1f);

            var combatant = new Combatant(RankPrefix(rank) + archetype.Name, Team.Enemy)
            {
                Rank = rank,
                VisualKey = rank == EnemyRank.Boss && !string.IsNullOrEmpty(archetype.BossVisualKey) ? archetype.BossVisualKey : archetype.VisualKey,
                Radius = archetype.Radius * (rank == EnemyRank.Boss ? t.BossSize : rank == EnemyRank.Elite ? t.EliteSize : 1f),
                BaseMoveSpeed = archetype.MoveSpeed * (rank == EnemyRank.Boss ? t.BossSpeed : 1f),
                Position = position
            };
            combatant.Stats.SetBase(StatType.MaxLife, archetype.Life * lifeScale);
            combatant.Stats.SetBase(StatType.WeaponDamage, archetype.Damage * damageScale);
            combatant.Stats.SetBase(StatType.Armor, archetype.Armor * scaling.Power * (1f + t.ArmorPerLevel * scaling.Level * 0.1f));
            combatant.Stats.SetBase(StatType.AllResistPct, Mathf.Min(t.ResistCap, scaling.Level * t.ResistPerLevel));
            combatant.Health.ResetToFull();

            var agent = new EnemyAgent(combatant, archetype, archetype.IsRanged ? _ranged : _melee) { Level = scaling.Level };
            combatant.Health.Damaged += _ => agent.Aggro = true;
            agent.AttackTimer = archetype.AttackInterval * 0.5f;
            return agent;
        }

        static string RankPrefix(EnemyRank rank) => rank == EnemyRank.Boss ? "[보스] " : rank == EnemyRank.Elite ? "[정예] " : string.Empty;
    }

    public static class EnemyRoster
    {
        /// <summary>일반 몬스터 (KayKit 해골 무리, CC0). Id는 DungeonTuning.Acts[].EnemyIds와 맞춥니다.</summary>
        public static List<EnemyArchetype> CreateDefault() => new List<EnemyArchetype>
        {
            new EnemyArchetype { Id = "skeleton_minion", Name = "해골 졸개", VisualKey = "skeleton_minion", Life = 30f, Damage = 5f, MoveSpeed = 2.5f },
            new EnemyArchetype { Id = "skeleton_rogue", Name = "해골 석궁병", VisualKey = "skeleton_rogue", Life = 24f, Damage = 6f, MoveSpeed = 2.2f, AttackRange = 6f, AttackInterval = 1.7f, IsRanged = true },
            new EnemyArchetype { Id = "skeleton_mage", Name = "해골 주술사", VisualKey = "skeleton_mage", Life = 26f, Damage = 7f, MoveSpeed = 1.9f, AttackRange = 6.5f, AttackInterval = 2f, IsRanged = true, DamageType = DamageType.Shadow },
            new EnemyArchetype { Id = "skeleton_warrior", Name = "해골 전사", VisualKey = "skeleton_warrior", Life = 48f, Damage = 8f, MoveSpeed = 2.1f, Armor = 40f, Radius = 0.4f }
        };

        public static List<EnemyArchetype> CreateElites() => new List<EnemyArchetype>
        {
            new EnemyArchetype { Id = "elite_warrior", Name = "해골 근위병", VisualKey = "skeleton_warrior", Life = 60f, Damage = 10f, MoveSpeed = 1.9f, Radius = 0.45f, Armor = 60f },
            new EnemyArchetype { Id = "elite_mage", Name = "해골 대주술사", VisualKey = "skeleton_mage", Life = 50f, Damage = 11f, MoveSpeed = 1.8f, Radius = 0.42f, AttackRange = 7f, AttackInterval = 1.6f, IsRanged = true, DamageType = DamageType.Shadow }
        };

        public static EnemyArchetype CreateBoss() => new EnemyArchetype
        {
            Id = "skeleton_lord", Name = "무덤의 군주", VisualKey = "skeleton_boss", BossVisualKey = "skeleton_boss", Life = 80f, Damage = 14f, MoveSpeed = 1.7f,
            Radius = 0.5f, Armor = 100f, AttackRange = 1.3f, AttackInterval = 1.4f, DamageType = DamageType.Physical
        };
    }
}
