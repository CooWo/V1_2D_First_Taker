namespace ShadowRPG.Core.Stats
{
    /// <summary>GDD 1.2: 모든 장비 착용과 스킬 계수의 기준이 되는 3대 코어 스탯.</summary>
    public enum CoreStat
    {
        Strength,
        Dexterity,
        Intelligence
    }

    /// <summary>
    /// 캐릭터·장비·버프가 공유하는 능력치 키. Pct 접미사는 "증가 %" 수치(10 = +10%)입니다.
    /// </summary>
    public enum StatType
    {
        // 코어 스탯
        Strength,
        Dexterity,
        Intelligence,

        // 생존
        MaxLife,
        LifePct,
        LifeRegen,
        Armor,
        ArmorEffectPct,
        EnergyShield,
        EnergyShieldPct,
        EvasionPct,
        BlockChancePct,
        AllResistPct,
        EliteDamageReductionPct,

        // 자원
        MaxResource,
        ResourceRegen,
        ResourceCostReductionPct,

        // 공격
        WeaponDamage,
        AttacksPerSecond,
        FlatDamage,
        AttackSpeedPct,
        CastSpeedPct,
        CritChancePct,
        CritDamagePct,
        LifeOnHit,

        // 피해 증가 (태그별)
        AllDamagePct,
        MeleeDamagePct,
        ProjectileDamagePct,
        SpellDamagePct,
        ElementalDamagePct,
        PhysicalDamagePct,
        BossDamagePct,

        // 스킬 형태
        AreaPct,
        CooldownReductionPct,
        ExtraProjectiles,
        ExtraChains,
        ExtraPierce,

        // 이동
        MoveSpeedPct
    }
}
