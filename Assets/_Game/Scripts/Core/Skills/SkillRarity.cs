namespace ShadowRPG.Core.Skills
{
    /// <summary>GDD 3장 스킬 등급.</summary>
    public enum SkillRarity
    {
        Normal,
        Magic,
        Rare,
        Legendary
    }

    /// <summary>
    /// 등급별 수치 프로필. 등급이 오를수록 계수 + 자동 사냥 편의성 + 메커니즘이 진화합니다.
    /// </summary>
    public readonly struct SkillRarityProfile
    {
        public readonly float DamageMultiplier;
        public readonly float CooldownMultiplier;
        public readonly float CostMultiplier;
        /// <summary>마법 이상: 체력 낮은 적 / 밀집 지역 우선 조준.</summary>
        public readonly bool SmartTargeting;
        /// <summary>희귀 이상: 보조 기믹 1종 (관통 +1, 범위 +20%, 상태이상 등).</summary>
        public readonly bool HasGimmick;
        /// <summary>전설: 메커니즘 각성.</summary>
        public readonly bool IsAwakened;

        SkillRarityProfile(float damage, float cooldown, float cost, bool smart, bool gimmick, bool awakened)
        {
            DamageMultiplier = damage;
            CooldownMultiplier = cooldown;
            CostMultiplier = cost;
            SmartTargeting = smart;
            HasGimmick = gimmick;
            IsAwakened = awakened;
        }

        public static SkillRarityProfile For(SkillRarity rarity)
        {
            switch (rarity)
            {
                case SkillRarity.Magic: return new SkillRarityProfile(1.15f, 0.90f, 0.85f, true, false, false);
                case SkillRarity.Rare: return new SkillRarityProfile(1.35f, 0.90f, 0.85f, true, true, false);
                case SkillRarity.Legendary: return new SkillRarityProfile(1.65f, 0.90f, 0.85f, true, true, true);
                default: return new SkillRarityProfile(1.0f, 1.0f, 1.0f, false, false, false);
            }
        }

        /// <summary>희귀 기믹 중 "범위 +20%"를 쓰는 스킬용 배율.</summary>
        public float GimmickArea => HasGimmick ? 1.2f : 1f;
    }
}
