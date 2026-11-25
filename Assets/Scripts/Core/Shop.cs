namespace LootHunters
{
    /// <summary>마을 기능: 판매와 대장간 강화. 상태 확인과 로그는 DungeonRun이 담당합니다.</summary>
    public static class Shop
    {
        public static bool Sell(Hero hero, ItemInstance item)
        {
            if (item == null || !hero.stash.Remove(item)) return false;
            hero.gold += item.SellPrice;
            return true;
        }

        /// <summary>
        /// 잡동사니 일괄 판매: 재료/보물 전부 + 지금 장착한 것보다 약하거나 같은 장비.
        /// 단, 합성 재료가 되는 장비(mergeResult가 있는 것)는 남겨 둡니다.
        /// </summary>
        public static (int count, int gold) SellJunk(Hero hero)
        {
            int count = 0, gold = 0;
            for (int i = hero.stash.Count - 1; i >= 0; i--)
            {
                var item = hero.stash[i];
                bool junk;
                if (!item.IsEquipment)
                {
                    junk = true;
                }
                else
                {
                    var equipped = hero.GetEquipped(item.Slot);
                    junk = item.Data.mergeResult == null && equipped != null && item.Power <= equipped.Power;
                }

                if (!junk) continue;
                hero.stash.RemoveAt(i);
                hero.gold += item.SellPrice;
                gold += item.SellPrice;
                count++;
            }
            return (count, gold);
        }

        public static bool Upgrade(Hero hero, ItemInstance item)
        {
            if (item == null || !item.CanUpgrade || hero.gold < item.UpgradeCost) return false;
            hero.gold -= item.UpgradeCost;
            item.Upgrade();
            hero.RefreshStats(fullHeal: true);
            return true;
        }
    }
}
