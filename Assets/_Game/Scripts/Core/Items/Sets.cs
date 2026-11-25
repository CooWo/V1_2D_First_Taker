using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Stats;

namespace ShadowRPG.Core.Items
{
    /// <summary>세트 단계 효과 (2/3/4세트).</summary>
    public sealed class SetBonus
    {
        public int Pieces { get; set; }
        public string Description { get; set; }
        public List<StatModifier> Modifiers { get; } = new List<StatModifier>();
        public List<TagMultiplier> TagMultipliers { get; } = new List<TagMultiplier>();
    }

    /// <summary>
    /// GDD 2.2: 세트는 직업 전용이 아니라 스킬 [태그] 조합을 강화합니다.
    /// 2세트(자원/쿨타임 시동) → 3세트(생존) → 4세트(메커니즘 변주 + 최종 곱연산).
    /// </summary>
    public sealed class SetDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Theme { get; set; }
        public List<SetBonus> Bonuses { get; } = new List<SetBonus>();
    }

    public interface ISetCatalog
    {
        IReadOnlyList<SetDefinition> All { get; }
        SetDefinition Get(string id);
    }

    public sealed class SetCatalog : ISetCatalog
    {
        readonly Dictionary<string, SetDefinition> _byId;

        public SetCatalog(IEnumerable<SetDefinition> sets)
        {
            All = sets.ToList();
            _byId = All.ToDictionary(s => s.Id);
        }

        public IReadOnlyList<SetDefinition> All { get; }
        public SetDefinition Get(string id) => id != null && _byId.TryGetValue(id, out var set) ? set : null;

        public static SetCatalog CreateDefault()
        {
            var shatteredEarth = new SetDefinition { Id = "set_shattered_earth", Name = "부서진 대지의 갑주", Theme = "[근접] + [범위]" };
            shatteredEarth.Bonuses.Add(Bonus(2, "근접 피해 +20%, 공격 속도 +10%",
                new StatModifier(StatType.MeleeDamagePct, 20f), new StatModifier(StatType.AttackSpeedPct, 10f)));
            shatteredEarth.Bonuses.Add(Bonus(3, "방어도 +250, 생명력 +20%, 정예 피해 감소 +15%",
                new StatModifier(StatType.Armor, 250f), new StatModifier(StatType.LifePct, 20f), new StatModifier(StatType.EliteDamageReductionPct, 15f)));
            var earth4 = Bonus(4, "범위 +30%, [근접]+[범위] 스킬 최종 피해 2.5배", new StatModifier(StatType.AreaPct, 30f));
            earth4.TagMultipliers.Add(new TagMultiplier(SkillTag.Melee | SkillTag.Area, 2.5f));
            shatteredEarth.Bonuses.Add(earth4);

            var stormcaller = new SetDefinition { Id = "set_abyssal_storm", Name = "심연 폭풍술사", Theme = "[지능] + [연쇄]" };
            stormcaller.Bonuses.Add(Bonus(2, "쿨타임 -10%, 자원 소모 -15%",
                new StatModifier(StatType.CooldownReductionPct, 10f), new StatModifier(StatType.ResourceCostReductionPct, 15f)));
            stormcaller.Bonuses.Add(Bonus(3, "에너지 보호막 +25%, 정예 피해 감소 +15%",
                new StatModifier(StatType.EnergyShieldPct, 25f), new StatModifier(StatType.EliteDamageReductionPct, 15f)));
            var storm4 = Bonus(4, "연쇄 +3, [연쇄] 스킬 최종 피해 2.2배", new StatModifier(StatType.ExtraChains, 3f));
            storm4.TagMultipliers.Add(new TagMultiplier(SkillTag.Chain, 2.2f));
            stormcaller.Bonuses.Add(storm4);

            var tempest = new SetDefinition { Id = "set_shadow_tempest", Name = "그림자 폭풍 사수", Theme = "[투사체]" };
            tempest.Bonuses.Add(Bonus(2, "투사체 피해 +20%, 이동 속도 +10%",
                new StatModifier(StatType.ProjectileDamagePct, 20f), new StatModifier(StatType.MoveSpeedPct, 10f)));
            tempest.Bonuses.Add(Bonus(3, "회피 +15%, 생명력 +15%",
                new StatModifier(StatType.EvasionPct, 15f), new StatModifier(StatType.LifePct, 15f)));
            var tempest4 = Bonus(4, "투사체 +2, [투사체] 스킬 최종 피해 2.2배", new StatModifier(StatType.ExtraProjectiles, 2f));
            tempest4.TagMultipliers.Add(new TagMultiplier(SkillTag.Projectile, 2.2f));
            tempest.Bonuses.Add(tempest4);

            return new SetCatalog(new[] { shatteredEarth, stormcaller, tempest });
        }

        static SetBonus Bonus(int pieces, string description, params StatModifier[] modifiers)
        {
            var bonus = new SetBonus { Pieces = pieces, Description = description };
            bonus.Modifiers.AddRange(modifiers);
            return bonus;
        }
    }

    /// <summary>
    /// 장착 세트 개수를 세어 단계 효과를 켜고 끕니다. 특수 장비칸의 "세트 요구 개수 -1"을 반영합니다.
    /// </summary>
    public sealed class SetBonusTracker
    {
        readonly Equipment _equipment;
        readonly Combatant _owner;
        readonly ISetCatalog _catalog;
        readonly List<SetBonus> _active = new List<SetBonus>();
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public event Action Changed;

        public SetBonusTracker(Equipment equipment, Combatant owner, ISetCatalog catalog)
        {
            _equipment = equipment;
            _owner = owner;
            _catalog = catalog;
            equipment.Changed += (_, __, ___) => Refresh();
        }

        public IReadOnlyList<SetBonus> ActiveBonuses => _active;
        public IReadOnlyDictionary<string, int> PieceCounts => _counts;
        public bool HasRequirementReduction { get; private set; }

        /// <summary>요구 개수 감소를 반영한 실효 개수.</summary>
        public int EffectiveCount(string setId)
            => _counts.TryGetValue(setId, out int count) ? count + (HasRequirementReduction && count > 0 ? 1 : 0) : 0;

        public void Refresh()
        {
            foreach (var bonus in _active)
            {
                _owner.Stats.RemoveSource(bonus);
                _owner.TagMultipliers.RemoveSource(bonus);
            }
            _active.Clear();
            _counts.Clear();
            HasRequirementReduction = false;

            foreach (var pair in _equipment.Equipped)
            {
                var item = pair.Value;
                if (item == null) continue;
                if (item.ReducesSetRequirement) HasRequirementReduction = true;
                if (item.SetId == null || !SlotRules.IsSetSlot(pair.Key)) continue;
                _counts[item.SetId] = _counts.TryGetValue(item.SetId, out int c) ? c + 1 : 1;
            }

            foreach (var setId in _counts.Keys)
            {
                var set = _catalog.Get(setId);
                if (set == null) continue;
                int effective = EffectiveCount(setId);
                foreach (var bonus in set.Bonuses)
                {
                    if (bonus.Pieces > effective) continue;
                    _active.Add(bonus);
                    _owner.Stats.AddModifiers(bonus.Modifiers, bonus);
                    foreach (var tm in bonus.TagMultipliers) _owner.TagMultipliers.Add(tm.WithSource(bonus));
                }
            }

            Changed?.Invoke();
        }
    }
}
