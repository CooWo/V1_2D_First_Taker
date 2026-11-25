using UnityEngine;

namespace LootHunters
{
    public enum StatKind { Str, Vit, Agi }

    /// <summary>
    /// 영웅의 레벨과 스탯 포인트. 몬스터를 잡으면 경험치를 얻고,
    /// 레벨업마다 스탯 포인트 3개를 받아 힘/체력/민첩에 직접 나눠 줍니다.
    /// </summary>
    public class HeroProgress
    {
        public const int PointsPerLevel = 3;

        public int level = 1;
        public int exp;
        public int points;
        public int str;   // 힘: 공격력
        public int vit;   // 체력: 최대 HP
        public int agi;   // 민첩: 방어력, 공격 속도, 치명타

        /// <summary>다음 레벨까지 필요한 경험치.</summary>
        public int ExpToNext => 20 + (level - 1) * 14 + (level - 1) * (level - 1) * 3;

        /// <summary>레벨 자체 보너스 + 분배한 스탯 보너스.</summary>
        public Stats Bonus => new Stats(
            (level - 1) * 6 + vit * 15,
            (level - 1) * 1 + str * 2,
            agi / 2);

        /// <summary>공격 속도 배율 (민첩 1당 +4%).</summary>
        public float AttackSpeedMult => 1f + agi * 0.04f;

        /// <summary>치명타 확률 (기본 5%, 민첩 1당 +1%, 최대 50%).</summary>
        public float CritChance => Mathf.Min(0.5f, 0.05f + agi * 0.01f);

        /// <summary>경험치를 더하고 오른 레벨 수를 돌려줍니다.</summary>
        public int AddExp(int amount)
        {
            exp += Mathf.Max(0, amount);
            int gained = 0;
            while (exp >= ExpToNext && level < 999)
            {
                exp -= ExpToNext;
                level++;
                points += PointsPerLevel;
                gained++;
            }
            return gained;
        }

        public bool Spend(StatKind kind)
        {
            if (points <= 0) return false;
            points--;
            switch (kind)
            {
                case StatKind.Str: str++; break;
                case StatKind.Vit: vit++; break;
                default: agi++; break;
            }
            return true;
        }

        public static string Name(StatKind k) => k switch
        {
            StatKind.Str => "힘",
            StatKind.Vit => "체력",
            _ => "민첩",
        };

        public static string Effect(StatKind k) => k switch
        {
            StatKind.Str => "ATK +2",
            StatKind.Vit => "HP +15",
            _ => "공속 +4% 치명 +1%",
        };
    }
}
