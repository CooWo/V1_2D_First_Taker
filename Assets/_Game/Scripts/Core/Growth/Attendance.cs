using System.Linq;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Tuning;

namespace ShadowRPG.Core.Growth
{
    /// <summary>
    /// 출석: 28일 주기 출석판 (매일 골드, 7·14·21·28일째 템포·상자) + 신규 7일 출석 (별도).
    /// 하루 한 번, 자정 기준.
    /// </summary>
    public sealed class AttendanceService
    {
        readonly AttendanceState _state;
        readonly AttendanceTuning _tuning;
        readonly IClock _clock;

        public AttendanceService(AttendanceState state, AttendanceTuning tuning, IClock clock)
        {
            _state = state;
            _tuning = tuning;
            _clock = clock;
        }

        public bool CanClaimToday => _state.LastClaimDay != DailyQuestService.DayKey(_clock.Now);
        public int CycleDay => _state.CycleDay;
        public int NewbieDay => _state.NewbieDay;
        public int CycleDays => _tuning.CycleDays;

        public AttendanceReward SpecialFor(int day) => _tuning.Special.FirstOrDefault(r => r.Day == day);
        public AttendanceReward NewbieFor(int day) => _tuning.Newbie.FirstOrDefault(r => r.Day == day);

        public long DailyGold(int actsOpened) => _tuning.DailyGold + _tuning.DailyGoldPerAct * (actsOpened - 1);

        /// <summary>오늘 출석 보상 수령. 반환: 받은 내용 설명 (이미 받았으면 null).</summary>
        public string Claim(Wallet gold, ProgressionState state, int actsOpened)
        {
            if (!CanClaimToday) return null;
            _state.LastClaimDay = DailyQuestService.DayKey(_clock.Now);
            _state.CycleDay = _state.CycleDay % _tuning.CycleDays + 1;

            long g = DailyGold(actsOpened);
            long tempo = 0;
            string items = string.Empty;
            var special = SpecialFor(_state.CycleDay);
            if (special != null)
            {
                g += special.Gold;
                tempo += special.Tempo;
                if (!string.IsNullOrEmpty(special.Item)) items += special.Item + " ";
            }
            if (_state.NewbieDay < _tuning.Newbie.Length)
            {
                _state.NewbieDay++;
                var newbie = NewbieFor(_state.NewbieDay);
                if (newbie != null)
                {
                    g += newbie.Gold;
                    tempo += newbie.Tempo;
                    if (!string.IsNullOrEmpty(newbie.Item)) items += newbie.Item;
                }
            }
            gold.Earn(g);
            state.Tempo += tempo;
            return $"출석 {_state.CycleDay}일째: 골드 {g:N0}" + (tempo > 0 ? $" · 템포 {tempo}" : "") + (items.Length > 0 ? $" · {items.Trim()}" : "");
        }
    }
}
