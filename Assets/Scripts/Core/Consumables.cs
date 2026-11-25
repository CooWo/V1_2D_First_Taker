using UnityEngine;

namespace LootHunters
{
    /// <summary>
    /// 포션과 주문 두루마리 설정. DungeonRun 인스펙터에서 바로 조절할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class ConsumableSettings
    {
        [Header("회복 포션")]
        public int potionPrice = 15;
        [Tooltip("최대 체력 대비 회복 비율")]
        [Range(0.1f, 1f)] public float potionHealRatio = 0.4f;
        public bool autoPotion = true;
        [Tooltip("체력이 이 비율 이하로 떨어지면 자동 사용")]
        [Range(0.05f, 0.9f)] public float autoPotionHpRatio = 0.35f;

        [Header("화염 두루마리")]
        public int scrollPrice = 25;
        [Tooltip("영웅 공격력 x 배율만큼 방어 무시 피해")]
        public float scrollDamageMultiplier = 3f;
        public bool autoScroll = true;
        [Tooltip("일반 공격으로 이 횟수 이상 때려야 잡히는 강한 몬스터에게 전투 시작 시 자동 사용")]
        [Min(1)] public int autoScrollMinHits = 6;
    }
}
