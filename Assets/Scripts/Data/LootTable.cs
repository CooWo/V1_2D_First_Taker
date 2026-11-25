
using System.Collections.Generic;
using UnityEngine;

namespace LootHunters
{
    [System.Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Min(1)] public int weight = 1;
    }

    /// <summary>몬스터 처치 시 떨어지는 아이템 확률표 (가중치 방식).</summary>
    [CreateAssetMenu(menuName = "LootHunters/Loot Table", fileName = "Loot_")]
    public class LootTable : ScriptableObject
    {
        [Range(0f, 1f)] public float dropChance = 0.6f;
        [Min(1)] public int rolls = 1;
        public List<LootEntry> entries = new List<LootEntry>();

        public List<ItemData> Roll()
        {
            var result = new List<ItemData>();
            int totalWeight = 0;
            foreach (var e in entries)
                if (e.item != null) totalWeight += e.weight;
            if (totalWeight == 0) return result;

            for (int r = 0; r < rolls; r++)
            {
                if (UnityEngine.Random.value > dropChance) continue;

                int pick = UnityEngine.Random.Range(0, totalWeight);
                foreach (var e in entries)
                {
                    if (e.item == null) continue;
                    pick -= e.weight;
                    if (pick < 0) { result.Add(e.item); break; }
                }
            }
            return result;
        }
    }
}
