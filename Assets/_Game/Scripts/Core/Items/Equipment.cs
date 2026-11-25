using System;
using System.Collections.Generic;
using ShadowRPG.Core.Stats;

namespace ShadowRPG.Core.Items
{
    public readonly struct EquipResult
    {
        public readonly bool Success;
        public readonly string Reason;
        public readonly Item Replaced;

        public EquipResult(bool success, string reason, Item replaced)
        {
            Success = success;
            Reason = reason;
            Replaced = replaced;
        }

        public static EquipResult Fail(string reason) => new EquipResult(false, reason, null);
    }

    /// <summary>장착 가능 여부 검사 규칙. 새 제약은 검사기를 추가해 주입합니다 (OCP).</summary>
    public interface IEquipValidator
    {
        bool Validate(Item item, EquipSlot slot, IStatReader stats, out string reason);
    }

    public sealed class SlotCompatibilityValidator : IEquipValidator
    {
        public bool Validate(Item item, EquipSlot slot, IStatReader stats, out string reason)
        {
            reason = SlotRules.CanPlace(item.Slot, slot) ? null : $"{SlotRules.DisplayName(slot)} 칸에 낄 수 없는 장비입니다.";
            return reason == null;
        }
    }

    /// <summary>GDD: 오직 요구 스탯(힘/민첩/지능)으로만 착용 가능 여부를 판정합니다.</summary>
    public sealed class StatRequirementValidator : IEquipValidator
    {
        public bool Validate(Item item, EquipSlot slot, IStatReader stats, out string reason)
        {
            reason = item.Requirements.IsMetBy(stats) ? null : $"요구 능력치 부족 ({item.Requirements})";
            return reason == null;
        }
    }

    /// <summary>9슬롯 장비 보관함. 장착 규칙 검사와 변경 통지만 책임집니다 (SRP).</summary>
    public sealed class Equipment
    {
        readonly Dictionary<EquipSlot, Item> _slots = new Dictionary<EquipSlot, Item>();
        readonly List<IEquipValidator> _validators;

        /// <summary>(슬롯, 이전 장비, 새 장비)</summary>
        public event Action<EquipSlot, Item, Item> Changed;

        public Equipment(IEnumerable<IEquipValidator> validators)
        {
            _validators = new List<IEquipValidator>(validators);
        }

        public Item Get(EquipSlot slot) => _slots.TryGetValue(slot, out var item) ? item : null;

        public IEnumerable<KeyValuePair<EquipSlot, Item>> Equipped => _slots;

        public bool CanEquip(Item item, EquipSlot slot, IStatReader stats, out string reason)
        {
            foreach (var validator in _validators)
                if (!validator.Validate(item, slot, stats, out reason)) return false;
            reason = null;
            return true;
        }

        public EquipResult TryEquip(Item item, EquipSlot slot, IStatReader stats)
        {
            if (item == null) return EquipResult.Fail("장비가 없습니다.");
            if (!CanEquip(item, slot, stats, out var reason)) return EquipResult.Fail(reason);

            var previous = Get(slot);
            _slots[slot] = item;
            Changed?.Invoke(slot, previous, item);
            return new EquipResult(true, null, previous);
        }

        /// <summary>검사 없이 장착 (시작 장비 지급용).</summary>
        public void ForceEquip(Item item, EquipSlot slot)
        {
            var previous = Get(slot);
            _slots[slot] = item;
            Changed?.Invoke(slot, previous, item);
        }

        public Item Unequip(EquipSlot slot)
        {
            var previous = Get(slot);
            if (previous == null) return null;
            _slots.Remove(slot);
            Changed?.Invoke(slot, previous, null);
            return previous;
        }
    }
}
