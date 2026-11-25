using System;

namespace ShadowRPG.Core.Combat
{
    /// <summary>
    /// GDD 4장 근·원거리 대칭 밸런싱 수치. 기획 조정은 이 값만 바꾸면 됩니다.
    /// </summary>
    [Serializable]
    public sealed class BalanceSettings
    {
        // 4.1-③ 기본 계수·유지력 비대칭
        public float MeleeDamageMultiplier = 1.4f;
        public float RangedDamageMultiplier = 1.0f;
        public float MeleeLifeOnHitEfficiency = 1.0f;
        public float RangedLifeOnHitEfficiency = 0.45f;

        // 4.1-① 그림자 갑주
        public float FortifyDuration = 4f;
        public float FortifyDamageReduction = 0.30f;

        // 4.1-② 그림자 추적 (Auto-Shadow Step)
        public float ShadowStepRadius = 3.5f;
        public float ShadowStepDuration = 0.15f;
        public float ShadowStepCooldown = 0.5f;

        // 4.1-⑤ / 4.2 자동 사냥 판단
        public float AutoCastInterval = 0.1f;
        public float RangedThreatRadius = 1.5f;
        public int RangedThreatCount = 2;
        public float MeleeEngageDistance = 4f;
        public float SurvivalLifeThreshold = 0.5f;
        public float EnemySearchRadius = 14f;

        // 치명타 / 상한
        public float BaseCritMultiplier = 1.5f;
        public float MaxResistPct = 75f;
        public float MaxBlockPct = 75f;
        public float MaxEvasionPct = 60f;
        public float MaxCritChancePct = 95f;
    }
}
