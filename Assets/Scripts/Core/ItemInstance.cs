using UnityEngine;

namespace LootHunters
{
    /// <summary>
    /// 실제로 가진 아이템 한 개. ItemData는 종류(설계도)이고, 강화 단계처럼
    /// 아이템마다 달라지는 값은 여기에 저장합니다. 3단계 합성도 이 클래스를 씁니다.
    /// </summary>
    public class ItemInstance
    {
        public const int MaxLevel = 10;

        public ItemData Data { get; }
        public int Level { get; private set; }

        public ItemInstance(ItemData data, int level = 0)
        {
            Data = data;
            Level = Mathf.Clamp(level, 0, MaxLevel);
        }

        public ItemSlot Slot => Data.slot;
        public Rarity Rarity => Data.rarity;
        public bool IsEquipment => Data.IsEquipment;
        public bool CanUpgrade => IsEquipment && Level < MaxLevel;

        /// <summary>강화 1단계마다 기본 수치의 25%(최소 1)씩 오릅니다.</summary>
        public Stats BonusAt(int level)
        {
            var b = Data.bonus;
            return new Stats(
                b.maxHp + PerLevel(b.maxHp) * level,
                b.attack + PerLevel(b.attack) * level,
                b.defense + PerLevel(b.defense) * level);
        }

        public Stats Bonus => BonusAt(Level);
        public Stats NextBonus => BonusAt(Mathf.Min(Level + 1, MaxLevel));
        public int Power => Bonus.Power;

        /// <summary>강화할수록 비싸게 팔립니다 (단계당 +50%).</summary>
        public int SellPrice => Mathf.RoundToInt(Data.sellPrice * (1f + Level * 0.5f));

        /// <summary>강화 비용. 비싼 아이템일수록, 단계가 높을수록 비쌉니다.</summary>
        public int UpgradeCost => (10 + Data.sellPrice * 2) * (Level + 1);

        public void Upgrade()
        {
            if (CanUpgrade) Level++;
        }

        public string DisplayName => Level > 0 ? $"{Data.displayName} +{Level}" : Data.displayName;

        public override string ToString() => $"[{Data.rarity}] {DisplayName}";

        static int PerLevel(int baseValue)
            => baseValue <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(baseValue * 0.25f));
    }
}
