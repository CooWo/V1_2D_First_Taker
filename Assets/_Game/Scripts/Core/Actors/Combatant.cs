using System.Collections.Generic;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Status;
using UnityEngine;

namespace ShadowRPG.Core.Actors
{
    public enum Team
    {
        Player,
        Enemy
    }

    public enum EnemyRank
    {
        Normal,
        Elite,
        Boss
    }

    /// <summary>
    /// 전투에 참여하는 개체(플레이어·몬스터)의 상태. 체력·자원·상태이상·이동만 책임지고,
    /// 무엇을 할지는 외부(자동 사냥 AI, 몬스터 두뇌)가 결정합니다 (SRP).
    /// </summary>
    public sealed class Combatant
    {
        static int _nextId;

        Vector2 _dashFrom;
        Vector2 _dashTo;
        float _dashTime;
        float _dashDuration;

        public Combatant(string name, Team team, IEnumerable<IStatDerivationRule> derivationRules = null)
        {
            Id = ++_nextId;
            Name = name;
            Team = team;
            Stats = new StatSheet(derivationRules);
            Health = new Health(Stats);
            Resource = new ResourcePool(Stats);
            Statuses = new StatusController(this);
            TagMultipliers = new TagMultiplierSet();
        }

        public int Id { get; }
        public string Name { get; set; }
        public Team Team { get; }
        public EnemyRank Rank { get; set; }
        public string VisualKey { get; set; }
        public float Radius { get; set; } = 0.35f;
        public float BaseMoveSpeed { get; set; } = 3f;

        public Vector2 Position { get; set; }
        public Vector2 Facing { get; set; } = Vector2.right;

        /// <summary>행동 시작 신호 (표현 계층이 공격·시전 동작을 재생). 규칙에는 영향 없음.</summary>
        public event System.Action<CombatAction> ActionStarted;

        public void NotifyAction(CombatAction action) => ActionStarted?.Invoke(action);

        public StatSheet Stats { get; }
        public Health Health { get; }
        public ResourcePool Resource { get; }
        public StatusController Statuses { get; }
        public TagMultiplierSet TagMultipliers { get; }

        /// <summary>장비에서 얻은 스킬 묶음 (몬스터는 null).</summary>
        public SkillLoadout Loadout { get; set; }

        public bool IsAlive => !Health.IsDead;
        public bool IsEliteOrBoss => Rank != EnemyRank.Normal;
        public float ActionLock { get; private set; }
        public bool IsDashing => _dashDuration > 0f && _dashTime < _dashDuration;
        public bool CanAct => IsAlive && ActionLock <= 0f && !IsDashing && !Statuses.IsDisabled;

        public float MoveSpeed => BaseMoveSpeed * (1f + Stats.Get(StatType.MoveSpeedPct) / 100f) * Statuses.MoveSpeedMultiplier;
        public float AttackSpeedMultiplier => Mathf.Max(0.1f, 1f + Stats.Get(StatType.AttackSpeedPct) / 100f);
        public float CastSpeedMultiplier => Mathf.Max(0.1f, 1f + Stats.Get(StatType.CastSpeedPct) / 100f);

        public float DistanceTo(Combatant other) => Vector2.Distance(Position, other.Position);

        /// <summary>시전 후딜레이. 이 시간 동안 다른 행동을 할 수 없습니다.</summary>
        public void LockActions(float seconds) => ActionLock = Mathf.Max(ActionLock, seconds);

        public void Move(Vector2 direction, float dt)
        {
            if (!IsAlive || IsDashing || Statuses.IsDisabled || direction.sqrMagnitude < 0.0001f) return;
            var dir = direction.normalized;
            Position += dir * MoveSpeed * dt;
            Facing = dir;
        }

        /// <summary>짧은 시간 동안 목표 지점으로 미끄러지듯 이동 (그림자 추적·점멸·질주).</summary>
        public void BeginDash(Vector2 destination, float duration)
        {
            _dashFrom = Position;
            _dashTo = destination;
            _dashTime = 0f;
            _dashDuration = Mathf.Max(0.01f, duration);
            var delta = destination - Position;
            if (delta.sqrMagnitude > 0.0001f) Facing = delta.normalized;
        }

        /// <summary>즉시 순간이동 (점멸 타격).</summary>
        public void Teleport(Vector2 destination)
        {
            var delta = destination - Position;
            if (delta.sqrMagnitude > 0.0001f) Facing = delta.normalized;
            Position = destination;
            _dashDuration = 0f;
        }

        public void Tick(float dt)
        {
            if (ActionLock > 0f) ActionLock -= dt;

            if (IsDashing)
            {
                _dashTime += dt;
                Position = Vector2.Lerp(_dashFrom, _dashTo, Mathf.Clamp01(_dashTime / _dashDuration));
            }

            Health.Tick(dt);
            Resource.Tick(dt);
            Statuses.Tick(dt);
        }
    }
}

namespace ShadowRPG.Core.Actors
{
    public enum CombatAction
    {
        Attack,
        RangedAttack,
        Cast,
        Movement,
        Heavy
    }
}
