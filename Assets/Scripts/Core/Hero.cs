using System.Collections.Generic;

namespace LootHunters
{
    /// <summary>
    /// 영웅 데이터. carriedLoot = 이번 던전에서 들고 있는 전리품 (죽으면 일부 손실),
    /// stash = 마을 창고에 안전하게 보관된 아이템.
    /// </summary>
    public class Hero
    {
        public Stats baseStats;
        public ItemInstance weapon;
        public ItemInstance armor;
        public int gold;

        public readonly List<ItemInstance> carriedLoot = new List<ItemInstance>();
        public readonly List<ItemInstance> stash = new List<ItemInstance>();
        public int carriedGold;

        // 소모품 보유 수 (죽어도 잃지 않음)
        public int potions;
        public int scrolls;

        /// <summary>레벨, 경험치, 스탯 포인트.</summary>
        public readonly HeroProgress progress = new HeroProgress();

        public Combatant Combatant { get; private set; }

        public Hero(string name, Stats baseStats)
        {
            this.baseStats = baseStats;
            Combatant = new Combatant(name, TotalStats);
        }

        public Stats TotalStats
        {
            get
            {
                var s = baseStats;
                if (progress != null) s += progress.Bonus;
                if (weapon != null) s += weapon.Bonus;
                if (armor != null) s += armor.Bonus;
                return s;
            }
        }

        public int Power => TotalStats.Power;

        public void RefreshStats(bool fullHeal) => Combatant.SetStats(TotalStats, fullHeal);

        public ItemInstance GetEquipped(ItemSlot slot)
            => slot == ItemSlot.Weapon ? weapon : slot == ItemSlot.Armor ? armor : null;

        /// <summary>창고의 장비를 장착. 원래 끼고 있던 장비는 창고로 돌아갑니다.</summary>
        public bool Equip(ItemInstance item)
        {
            if (item == null || !item.IsEquipment || !stash.Remove(item)) return false;

            var old = GetEquipped(item.Slot);
            if (old != null) stash.Add(old);

            if (item.Slot == ItemSlot.Weapon) weapon = item;
            else armor = item;

            RefreshStats(fullHeal: true);
            return true;
        }

        /// <summary>창고에서 슬롯별 전투력이 가장 높은 장비를 장착.</summary>
        public int EquipBestFromStash()
        {
            int changed = 0;
            foreach (var slot in new[] { ItemSlot.Weapon, ItemSlot.Armor })
            {
                ItemInstance best = null;
                foreach (var item in stash)
                    if (item.Slot == slot && (best == null || item.Power > best.Power))
                        best = item;

                var current = GetEquipped(slot);
                if (best != null && (current == null || best.Power > current.Power) && Equip(best))
                    changed++;
            }
            RefreshStats(fullHeal: true);
            return changed;
        }
    }
}
