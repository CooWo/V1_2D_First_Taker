using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LootHunters
{
    [Serializable]
    public class ItemSave
    {
        public string id;   // ItemData 에셋 이름
        public int level;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int gold;
        public int potions;
        public int scrolls;
        public int selectedIndex;
        public ItemSave weapon;
        public ItemSave armor;
        public List<ItemSave> stash = new List<ItemSave>();
        public int[] bestFloor = new int[0];
        public long savedAtUnix;

        // 레벨 / 스탯 (예전 저장 파일에는 없으면 Lv 1로 시작)
        public int level = 1;
        public int exp;
        public int statPoints;
        public int str;
        public int vit;
        public int agi;
    }

    /// <summary>
    /// JSON 파일 저장/불러오기. 아이템은 ItemData의 에셋 이름(name)으로 저장하므로
    /// 직접 만든 아이템 에셋도 이름이 서로 겹치지 않게 해 주세요.
    /// </summary>
    public static class SaveSystem
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "loothunters_save.json");

        public static void Save(SaveData data)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LootHunters] 저장 실패: {e.Message}");
            }
        }

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(FilePath)) return false;
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LootHunters] 불러오기 실패, 새로 시작합니다: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        public static ItemSave ToSave(ItemInstance item)
            => item == null ? null : new ItemSave { id = item.Data.name, level = item.Level };

        public static ItemInstance FromSave(ItemSave s, Dictionary<string, ItemData> registry)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) return null;
            return registry.TryGetValue(s.id, out var data) ? new ItemInstance(data, s.level) : null;
        }

        /// <summary>던전 → 몬스터 → 드랍 테이블 → 합성 결과까지 따라가며 모든 아이템을 이름으로 찾을 수 있게 모읍니다.</summary>
        public static Dictionary<string, ItemData> BuildItemRegistry(IEnumerable<DungeonData> dungeons)
        {
            var map = new Dictionary<string, ItemData>();

            void Add(ItemData item)
            {
                while (item != null && !map.ContainsKey(item.name))
                {
                    map[item.name] = item;
                    item = item.mergeResult;
                }
            }

            Add(SampleData.StarterWeapon);
            foreach (var d in dungeons)
                foreach (var floor in d.floors)
                    foreach (var monster in floor.monsterPool)
                        if (monster != null && monster.lootTable != null)
                            foreach (var entry in monster.lootTable.entries)
                                Add(entry.item);
            return map;
        }
    }
}
