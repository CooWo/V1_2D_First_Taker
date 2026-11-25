using System.Collections.Generic;
using System.Linq;

namespace ShadowRPG.Core.AutoBattle
{
    /// <summary>
    /// GDD 4.2 자동 사냥 루프: 매 0.1초 우선순위 1→6을 검사해 처음 성공한 규칙 하나만 실행합니다.
    /// </summary>
    public sealed class AutoCastPlanner
    {
        readonly AutoBattleContext _context;
        readonly List<IAutoCastRule> _rules;
        float _timer;

        public AutoCastPlanner(AutoBattleContext context, IEnumerable<IAutoCastRule> rules)
        {
            _context = context;
            _rules = rules.OrderBy(r => r.Priority).ToList();
        }

        public bool Enabled { get; set; } = true;
        public string LastDecision { get; private set; } = "-";
        public IReadOnlyList<IAutoCastRule> Rules => _rules;

        public static IEnumerable<IAutoCastRule> DefaultRules() => new IAutoCastRule[]
        {
            new AuraRule(), new SurvivalRule(), new MobilityRule(), new TacticalRule(), new BurstRule(), new PrimaryRule()
        };

        public void Tick(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _context.Balance.AutoCastInterval;

            if (!Enabled || !_context.Self.IsAlive) return;
            _context.Refresh();
            if (!_context.Self.CanAct) return;

            foreach (var rule in _rules)
            {
                if (!rule.TryExecute(_context)) continue;
                LastDecision = rule.Name;
                return;
            }
        }
    }
}
