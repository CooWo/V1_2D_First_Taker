using UnityEngine;

namespace LootHunters
{
    /// <summary>맵 위에 있는 몬스터 한 마리의 실시간 상태.</summary>
    public class MapMonster
    {
        public MonsterData Data { get; }
        public Combatant Combatant { get; }
        public Vector2 Pos;
        public bool Awake;            // 영웅을 발견해서 쫓는 중
        public float AttackTimer;
        public bool ScrollChecked;    // 자동 두루마리 판단을 이미 했는지
        public Vector2 WanderTarget;  // 쉬는 중 어슬렁거릴 목적지
        public float WanderTimer;

        public MapMonster(MonsterData data, Stats stats, Vector2 pos)
        {
            Data = data;
            Combatant = new Combatant(data.displayName, stats);
            Pos = pos;
            WanderTarget = pos;
            AttackTimer = data.attackInterval * 0.5f;
        }

        public bool IsDead => Combatant.IsDead;
    }
}
