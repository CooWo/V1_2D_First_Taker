using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Supports;
using UnityEngine;

namespace ShadowRPG.Core.Items
{
    public interface ILootGenerator
    {
        Item Generate(int level, EnemyRank rank);
        Item Create(EquipSlot slot, ItemRarity rarity, int level, string skillCode = null);
    }

    /// <summary>
    /// GDD 2.1 슬롯별 속성 풀과 3장 스킬 등급을 따르는 랜덤 장비 생성기.
    /// </summary>
    public sealed class LootGenerator : ILootGenerator
    {
        readonly struct AffixRange
        {
            public readonly float Min;
            public readonly float Max;
            public readonly float PerLevel;

            public AffixRange(float min, float max, float perLevel)
            {
                Min = min;
                Max = max;
                PerLevel = perLevel;
            }
        }

        static readonly Dictionary<StatType, AffixRange> Ranges = new Dictionary<StatType, AffixRange>
        {
            { StatType.Strength, new AffixRange(4, 8, 1.2f) },
            { StatType.Dexterity, new AffixRange(4, 8, 1.2f) },
            { StatType.Intelligence, new AffixRange(4, 8, 1.2f) },
            { StatType.MaxLife, new AffixRange(15, 30, 6f) },
            { StatType.LifePct, new AffixRange(4, 8, 0.3f) },
            { StatType.MaxResource, new AffixRange(8, 15, 2f) },
            { StatType.AllResistPct, new AffixRange(4, 8, 0.4f) },
            { StatType.EliteDamageReductionPct, new AffixRange(3, 6, 0.2f) },
            { StatType.AllDamagePct, new AffixRange(6, 12, 0.6f) },
            { StatType.FlatDamage, new AffixRange(2, 4, 0.8f) },
            { StatType.AttackSpeedPct, new AffixRange(4, 8, 0.3f) },
            { StatType.LifeOnHit, new AffixRange(2, 5, 0.6f) },
            { StatType.CritChancePct, new AffixRange(2, 4, 0.15f) },
            { StatType.CritDamagePct, new AffixRange(10, 20, 1f) },
            { StatType.CooldownReductionPct, new AffixRange(3, 6, 0.2f) },
            { StatType.ResourceCostReductionPct, new AffixRange(4, 8, 0.25f) },
            { StatType.MeleeDamagePct, new AffixRange(8, 15, 0.8f) },
            { StatType.ProjectileDamagePct, new AffixRange(8, 15, 0.8f) },
            { StatType.SpellDamagePct, new AffixRange(8, 15, 0.8f) },
            { StatType.ElementalDamagePct, new AffixRange(8, 15, 0.8f) },
            { StatType.MoveSpeedPct, new AffixRange(4, 8, 0.2f) },
            { StatType.BossDamagePct, new AffixRange(10, 20, 1f) },
            { StatType.FireDamagePct, new AffixRange(8, 16, 0.8f) },
            { StatType.ColdDamagePct, new AffixRange(8, 16, 0.8f) },
            { StatType.LightningDamagePct, new AffixRange(8, 16, 0.8f) },
            { StatType.EarthDamagePct, new AffixRange(8, 16, 0.8f) },
            { StatType.LightDamagePct, new AffixRange(8, 16, 0.8f) },
            { StatType.DarkDamagePct, new AffixRange(8, 16, 0.8f) },
            { StatType.FireResistPct, new AffixRange(6, 12, 0.4f) },
            { StatType.ColdResistPct, new AffixRange(6, 12, 0.4f) },
            { StatType.LightningResistPct, new AffixRange(6, 12, 0.4f) },
            { StatType.EarthResistPct, new AffixRange(6, 12, 0.4f) },
            { StatType.LightResistPct, new AffixRange(6, 12, 0.4f) },
            { StatType.DarkResistPct, new AffixRange(6, 12, 0.4f) },
            { StatType.GoldFindPct, new AffixRange(5, 10, 0.3f) }
        };

        static readonly StatType[] ElementDamage =
        {
            StatType.FireDamagePct, StatType.ColdDamagePct, StatType.LightningDamagePct,
            StatType.EarthDamagePct, StatType.LightDamagePct, StatType.DarkDamagePct
        };

        static readonly StatType[] ElementResist =
        {
            StatType.FireResistPct, StatType.ColdResistPct, StatType.LightningResistPct,
            StatType.EarthResistPct, StatType.LightResistPct, StatType.DarkResistPct
        };

        static StatType[] With(StatType[] pool, params StatType[][] extra)
        {
            var list = new List<StatType>(pool);
            foreach (var e in extra) list.AddRange(e);
            return list.ToArray();
        }

        static readonly Dictionary<EquipSlot, StatType[]> AffixPools = new Dictionary<EquipSlot, StatType[]>
        {
            { EquipSlot.RightHand, With(new[] { StatType.AllDamagePct, StatType.FlatDamage, StatType.Strength, StatType.Dexterity, StatType.Intelligence, StatType.AttackSpeedPct, StatType.LifeOnHit }, ElementDamage) },
            { EquipSlot.LeftHand, new[] { StatType.CritChancePct, StatType.CooldownReductionPct, StatType.ResourceCostReductionPct, StatType.MeleeDamagePct, StatType.ProjectileDamagePct, StatType.SpellDamagePct } },
            { EquipSlot.Head, With(new[] { StatType.Strength, StatType.Dexterity, StatType.Intelligence, StatType.MaxLife, StatType.CritChancePct, StatType.CooldownReductionPct, StatType.MaxResource }, ElementResist) },
            { EquipSlot.Body, With(new[] { StatType.Strength, StatType.Dexterity, StatType.Intelligence, StatType.MaxLife, StatType.LifePct, StatType.AllResistPct, StatType.EliteDamageReductionPct }, ElementResist) },
            { EquipSlot.Boots, With(new[] { StatType.Strength, StatType.Dexterity, StatType.Intelligence, StatType.MaxLife, StatType.MoveSpeedPct, StatType.AllResistPct, StatType.CooldownReductionPct }, ElementResist) },
            { EquipSlot.Amulet, With(new[] { StatType.ElementalDamagePct, StatType.CritDamagePct, StatType.CritChancePct, StatType.Strength, StatType.Dexterity, StatType.Intelligence }, ElementDamage) },
            { EquipSlot.Ring1, With(new[] { StatType.CritChancePct, StatType.CritDamagePct, StatType.AttackSpeedPct, StatType.CooldownReductionPct, StatType.ResourceCostReductionPct, StatType.GoldFindPct }, ElementDamage) },
            { EquipSlot.Relic, new[] { StatType.BossDamagePct, StatType.ResourceCostReductionPct, StatType.GoldFindPct } }
        };

        static readonly Dictionary<EquipSlot, string[]> SupportPools = new Dictionary<EquipSlot, string[]>
        {
            { EquipSlot.Amulet, new[] { "SUP-01", "SUP-03", "SUP-05" } },
            { EquipSlot.Ring1, new[] { "SUP-03", "SUP-05", "SUP-06" } },
            { EquipSlot.Relic, new[] { "SUP-01", "SUP-02", "SUP-04", "SUP-06" } }
        };

        static readonly string[] Prefixes = { "낡은", "그림자", "푸른 안광의", "아지랑이", "심연", "잿빛", "칠흑의", "망각의" };
        static readonly string[] LegendaryNames = { "심연 아지랑이", "검은 태양", "푸른 망령", "끝없는 밤", "영겁의 그림자" };

        readonly ISkillCatalog _skills;
        readonly ISupportCatalog _supports;
        readonly ISetCatalog _sets;
        readonly IRandom _random;

        public LootGenerator(ISkillCatalog skills, ISupportCatalog supports, ISetCatalog sets, IRandom random)
        {
            _skills = skills;
            _supports = supports;
            _sets = sets;
            _random = random;
        }

        public Item Generate(int level, EnemyRank rank)
        {
            var slot = SlotRules.All[_random.Range(0, SlotRules.All.Length)];
            if (slot == EquipSlot.Ring2) slot = EquipSlot.Ring1;
            return Create(slot, RollRarity(rank, slot), level);
        }

        ItemRarity RollRarity(EnemyRank rank, EquipSlot slot)
        {
            float bonus = rank == EnemyRank.Boss ? 25f : rank == EnemyRank.Elite ? 10f : 0f;
            float roll = _random.Range(0f, 100f) - bonus;

            if (SlotRules.IsSetSlot(slot) && roll < 6f) return ItemRarity.Set;
            if (roll < 8f) return ItemRarity.Legendary;
            if (roll < 25f) return ItemRarity.Rare;
            if (roll < 55f) return ItemRarity.Magic;
            return ItemRarity.Normal;
        }

        public Item Create(EquipSlot slot, ItemRarity rarity, int level, string skillCode = null)
        {
            var item = new Item { Slot = slot, Rarity = rarity, ItemLevel = level };

            SkillDefinition skill = null;
            if (SlotRules.GrantsSkill(slot))
            {
                skill = skillCode != null ? _skills.Get(skillCode) : PickSkill(slot);
                if (skill != null) item.GrantedSkill = new SkillGrant(skill.Code, rarity.ToSkillRarity());
            }

            if (SlotRules.GrantsSupport(slot) && SupportPools.TryGetValue(slot, out var supportPool))
            {
                bool hasSupport = rarity >= ItemRarity.Rare || _random.Chance(35f);
                if (hasSupport) item.SupportCode = supportPool[_random.Range(0, supportPool.Length)];
            }

            if (rarity == ItemRarity.Set && SlotRules.IsSetSlot(slot))
                item.SetId = _sets.All[_random.Range(0, _sets.All.Count)].Id;

            if (slot == EquipSlot.Relic && (rarity >= ItemRarity.Legendary || _random.Chance(12f)))
                item.ReducesSetRequirement = true;

            AddBaseStats(item, skill, level);
            AddAffixes(item, level);
            if (rarity == ItemRarity.Legendary) AddLegendaryPower(item);
            item.Requirements = BuildRequirements(skill, level, rarity);
            item.Name = BuildName(item, skill);
            return item;
        }

        SkillDefinition PickSkill(EquipSlot slot)
        {
            var categories = SlotRules.SkillCategories(slot);
            var candidates = _skills.All.Where(s => categories.Contains(s.Category)).ToList();
            return candidates.Count == 0 ? null : candidates[_random.Range(0, candidates.Count)];
        }

        void AddBaseStats(Item item, SkillDefinition skill, int level)
        {
            float rarityFactor = 1f + 0.12f * (int)item.Rarity;
            switch (item.Slot)
            {
                case EquipSlot.RightHand:
                    item.Subtype = WeaponFor(skill);
                    item.BaseStats.Add(new StatModifier(StatType.WeaponDamage, Mathf.Round((6f + 1.6f * level) * rarityFactor)));
                    item.BaseStats.Add(new StatModifier(StatType.AttacksPerSecond, AttackSpeedOf(item.Subtype) - 1.2f));
                    break;
                case EquipSlot.LeftHand:
                    item.Subtype = OffhandFor(skill);
                    AddOffhandBase(item, level, rarityFactor);
                    break;
                case EquipSlot.Head:
                    item.BaseStats.Add(new StatModifier(StatType.Armor, Mathf.Round((20f + 6f * level) * rarityFactor)));
                    break;
                case EquipSlot.Body:
                    item.BaseStats.Add(new StatModifier(StatType.Armor, Mathf.Round((40f + 12f * level) * rarityFactor)));
                    item.BaseStats.Add(new StatModifier(StatType.MaxLife, Mathf.Round(20f + 5f * level)));
                    break;
                case EquipSlot.Boots:
                    item.BaseStats.Add(new StatModifier(StatType.Armor, Mathf.Round((15f + 4f * level) * rarityFactor)));
                    item.BaseStats.Add(new StatModifier(StatType.MoveSpeedPct, 5f));
                    break;
                case EquipSlot.Amulet:
                    item.BaseStats.Add(new StatModifier(StatType.AllResistPct, 5f));
                    break;
                case EquipSlot.Ring1:
                case EquipSlot.Ring2:
                    item.BaseStats.Add(new StatModifier(StatType.FlatDamage, Mathf.Round(2f + 0.5f * level)));
                    break;
            }
        }

        void AddOffhandBase(Item item, int level, float rarityFactor)
        {
            switch (item.Subtype)
            {
                case WeaponSubtype.Shield:
                    item.BaseStats.Add(new StatModifier(StatType.BlockChancePct, 12f + 0.3f * level));
                    item.BaseStats.Add(new StatModifier(StatType.Armor, Mathf.Round((15f + 4f * level) * rarityFactor)));
                    break;
                case WeaponSubtype.Quiver:
                    item.BaseStats.Add(new StatModifier(StatType.AttackSpeedPct, 6f));
                    break;
                case WeaponSubtype.CombatGlove:
                    item.BaseStats.Add(new StatModifier(StatType.WeaponDamage, Mathf.Round((3f + 0.8f * level) * rarityFactor)));
                    break;
                default:
                    item.BaseStats.Add(new StatModifier(StatType.SpellDamagePct, 8f + 0.4f * level));
                    break;
            }
        }

        void AddAffixes(Item item, int level)
        {
            var poolSlot = SlotRules.IsRing(item.Slot) ? EquipSlot.Ring1 : item.Slot;
            if (!AffixPools.TryGetValue(poolSlot, out var pool)) return;

            int count = AffixCount(item.Rarity, item.Slot);
            // 남은 후보에서 뽑아 제거하는 방식: 난수 구현과 무관하게 항상 끝납니다.
            var remaining = new List<StatType>(pool);
            for (int i = 0; i < count && remaining.Count > 0; i++)
            {
                int index = _random.Range(0, remaining.Count);
                var stat = remaining[index];
                remaining.RemoveAt(index);

                var range = Ranges[stat];
                float value = Mathf.Round(_random.Range(range.Min, range.Max) + range.PerLevel * level);
                item.Affixes.Add(new StatModifier(stat, value));
            }

            if (item.Rarity == ItemRarity.Legendary && item.Slot == EquipSlot.Relic)
                item.Affixes.Add(new StatModifier(StatType.BossDamagePct, 20f));
        }

        static int AffixCount(ItemRarity rarity, EquipSlot slot)
        {
            if (slot == EquipSlot.Relic) return rarity >= ItemRarity.Rare ? 2 : 1;
            switch (rarity)
            {
                case ItemRarity.Magic: return 2;
                case ItemRarity.Rare: return 3;
                case ItemRarity.Set: return 3;
                case ItemRarity.Legendary: return 4;
                default: return 1;
            }
        }

        void AddLegendaryPower(Item item)
        {
            var powers = new[]
            {
                (SkillTag.Melee | SkillTag.Movement, 1.35f, "[이동]+[근접] 스킬 최종 피해 1.35배"),
                (SkillTag.Projectile, 1.25f, "[투사체] 스킬 최종 피해 1.25배"),
                (SkillTag.Spell | SkillTag.Area, 1.3f, "[주문]+[범위] 스킬 최종 피해 1.3배"),
                (SkillTag.Slam, 1.4f, "[강타] 스킬 최종 피해 1.4배"),
                (SkillTag.Chain, 1.3f, "[연쇄] 스킬 최종 피해 1.3배"),
                (SkillTag.Trigger, 1.5f, "[트리거] 스킬 최종 피해 1.5배")
            };
            var power = powers[_random.Range(0, powers.Length)];
            item.TagMultipliers.Add(new TagMultiplier(power.Item1, power.Item2));
            item.PowerDescription = power.Item3;
        }

        Requirements BuildRequirements(SkillDefinition skill, int level, ItemRarity rarity)
        {
            int main = Mathf.RoundToInt(10f + 2.5f * level + 2f * (int)rarity);
            var attribute = skill?.Attribute ?? (CoreStat)_random.Range(0, 3);
            switch (attribute)
            {
                case CoreStat.Strength: return new Requirements(main, 0, 0);
                case CoreStat.Dexterity: return new Requirements(0, main, 0);
                default: return new Requirements(0, 0, main);
            }
        }

        string BuildName(Item item, SkillDefinition skill)
        {
            string baseName = BaseName(item);
            if (item.Rarity == ItemRarity.Set)
            {
                var set = _sets.Get(item.SetId);
                return $"{set?.Name ?? "세트"}: {baseName}";
            }
            if (item.Rarity == ItemRarity.Legendary)
                return $"{LegendaryNames[_random.Range(0, LegendaryNames.Length)]} {baseName}";
            return $"{Prefixes[_random.Range(0, Prefixes.Length)]} {baseName}";
        }

        static string BaseName(Item item)
        {
            switch (item.Slot)
            {
                case EquipSlot.RightHand:
                case EquipSlot.LeftHand: return SubtypeName(item.Subtype);
                case EquipSlot.Head: return "두건";
                case EquipSlot.Body: return "갑주";
                case EquipSlot.Boots: return "장화";
                case EquipSlot.Amulet: return "목걸이";
                case EquipSlot.Ring1:
                case EquipSlot.Ring2: return "반지";
                default: return "성물";
            }
        }

        public static string SubtypeName(WeaponSubtype subtype)
        {
            switch (subtype)
            {
                case WeaponSubtype.Sword: return "한손검";
                case WeaponSubtype.Greatsword: return "대검";
                case WeaponSubtype.Wand: return "마법봉";
                case WeaponSubtype.Staff: return "지팡이";
                case WeaponSubtype.Bow: return "활";
                case WeaponSubtype.CombatGlove: return "격투 장갑";
                case WeaponSubtype.Shield: return "방패";
                case WeaponSubtype.Quiver: return "화살통";
                case WeaponSubtype.Focus: return "마력원";
                default: return "장비";
            }
        }

        WeaponSubtype WeaponFor(SkillDefinition skill)
        {
            var attribute = skill?.Attribute ?? CoreStat.Strength;
            bool alt = _random.Chance(50f);
            switch (attribute)
            {
                case CoreStat.Strength: return alt ? WeaponSubtype.Greatsword : WeaponSubtype.Sword;
                case CoreStat.Dexterity: return skill != null && skill.Tags.IsRanged() ? WeaponSubtype.Bow : WeaponSubtype.CombatGlove;
                default: return alt ? WeaponSubtype.Staff : WeaponSubtype.Wand;
            }
        }

        WeaponSubtype OffhandFor(SkillDefinition skill)
        {
            var attribute = skill?.Attribute ?? CoreStat.Strength;
            switch (attribute)
            {
                case CoreStat.Strength: return WeaponSubtype.Shield;
                case CoreStat.Dexterity: return _random.Chance(50f) ? WeaponSubtype.Quiver : WeaponSubtype.CombatGlove;
                default: return WeaponSubtype.Focus;
            }
        }

        static float AttackSpeedOf(WeaponSubtype subtype)
        {
            switch (subtype)
            {
                case WeaponSubtype.Greatsword: return 1.0f;
                case WeaponSubtype.Staff: return 1.1f;
                case WeaponSubtype.Wand:
                case WeaponSubtype.Bow: return 1.3f;
                case WeaponSubtype.Sword: return 1.4f;
                case WeaponSubtype.CombatGlove: return 1.6f;
                default: return 1.2f;
            }
        }
    }
}
