using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Stats;

namespace ShadowRPG.Core.Classes
{
    /// <summary>
    /// GDD 1.1 직업 = PoE 전직(Ascendancy). 스킬을 해금하지 않고 빌드 메커니즘만 극대화합니다.
    /// </summary>
    public sealed class CharacterClass
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public CoreStat Affinity { get; set; }
        public string Description { get; set; }
        public List<StatModifier> Passives { get; } = new List<StatModifier>();
        public List<TagMultiplier> TagMultipliers { get; } = new List<TagMultiplier>();
    }

    public interface IClassCatalog
    {
        IReadOnlyList<CharacterClass> All { get; }
        CharacterClass Get(string id);
    }

    public sealed class ClassCatalog : IClassCatalog
    {
        public ClassCatalog(IEnumerable<CharacterClass> classes) => All = classes.ToList();

        public IReadOnlyList<CharacterClass> All { get; }
        public CharacterClass Get(string id) => All.FirstOrDefault(c => c.Id == id);

        public static ClassCatalog CreateDefault()
        {
            var knight = new CharacterClass { Id = "knight", Name = "기사", Affinity = CoreStat.Strength, Description = "막기·방어도 극대화, 피격 반격과 근접 탱딜" };
            knight.Passives.AddRange(new[]
            {
                new StatModifier(StatType.Strength, 15f), new StatModifier(StatType.BlockChancePct, 15f),
                new StatModifier(StatType.ArmorEffectPct, 30f), new StatModifier(StatType.LifePct, 10f)
            });
            knight.TagMultipliers.Add(new TagMultiplier(SkillTag.Trigger | SkillTag.Melee, 1.3f));

            var mage = new CharacterClass { Id = "mage", Name = "마법사", Affinity = CoreStat.Intelligence, Description = "상태이상 확산, 범위 확장, 보호막, 쿨타임 단축" };
            mage.Passives.AddRange(new[]
            {
                new StatModifier(StatType.Intelligence, 15f), new StatModifier(StatType.AreaPct, 20f),
                new StatModifier(StatType.CooldownReductionPct, 10f), new StatModifier(StatType.EnergyShieldPct, 10f)
            });

            var archer = new CharacterClass { Id = "archer", Name = "궁수", Affinity = CoreStat.Dexterity, Description = "[투사체] +1, 관통/연쇄 보너스, 이동 속도" };
            archer.Passives.AddRange(new[]
            {
                new StatModifier(StatType.Dexterity, 15f), new StatModifier(StatType.ExtraProjectiles, 1f),
                new StatModifier(StatType.ProjectileDamagePct, 15f), new StatModifier(StatType.MoveSpeedPct, 10f)
            });

            var monk = new CharacterClass { Id = "monk", Name = "수도사", Affinity = CoreStat.Dexterity, Description = "공격 속도, 치명타 한계 돌파, 분신·치명타 트리거" };
            monk.Passives.AddRange(new[]
            {
                new StatModifier(StatType.Dexterity, 8f), new StatModifier(StatType.Strength, 4f), new StatModifier(StatType.Intelligence, 4f),
                new StatModifier(StatType.AttackSpeedPct, 15f), new StatModifier(StatType.CritChancePct, 5f), new StatModifier(StatType.CritDamagePct, 30f)
            });
            monk.TagMultipliers.Add(new TagMultiplier(SkillTag.Clone, 1.3f));

            return new ClassCatalog(new[] { knight, mage, archer, monk });
        }
    }

    /// <summary>직업 패시브를 전투원에 붙이고 교체합니다. 직업은 장착 가능한 스킬을 제한하지 않습니다.</summary>
    public sealed class ClassPassiveApplier
    {
        readonly Combatant _owner;

        public ClassPassiveApplier(Combatant owner) => _owner = owner;

        public CharacterClass Current { get; private set; }

        public void Apply(CharacterClass characterClass)
        {
            if (Current != null)
            {
                _owner.Stats.RemoveSource(Current);
                _owner.TagMultipliers.RemoveSource(Current);
            }

            Current = characterClass;
            if (characterClass == null) return;

            _owner.Stats.AddModifiers(characterClass.Passives, characterClass);
            foreach (var tm in characterClass.TagMultipliers) _owner.TagMultipliers.Add(tm.WithSource(characterClass));
        }
    }
}
