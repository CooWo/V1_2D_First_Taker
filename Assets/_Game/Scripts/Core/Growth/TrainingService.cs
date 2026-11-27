using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tuning;
using UnityEngine;

namespace ShadowRPG.Core.Growth
{
    public enum TrainingTrack
    {
        Attack,
        Life,
        Armor,
        Resist
    }

    /// <summary>
    /// 단련: 골드로 기본 체급(공격·생명·방어·저항)을 올립니다.
    /// 비용(n) = CostBase × CostGrowth^n — 단계마다 골드 효율이 떨어짐.
    /// 한도 = 열린 ACT 수 × LevelsPerAct — 다음 ACT를 빌드로 뚫어야 한도가 풀림 (Balance_Draft_v1 §2-a).
    /// </summary>
    public sealed class TrainingService
    {
        readonly ProgressionState _state;
        readonly TrainingTuning _tuning;
        readonly object _source = new object();

        public TrainingService(ProgressionState state, TrainingTuning tuning)
        {
            _state = state;
            _tuning = tuning;
        }

        public int LevelOf(TrainingTrack track)
        {
            switch (track)
            {
                case TrainingTrack.Attack: return _state.TrainAttack;
                case TrainingTrack.Life: return _state.TrainLife;
                case TrainingTrack.Armor: return _state.TrainArmor;
                default: return _state.TrainResist;
            }
        }

        public int Cap(int actsOpened) => Mathf.Max(1, actsOpened) * _tuning.LevelsPerAct;

        public long Cost(TrainingTrack track) => (long)Mathf.Round(_tuning.CostBase * Mathf.Pow(_tuning.CostGrowth, LevelOf(track)));

        public float EffectPerLevel(TrainingTrack track)
        {
            switch (track)
            {
                case TrainingTrack.Attack: return _tuning.AttackPctPerLevel;
                case TrainingTrack.Life: return _tuning.LifePctPerLevel;
                case TrainingTrack.Armor: return _tuning.ArmorPctPerLevel;
                default: return _tuning.ResistPctPerLevel;
            }
        }

        public string Name(TrainingTrack track)
        {
            switch (track)
            {
                case TrainingTrack.Attack: return "공격력";
                case TrainingTrack.Life: return "생명력";
                case TrainingTrack.Armor: return "방어력";
                default: return "모든 저항";
            }
        }

        /// <returns>실패 사유 (성공이면 null)</returns>
        public string TryTrain(TrainingTrack track, Wallet wallet, int actsOpened)
        {
            if (LevelOf(track) >= Cap(actsOpened)) return $"이번 ACT 한도 도달 ({Cap(actsOpened)}단계) — 다음 ACT를 열면 늘어납니다";
            long cost = Cost(track);
            if (!wallet.TrySpend(cost)) return $"골드 부족 (필요 {cost:N0})";
            switch (track)
            {
                case TrainingTrack.Attack: _state.TrainAttack++; break;
                case TrainingTrack.Life: _state.TrainLife++; break;
                case TrainingTrack.Armor: _state.TrainArmor++; break;
                default: _state.TrainResist++; break;
            }
            return null;
        }

        public void ApplyTo(Combatant combatant)
        {
            combatant.Stats.RemoveSource(_source);
            combatant.Stats.AddModifier(new StatModifier(StatType.AllDamagePct, _state.TrainAttack * _tuning.AttackPctPerLevel, _source));
            combatant.Stats.AddModifier(new StatModifier(StatType.LifePct, _state.TrainLife * _tuning.LifePctPerLevel, _source));
            combatant.Stats.AddModifier(new StatModifier(StatType.ArmorEffectPct, _state.TrainArmor * _tuning.ArmorPctPerLevel, _source));
            combatant.Stats.AddModifier(new StatModifier(StatType.AllResistPct, _state.TrainResist * _tuning.ResistPctPerLevel, _source));
        }
    }
}
