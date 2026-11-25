using System.Collections.Generic;
using UnityEngine;

namespace LootHunters
{
    /// <summary>
    /// 에셋을 하나도 만들지 않아도 바로 테스트할 수 있도록 예제 데이터를 코드로 생성합니다.
    /// 익숙해지면 Create > LootHunters 메뉴로 진짜 에셋을 만들어 DungeonRun에 연결하세요.
    /// </summary>
    public static class SampleData
    {
        static ItemData starterWeapon;
        public static ItemData StarterWeapon =>
            starterWeapon != null ? starterWeapon
            : starterWeapon = Item("낡은 단검", ItemSlot.Weapon, Rarity.Common, new Stats(0, 2, 0), 1);

        public static List<DungeonData> CreateDungeons()
        {
            // ── 아이템 ──
            var rustySword  = Item("녹슨 검",       ItemSlot.Weapon,   Rarity.Common, new Stats(0, 4, 0), 3);
            var ironSword   = Item("철검",          ItemSlot.Weapon,   Rarity.Rare,   new Stats(0, 8, 0), 12);
            var flameBlade  = Item("화염검",        ItemSlot.Weapon,   Rarity.Epic,   new Stats(10, 15, 0), 50);
            var darkSword   = Item("흑철 대검",     ItemSlot.Weapon,   Rarity.Rare,   new Stats(0, 13, 0), 30);
            var magmaAxe    = Item("용암 도끼",     ItemSlot.Weapon,   Rarity.Epic,   new Stats(20, 24, 0), 120);
            var leather     = Item("가죽 갑옷",     ItemSlot.Armor,    Rarity.Common, new Stats(10, 0, 2), 3);
            var chainMail   = Item("사슬 갑옷",     ItemSlot.Armor,    Rarity.Rare,   new Stats(25, 0, 5), 12);
            var mithril     = Item("미스릴 갑옷",   ItemSlot.Armor,    Rarity.Rare,   new Stats(40, 0, 8), 30);
            var dragonPlate = Item("용비늘 갑옷",   ItemSlot.Armor,    Rarity.Epic,   new Stats(60, 0, 12), 120);
            var slimeGel    = Item("슬라임 젤",     ItemSlot.Material, Rarity.Common, default, 1);
            var bone        = Item("뼈 조각",       ItemSlot.Material, Rarity.Common, default, 2);
            var emberCore   = Item("불꽃 정수",     ItemSlot.Material, Rarity.Common, default, 6);
            var gem         = Item("빛나는 보석",   ItemSlot.Treasure, Rarity.Rare,   default, 25);
            var goldIdol    = Item("황금 우상",     ItemSlot.Treasure, Rarity.Epic,   default, 80);

            // ── 합성 경로 (같은 장비 3개 → 상위 장비) ──
            rustySword.mergeResult = ironSword;
            ironSword.mergeResult  = flameBlade;
            darkSword.mergeResult  = magmaAxe;
            leather.mergeResult    = chainMail;
            chainMail.mergeResult  = dragonPlate;
            mithril.mergeResult    = dragonPlate;

            // ── 드랍 테이블 ──
            var lowLoot  = Loot(0.7f, 1, (slimeGel, 6), (rustySword, 2), (leather, 2), (ironSword, 1));
            var midLoot  = Loot(0.75f, 1, (bone, 5), (ironSword, 3), (chainMail, 3), (gem, 1), (flameBlade, 1));
            var highLoot = Loot(0.85f, 2, (bone, 4), (chainMail, 3), (gem, 3), (flameBlade, 2), (dragonPlate, 1));
            var mineLoot = Loot(0.8f, 1, (emberCore, 6), (darkSword, 3), (mithril, 3), (gem, 2), (goldIdol, 1));
            var deepLoot = Loot(0.9f, 2, (emberCore, 4), (mithril, 3), (goldIdol, 2), (magmaAxe, 1), (dragonPlate, 2));

            // ── 몬스터 ──
            var slime    = Monster("슬라임",     new Stats(18, 4, 0),    lowLoot, 1, 3);
            var goblin   = Monster("고블린",     new Stats(26, 6, 1),    lowLoot, 2, 5);
            var skeleton = Monster("스켈레톤",   new Stats(40, 9, 3),    midLoot, 4, 8);
            var orc      = Monster("오크",       new Stats(60, 12, 4),   midLoot, 6, 12);
            var knight   = Monster("망령 기사",  new Stats(90, 16, 7),   highLoot, 12, 20);
            var imp      = Monster("화염 임프",  new Stats(70, 16, 5),   mineLoot, 10, 18);
            var golem    = Monster("용암 골렘",  new Stats(140, 18, 10), mineLoot, 15, 25);
            var salamander = Monster("샐러맨더", new Stats(120, 24, 8),  deepLoot, 20, 35);

            // ── 실시간 특성 (색은 스프라이트가 없을 때 쓰는 원 색상) ──
            Tune(slime,      new Color(0.45f, 0.85f, 0.45f), 2.0f, 1.1f);
            Tune(goblin,     new Color(0.55f, 0.75f, 0.30f), 3.0f, 0.9f);
            Tune(skeleton,   new Color(0.90f, 0.90f, 0.85f), 2.4f, 1.0f);
            Tune(orc,        new Color(0.35f, 0.60f, 0.35f), 2.2f, 1.3f);
            Tune(knight,     new Color(0.60f, 0.60f, 0.95f), 2.6f, 1.1f);
            Tune(imp,        new Color(1.00f, 0.45f, 0.20f), 3.4f, 0.8f);
            Tune(golem,      new Color(0.70f, 0.30f, 0.15f), 1.6f, 1.6f);
            Tune(salamander, new Color(1.00f, 0.25f, 0.25f), 3.0f, 1.0f);

            // ── 던전 ──
            var cave = Dungeon("버려진 동굴", "초보 사냥터. 깊이 내려갈수록 끝없이 강해집니다.", 0, 0.15f,
                Floor("습한 동굴",   5, slime, goblin),
                Floor("고블린 굴",   6, goblin, slime, goblin),
                Floor("뼈의 회랑",   6, skeleton, goblin),
                Floor("오크 요새",   7, orc, skeleton),
                Floor("저주받은 성", 8, knight, orc, skeleton));

            var mine = Dungeon("불타는 광산", "강화된 장비가 필요한 두 번째 던전. 보물이 많이 나옵니다.", 75, 0.18f,
                Floor("광산 입구",   5, imp),
                Floor("용암 갱도",   6, imp, golem),
                Floor("화염 심층",   7, golem, salamander));

            return new List<DungeonData> { cave, mine };
        }

        static DungeonData Dungeon(string name, string desc, int requiredPower, float scaling, params FloorData[] floors)
        {
            var d = ScriptableObject.CreateInstance<DungeonData>();
            d.name = d.dungeonName = name;
            d.description = desc;
            d.requiredPower = requiredPower;
            d.scalingPerFloor = scaling;
            d.deathLossRatio = 0.5f;
            d.floors = new List<FloorData>(floors);
            // 몬스터 40~60마리가 돌아다닐 수 있는 넓은 맵
            d.mapWidth = 64;
            d.mapHeight = 46;
            d.roomCount = 11;
            d.roomMinSize = 7;
            d.roomMaxSize = 13;
            return d;
        }

        static ItemData Item(string name, ItemSlot slot, Rarity rarity, Stats bonus, int price)
        {
            var i = ScriptableObject.CreateInstance<ItemData>();
            i.name = i.displayName = name;
            i.slot = slot;
            i.rarity = rarity;
            i.bonus = bonus;
            i.sellPrice = price;
            return i;
        }

        static LootTable Loot(float chance, int rolls, params (ItemData item, int weight)[] list)
        {
            var t = ScriptableObject.CreateInstance<LootTable>();
            t.dropChance = chance;
            t.rolls = rolls;
            foreach (var (item, weight) in list)
                t.entries.Add(new LootEntry { item = item, weight = weight });
            return t;
        }

        static MonsterData Monster(string name, Stats stats, LootTable loot, int gMin, int gMax)
        {
            var m = ScriptableObject.CreateInstance<MonsterData>();
            m.name = m.displayName = name;
            m.baseStats = stats;
            m.lootTable = loot;
            m.goldMin = gMin;
            m.goldMax = gMax;
            return m;
        }

        static void Tune(MonsterData m, Color tint, float speed, float attackInterval)
        {
            m.tint = tint;
            m.moveSpeed = speed;
            m.attackInterval = attackInterval;
        }

        static FloorData Floor(string name, int count, params MonsterData[] pool)
            => new FloorData { floorName = name, monstersPerFloor = count, monsterPool = new List<MonsterData>(pool) };
    }
}
