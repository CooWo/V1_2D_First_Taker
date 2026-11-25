using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Status
{
    public static class StatusIds
    {
        public const string ShadowFortify = "shadow_fortify";
        public const string Slow = "slow";
        public const string Shock = "shock";
        public const string Freeze = "freeze";
        public const string Ignite = "ignite";
        public const string Poison = "poison";
        public const string Bleed = "bleed";
        public const string AssassinMark = "assassin_mark";
        public const string Invulnerable = "invulnerable";
    }

    /// <summary>지속시간만 가진 기본 효과. 재적용 시 더 긴 시간으로 갱신합니다.</summary>
    public abstract class TimedStatus : IStatusEffect
    {
        protected TimedStatus(string id, float duration)
        {
            Id = id;
            Remaining = duration;
        }

        public string Id { get; }
        public float Remaining { get; protected set; }
        public virtual bool IsExpired => Remaining <= 0f;

        public virtual void OnApplied(Combatant owner) { }
        public virtual void OnRemoved(Combatant owner) { }

        public virtual void Tick(Combatant owner, float dt) => Remaining -= dt;

        public virtual void Refresh(IStatusEffect incoming) => Remaining = Mathf.Max(Remaining, incoming.Remaining);
    }

    /// <summary>
    /// GDD 4.1-① 그림자 갑주: 받는 모든 피해 최종 감소 + 경직 면역.
    /// [원거리] 스킬 시전 시 남은 시간이 절반으로 줄어듭니다.
    /// </summary>
    public sealed class ShadowFortifyStatus : TimedStatus, IIncomingDamageModifier, IStaggerImmunity
    {
        readonly float _reduction;

        public ShadowFortifyStatus(float duration, float reduction) : base(StatusIds.ShadowFortify, duration)
        {
            _reduction = reduction;
        }

        public float ModifyIncoming(float amount) => amount * (1f - _reduction);

        public void HalveRemaining() => Remaining *= 0.5f;
    }

    public sealed class SlowStatus : TimedStatus, IMoveSpeedModifier, IAilment
    {
        public SlowStatus(float duration, float multiplier) : base(StatusIds.Slow, duration)
        {
            MoveSpeedMultiplier = multiplier;
        }

        public float MoveSpeedMultiplier { get; }
    }

    /// <summary>감전: 받는 피해 증가.</summary>
    public sealed class ShockStatus : TimedStatus, IIncomingDamageModifier, IAilment
    {
        readonly float _increase;

        public ShockStatus(float duration, float increase) : base(StatusIds.Shock, duration)
        {
            _increase = increase;
        }

        public float ModifyIncoming(float amount) => amount * (1f + _increase);
    }

    public sealed class FreezeStatus : TimedStatus, IMoveSpeedModifier, IActionDisabler, IAilment
    {
        public FreezeStatus(float duration) : base(StatusIds.Freeze, duration) { }
        public float MoveSpeedMultiplier => 0f;
    }

    public sealed class InvulnerableStatus : TimedStatus, IIncomingDamageModifier, IStaggerImmunity
    {
        public InvulnerableStatus(float duration) : base(StatusIds.Invulnerable, duration) { }
        public float ModifyIncoming(float amount) => 0f;
    }

    /// <summary>
    /// 지속 피해 (점화/중독/출혈). 중첩형은 재적용 시 스택이 쌓입니다.
    /// </summary>
    public sealed class DamageOverTimeStatus : TimedStatus, IAilment
    {
        readonly float _damagePerSecondPerStack;
        readonly int _maxStacks;
        readonly float _duration;

        public DamageOverTimeStatus(string id, float duration, float damagePerSecond, int maxStacks = 1)
            : base(id, duration)
        {
            _duration = duration;
            _damagePerSecondPerStack = damagePerSecond;
            _maxStacks = Mathf.Max(1, maxStacks);
            Stacks = 1;
        }

        public int Stacks { get; private set; }
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>남은 지속 피해 총량 (맹독 절개 일괄 정산용).</summary>
        public float RemainingDamage => _damagePerSecondPerStack * Stacks * Mathf.Max(0f, Remaining);

        public override void Tick(Combatant owner, float dt)
        {
            float step = dt * SpeedMultiplier;
            owner.Health.ApplyDamage(_damagePerSecondPerStack * Stacks * step);
            Remaining -= step;
        }

        public override void Refresh(IStatusEffect incoming)
        {
            Remaining = _duration;
            if (incoming is DamageOverTimeStatus) Stacks = Mathf.Min(_maxStacks, Stacks + 1);
        }

        public void Consume() => Remaining = 0f;
    }

    /// <summary>GDD DEX-08 암살자의 표식: 대상이 받는 치명타 확률/피해 증가.</summary>
    public sealed class AssassinMarkStatus : TimedStatus, ICritVulnerability
    {
        public AssassinMarkStatus(float duration, float critChance, float critDamage, int empowerLevel)
            : base(StatusIds.AssassinMark, duration)
        {
            CritChanceBonus = critChance;
            CritDamageBonus = critDamage;
            EmpowerLevel = empowerLevel;
        }

        public float CritChanceBonus { get; }
        public float CritDamageBonus { get; }
        public int EmpowerLevel { get; }

        /// <summary>표식을 새긴 시전자 (전설 각성: 처치 시 회복·전이 대상).</summary>
        public Combatant Caster { get; set; }
        public bool TransfersOnDeath { get; set; }
    }

    /// <summary>지속시간 동안 능력치 수정치를 붙였다 떼는 범용 버프.</summary>
    public sealed class StatBuffStatus : TimedStatus
    {
        readonly List<StatModifier> _modifiers;

        public StatBuffStatus(string id, float duration, params StatModifier[] modifiers) : base(id, duration)
        {
            _modifiers = new List<StatModifier>(modifiers);
        }

        public override void OnApplied(Combatant owner) => owner.Stats.AddModifiers(_modifiers, this);
        public override void OnRemoved(Combatant owner) => owner.Stats.RemoveSource(this);
    }
}
