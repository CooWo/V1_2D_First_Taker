using System.Linq;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tuning;
using UnityEngine;

namespace ShadowRPG.Core.Growth
{
    /// <summary>
    /// 전투력 표기 (모든 아이템에 표시, 방향성 v1 §2). 가중치는 ItemTuning.Power*.
    /// 정확한 강함이 아니라 비교용 지표입니다 — 빌드 궁합은 반영하지 않습니다.
    /// </summary>
    public interface IPowerCalculator
    {
        int ItemPower(Item item);
        int TotalPower(IStatReader stats, Equipment equipment);
    }

    public sealed class PowerCalculator : IPowerCalculator
    {
        readonly ItemTuning _t;

        public PowerCalculator(ItemTuning tuning) => _t = tuning;

        public int ItemPower(Item item)
        {
            if (item == null) return 0;
            float power = 0f;
            foreach (var m in item.AllModifiers) power += Weight(m.Stat) * m.Value;
            if (item.GrantedSkill.IsValid) power += _t.PowerPerSkillRarity * (1 + (int)item.GrantedSkill.Rarity);
            if (!string.IsNullOrEmpty(item.SupportCode)) power += _t.PowerPerSkillRarity * 0.75f;
            power += item.TagMultipliers.Sum(t => (t.Multiplier - 1f) * 200f);
            return Mathf.Max(1, Mathf.RoundToInt(power));
        }

        public int TotalPower(IStatReader stats, Equipment equipment)
        {
            int items = equipment == null ? 0 : equipment.Equipped.Select(p => p.Value).Where(i => i != null).Sum(ItemPower);
            float core = (stats.Get(StatType.Strength) + stats.Get(StatType.Dexterity) + stats.Get(StatType.Intelligence)) * _t.PowerPerStatPoint;
            float life = stats.Get(StatType.MaxLife) * _t.PowerPerLife;
            return Mathf.RoundToInt(items + core + life);
        }

        float Weight(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:
                case StatType.Dexterity:
                case StatType.Intelligence: return _t.PowerPerStatPoint;
                case StatType.MaxLife: return _t.PowerPerLife;
                case StatType.WeaponDamage:
                case StatType.FlatDamage: return _t.PowerPerDamage;
                case StatType.Armor: return _t.PowerPerArmor;
                case StatType.ExtraProjectiles:
                case StatType.ExtraChains:
                case StatType.ExtraPierce: return 60f;
                case StatType.AttacksPerSecond: return 40f;
                default: return _t.PowerPerPct;
            }
        }
    }
}
