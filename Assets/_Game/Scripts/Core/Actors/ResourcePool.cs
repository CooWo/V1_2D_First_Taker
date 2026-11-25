using System.Collections.Generic;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Actors
{
    /// <summary>마나/기력 자원. 오라는 최대치의 일부를 점유(Reservation)합니다.</summary>
    public sealed class ResourcePool
    {
        const float BaseRegenFractionPerSecond = 0.04f;

        readonly IStatReader _stats;
        readonly Dictionary<object, float> _reservations = new Dictionary<object, float>();

        public float Current { get; private set; }
        public float Total => Mathf.Max(0f, _stats.Get(StatType.MaxResource));
        public float ReservedFraction { get; private set; }
        public float Max => Total * (1f - ReservedFraction);
        public float Fraction => Max <= 0f ? 0f : Current / Max;

        public ResourcePool(IStatReader stats)
        {
            _stats = stats;
        }

        public void Fill() => Current = Max;

        public bool CanAfford(float cost) => Current >= cost;

        public bool TrySpend(float cost)
        {
            if (cost <= 0f) return true;
            if (Current < cost) return false;
            Current -= cost;
            return true;
        }

        public void Gain(float amount) => Current = Mathf.Min(Max, Current + Mathf.Max(0f, amount));

        public bool IsReserved(object source) => _reservations.ContainsKey(source);

        public void Reserve(object source, float fraction)
        {
            _reservations[source] = fraction;
            Recalculate();
        }

        public void Release(object source)
        {
            if (_reservations.Remove(source)) Recalculate();
        }

        public void ReleaseAll()
        {
            _reservations.Clear();
            Recalculate();
        }

        public void Tick(float dt)
        {
            float regen = Total * BaseRegenFractionPerSecond + _stats.Get(StatType.ResourceRegen);
            Gain(regen * dt);
        }

        void Recalculate()
        {
            float sum = 0f;
            foreach (var value in _reservations.Values) sum += value;
            ReservedFraction = Mathf.Clamp(sum, 0f, 0.9f);
            Current = Mathf.Min(Current, Max);
        }
    }
}
