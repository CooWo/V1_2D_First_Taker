using System;

namespace ShadowRPG.Core
{
    /// <summary>PoE 스타일 스킬 태그. 보조 권능·세트·피해 규칙이 모두 이 태그로 대상을 고릅니다.</summary>
    [Flags]
    public enum SkillTag : long
    {
        None = 0,
        Strength = 1L << 0,
        Dexterity = 1L << 1,
        Intelligence = 1L << 2,
        Attack = 1L << 3,
        Spell = 1L << 4,
        Melee = 1L << 5,
        Ranged = 1L << 6,
        Projectile = 1L << 7,
        Area = 1L << 8,
        Physical = 1L << 9,
        Fire = 1L << 10,
        Cold = 1L << 11,
        Lightning = 1L << 12,
        Shadow = 1L << 13,
        Arcane = 1L << 14,
        Channelling = 1L << 15,
        Movement = 1L << 16,
        Slam = 1L << 17,
        Clone = 1L << 18,
        Duration = 1L << 19,
        Chain = 1L << 20,
        Pierce = 1L << 21,
        Warcry = 1L << 22,
        Buff = 1L << 23,
        Survival = 1L << 24,
        Trigger = 1L << 25,
        Aura = 1L << 26,
        Mark = 1L << 27,
        Critical = 1L << 28,
        MultiHit = 1L << 29,
        Poison = 1L << 30,
        Evasion = 1L << 31,
        Defense = 1L << 32,

        Elemental = Fire | Cold | Lightning
    }

    public static class SkillTagExtensions
    {
        public static bool HasAll(this SkillTag tags, SkillTag required) => (tags & required) == required;
        public static bool HasAny(this SkillTag tags, SkillTag any) => (tags & any) != 0;

        /// <summary>GDD 4.1: [근접] 태그가 있고 [원거리]가 아닌 스킬만 근접 규칙을 받습니다.</summary>
        public static bool IsMelee(this SkillTag tags) => tags.HasAny(SkillTag.Melee) && !tags.HasAny(SkillTag.Ranged);
        public static bool IsRanged(this SkillTag tags) => tags.HasAny(SkillTag.Ranged);
    }
}
