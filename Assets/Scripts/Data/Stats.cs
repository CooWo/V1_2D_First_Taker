using System;

namespace LootHunters
{
    /// <summary>체력/공격력/방어력 묶음. 영웅, 몬스터, 장비 보너스 모두 이 구조를 씁니다.</summary>
    [Serializable]
    public struct Stats
    {
        public int maxHp;
        public int attack;
        public int defense;

        public Stats(int maxHp, int attack, int defense)
        {
            this.maxHp = maxHp;
            this.attack = attack;
            this.defense = defense;
        }

        public static Stats operator +(Stats a, Stats b)
            => new Stats(a.maxHp + b.maxHp, a.attack + b.attack, a.defense + b.defense);

        /// <summary>던전 깊이에 따른 몬스터 강화용 배율.</summary>
        public Stats Scaled(float multiplier)
            => new Stats(
                (int)Math.Round(maxHp * multiplier),
                (int)Math.Round(attack * multiplier),
                (int)Math.Round(defense * multiplier));

        /// <summary>전투력. 장비 비교와 던전 해금 조건에 씁니다.</summary>
        public int Power => attack * 2 + defense * 2 + maxHp / 5;

        public override string ToString() => $"HP {maxHp} / ATK {attack} / DEF {defense}";
    }
}
