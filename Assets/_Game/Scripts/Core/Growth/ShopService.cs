using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Tuning;

namespace ShadowRPG.Core.Growth
{
    /// <summary>실제 결제(현금) 처리. 스토어 연동 전에는 "준비 중"을 돌려주는 구현을 씁니다.</summary>
    public interface IStorePurchaser
    {
        /// <summary>결제 요청. 완료 시 콜백 (성공 여부, 메시지).</summary>
        void Purchase(ShopProduct product, Action<bool, string> done);
    }

    public sealed class UnavailableStorePurchaser : IStorePurchaser
    {
        public void Purchase(ShopProduct product, Action<bool, string> done) => done(false, "결제는 스토어 연동 후 열립니다 (준비 중)");
    }

    /// <summary>
    /// 상점 (Menu_Structure §J). 원칙: 전투력을 직접 파는 상품은 지양 — 편의·꾸미기·시간 단축 위주.
    /// 템포(캐시 재화) 상품과 골드 상점(매일 바뀌는 장비)을 처리합니다.
    /// </summary>
    public sealed class ShopService
    {
        readonly GameProfile _profile;
        readonly ShopTuning _tuning;
        readonly ConvenienceTuning _convenience;
        readonly BagTuning _bag;
        readonly IStorePurchaser _store;
        readonly IClock _clock;
        readonly List<Item> _goldStock = new List<Item>();
        string _stockDay;

        public ShopService(GameProfile profile, GameTuning tuning, IStorePurchaser store, IClock clock)
        {
            _profile = profile;
            _tuning = tuning.Shop;
            _convenience = tuning.Convenience;
            _bag = tuning.Bag;
            _store = store;
            _clock = clock;
        }

        public ShopTuning Catalog => _tuning;
        ProgressionState State => _profile.State;

        public bool Speed3Active => State.MonthlyPass || DateTime.UtcNow.Ticks < State.Speed3UntilUtcTicks;

        /// <summary>현금 상품 (템포 묶음·패키지·시즌 패스).</summary>
        public void BuyWithMoney(ShopProduct product, Action<string> report)
        {
            _store.Purchase(product, (ok, message) =>
            {
                if (!ok)
                {
                    report(message);
                    return;
                }
                long tempo = product.Tempo;
                if (product.Id.StartsWith("tempo_") && !State.FirstTempoPurchaseDone)
                {
                    tempo *= 2;
                    State.FirstTempoPurchaseDone = true;
                }
                State.Tempo += tempo;
                _profile.Wallet.Earn(product.Gold);
                if (product.Id == "monthly") State.MonthlyPass = true;
                report($"{product.Name} 구매 완료");
            });
        }

        /// <summary>템포 편의 상품. 반환: 실패 사유 (성공이면 null).</summary>
        public string BuyWithTempo(ShopProduct product)
        {
            if (State.Tempo < product.Price) return $"템포 부족 (필요 {product.Price})";
            switch (product.Id)
            {
                case "preset_slot":
                    if (State.PresetSlots >= _convenience.PresetSlotsMax) return "세팅 슬롯이 최대입니다";
                    State.PresetSlots++;
                    State.Presets.Add(new EquipmentPresetState { Name = $"세팅 {State.Presets.Count + 1}" });
                    break;
                case "stash_rows":
                    if (_bag.StashHeight + State.StashExtraRows + _bag.StashExpandRows > _bag.StashMaxHeight) return "창고가 최대 크기입니다";
                    State.StashExtraRows += _bag.StashExpandRows;
                    break;
                case "speed3_day":
                    long from = Math.Max(DateTime.UtcNow.Ticks, State.Speed3UntilUtcTicks);
                    State.Speed3UntilUtcTicks = from + TimeSpan.FromDays(1).Ticks;
                    break;
                default:
                    return "알 수 없는 상품";
            }
            State.Tempo -= product.Price;
            return null;
        }

        /// <summary>골드 상점: 매일 바뀌는 장비 (현재 ACT 기준). 가격 = 판매가 × 배율.</summary>
        public IReadOnlyList<Item> GoldStock(ILootGenerator loot, int act, int dungeon)
        {
            string today = DailyQuestService.DayKey(_clock.Now);
            if (_stockDay == today && _goldStock.Count > 0) return _goldStock;
            _stockDay = today;
            _goldStock.Clear();
            for (int i = 0; i < _tuning.GoldShopItems; i++)
                _goldStock.Add(loot.Generate(new DropContext(act, dungeon, 7, Actors.EnemyRank.Elite)));
            return _goldStock;
        }

        public long GoldPrice(Item item, IItemPricing pricing) => (long)Math.Round(pricing.SellValue(item, null) * _tuning.GoldShopPriceMultiplier);

        public string BuyWithGold(Item item, IItemPricing pricing)
        {
            if (!_goldStock.Contains(item)) return "품절";
            long price = GoldPrice(item, pricing);
            if (!_profile.Stash.CanFit(item) && !_profile.Bag.CanFit(item)) return "창고·가방 공간 부족";
            if (!_profile.Wallet.TrySpend(price)) return $"골드 부족 (필요 {price:N0})";
            if (!_profile.Stash.TryAdd(item)) _profile.Bag.TryAdd(item);
            _goldStock.Remove(item);
            return null;
        }

        public string RerollGoldStock()
        {
            if (!_profile.Wallet.TrySpend(_tuning.GoldShopRerollGold)) return $"골드 부족 (필요 {_tuning.GoldShopRerollGold:N0})";
            _goldStock.Clear();
            _stockDay = null;
            return null;
        }
    }
}
