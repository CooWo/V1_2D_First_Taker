using System.Collections.Generic;

namespace LootHunters
{
    /// <summary>장비 합성: 창고에 있는 같은 장비 N개 → 상위 장비 1개.</summary>
    public static class Crafting
    {
        /// <summary>창고에 있는 합성 가능한 장비 종류와 개수 (화면 표시용).</summary>
        public static List<(ItemData data, int count)> MergeGroups(Hero hero)
        {
            var order = new List<ItemData>();
            var counts = new Dictionary<ItemData, int>();
            foreach (var item in hero.stash)
            {
                if (item.Data.mergeResult == null) continue;
                if (!counts.ContainsKey(item.Data)) { counts[item.Data] = 0; order.Add(item.Data); }
                counts[item.Data]++;
            }

            var result = new List<(ItemData, int)>();
            foreach (var d in order) result.Add((d, counts[d]));
            return result;
        }

        /// <summary>
        /// 합성 실행. 강화 단계가 낮은 것부터 재료로 쓰고,
        /// 결과물은 재료 중 가장 높았던 강화 단계를 이어받습니다.
        /// </summary>
        public static ItemInstance Merge(Hero hero, ItemData data)
        {
            if (data == null || data.mergeResult == null) return null;

            var candidates = hero.stash.FindAll(i => i.Data == data);
            if (candidates.Count < data.mergeCount) return null;

            candidates.Sort((a, b) => a.Level.CompareTo(b.Level));
            int keepLevel = 0;
            for (int i = 0; i < data.mergeCount; i++)
            {
                keepLevel = System.Math.Max(keepLevel, candidates[i].Level);
                hero.stash.Remove(candidates[i]);
            }

            var made = new ItemInstance(data.mergeResult, keepLevel);
            hero.stash.Add(made);
            return made;
        }
    }
}
