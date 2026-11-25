using System;

namespace ShadowRPG.Core.Stats
{
    /// <summary>
    /// GDD 1.2 3대 코어 스탯 환산표 (10포인트 단위).
    /// 힘: 생명력 +50, 근접·물리 피해 +2%, 방어도 효율 +1%
    /// 민첩: 공격·시전 속도 +0.5%, 치명타 +1%, 회피 +1%, 이동 반응성 +0.5%
    /// 지능: 최대 자원 +20, 자원 재생 +0.5, 보호막 +2%, 주문·원소 피해 +2%
    /// </summary>
    public sealed class CoreStatDerivationRule : IStatDerivationRule
    {
        public float Contribute(StatType target, Func<StatType, float> raw)
        {
            switch (target)
            {
                case StatType.MaxLife: return Per10(raw, StatType.Strength) * 50f;
                case StatType.MeleeDamagePct: return Per10(raw, StatType.Strength) * 2f;
                case StatType.PhysicalDamagePct: return Per10(raw, StatType.Strength) * 2f;
                case StatType.ArmorEffectPct: return Per10(raw, StatType.Strength) * 1f;

                case StatType.AttackSpeedPct: return Per10(raw, StatType.Dexterity) * 0.5f;
                case StatType.CastSpeedPct: return Per10(raw, StatType.Dexterity) * 0.5f;
                case StatType.CritChancePct: return Per10(raw, StatType.Dexterity) * 1f;
                case StatType.EvasionPct: return Per10(raw, StatType.Dexterity) * 1f;
                case StatType.MoveSpeedPct: return Per10(raw, StatType.Dexterity) * 0.5f;

                case StatType.MaxResource: return Per10(raw, StatType.Intelligence) * 20f;
                case StatType.ResourceRegen: return Per10(raw, StatType.Intelligence) * 0.5f;
                case StatType.EnergyShieldPct: return Per10(raw, StatType.Intelligence) * 2f;
                case StatType.SpellDamagePct: return Per10(raw, StatType.Intelligence) * 2f;
                case StatType.ElementalDamagePct: return Per10(raw, StatType.Intelligence) * 2f;

                default: return 0f;
            }
        }

        static float Per10(Func<StatType, float> raw, StatType stat) => raw(stat) / 10f;
    }
}
