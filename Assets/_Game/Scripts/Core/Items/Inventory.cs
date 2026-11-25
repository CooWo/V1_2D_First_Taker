using System;
using System.Collections.Generic;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;

namespace ShadowRPG.Core.Items
{
    public sealed class Inventory
    {
        readonly List<Item> _items = new List<Item>();

        public Inventory(int capacity) => Capacity = capacity;

        /// <summary>가방 크기 (한정). 가득 차면 더 들고 갈 수 없습니다.</summary>
        public int Capacity { get; }
        public IReadOnlyList<Item> Items => _items;
        public bool IsFull => _items.Count >= Capacity;
        public int FreeSlots => Capacity - _items.Count;

        public event Action Changed;

        public bool TryAdd(Item item)
        {
            if (item == null || IsFull) return false;
            _items.Add(item);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(Item item)
        {
            bool removed = _items.Remove(item);
            if (removed) Changed?.Invoke();
            return removed;
        }
    }

    /// <summary>장비 가치 평가 (자동 장착·인벤토리 정리용).</summary>
    public interface IItemScorer
    {
        float Score(Item item);
    }

    public sealed class ItemScorer : IItemScorer
    {
        public float Score(Item item)
        {
            if (item == null) return 0f;
            float score = item.ItemLevel * 2f + (int)item.Rarity * 12f;
            foreach (var m in item.AllModifiers) score += Weight(m.Stat) * m.Value;
            if (item.GrantedSkill.IsValid) score += 10f + (int)item.GrantedSkill.Rarity * 10f;
            if (item.SupportCode != null) score += 15f;
            if (item.SetId != null) score += 10f;
            if (item.ReducesSetRequirement) score += 20f;
            return score;
        }

        static float Weight(StatType stat)
        {
            switch (stat)
            {
                case StatType.WeaponDamage: return 2f;
                case StatType.MaxLife: return 0.15f;
                case StatType.Armor: return 0.05f;
                case StatType.EnergyShield: return 0.1f;
                case StatType.Strength:
                case StatType.Dexterity:
                case StatType.Intelligence: return 0.6f;
                case StatType.ExtraProjectiles:
                case StatType.ExtraChains:
                case StatType.ExtraPierce: return 12f;
                default: return 0.5f;
            }
        }
    }

    /// <summary>새 장비가 현재보다 좋으면 자동 장착합니다 (자동 사냥 편의).</summary>
    public sealed class AutoEquipPolicy
    {
        readonly IItemScorer _scorer;

        public AutoEquipPolicy(IItemScorer scorer) => _scorer = scorer;

        /// <summary>장착했다면 true. 밀려난 장비는 replaced로 돌려줍니다.</summary>
        public bool TryAutoEquip(Equipment equipment, Item item, IStatReader stats, out EquipSlot slot, out Item replaced)
        {
            replaced = null;
            slot = ChooseSlot(equipment, item);
            var current = equipment.Get(slot);
            if (current != null && _scorer.Score(current) >= _scorer.Score(item)) return false;
            if (!equipment.CanEquip(item, slot, stats, out _)) return false;

            var result = equipment.TryEquip(item, slot, stats);
            replaced = result.Replaced;
            return result.Success;
        }

        EquipSlot ChooseSlot(Equipment equipment, Item item)
        {
            if (!SlotRules.IsRing(item.Slot)) return item.Slot;
            var ring1 = equipment.Get(EquipSlot.Ring1);
            var ring2 = equipment.Get(EquipSlot.Ring2);
            if (ring1 == null) return EquipSlot.Ring1;
            if (ring2 == null) return EquipSlot.Ring2;
            return _scorer.Score(ring1) <= _scorer.Score(ring2) ? EquipSlot.Ring1 : EquipSlot.Ring2;
        }
    }

    public static class ItemRarityExtensions
    {
        public static SkillRarity ToSkillRarity(this ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return SkillRarity.Magic;
                case ItemRarity.Rare:
                case ItemRarity.Set: return SkillRarity.Rare;
                case ItemRarity.Legendary: return SkillRarity.Legendary;
                default: return SkillRarity.Normal;
            }
        }

        public static string DisplayName(this ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return "마법";
                case ItemRarity.Rare: return "희귀";
                case ItemRarity.Legendary: return "전설";
                case ItemRarity.Set: return "세트";
                default: return "일반";
            }
        }
    }
}
