using ShadowRPG.Core.Character;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tuning;

namespace ShadowRPG.Core.Growth
{
    /// <summary>
    /// 스탯 포인트 분배·재분배 (방향성 v1 §6). 재분배 비용은 "기본적인 골드 소모" 수준 = 레벨 × RespecGoldPerLevel.
    /// </summary>
    public sealed class StatAllocationService
    {
        readonly ProgressionState _state;
        readonly GrowthTuning _tuning;
        readonly Wallet _wallet;

        public StatAllocationService(ProgressionState state, Wallet wallet, GrowthTuning tuning)
        {
            _state = state;
            _wallet = wallet;
            _tuning = tuning;
        }

        public int Unspent => _state.UnspentStatPoints;

        public void GrantForLevels(int levels) => _state.UnspentStatPoints += levels * _tuning.StatPointsPerLevel;

        public bool Allocate(CoreStat stat, int points = 1)
        {
            if (points <= 0 || _state.UnspentStatPoints < points) return false;
            _state.UnspentStatPoints -= points;
            switch (stat)
            {
                case CoreStat.Strength: _state.AllocatedStrength += points; break;
                case CoreStat.Dexterity: _state.AllocatedDexterity += points; break;
                default: _state.AllocatedIntelligence += points; break;
            }
            return true;
        }

        public long RespecCost(int level) => level * _tuning.RespecGoldPerLevel;

        public bool Respec(int level)
        {
            if (_state.TotalAllocated == 0) return false;
            if (!_wallet.TrySpend(RespecCost(level))) return false;
            _state.UnspentStatPoints += _state.TotalAllocated;
            _state.AllocatedStrength = _state.AllocatedDexterity = _state.AllocatedIntelligence = 0;
            return true;
        }

        public void ApplyTo(PlayerProgress progress) =>
            progress.SetAllocation(_state.AllocatedStrength, _state.AllocatedDexterity, _state.AllocatedIntelligence);
    }
}
