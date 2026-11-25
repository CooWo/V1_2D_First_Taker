using System.Collections.Generic;
using ShadowRPG.Core.Actors;

namespace ShadowRPG.Core.Status
{
    /// <summary>한 전투원에게 걸린 효과 목록을 관리하고, 역할 인터페이스별 합산 결과를 제공합니다.</summary>
    public sealed class StatusController
    {
        readonly Combatant _owner;
        readonly List<IStatusEffect> _effects = new List<IStatusEffect>();

        public StatusController(Combatant owner)
        {
            _owner = owner;
        }

        public IReadOnlyList<IStatusEffect> Effects => _effects;

        public void Apply(IStatusEffect effect)
        {
            var existing = Find(effect.Id);
            if (existing != null)
            {
                existing.Refresh(effect);
                return;
            }
            _effects.Add(effect);
            effect.OnApplied(_owner);
        }

        public IStatusEffect Find(string id)
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Id == id) return _effects[i];
            return null;
        }

        public T Get<T>() where T : class
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i] is T match) return match;
            return null;
        }

        public bool Has(string id) => Find(id) != null;

        public bool Any<T>() where T : class => Get<T>() != null;

        public void Remove(string id)
        {
            var effect = Find(id);
            if (effect == null) return;
            _effects.Remove(effect);
            effect.OnRemoved(_owner);
        }

        public float ModifyIncoming(float amount)
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i] is IIncomingDamageModifier modifier) amount = modifier.ModifyIncoming(amount);
            return amount;
        }

        public float MoveSpeedMultiplier
        {
            get
            {
                float result = 1f;
                for (int i = 0; i < _effects.Count; i++)
                    if (_effects[i] is IMoveSpeedModifier modifier) result *= modifier.MoveSpeedMultiplier;
                return result;
            }
        }

        public float CritChanceTakenBonus
        {
            get
            {
                float result = 0f;
                for (int i = 0; i < _effects.Count; i++)
                    if (_effects[i] is ICritVulnerability v) result += v.CritChanceBonus;
                return result;
            }
        }

        public float CritDamageTakenBonus
        {
            get
            {
                float result = 0f;
                for (int i = 0; i < _effects.Count; i++)
                    if (_effects[i] is ICritVulnerability v) result += v.CritDamageBonus;
                return result;
            }
        }

        public bool IsDisabled => Any<IActionDisabler>();
        public bool IsStaggerImmune => Any<IStaggerImmunity>();
        public bool HasAilment => Any<IAilment>();

        public void Tick(float dt)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (i >= _effects.Count) continue;
                var effect = _effects[i];
                effect.Tick(_owner, dt);
                if (!effect.IsExpired) continue;
                _effects.Remove(effect);
                effect.OnRemoved(_owner);
            }
        }

        public void Clear()
        {
            for (int i = _effects.Count - 1; i >= 0; i--) _effects[i].OnRemoved(_owner);
            _effects.Clear();
        }
    }
}
