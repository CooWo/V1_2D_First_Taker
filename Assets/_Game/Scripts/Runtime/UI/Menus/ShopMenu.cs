using System.Collections.Generic;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Tuning;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>
    /// 상점: 템포 상점(현금) · 패키지 · 편의(템포) · 골드 상점(매일 바뀌는 장비).
    /// 원칙: 전투력을 직접 파는 상품은 지양. 현금 결제는 스토어 연동 전까지 "준비 중".
    /// </summary>
    public sealed class ShopMenu : IMenu
    {
        static readonly string[] Tabs = { "템포", "패키지", "편의", "골드 상점" };
        readonly MenuContext _ctx;
        int _tab;

        public ShopMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "ShopMenu", 7);
            Panel.RowClicked += OnRow;
        }

        public string ButtonLabel => "상점";
        public ListPanel Panel { get; }

        ShopTuning Catalog => _ctx.Meta.Shop.Catalog;

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var meta = _ctx.Meta;
            Panel.SetTitle($"<b>상점 · {Tabs[_tab]}</b>  <size=20><color=#FFD84A>{meta.Wallet.Gold:N0}G</color> · 템포 {meta.State.Tempo:N0}</size>");
            switch (_tab)
            {
                case 0: ShowProducts(Catalog.TempoPacks); break;
                case 1: ShowProducts(Catalog.Packages); break;
                case 2: ShowProducts(Catalog.Convenience); break;
                default: ShowGoldShop(); break;
            }
            Panel.SetDetail("<size=18><color=#8090A8>전투력을 직접 파는 상품은 두지 않습니다 — 편의·꾸미기·시간 단축 위주.</color></size>");
            Panel.SetFooter(0, "◀ " + Tabs[(_tab + Tabs.Length - 1) % Tabs.Length], () => { _tab = (_tab + Tabs.Length - 1) % Tabs.Length; Refresh(); });
            Panel.SetFooter(1, Tabs[(_tab + 1) % Tabs.Length] + " ▶", () => { _tab = (_tab + 1) % Tabs.Length; Refresh(); });
            Panel.SetFooter(2, _tab == 3 ? $"새로고침 {Catalog.GoldShopRerollGold:N0}G" : "닫기", () =>
            {
                if (_tab == 3) _ctx.Log(meta.Shop.RerollGoldStock() ?? "골드 상점을 새로 고쳤습니다");
                else Panel.SetVisible(false);
                Refresh();
            });
        }

        void ShowProducts(ShopProduct[] products)
        {
            Panel.SetItems(products.Length, i =>
            {
                var p = products[i];
                string price = p.Currency == "KRW" ? $"₩{p.Price:N0}" : $"템포 {p.Price}";
                return $"<b>{p.Name}</b> <color=#FFD84A>{price}</color> <size=17><color=#8090A8>{p.Note}</color></size>";
            });
        }

        IReadOnlyList<Item> Stock()
        {
            var meta = _ctx.Meta;
            var (act, dungeon) = Core.Dungeon.ActCatalogExtensions.Locate(meta.Content.Acts, meta.Profile.UnlockedDungeons - 1);
            return meta.Shop.GoldStock(meta.Loot, act, dungeon);
        }

        void ShowGoldShop()
        {
            var stock = Stock();
            Panel.SetItems(stock.Count, i =>
            {
                var item = stock[i];
                return $"{ItemFormatter.ColoredName(item)} <size=17><color=#8090A8>전투력 {_ctx.Meta.Power.ItemPower(item):N0}</color> <color=#FFD84A>{_ctx.Meta.Shop.GoldPrice(item, _ctx.Meta.Pricing):N0}G</color></size>";
            });
        }

        void OnRow(int index)
        {
            var meta = _ctx.Meta;
            switch (_tab)
            {
                case 0:
                case 1:
                    var list = _tab == 0 ? Catalog.TempoPacks : Catalog.Packages;
                    if (index < list.Length) meta.Shop.BuyWithMoney(list[index], msg => { _ctx.Log(msg); meta.RequestSave(); });
                    break;
                case 2:
                    if (index < Catalog.Convenience.Length) _ctx.Log(meta.Shop.BuyWithTempo(Catalog.Convenience[index]) ?? $"{Catalog.Convenience[index].Name} 구매 완료");
                    break;
                default:
                    var stock = Stock();
                    if (index < stock.Count)
                    {
                        var item = stock[index];
                        _ctx.Log(meta.Shop.BuyWithGold(item, meta.Pricing) ?? $"{item.DisplayName} 구매 — 창고로");
                    }
                    break;
            }
            meta.RequestSave();
            Refresh();
        }
    }
}
