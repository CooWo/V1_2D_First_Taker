namespace LootHunters
{
    public enum SkillId { Whirlwind = 0, Dash = 1, Guard = 2, Frenzy = 3 }

    /// <summary>스킬 정의. 단축키 1~4, 레벨이 되면 해금됩니다.</summary>
    public class SkillDef
    {
        public SkillId id;
        public string name;
        public string desc;
        public int unlockLevel;
        public float cooldown;

        public static readonly SkillDef[] All =
        {
            new SkillDef { id = SkillId.Whirlwind, name = "회전베기", unlockLevel = 1, cooldown = 5f,
                           desc = "주변 모든 적에게 공격력 160% 피해" },
            new SkillDef { id = SkillId.Dash,      name = "돌진",     unlockLevel = 3, cooldown = 7f,
                           desc = "5칸 안의 적에게 순식간에 돌진, 250% 피해" },
            new SkillDef { id = SkillId.Guard,     name = "방어막",   unlockLevel = 5, cooldown = 14f,
                           desc = "6초 동안 최대 HP 35%만큼 피해 흡수" },
            new SkillDef { id = SkillId.Frenzy,    name = "광폭화",   unlockLevel = 8, cooldown = 18f,
                           desc = "6초 동안 공격 속도 2배, 이동 속도 1.3배" },
        };

        public static SkillDef Get(SkillId id) => All[(int)id];
    }
}
