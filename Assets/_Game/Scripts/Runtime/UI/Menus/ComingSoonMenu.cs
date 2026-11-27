using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>
    /// 서버 연동 또는 기획 확정 후 열리는 메뉴 자리 (PvP·길드·도감·업적·시즌 패스·꾸미기·친구·랭킹·우편함·공지·고객지원).
    /// 메뉴 구조(전체 메뉴 창)는 미리 보여 주고, 내용은 무엇이 정해졌는지만 안내합니다.
    /// </summary>
    public sealed class ComingSoonMenu : IMenu
    {
        readonly string _label;
        readonly string _description;

        public ComingSoonMenu(Transform parent, string label, string description)
        {
            _label = label;
            _description = description;
            Panel = new ListPanel(parent, "Soon_" + label, 0);
        }

        public string ButtonLabel => _label;
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            Panel.SetTitle($"<b>{_label}</b>  <size=20><color=#8090A8>준비 중</color></size>");
            Panel.SetItems(0, _ => string.Empty);
            Panel.SetDetail($"<size=21>{_description}</size>");
            Panel.SetFooter(0, string.Empty, null, false);
            Panel.SetFooter(1, string.Empty, null, false);
            Panel.SetFooter(2, "닫기", () => Panel.SetVisible(false));
        }
    }
}
