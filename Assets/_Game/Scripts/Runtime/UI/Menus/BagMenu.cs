using ShadowRPG.Core.Items;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>가방 (한정 크기): 장착 / 판매 / 강화.</summary>
    public sealed class BagMenu : IMenu
    {
        readonly MenuContext _ctx;
        Item _selected;

        public BagMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "BagMenu");
            Panel.RowClicked += i =>
            {
                var items = _ctx.Session.Player.Inventory.Items;
                _selected = i < items.Count ? items[i] : null;
                Refresh();
            };
        }

        public string ButtonLabel => "가방";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var player = _ctx.Session.Player;
            var bag = player.Inventory;
            if (_selected != null && !Contains(bag, _selected)) _selected = null;

            Panel.SetTitle($"<b>가방</b> {bag.Items.Count}/{bag.Capacity}  <size=22><color=#FFD84A>{_ctx.Meta.Wallet.Gold:N0} G</color></size>");
            Panel.SetItems(bag.Items.Count, i =>
            {
                var item = bag.Items[i];
                string marker = item == _selected ? "▶ " : string.Empty;
                return $"{marker}{ItemFormatter.ColoredName(item)} <size=17><color=#8090A8>{SlotRules.DisplayName(item.Slot)} · Lv.{item.ItemLevel}</color> {ItemFormatter.ShortGrant(item, _ctx.Meta.Content)}</size>";
            });

            if (_selected == null)
            {
                Panel.SetDetail("<color=#5A6070>장비를 선택하세요. 가방이 가득 차면 층에서 더 가져갈 수 없습니다.</color>");
                Panel.SetFooter(0, "장착", null, false);
                Panel.SetFooter(1, "판매", null, false);
                Panel.SetFooter(2, "강화", null, false);
                return;
            }

            var current = player.Equipment.Get(TargetSlot(_selected));
            Panel.SetDetail(ItemFormatter.Describe(_selected, _ctx.Meta.Content) +
                            (current != null ? $"\n<size=18><color=#8090A8>현재 착용: {ItemFormatter.ColoredName(current)}</color></size>" : string.Empty));
            Panel.SetFooter(0, "장착", Equip);
            Panel.SetFooter(1, $"판매 +{_ctx.Meta.Pricing.SellValue(_selected, player.Combatant.Stats):N0}G", Sell);
            Panel.SetFooter(2, _selected.EnhanceLevel < Item.MaxEnhanceLevel ? $"강화 {_ctx.Meta.Enhancement.Cost(_selected):N0}G" : "최대 강화", Enhance);
        }

        static bool Contains(Inventory bag, Item item)
        {
            foreach (var i in bag.Items) if (i == item) return true;
            return false;
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
