using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Skills;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>색상 규칙 한 곳 모음 (속성·등급).</summary>
    public static class Palette
    {
        public static readonly Color ShadowBlue = new Color(0.25f, 0.66f, 1f);
        public static readonly Color PanelBackground = new Color(0.05f, 0.06f, 0.09f, 0.82f);
        public static readonly Color PanelBorder = new Color(0.2f, 0.4f, 0.7f, 0.9f);
        public static readonly Color Life = new Color(0.86f, 0.18f, 0.22f);
        public static readonly Color EnergyShield = new Color(0.35f, 0.75f, 1f);
        public static readonly Color Barrier = new Color(0.92f, 0.95f, 1f);
        public static readonly Color Resource = new Color(0.3f, 0.45f, 1f);
        public static readonly Color Experience = new Color(0.75f, 0.55f, 1f);

        public static Color For(DamageType type)
        {
            switch (type)
            {
                case DamageType.Fire: return new Color(1f, 0.48f, 0.18f);
                case DamageType.Cold: return new Color(0.5f, 0.85f, 1f);
                case DamageType.Lightning: return new Color(1f, 0.9f, 0.36f);
                case DamageType.Shadow: return new Color(0.48f, 0.36f, 1f);
                case DamageType.Light: return new Color(1f, 0.97f, 0.7f);
                case DamageType.Earth: return new Color(0.72f, 0.55f, 0.32f);
                default: return new Color(0.86f, 0.86f, 0.9f);
            }
        }

        public static Color For(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return new Color(0.36f, 0.55f, 1f);
                case ItemRarity.Rare: return new Color(1f, 0.85f, 0.29f);
                case ItemRarity.Legendary: return new Color(1f, 0.54f, 0.16f);
                case ItemRarity.Set: return new Color(0.25f, 0.88f, 0.42f);
                default: return new Color(0.9f, 0.9f, 0.9f);
            }
        }

        public static Color For(SkillRarity rarity)
        {
            switch (rarity)
            {
                case SkillRarity.Magic: return For(ItemRarity.Magic);
                case SkillRarity.Rare: return For(ItemRarity.Rare);
                case SkillRarity.Legendary: return For(ItemRarity.Legendary);
                default: return For(ItemRarity.Normal);
            }
        }

        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
