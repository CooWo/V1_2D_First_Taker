using ShadowRPG.Core.Items;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>칸 가방: 아이템을 눌러 선택 → 장착 / 판매 / 강화. 정리 버튼으로 빈 칸을 모읍니다.</summary>
    public sealed class BagMenu : IMenu
    {
        const float Cell = 78f;

        readonly MenuContext _ctx;
        readonly GridView _grid;
        readonly UnityEngine.UI.Text _detail;
        Item _selected;

        public BagMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "BagMenu", 0);
            var bag = ctx.Meta.Profile.Bag;
            _grid = new GridView(Panel.DetailArea, "BagGrid", new Vector2(0.5f, 1f), Vector2.zero, bag.Width, bag.Height, Cell);
            _grid.ItemClicked += item => { _selected = item; Refresh(); };
            _detail = UiFactory.Label(UiFactory.Anchored(Panel.DetailArea, "ItemDetail", new Vector2(0f, 1f), new Vector2(0f, -(bag.Height * Cell + 12f)),
                new Vector2(Panel.DetailArea.sizeDelta.x, Panel.DetailArea.sizeDelta.y - bag.Height * Cell - 12f)), string.Empty, 19, TextAnchor.UpperLeft, Color.white);
        }

        public string ButtonLabel => "가방";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var player = _ctx.Session.Player;
            var bag = player.Inventory;
            if (_selected != null && !bag.TryGetPlacement(_selected, out _)) _selected = null;

            Panel.SetTitle($"<b>가방</b> {bag.UsedCells}/{bag.Capacity}칸  <size=22><color=#FFD84A>{_ctx.Meta.Wallet.Gold:N0} G</color></size>");
            Panel.SetItems(0, _ => string.Empty);
            _grid.Refresh(bag, _selected);

            if (_selected == null)
            {
                _detail.text = "<color=#8090A8>칸을 눌러 장비를 선택하세요. 큰 장비일수록 칸을 많이 차지합니다.\n던전에서 쓰러지면 가방 아이템의 절반을 잃습니다 — 귀중품은 마을 창고에.</color>";
                Panel.SetFooter(0, "정리 (빈 칸 모으기)", () => { bag.Compact(); Refresh(); });
                Panel.SetFooter(1, "판매", null, false);
                Panel.SetFooter(2, "강화", null, false);
                return;
            }

            var current = player.Equipment.Get(TargetSlot(_selected));
            var size = ItemShapes.SizeOf(_selected);
            _detail.text = ItemFormatter.Describe(_selected, _ctx.Meta.Content) + $"<size=17><color=#8090A8>{size.x}x{size.y}칸</color></size>" +
                           (current != null ? $"\n<size=18><color=#8090A8>현재 착용: {ItemFormatter.ColoredName(current)}</color></size>" : string.Empty);
            Panel.SetFooter(0, "장착", Equip);
            Panel.SetFooter(1, $"판매 +{_ctx.Meta.Pricing.SellValue(_selected, player.Combatant.Stats):N0}G", Sell);
            Panel.SetFooter(2, _selected.EnhanceLevel < Item.MaxEnhanceLevel ? $"강화 {_ctx.Meta.Enhancement.Cost(_selected):N0}G" : "최대 강화", Enhance);
        }

        EquipSlot TargetSlot(Item item)
        {
            if (!SlotRules.IsRing(item.Slot)) return item.Slot;
            var equipment = _ctx.Session.Player.Equipment;
            if (equipment.Get(EquipSlot.Ring1) == null) return EquipSlot.Ring1;
            if (equipment.Get(EquipSlot.Ring2) == null) return EquipSlot.Ring2;
            return _ctx.Meta.Scorer.Score(equipment.Get(EquipSlot.Ring1)) <= _ctx.Meta.Scorer.Score(equipment.Get(EquipSlot.Ring2)) ? EquipSlot.Ring1 : EquipSlot.Ring2;
        }

        void Equip()
        {
            var result = _ctx.Meta.Equip(_ctx.Session.Player, _selected, TargetSlot(_selected));
            _ctx.Log(result.Success ? $"장착: {_selected.DisplayName}" : $"장착 실패: {result.Reason}");
            _selected = null;
            Refresh();
        }

        void Sell()
        {
            long gold = _ctx.Meta.Sell(_selected, _ctx.Session.Player);
            _ctx.Log($"판매: {_selected.DisplayName} +{gold:N0}G");
            _selected = null;
            Refresh();
        }

        void Enhance()
        {
            _ctx.Log(_ctx.Meta.Enhance(_ctx.Session.Player, _selected).Message);
            Refresh();
        }
    }
}
