using ShadowRPG.Core.Items;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>마을 창고: 창고(안전)와 가방(던전에 들고 감) 사이 이동, 판매, 장착.</summary>
    public sealed class StashMenu : IMenu
    {
        const float Cell = 50f;

        readonly MenuContext _ctx;
        readonly GridView _stashGrid;
        readonly GridView _bagGrid;
        readonly UnityEngine.UI.Text _detail;
        Item _selected;

        public StashMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "StashMenu", 0);
            var stash = ctx.Meta.Profile.Stash;
            var bag = ctx.Meta.Profile.Bag;
            var area = Panel.DetailArea;

            UiFactory.Label(UiFactory.Anchored(area, "StashLabel", new Vector2(0f, 1f), Vector2.zero, new Vector2(stash.Width * Cell, 26f)), "창고 (안전)", 19, TextAnchor.MiddleLeft, new Color(0.7f, 0.85f, 1f));
            _stashGrid = new GridView(area, "StashGrid", new Vector2(0f, 1f), new Vector2(0f, -28f), stash.Width, stash.Height, Cell);
            float bagTop = -(28f + stash.Height * Cell + 12f);
            UiFactory.Label(UiFactory.Anchored(area, "BagLabel", new Vector2(0f, 1f), new Vector2(0f, bagTop), new Vector2(bag.Width * Cell, 26f)), "가방 (던전 휴대)", 19, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.6f));
            _bagGrid = new GridView(area, "BagGrid", new Vector2(0f, 1f), new Vector2(0f, bagTop - 28f), bag.Width, bag.Height, Cell);

            _detail = UiFactory.Label(UiFactory.Anchored(area, "Detail", new Vector2(1f, 1f), new Vector2(0f, bagTop - 28f),
                new Vector2(area.sizeDelta.x - bag.Width * Cell - 16f, bag.Height * Cell)), string.Empty, 17, TextAnchor.UpperLeft, Color.white);

            _stashGrid.ItemClicked += item => { _selected = item; Refresh(); };
            _bagGrid.ItemClicked += item => { _selected = item; Refresh(); };
        }

        public string ButtonLabel => "창고";
        public ListPanel Panel { get; }

        bool InStash(Item item) => _ctx.Meta.Profile.Stash.TryGetPlacement(item, out _);

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var profile = _ctx.Meta.Profile;
            if (_selected != null && !InStash(_selected) && !profile.Bag.TryGetPlacement(_selected, out _)) _selected = null;

            Panel.SetTitle($"<b>창고</b> {profile.Stash.UsedCells}/{profile.Stash.Capacity}칸  <size=22><color=#FFD84A>{_ctx.Meta.Wallet.Gold:N0} G</color></size>");
            Panel.SetItems(0, _ => string.Empty);
            _stashGrid.Refresh(profile.Stash, _selected);
            _bagGrid.Refresh(profile.Bag, _selected);

            if (_selected == null)
            {
                _detail.text = "<color=#8090A8>장비를 눌러 선택한 뒤 이동하세요.\n가방 안 장비는 던전에서 쓰러지면 절반을 잃습니다.</color>";
                Panel.SetFooter(0, "가방 → 창고 전부", MoveAllToStash);
                Panel.SetFooter(1, "창고 정리", () => { profile.Stash.Compact(); Refresh(); });
                Panel.SetFooter(2, "가방 정리", () => { profile.Bag.Compact(); Refresh(); });
                return;
            }

            bool inStash = InStash(_selected);
            _detail.text = ItemFormatter.Describe(_selected, _ctx.Meta.Content);
            Panel.SetFooter(0, inStash ? "가방으로" : "창고로", Move);
            Panel.SetFooter(1, $"판매 +{_ctx.Meta.Pricing.SellValue(_selected, _ctx.Session.Player.Combatant.Stats):N0}G", Sell);
            Panel.SetFooter(2, "장착", Equip);
        }

        void Move()
        {
            bool ok = InStash(_selected) ? _ctx.Meta.MoveToBag(_selected) : _ctx.Meta.MoveToStash(_selected);
            if (!ok) _ctx.Log("칸이 부족합니다.");
            Refresh();
        }

        void MoveAllToStash()
        {
            int moved = 0;
            foreach (var item in new System.Collections.Generic.List<Item>(_ctx.Meta.Profile.Bag.Items))
                if (_ctx.Meta.MoveToStash(item)) moved++;
            _ctx.Log($"창고로 {moved}개 이동");
            Refresh();
        }

        void Sell()
        {
            var item = _selected;
            long gold = InStash(item) ? _ctx.Meta.SellFromStash(item, _ctx.Session.Player) : _ctx.Meta.Sell(item, _ctx.Session.Player);
            _ctx.Log($"판매: {item.DisplayName} +{gold:N0}G");
            _selected = null;
            Refresh();
        }

        void Equip()
        {
            var item = _selected;
            var slot = SlotRules.IsRing(item.Slot) && _ctx.Session.Player.Equipment.Get(EquipSlot.Ring1) != null ? EquipSlot.Ring2 : item.Slot;
            var result = _ctx.Meta.Equip(_ctx.Session.Player, item, slot);
            _ctx.Log(result.Success ? $"장착: {item.DisplayName}" : $"장착 실패: {result.Reason}");
            _selected = null;
            Refresh();
        }
    }
}
