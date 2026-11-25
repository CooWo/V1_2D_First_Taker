using System;
using System.Collections.Generic;

namespace ShadowRPG.Core.Combat
{
    /// <summary>
    /// 피해 파이프라인: 공격 규칙 → 방어 규칙 → 체력 적용 → 관찰자 통지.
    /// 각 단계 규칙은 생성자로 주입받습니다 (DIP). 이 클래스는 순서만 책임집니다 (SRP).
    /// </summary>
    public sealed class DamageService
    {
        readonly List<IOutgoingDamageRule> _outgoing;
        readonly List<IIncomingDamageRule> _incoming;
        readonly List<IDamageListener> _listeners = new List<IDamageListener>();
        readonly BalanceSettings _balance;
        int _depth;

        public event Action<DamageResult> Resolved;

        public DamageService(BalanceSettings balance, IEnumerable<IOutgoingDamageRule> outgoing, IEnumerable<IIncomingDamageRule> incoming)
        {
            _balance = balance;
            _outgoing = new List<IOutgoingDamageRule>(outgoing);
            _incoming = new List<IIncomingDamageRule>(incoming);
        }

        public void AddListener(IDamageListener listener)
        {
            if (!_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void RemoveListener(IDamageListener listener) => _listeners.Remove(listener);

        public DamageResult Deal(DamageRequest request)
        {
            var target = request.Target;
            if (target == null || !target.IsAlive || request.BaseAmount <= 0f) return default;

            var outgoing = new OutgoingDamage
            {
                Request = request,
                IncreasedPct = 0f,
                More = 1f,
                CritChancePct = 0f,
                CritMultiplier = _balance.BaseCritMultiplier,
                IsCrit = false
            };
            for (int i = 0; i < _outgoing.Count; i++) _outgoing[i].Apply(ref outgoing);

            var incoming = new IncomingDamage
            {
                Attacker = request.Attacker,
                Target = target,
                Tags = request.Tags,
                Type = request.Type,
                Amount = outgoing.Total,
                IsCrit = outgoing.IsCrit
            };
            for (int i = 0; i < _incoming.Count && !incoming.Evaded; i++) _incoming[i].Apply(ref incoming);

            float applied = incoming.Evaded ? 0f : target.Health.ApplyDamage(incoming.Amount);

            var result = new DamageResult
            {
                Attacker = request.Attacker,
                Target = target,
                Tags = request.Tags,
                Type = request.Type,
                Amount = applied,
                IsCrit = incoming.IsCrit,
                Blocked = incoming.Blocked,
                Evaded = incoming.Evaded,
                Killed = !target.IsAlive
            };

            Resolved?.Invoke(result);

            // 트리거가 연쇄적으로 피해를 일으켜도 무한 재귀하지 않도록 깊이를 제한합니다.
            if (_depth < 4)
            {
                _depth++;
                try
                {
                    for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnDamageResolved(result);
                }
                finally
                {
                    _depth--;
                }
            }

            return result;
        }
    }
}
