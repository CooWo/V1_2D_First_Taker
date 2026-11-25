using System.Collections.Generic;
using UnityEngine;

namespace LootHunters
{
    /// <summary>
    /// 픽셀아트 스프라이트 묶음. Create > LootHunters > Dungeon Sprites 로 만들고
    /// DungeonView의 Sprites 칸에 넣으세요. 비어 있는 칸은 색 도형으로 대신 그립니다.
    /// 0x72 DungeonTileset II용 묶음은 메뉴 LootHunters > Build 0x72 Sprites 로 자동 생성됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "LootHunters/Dungeon Sprites", fileName = "DungeonSprites")]
    public class DungeonSprites : ScriptableObject
    {
        [Tooltip("바닥 타일 (여러 개 넣으면 섞어서 사용)")]
        public Sprite[] floor;
        [Tooltip("가끔 섞이는 바닥 (금 간 바닥 등)")]
        public Sprite[] floorRare;
        [Tooltip("벽 타일 (여러 개 넣으면 섞어서 사용). 아래 앞면/윗면이 비어 있을 때 사용")]
        public Sprite[] wall;
        [Tooltip("바로 아래가 바닥인 벽의 앞면 (벽돌)")]
        public Sprite wallFront;
        [Tooltip("앞면에 가끔 섞이는 장식 (구멍, 깃발, 이끼)")]
        public Sprite[] wallFrontVariants;
        [Tooltip("그 밖의 벽 윗면")]
        public Sprite wallTop;
        public Sprite stairs;
        public Sprite hero;
        [Tooltip("이름으로 못 찾은 몬스터에 쓸 기본 스프라이트")]
        public Sprite defaultMonster;

        [System.Serializable]
        public class MonsterSprite
        {
            [Tooltip("MonsterData의 displayName과 똑같이 (예: 슬라임, 고블린)")]
            public string monsterName;
            public Sprite sprite;
        }

        [Tooltip("몬스터 이름 → 스프라이트 (예제 데이터 몬스터에 그림을 붙일 때)")]
        public List<MonsterSprite> monsters = new List<MonsterSprite>();

        // ───────── 애니메이션 (있으면 위의 한 장짜리 그림보다 우선) ─────────

        [System.Serializable]
        public class Anim
        {
            public Sprite[] idle;
            [Tooltip("비어 있으면 움직일 때도 idle 사용")]
            public Sprite[] run;
            [Tooltip("크기 배율 (16픽셀 = 1칸 기준)")]
            public float scale = 1f;
            public bool IsValid => idle != null && idle.Length > 0 && idle[0] != null;
        }

        [System.Serializable]
        public class MonsterAnim
        {
            [Tooltip("MonsterData의 displayName과 똑같이")]
            public string monsterName;
            public Anim anim = new Anim();
        }

        public Anim heroAnim = new Anim();
        public List<MonsterAnim> monsterAnims = new List<MonsterAnim>();
        public Anim defaultMonsterAnim = new Anim();

        public Sprite FindMonster(MonsterData data)
        {
            if (data.sprite != null) return data.sprite;
            foreach (var m in monsters)
                if (m.sprite != null && m.monsterName == data.displayName) return m.sprite;
            return defaultMonster;
        }

        public Anim FindMonsterAnim(MonsterData data)
        {
            foreach (var m in monsterAnims)
                if (m.anim != null && m.anim.IsValid && m.monsterName == data.displayName) return m.anim;
            return defaultMonsterAnim != null && defaultMonsterAnim.IsValid ? defaultMonsterAnim : null;
        }

        public static Sprite Pick(Sprite[] list, int x, int y)
        {
            if (list == null || list.Length == 0) return null;
            return list[Hash(x, y) % list.Length];
        }

        /// <summary>칸 좌표로 정해지는 0 이상 난수 (같은 칸은 항상 같은 값).</summary>
        public static int Hash(int x, int y)
        {
            int h = (x * 73856093) ^ (y * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            return h & int.MaxValue;
        }
    }
}
