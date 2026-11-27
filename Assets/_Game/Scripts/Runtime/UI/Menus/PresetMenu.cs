using ShadowRPG.Core.Town;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>세팅 슬롯: 리듬 빌드 ↔ 자동 빌드 전환. 기본 3개, 템포로 최대 6개. 하나를 PvP 세팅으로 지정.</summary>
    public sealed class PresetMenu : IMenu
    {
        readonly MenuContext _ctx;
        int _selected;

        public PresetMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "PresetMenu", 7);
            Panel.RowClicked += OnRow;
        }

        public string ButtonLabel => "세팅 슬롯";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var presets = _ctx.Meta.Presets;
            _selected = Mathf.Clamp(_selected, 0, presets.SlotCount - 1);
            Panel.SetTitle($"<b>세팅 슬롯</b> {presets.SlotCount}/{presets.MaxSlots}  <size=20>템포 {_ctx.Meta.State.Tempo:N0}</size>");
            int rows = presets.SlotCount + (presets.CanExpand ? 1 : 0);
            Panel.SetItems(rows, i =>
            {
                if (i >= presets.SlotCount) return $"<color=#9FD3FF>＋ 슬롯 확장</color> <size=17>(템포 {_ctx.Meta.Tuning.Convenience.PresetSlotTempoPrice})</size>";
                var p = presets.Presets[i];
                string tags = (i == presets.Active ? " <color=#3FE06A>[사용 중]</color>" : string.Empty) + (i == presets.PvpPreset ? " <color=#FF8A6A>[PvP 세팅]</color>" : string.Empty);
                return $"{(i == _selected ? "▶ " : string.Empty)}<b>{p.Name}</b>{tags} <size=17><color=#8090A8>장비 {p.Items.Count}개</color></size>";
            });
            bool inTown = _ctx.Session is TownSession;
            Panel.SetDetail("<size=19>지금 장착한 장비를 슬롯에 저장하고, 마을에서 한 번에 불러옵니다.\n폰을 내려놓을 땐 자동 빌드 세팅으로 바꿔 두세요." +
                            (inTown ? string.Empty : "\n<color=#FF8A6A>불러오기는 마을에서만 가능합니다.</color>") + "</size>");
            Panel.SetFooter(0, "현재 장비 저장", () =>
            {
                presets.SaveCurrent(_selected);
                _ctx.Meta.RequestSave();
                _ctx.Log($"{presets.Presets[_selected].Name}에 저장");
                Refresh();
            });
            Panel.SetFooter(1, "불러오기", Load, inTown);
            Panel.SetFooter(2, _selected == presets.PvpPreset ? "PvP 지정 해제" : "PvP 세팅 지정", () =>
            {
                presets.SetPvp(_selected == presets.PvpPreset ? -1 : _selected);
                _ctx.Meta.RequestSave();
                Refresh();
            });
        }

        void Load()
        {
            var player = _ctx.Session.Player;
            int missing = _ctx.Meta.Presets.Load(_selected, (item, slot) => _ctx.Meta.Equip(player, item, slot).Success);
            _ctx.Meta.Capture(player);
            _ctx.Meta.RequestSave();
            _ctx.Log(missing == 0 ? $"{_ctx.Meta.Presets.Presets[_selected].Name} 불러옴" : $"불러옴 — 찾지 못하거나 착용 불가한 장비 {missing}개");
            Refresh();
        }

        void OnRow(int index)
        {
            var presets = _ctx.Meta.Presets;
            if (index >= presets.SlotCount)
            {
                var error = presets.TryExpand();
                _ctx.Log(error ?? "세팅 슬롯이 늘었습니다");
                _ctx.Meta.RequestSave();
            }
            else _selected = index;
            Refresh();
        }
    }
}
