using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Economy
{
    /// <summary>골드 지갑. 판매(폐지 = 돈)로 벌고 강화·트리·펫에 씁니다.</summary>
    public sealed class Wallet
    {
        public long Gold { get; private set; }

        public event Action<long> Changed;

        public Wallet(long gold = 0) => Gold = gold;

        public void Earn(long amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke(Gold);
        }

        public bool CanAfford(long cost) => Gold >= cost;

        public bool TrySpend(long cost)
        {
            if (cost < 0 || Gold < cost) return false;
            Gold -= cost;
            Changed?.Invoke(Gold);
            return true;
        }
    }

    /// <summary>장비 판매가 규칙.</summary>
    public interface IItemPricing
    {
        long SellValue(Item item, IStatReader seller);
    }

    public sealed class ItemPricing : IItemPricing
    {
        public long SellValue(Item item, IStatReader seller)
        {
            if (item == null) return 0;
            float baseValue;
            switch (item.Rarity)
            {
                case ItemRarity.Magic: baseValue = 15f; break;
                case ItemRarity.Rare: baseValue = 45f; break;
                case ItemRarity.Legendary: baseValue = 150f; break;
                case ItemRarity.Set: baseValue = 120f; break;
                default: baseValue = 5f; break;
            }
            float level = 1f + item.ItemLevel * 0.15f;
            float enhance = 1f + item.EnhanceLevel * 0.25f;
            float goldFind = 1f + (seller?.Get(StatType.GoldFindPct) ?? 0f) / 100f;
            return (long)Mathf.Round(baseValue * level * enhance * goldFind);
        }
    }

    public readonly struct EnhanceResult
    {
        public readonly bool Success;
        public readonly string Message;

        public EnhanceResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }

    /// <summary>장비 강화: 단계가 오를수록 비용이 계속 증가합니다.</summary>
    public sealed class EnhancementService
    {
        public long Cost(Item item)
        {
            float rarity;
            switch (item.Rarity)
            {
                case ItemRarity.Magic: rarity = 1.5f; break;
                case ItemRarity.Rare: rarity = 2.5f; break;
                case ItemRarity.Legendary: rarity = 4f; break;
                case ItemRarity.Set: rarity = 3.5f; break;
                default: rarity = 1f; break;
            }
            return (long)Mathf.Round(30f * Mathf.Pow(1.32f, item.EnhanceLevel) * rarity * (1f + item.ItemLevel * 0.1f));
        }

        public EnhanceResult TryEnhance(Item item, Wallet wallet, Equipment equipment)
        {
            if (item == null) return new EnhanceResult(false, "장비를 선택하세요.");
            if (item.EnhanceLevel >= Item.MaxEnhanceLevel) return new EnhanceResult(false, "최대 강화 단계입니다.");
            long cost = Cost(item);
            if (!wallet.TrySpend(cost)) return new EnhanceResult(false, $"골드 부족 (필요 {cost:N0})");

            item.EnhanceLevel++;
            equipment?.NotifyItemChanged(item);
            return new EnhanceResult(true, $"{item.DisplayName} 강화 성공 (-{cost:N0}G)");
        }
    }

    /// <summary>
    /// 한 층에서 주운 장비. 층을 클리어하면 반출 개수(KeepLimit)와 가방 여유만큼만 가져가고,
    /// 나머지는 자동 판매되어 골드가 됩니다.
    /// </summary>
    public sealed class FloorLoot
    {
        readonly List<Item> _found = new List<Item>();
        readonly HashSet<Item> _selected = new HashSet<Item>();

        public FloorLoot(int keepLimit) => KeepLimit = keepLimit;

        public int KeepLimit { get; }
        public IReadOnlyList<Item> Found => _found;
        public IReadOnlyCollection<Item> Selected => _selected;

        public event Action Changed;

        public void Add(Item item)
        {
            _found.Add(item);
            Changed?.Invoke();
        }

        public void Clear()
        {
            _found.Clear();
            _selected.Clear();
            Changed?.Invoke();
        }

        public bool IsSelected(Item item) => _selected.Contains(item);

        public int AllowedPicks(int bagFreeSlots) => Mathf.Max(0, Mathf.Min(KeepLimit, bagFreeSlots));

        /// <summary>선택 토글. 반출 한도를 넘으면 false.</summary>
        public bool Toggle(Item item, int bagFreeSlots)
        {
            if (!_found.Contains(item)) return false;
            if (_selected.Remove(item))
            {
                Changed?.Invoke();
                return true;
            }
            if (_selected.Count >= AllowedPicks(bagFreeSlots)) return false;
            _selected.Add(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>자동 선택: 가치가 높은 순서로 한도까지.</summary>
        public void AutoSelect(IItemScorer scorer, int bagFreeSlots)
        {
            _selected.Clear();
            foreach (var item in _found.OrderByDescending(scorer.Score).Take(AllowedPicks(bagFreeSlots)))
                _selected.Add(item);
            Changed?.Invoke();
        }

        public long PreviewSaleGold(IItemPricing pricing, IStatReader seller)
            => _found.Where(i => !_selected.Contains(i)).Sum(i => pricing.SellValue(i, seller));

        /// <summary>선택한 장비는 가방으로, 나머지는 판매. (반출 수, 판매 골드)</summary>
        public (int kept, long gold) Resolve(Inventory bag, Wallet wallet, IItemPricing pricing, IStatReader seller)
        {
            int kept = 0;
            long gold = 0;
            foreach (var item in _found)
            {
                if (_selected.Contains(item) && bag.TryAdd(item))
                {
                    kept++;
                    continue;
                }
                gold += pricing.SellValue(item, seller);
            }
            wallet.Earn(gold);
            Clear();
            return (kept, gold);
        }
    }
}
