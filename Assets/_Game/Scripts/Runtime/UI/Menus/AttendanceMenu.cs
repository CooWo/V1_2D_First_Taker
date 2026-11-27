using System.Text;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>출석: 28일 출석판 + 신규 7일 출석.</summary>
    public sealed class AttendanceMenu : IMenu
    {
        readonly MenuContext _ctx;

        public AttendanceMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "AttendanceMenu", 0);
        }

        public string ButtonLabel => "출석";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var a = _ctx.Meta.Attendance;
            Panel.SetTitle($"<b>출석</b>  <size=20>{a.CycleDay}/{a.CycleDays}일 · 신규 {a.NewbieDay}/7일</size>");
            Panel.SetItems(0, _ => string.Empty);
            var sb = new StringBuilder();
            sb.Append($"<size=20>매일 골드 {a.DailyGold(_ctx.Meta.ActsOpened):N0}</size>\n\n");
            for (int day = 1; day <= a.CycleDays; day++)
            {
                var special = a.SpecialFor(day);
                bool claimed = day <= a.CycleDay;
                string label = special != null ? $"{day}일 템포{special.Tempo}" : $"{day}일";
                if (claimed) sb.Append($"<color=#506070>{label}✓</color>");
                else if (special != null) sb.Append($"<color=#FFD84A>{label}</color>");
                else sb.Append(label);
                sb.Append(day % 7 == 0 ? "\n" : "   ");
            }
            sb.Append("\n<size=19>신규 7일: ");
            for (int day = 1; day <= 7; day++)
            {
                var n = a.NewbieFor(day);
                if (n == null) continue;
                string reward = n.Gold > 0 ? $"골드{n.Gold:N0}" : $"템포{n.Tempo}";
                sb.Append(day <= a.NewbieDay ? $"<color=#506070>{day}일✓</color> " : $"{day}일 {reward} ");
            }
            sb.Append("</size>");
            Panel.SetDetail(sb.ToString());
            Panel.SetFooter(0, a.CanClaimToday ? "오늘 출석 받기" : "내일 다시", () =>
            {
                var result = a.Claim(_ctx.Meta.Wallet, _ctx.Meta.State, _ctx.Meta.ActsOpened);
                _ctx.Log(result ?? "오늘은 이미 출석했습니다.");
                _ctx.Meta.RequestSave();
                Refresh();
            });
            Panel.SetFooter(1, string.Empty, null, false);
            Panel.SetFooter(2, "닫기", () => Panel.SetVisible(false));
        }
    }
}
