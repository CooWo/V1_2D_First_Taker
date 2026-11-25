using UnityEngine;

namespace LootHunters
{
    /// <summary>전투 중인 대상(영웅/몬스터) 공통 런타임 상태.</summary>
    public class Combatant
    {
        public string Name { get; }
        public Stats Stats { get; private set; }
        public int CurrentHp { get; private set; }
        public bool IsDead => CurrentHp <= 0;

        public Combatant(string name, Stats stats)
        {
            Name = name;
            Stats = stats;
            CurrentHp = stats.maxHp;
        }

        /// <summary>장비 변경 등으로 스탯이 바뀔 때. 현재 체력은 비율을 유지하지 않고 최대치로 자릅니다.</summary>
        public void SetStats(Stats stats, bool fullHeal)
        {
            Stats = stats;
            CurrentHp = fullHeal ? stats.maxHp : Mathf.Min(CurrentHp, stats.maxHp);
        }

        /// <summary>남은 방어막. 피해를 HP보다 먼저 흡수합니다.</summary>
        public int Shield { get; set; }

        /// <summary>데미지 공식: 공격력 - 방어력, 최소 1.</summary>
        public int AttackTarget(Combatant target) => Hit(target, 1f, false);

        /// <summary>배율과 치명타(x1.8)를 적용한 공격. 실제로 준 피해(방어막 흡수 포함)를 돌려줍니다.</summary>
        public int Hit(Combatant target, float multiplier, bool crit)
        {
            float raw = Stats.attack * multiplier * (crit ? 1.8f : 1f);
            int damage = Mathf.Max(1, Mathf.RoundToInt(raw) - target.Stats.defense);
            return target.ApplyDamage(damage);
        }

        /// <summary>방어막부터 깎고 남은 피해를 HP에 적용.</summary>
        public int ApplyDamage(int damage)
        {
            damage = Mathf.Max(0, damage);
            int absorbed = Mathf.Min(Shield, damage);
            Shield -= absorbed;
            CurrentHp = Mathf.Max(0, CurrentHp - (damage - absorbed));
            return damage;
        }

        public void HealFull() => CurrentHp = Stats.maxHp;

        /// <summary>회복량만큼 체력 회복 (최대치 초과 불가). 실제 회복량을 돌려줍니다.</summary>
        public int Heal(int amount)
        {
            int before = CurrentHp;
            CurrentHp = Mathf.Min(Stats.maxHp, CurrentHp + Mathf.Max(0, amount));
            return CurrentHp - before;
        }

        /// <summary>방어력을 무시하는 피해 (주문 두루마리용).</summary>
        public int TakeTrueDamage(int amount)
        {
            return ApplyDamage(Mathf.Max(1, amount));
        }
    }
}
