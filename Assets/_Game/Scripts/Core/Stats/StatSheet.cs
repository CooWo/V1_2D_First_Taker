using System;
using System.Collections.Generic;

namespace ShadowRPG.Core.Stats
{
    /// <summary>
    /// 기본값 + 출처별 수정치 + 파생 규칙으로 최종 능력치를 계산합니다. 계산 결과는 캐시됩니다.
    /// </summary>
    public sealed class StatSheet : IStatReader
    {
        static readonly int StatCount = Enum.GetValues(typeof(StatType)).Length;

        readonly float[] _base = new float[StatCount];
        readonly List<StatModifier> _modifiers = new List<StatModifier>();
        readonly List<IStatDerivationRule> _rules = new List<IStatDerivationRule>();
        readonly float[] _cache = new float[StatCount];
        readonly bool[] _cached = new bool[StatCount];
        readonly Func<StatType, float> _rawGetter;

        public event Action Changed;

        public StatSheet(IEnumerable<IStatDerivationRule> rules = null)
        {
            if (rules != null) _rules.AddRange(rules);
            _rawGetter = GetRaw;
        }

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public void SetBase(StatType stat, float value)
        {
            _base[(int)stat] = value;
            Invalidate();
        }

        public float GetBase(StatType stat) => _base[(int)stat];

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
            Invalidate();
        }

        public void AddModifiers(IEnumerable<StatModifier> modifiers, object source)
        {
            foreach (var m in modifiers) _modifiers.Add(m.WithSource(source));
            Invalidate();
        }

        public int RemoveSource(object source)
        {
            int removed = _modifiers.RemoveAll(m => Equals(m.Source, source));
            if (removed > 0) Invalidate();
            return removed;
        }

        /// <summary>파생 규칙을 적용하기 전 값 (기본값 + 수정치 합).</summary>
        public float GetRaw(StatType stat)
        {
            float value = _base[(int)stat];
            for (int i = 0; i < _modifiers.Count; i++)
                if (_modifiers[i].Stat == stat) value += _modifiers[i].Value;
            return value;
        }

        public float Get(StatType stat)
        {
            int index = (int)stat;
            if (_cached[index]) return _cache[index];

            float value = GetRaw(stat);
            for (int i = 0; i < _rules.Count; i++) value += _rules[i].Contribute(stat, _rawGetter);

            _cache[index] = value;
            _cached[index] = true;
            return value;
        }

        void Invalidate()
        {
            Array.Clear(_cached, 0, _cached.Length);
            Changed?.Invoke();
        }
    }
}
