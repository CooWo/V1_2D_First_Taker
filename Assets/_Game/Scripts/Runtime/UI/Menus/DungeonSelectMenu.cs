using ShadowRPG.Core.Dungeon;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>마을의 던전 입장: 개방된 던전 중 골라 1층부터 시작합니다.</summary>
    public sealed class DungeonSelectMenu : IMenu
    {
        readonly MenuContext _ctx;
        int _selected = -1;

        public DungeonSelectMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "DungeonSelect");
            Panel.RowClicked += OnRow;
        }

        public string ButtonLabel => "던전";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var acts = _ctx.Meta.Content.Acts;
            int unlocked = _ctx.Meta.Profile.UnlockedDungeons;
            if (_selected < 0) _selected = unlocked - 1;
            _selected = Mathf.Clamp(_selected, 0, unlocked - 1);

            Panel.SetTitle("<b>던전 입장</b>");
            // 최근 개방 던전이 위로
            Panel.SetItems(unlocked, i =>
            {
                int global = unlocked - 1 - i;
                var plan = acts.Plan(acts.StartOf(global, 0));
                string marker = global == _selected ? "▶ " : string.Empty;
                return $"{marker}{acts.DungeonLabel(global)}  <size=17><color=#8090A8>Lv.{plan.Level}~ · 층당 {plan.TotalKills}마리~</color></size>";
            });

            var bag = _ctx.Meta.Profile.Bag;
            Panel.SetDetail($"선택: <b>{acts.DungeonLabel(_selected)}</b>\n" +
                            $"5층 구성, 마지막 층은 보스. 층마다 결과창에서 계속 진행 / 마을 귀환을 고릅니다.\n" +
                            $"<color=#FF8A6A>가방 {bag.Items.Count}개 ({bag.UsedCells}/{bag.Capacity}칸) — 쓰러지면 절반 손실</color>\n" +
                            $"<size=18><color=#8090A8>보스 층을 깨면 다음 던전이 열립니다. (개방 {unlocked}/{acts.TotalDungeons()})</color></size>");
            Panel.SetFooter(0, "입장", () => _ctx.Modes.EnterDungeon(_selected));
            Panel.SetFooter(1, string.Empty, null, false);
            Panel.SetFooter(2, "닫기", () => Panel.SetVisible(false));
        }

        void OnRow(int row)
        {
            int unlocked = _ctx.Meta.Profile.UnlockedDungeons;
            _selected = unlocked - 1 - row;
            Refresh();
        }
    }
}
