using System;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Actors
{
    /// <summary>
    /// 생명력 / 에너지 보호막 / 임시 방벽(Barrier) 3층 체력. 피해는 방벽 → 보호막 → 생명력 순서로 깎입니다.
    /// </summary>
    public sealed class Health
    {
        const float EnergyShieldRechargeDelay = 2f;
        const float EnergyShieldRechargePerSecond = 0.25f;

        readonly IStatReader _stats;
        float _sinceDamaged;
        float _barrierTimer;

        public float Life { get; private set; }
        public float EnergyShield { get; private set; }
        public float Barrier { get; private set; }

        public float MaxLife => Mathf.Max(1f, _stats.Get(StatType.MaxLife) * (1f + _stats.Get(StatType.LifePct) / 100f));
        public float MaxEnergyShield => Mathf.Max(0f, _stats.Get(StatType.EnergyShield) + MaxLife * _stats.Get(StatType.EnergyShieldPct) / 100f);
        public float LifeFraction => Life / MaxLife;
        public bool IsDead => Life <= 0f;

        /// <summary>마지막으로 보호막이 깨진 뒤 지난 시간 (자동 사냥 생존 판단용).</summary>
        public float SinceEnergyShieldBroken { get; private set; } = float.MaxValue;

        public event Action<float> Damaged;
        public event Action<float> Healed;
        public event Action EnergyShieldBroken;
        public event Action Died;

        public Health(IStatReader stats)
        {
            _stats = stats;
            _stats.Changed += ClampToMax;
        }

        public void ResetToFull()
        {
            Life = MaxLife;
            EnergyShield = MaxEnergyShield;
            Barrier = 0f;
            _sinceDamaged = EnergyShieldRechargeDelay;
        }

        /// <summary>최종 피해 적용. 실제로 깎인 총량을 돌려줍니다.</summary>
        public float ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;

            float remaining = amount;

            float fromBarrier = Mathf.Min(Barrier, remaining);
            Barrier -= fromBarrier;
            remaining -= fromBarrier;

            if (EnergyShield > 0f && remaining > 0f)
            {
                float fromShield = Mathf.Min(EnergyShield, remaining);
                EnergyShield -= fromShield;
                remaining -= fromShield;
                if (EnergyShield <= 0f)
                {
                    SinceEnergyShieldBroken = 0f;
                    EnergyShieldBroken?.Invoke();
                }
            }

            Life = Mathf.Max(0f, Life - remaining);
            _sinceDamaged = 0f;
            Damaged?.Invoke(amount);
            if (IsDead) Died?.Invoke();
            return amount;
        }

        public float Heal(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;
            float before = Life;
            Life = Mathf.Min(MaxLife, Life + amount);
            float healed = Life - before;
            if (healed > 0f) Healed?.Invoke(healed);
            return healed;
        }

        public void AddBarrier(float amount, float duration)
        {
            Barrier = Mathf.Max(Barrier, amount);
            _barrierTimer = Mathf.Max(_barrierTimer, duration);
        }

        public void AddEnergyShield(float amount) => EnergyShield = Mathf.Min(MaxEnergyShield, EnergyShield + amount);

        public void Tick(float dt)
        {
            if (IsDead) return;

            _sinceDamaged += dt;
            SinceEnergyShieldBroken += dt;

            if (_barrierTimer > 0f)
            {
                _barrierTimer -= dt;
                if (_barrierTimer <= 0f) Barrier = 0f;
            }

            if (_sinceDamaged >= EnergyShieldRechargeDelay && EnergyShield < MaxEnergyShield)
                EnergyShield = Mathf.Min(MaxEnergyShield, EnergyShield + MaxEnergyShield * EnergyShieldRechargePerSecond * dt);

            float regen = _stats.Get(StatType.LifeRegen);
            if (regen > 0f) Heal(regen * dt);
        }

        void ClampToMax()
        {
            Life = Mathf.Min(Life, MaxLife);
            EnergyShield = Mathf.Min(EnergyShield, MaxEnergyShield);
        }
    }
}
