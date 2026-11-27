using System.Collections.Generic;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;

namespace ShadowRPG.Core.Items
{
    /// <summary>GDD 2.1 9슬롯.</summary>
    public enum EquipSlot
    {
        RightHand,
        LeftHand,
        Head,
        Body,
        Boots,
        Amulet,
        Ring1,
        Ring2,
        Relic
    }

    public enum ItemRarity
    {
        Normal,
        Magic,
        Rare,
        Legendary,
        Set
    }

    public enum WeaponSubtype
    {
        None,
        Sword,
        Greatsword,
        Wand,
        Staff,
        Bow,
        CombatGlove,
        Shield,
        Quiver,
        Focus
    }

    /// <summary>슬롯 역할 규칙 (GDD 2.1): 무기·방어구 5칸은 스킬, 장신구 4칸은 보조 권능.</summary>
    public static class SlotRules
    {
        public static readonly EquipSlot[] All =
        {
            EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.Head, EquipSlot.Body, EquipSlot.Boots,
            EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2, EquipSlot.Relic
        };

        public static bool GrantsSkill(EquipSlot slot) => slot <= EquipSlot.Boots;
        public static bool GrantsSupport(EquipSlot slot) => slot >= EquipSlot.Amulet;

        /// <summary>메인 세트 구성 부위: 머리 + 몸 + 신발 + 목걸이.</summary>
        public static bool IsSetSlot(EquipSlot slot)
            => slot == EquipSlot.Head || slot == EquipSlot.Body || slot == EquipSlot.Boots || slot == EquipSlot.Amulet;

        public static bool IsRing(EquipSlot slot) => slot == EquipSlot.Ring1 || slot == EquipSlot.Ring2;

        /// <summary>반지는 두 반지 칸 어디든 낄 수 있습니다.</summary>
        public static bool CanPlace(EquipSlot itemSlot, EquipSlot target)
            => itemSlot == target || (IsRing(itemSlot) && IsRing(target));

        /// <summary>슬롯에 내장될 수 있는 스킬 분류.</summary>
        public static AutoCastCategory[] SkillCategories(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.RightHand: return new[] { AutoCastCategory.Primary };
                case EquipSlot.LeftHand: return new[] { AutoCastCategory.Primary, AutoCastCategory.Burst, AutoCastCategory.Aura };
                case EquipSlot.Head: return new[] { AutoCastCategory.Tactical };
                case EquipSlot.Body: return new[] { AutoCastCategory.Survival, AutoCastCategory.Reactive };
                case EquipSlot.Boots: return new[] { AutoCastCategory.Mobility };
                default: return new AutoCastCategory[0];
            }
        }

        public static string DisplayName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.RightHand: return "오른손";
                case EquipSlot.LeftHand: return "왼손";
                case EquipSlot.Head: return "머리";
                case EquipSlot.Body: return "몸";
                case EquipSlot.Boots: return "신발";
                case EquipSlot.Amulet: return "목걸이";
                case EquipSlot.Ring1: return "반지1";
                case EquipSlot.Ring2: return "반지2";
                default: return "성물";
            }
        }
    }

    /// <summary>요구 능력치. 직업 제한은 없습니다 (GDD 1.1 무직업 스킬 개방).</summary>
    public readonly struct Requirements
    {
        public readonly int Strength;
        public readonly int Dexterity;
        public readonly int Intelligence;

        public Requirements(int strength, int dexterity, int intelligence)
        {
            Strength = strength;
            Dexterity = dexterity;
            Intelligence = intelligence;
        }

        public bool IsMetBy(IStatReader stats)
            => stats.Get(StatType.Strength) >= Strength
               && stats.Get(StatType.Dexterity) >= Dexterity
               && stats.Get(StatType.Intelligence) >= Intelligence;

        public override string ToString()
        {
            var parts = new List<string>();
            if (Strength > 0) parts.Add($"힘 {Strength}");
            if (Dexterity > 0) parts.Add($"민첩 {Dexterity}");
            if (Intelligence > 0) parts.Add($"지능 {Intelligence}");
            return parts.Count == 0 ? "없음" : string.Join(" / ", parts);
        }
    }

    /// <summary>장비 내장 스킬 (스킬 코드 + 스킬 등급).</summary>
    public readonly struct SkillGrant
    {
        public readonly string SkillCode;
        public readonly SkillRarity Rarity;

        public SkillGrant(string skillCode, SkillRarity rarity)
        {
            SkillCode = skillCode;
            Rarity = rarity;
        }

        public bool IsValid => !string.IsNullOrEmpty(SkillCode);
    }

    /// <summary>
    /// 장비 한 개 (GDD 6.1 JSON 스키마에 대응). 생성 후에는 바뀌지 않는 값 객체로 다룹니다.
    /// </summary>
    public sealed class Item
    {
        static int _nextId;

        public Item()
        {
            Id = "i" + System.Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public EquipSlot Slot { get; set; }
        public WeaponSubtype Subtype { get; set; }
        public ItemRarity Rarity { get; set; }
        public int ItemLevel { get; set; } = 1;
        public Requirements Requirements { get; set; }

        /// <summary>고정 베이스 (무기 공격력, 방어도 등).</summary>
        public List<StatModifier> BaseStats { get; } = new List<StatModifier>();
        /// <summary>주/보조 속성.</summary>
        public List<StatModifier> Affixes { get; } = new List<StatModifier>();
        /// <summary>전설 권능 등 태그 조건부 최종 곱연산.</summary>
        public List<TagMultiplier> TagMultipliers { get; } = new List<TagMultiplier>();

        public SkillGrant GrantedSkill { get; set; }
        public string SupportCode { get; set; }
        public string SetId { get; set; }
        /// <summary>특수 장비칸 메타: 세트 요구 개수 -1.</summary>
        public bool ReducesSetRequirement { get; set; }
        public string PowerDescription { get; set; }

        /// <summary>강화 단계 (+0 ~ +15). 단계마다 모든 수치가 8%씩 오릅니다.</summary>
        /// <summary>리듬 무기면 박자 간격(초), 아니면 0. 무기마다 박자가 다릅니다.</summary>
        public float BeatSeconds { get; set; }
        public bool IsRhythm => BeatSeconds > 0.05f;

        public int EnhanceLevel { get; set; }
        public static int MaxEnhanceLevel => Tuning.GameTuning.Current.Enhance.MaxLevel;
        public static float EnhanceBonusPerLevel => Tuning.GameTuning.Current.Enhance.BonusPerLevel;

        public float EnhanceMultiplier => 1f + EnhanceBonusPerLevel * EnhanceLevel;

        /// <summary>강화가 반영된 최종 수치.</summary>
        public IEnumerable<StatModifier> AllModifiers
        {
            get
            {
                foreach (var m in BaseStats) yield return Scale(m);
                foreach (var m in Affixes) yield return Scale(m);
            }
        }

        StatModifier Scale(StatModifier m)
            => m.Stat == StatType.AttacksPerSecond ? m : new StatModifier(m.Stat, m.Value * EnhanceMultiplier, m.Source);

        public string DisplayName => EnhanceLevel > 0 ? $"+{EnhanceLevel} {Name}" : Name;

        public override string ToString() => $"{DisplayName} ({Rarity}, {SlotRules.DisplayName(Slot)})";
    }
}
