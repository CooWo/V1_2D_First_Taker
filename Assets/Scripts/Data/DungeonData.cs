
using System.Collections.Generic;
using UnityEngine;

namespace LootHunters
{
    [System.Serializable]
    public class FloorData
    {
        public string floorName = "Floor";
        public List<MonsterData> monsterPool = new List<MonsterData>();
        [Min(1)] public int monstersPerFloor = 3;
    }

    /// <summary>
    /// 던전 전체 설정. floors 목록보다 깊이 내려가면 마지막 층 구성을 반복하되
    /// 몬스터 스탯이 scalingPerFloor 만큼 계속 강해집니다.
    /// </summary>
    [CreateAssetMenu(menuName = "LootHunters/Dungeon", fileName = "Dungeon_")]
    public class DungeonData : ScriptableObject
    {
        public string dungeonName = "Dungeon";
        [TextArea] public string description = "";
        [Tooltip("입장에 필요한 영웅 전투력")]
        public int requiredPower = 0;
        public List<FloorData> floors = new List<FloorData>();
        [Tooltip("층마다 몬스터 스탯 증가율 (0.15 = 층당 +15%)")]
        public float scalingPerFloor = 0.15f;
        [Tooltip("사망 시 잃는 전리품/골드 비율 (0.5 = 절반)")]
        [Range(0f, 1f)] public float deathLossRatio = 0.5f;

        [Header("맵 생성 (4단계)")]
        public int mapWidth = 44;
        public int mapHeight = 32;
        public int roomCount = 9;
        public int roomMinSize = 4;
        public int roomMaxSize = 8;

        public FloorData GetFloor(int depth)
            => floors[Mathf.Clamp(depth, 0, floors.Count - 1)];

        public float GetMultiplier(int depth) => 1f + scalingPerFloor * depth;
    }
}
