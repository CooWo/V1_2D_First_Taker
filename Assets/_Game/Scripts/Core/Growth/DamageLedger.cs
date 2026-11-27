using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;

namespace ShadowRPG.Core.Growth
{
    /// <summary>사망창에 보여 줄 "왜 죽었나" 요약 (Menu_Structure §3-5).</summary>
    public sealed class DeathReport
    {
        public sealed class SourceLine
        {
            public string Name;
            public float Damage;
            public float Share;
            public DamageType MainType;
        }

        public float Total;
        public Dictionary<DamageType, float> ByType = new Dictionary<DamageType, float>();
        public List<SourceLine> TopSources = new List<SourceLine>();
        public int Evaded;
        public int Blocked;
        public int HitsTaken;
        public string LastHitBy;
        public float LastHitAmount;
        public List<string> Hints = new List<string>();

        public float ShareOf(DamageType type) => Total <= 0f || !ByType.TryGetValue(type, out var v) ? 0f : v / Total;
    }

    /// <summary>
    /// 플레이어가 받은 피해를 층 단위로 기록합니다. 전투 규칙과 분리된 관찰자 (DamageService.Resolved 구독).
    /// </summary>
    public sealed class DamageLedger : IDisposable
    {
        readonly DamageService _damage;
        readonly Combatant _player;
        readonly Dictionary<DamageType, float> _byType = new Dictionary<DamageType, float>();
        readonly Dictionary<string, (float damage, Dictionary<DamageType, float> types)> _bySource = new Dictionary<string, (float, Dictionary<DamageType, float>)>();
        int _evaded, _blocked, _hits;
        string _lastSource;
        float _lastAmount;

        public DamageLedger(DamageService damage, Combatant player)
        {
            _damage = damage;
            _player = player;
            damage.Resolved += OnResolved;
        }

        public void Reset()
        {
            _byType.Clear();
            _bySource.Clear();
            _evaded = _blocked = _hits = 0;
            _lastSource = null;
            _lastAmount = 0f;
        }

        void OnResolved(DamageResult result)
        {
            if (result.Target != _player) return;
            if (result.Evaded) { _evaded++; return; }
            if (result.Blocked) _blocked++;
            if (result.Amount <= 0f) return;
            _hits++;
            string source = result.Attacker?.Name ?? "알 수 없음";
            _byType[result.Type] = (_byType.TryGetValue(result.Type, out var t) ? t : 0f) + result.Amount;
            if (!_bySource.TryGetValue(source, out var entry)) entry = (0f, new Dictionary<DamageType, float>());
            entry.types[result.Type] = (entry.types.TryGetValue(result.Type, out var st) ? st : 0f) + result.Amount;
            _bySource[source] = (entry.damage + result.Amount, entry.types);
            _lastSource = source;
            _lastAmount = result.Amount;
        }

        public DeathReport Build(IStatReader playerStats, int topCount = 5)
        {
            var report = new DeathReport
            {
                Total = _byType.Values.Sum(),
                ByType = new Dictionary<DamageType, float>(_byType),
                Evaded = _evaded,
                Blocked = _blocked,
                HitsTaken = _hits,
                LastHitBy = _lastSource,
                LastHitAmount = _lastAmount
            };
            report.TopSources = _bySource
                .OrderByDescending(p => p.Value.damage)
                .Take(topCount)
                .Select(p => new DeathReport.SourceLine
                {
                    Name = p.Key,
                    Damage = p.Value.damage,
                    Share = report.Total > 0f ? p.Value.damage / report.Total : 0f,
                    MainType = p.Value.types.OrderByDescending(t => t.Value).First().Key
                }).ToList();
            BuildHints(report, playerStats);
            return report;
        }

        /// <summary>비율이 큰 피해 유형에 비해 낮은 방어 항목을 짚어 줍니다 (예: "불 피해 48% — 불 저항 12%").</summary>
        static void BuildHints(DeathReport report, IStatReader stats)
        {
            if (stats == null || report.Total <= 0f) return;
            foreach (var pair in report.ByType.OrderByDescending(p => p.Value))
            {
                float share = pair.Value / report.Total;
                if (share < 0.25f) continue;
                if (pair.Key == DamageType.Physical)
                {
                    float armor = stats.Get(StatType.Armor);
                    report.Hints.Add($"물리 피해 {share:P0} — 방어력 {armor:N0}, 회피 {stats.Get(StatType.EvasionPct):0}%, 막기 {stats.Get(StatType.BlockChancePct):0}%");
                }
                else
                {
                    var resist = ResistFor(pair.Key);
                    float value = stats.Get(resist) + stats.Get(StatType.AllResistPct);
                    report.Hints.Add($"{DamageTypes.Korean(pair.Key)} 피해 {share:P0} — {DamageTypes.Korean(pair.Key)} 저항 {value:0}%");
                }
            }
            if (report.Hints.Count == 0) report.Hints.Add("특정 유형에 치우치지 않음 — 생명력·방어 전반을 보강하세요");
        }

        static StatType ResistFor(DamageType type)
        {
            switch (type)
            {
                case DamageType.Fire: return StatType.FireResistPct;
                case DamageType.Cold: return StatType.ColdResistPct;
                case DamageType.Lightning: return StatType.LightningResistPct;
                case DamageType.Earth: return StatType.EarthResistPct;
                case DamageType.Light: return StatType.LightResistPct;
                default: return StatType.DarkResistPct;
            }
        }

        public void Dispose() => _damage.Resolved -= OnResolved;
    }

    public static class DamageTypes
    {
        public static string Korean(DamageType type)
        {
            switch (type)
            {
                case DamageType.Physical: return "물리";
                case DamageType.Fire: return "불";
                case DamageType.Cold: return "얼음";
                case DamageType.Lightning: return "번개";
                case DamageType.Earth: return "대지";
                case DamageType.Light: return "빛";
                default: return "어둠";
            }
        }
    }
}
