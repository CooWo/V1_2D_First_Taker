using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tuning;

namespace ShadowRPG.Core.Growth
{
    /// <summary>
    /// 일일 퀘스트 (Menu_Structure §I): 매일 풀에서 DailyCount개를 날짜 시드로 뽑고, 다 끝내면 보너스.
    /// 진행은 게임 이벤트를 받아 Report(id, amount)로 올립니다. 반납 퀘스트는 장비를 내면 사라지고 보상을 받음.
    /// </summary>
    public sealed class DailyQuestService
    {
        readonly QuestState _state;
        readonly QuestTuning _tuning;
        readonly IClock _clock;

        public DailyQuestService(QuestState state, QuestTuning tuning, IClock clock)
        {
            _state = state;
            _tuning = tuning;
            _clock = clock;
            Refresh();
        }

        public static string DayKey(DateTime now) => now.ToString("yyyyMMdd");

        public IReadOnlyList<QuestProgress> Active
        {
            get
            {
                Refresh();
                return _state.Active;
            }
        }

        public bool AllClearClaimed => _state.AllClearClaimed;
        public bool AllDone => _state.Active.Count > 0 && _state.Active.All(q => q.Claimed);

        public QuestTemplate Template(string id) => _tuning.Pool.FirstOrDefault(t => t.Id == id);

        public string Describe(QuestProgress q)
        {
            var t = Template(q.Id);
            return t == null ? q.Id : string.Format(t.Text, t.Target);
        }

        public bool IsComplete(QuestProgress q) => q.Progress >= (Template(q.Id)?.Target ?? int.MaxValue);

        public event Action Changed;

        void Refresh()
        {
            string today = DayKey(_clock.Now);
            if (_state.DayKey == today && _state.Active.Count > 0) return;
            _state.DayKey = today;
            _state.AllClearClaimed = false;
            _state.Active.Clear();
            var random = new Random(today.GetHashCode());
            var pool = _tuning.Pool.ToList();
            for (int i = 0; i < _tuning.DailyCount && pool.Count > 0; i++)
            {
                int pick = random.Next(pool.Count);
                _state.Active.Add(new QuestProgress { Id = pool[pick].Id });
                pool.RemoveAt(pick);
            }
        }

        /// <summary>진행 보고 (예: "kill", 1). 오늘 퀘스트에 없으면 무시.</summary>
        public void Report(string id, int amount = 1)
        {
            Refresh();
            var q = _state.Active.FirstOrDefault(x => x.Id == id);
            if (q == null || q.Claimed) return;
            q.Progress += amount;
            Changed?.Invoke();
        }

        /// <summary>깊이처럼 "최고값"으로 세는 퀘스트.</summary>
        public void ReportMax(string id, int value)
        {
            Refresh();
            var q = _state.Active.FirstOrDefault(x => x.Id == id);
            if (q == null || q.Claimed || value <= q.Progress) return;
            q.Progress = value;
            Changed?.Invoke();
        }

        /// <summary>반납 퀘스트에 낼 수 있는 장비인지.</summary>
        public bool CanTurnIn(QuestProgress q, Item item)
        {
            var t = Template(q.Id);
            if (t == null || t.Category != "반납" || q.Claimed || item == null) return false;
            switch (t.Id)
            {
                case "turnin_rare": return Enum.TryParse(t.Param, out ItemRarity r) && item.Rarity >= r;
                case "turnin_big": return int.TryParse(t.Param, out int cells) && ItemShapes.Cells(item) >= cells;
                default:
                    return Enum.TryParse(t.Param, out StatType stat) && item.AllModifiers.Any(m => m.Stat == stat);
            }
        }

        public void TurnIn(QuestProgress q) => q.Progress++;

        /// <summary>완료 보상 수령.</summary>
        public bool Claim(QuestProgress q, Wallet gold, ProgressionState state)
        {
            var t = Template(q.Id);
            if (t == null || q.Claimed || !IsComplete(q)) return false;
            q.Claimed = true;
            gold.Earn(t.RewardGold);
            state.Tempo += t.RewardTempo;
            Changed?.Invoke();
            return true;
        }

        public bool ClaimAllClear(Wallet gold, ProgressionState state)
        {
            if (!AllDone || _state.AllClearClaimed) return false;
            _state.AllClearClaimed = true;
            gold.Earn(_tuning.AllClearGold);
            state.Tempo += _tuning.AllClearTempo;
            Changed?.Invoke();
            return true;
        }
    }
}
