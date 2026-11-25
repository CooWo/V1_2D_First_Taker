using UnityEngine;

namespace LootHunters
{
    /// <summary>몬스터 한 종류의 정의.</summary>
    [CreateAssetMenu(menuName = "LootHunters/Monster", fileName = "Monster_")]
    public class MonsterData : ScriptableObject
    {
        public string displayName = "Monster";
        public Stats baseStats = new Stats(20, 4, 1);
        public LootTable lootTable;
        public int goldMin = 1;
        public int goldMax = 3;

        [Header("실시간 (4단계)")]
        [Tooltip("초당 이동 칸 수")]
        public float moveSpeed = 2.5f;
        [Tooltip("공격 간격(초)")]
        public float attackInterval = 1.0f;

        [Header("그래픽 (비워 두면 DungeonSprites 또는 색 원으로 표시)")]
        public Sprite sprite;
        public Color tint = new Color(0.9f, 0.3f, 0.3f);
    }
}
