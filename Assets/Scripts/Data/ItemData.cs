using UnityEngine;

namespace LootHunters
{
    public enum ItemSlot { Weapon, Armor, Material, Treasure }
    public enum Rarity { Common, Rare, Epic }

    /// <summary>아이템 한 종류의 정의. Project 창 우클릭 > Create > LootHunters > Item 으로 만듭니다.</summary>
    [CreateAssetMenu(menuName = "LootHunters/Item", fileName = "Item_")]
    public class ItemData : ScriptableObject
    {
        public string displayName = "New Item";
        public ItemSlot slot = ItemSlot.Material;
        public Rarity rarity = Rarity.Common;
        [Tooltip("장착 시 더해지는 스탯 (무기/방어구만 의미 있음)")]
        public Stats bonus;
        [Tooltip("마을에서 팔 때 받는 기본 골드. 강화 비용 계산에도 쓰입니다.")]
        public int sellPrice = 1;

        [Header("합성")]
        [Tooltip("같은 아이템을 mergeCount개 합치면 나오는 상위 아이템. 비워 두면 합성 불가")]
        public ItemData mergeResult;
        [Min(2)] public int mergeCount = 3;

        public bool IsEquipment => slot == ItemSlot.Weapon || slot == ItemSlot.Armor;

        /// <summary>강화 0단계 기준 전투력. 실제 장비 비교는 ItemInstance.Power를 씁니다.</summary>
        public int Power => bonus.Power;

        public override string ToString() => $"[{rarity}] {displayName}";
    }
}
