using System.Linq;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>일일 퀘스트 (매일 5개) + 전부 완료 보너스. 반납 퀘스트는 조건에 맞는 장비를 가방·창고에서 골라 냅니다.</summary>
    public sealed class QuestMenu : IMenu
    {
        readonly MenuContext _ctx;

        public QuestMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "QuestMenu", 6);
            Panel.RowClicked += OnRow;
        }

        public string ButtonLabel => "퀘스트";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var quests = _ctx.Meta.Quests;
            var list = quests.Active;
            Panel.SetTitle("<b>일일 퀘스트</b>  <size=20>자정에 바뀝니다</size>");
            Panel.SetItems(list.Count, i =>
            {
                var q = list[i];
                var t = quests.Template(q.Id);
                string state = q.Claimed ? "<color=#8090A8>[완료]</color>"
                    : quests.IsComplete(q) ? "<color=#3FE06A>[보상 받기]</color>"
                    : t != null && t.Category == "반납" ? "<color=#9FD3FF>[반납하기]</color>"
                    : $"<color=#8090A8>{Mathf.Min(q.Progress, t?.Target ?? 0)}/{t?.Target}</color>";
                return $"<size=17><color=#C8A0FF>{t?.Category}</color></size> {quests.Describe(q)}  {state} <size=16><color=#FFD84A>{t?.RewardGold:N0}G</color> · 템포 {t?.RewardTempo}</size>";
            });
            var tuning = _ctx.Meta.Tuning.Quest;
            Panel.SetDetail($"<size=19>전부 완료 보너스: 골드 {tuning.AllClearGold:N0} · 템포 {tuning.AllClearTempo}" +
                            (quests.AllClearClaimed ? " <color=#8090A8>(받음)</color>" : string.Empty) +
                            "\n반납한 장비는 사라지고 보상을 받습니다 — 팔지 반납할지 고민하세요.</size>");
            Panel.SetFooter(0, "전부 완료 보상", () =>
            {
                _ctx.Log(quests.ClaimAllClear(_ctx.Meta.Wallet, _ctx.Meta.State) ? "일일 퀘스트 전부 완료 보상!" : "아직 모든 퀘스트를 끝내지 않았습니다.");
                _ctx.Meta.RequestSave();
            });
            Panel.SetFooter(1, string.Empty, null, false);
            Panel.SetFooter(2, "닫기", () => Panel.SetVisible(false));
        }

        void OnRow(int index)
        {
            var quests = _ctx.Meta.Quests;
            var list = quests.Active;
            if (index < 0 || index >= list.Count) return;
            var q = list[index];
            var meta = _ctx.Meta;
            if (quests.IsComplete(q))
            {
                if (quests.Claim(q, meta.Wallet, meta.State)) _ctx.Log($"퀘스트 보상: {quests.Describe(q)}");
            }
            else
            {
                var t = quests.Template(q.Id);
                if (t != null && t.Category == "반납")
                {
                    var item = meta.Profile.Bag.Items.Concat(meta.Profile.Stash.Items).FirstOrDefault(i => quests.CanTurnIn(q, i));
                    if (item == null) _ctx.Log("조건에 맞는 장비가 가방·창고에 없습니다.");
                    else
                    {
                        if (!meta.Profile.Bag.Remove(item)) meta.Profile.Stash.Remove(item);
                        quests.TurnIn(q);
                        _ctx.Log($"반납: {item.DisplayName}");
                    }
                }
            }
            meta.RequestSave();
            Refresh();
        }
    }
}
