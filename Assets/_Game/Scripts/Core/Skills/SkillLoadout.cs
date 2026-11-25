using System;
using System.Collections.Generic;

namespace ShadowRPG.Core.Skills
{
    /// <summary>
    /// 현재 장비로부터 얻은 액티브 스킬과 보조 권능 묶음. 내용 구성은 LoadoutBuilder가 담당합니다 (SRP).
    /// </summary>
    public sealed class SkillLoadout
    {
        readonly List<SkillInstance> _skills = new List<SkillInstance>();
        readonly List<ISkillModifierSource> _modifierSources = new List<ISkillModifierSource>();

        public IReadOnlyList<SkillInstance> Skills => _skills;
        public IReadOnlyList<ISkillModifierSource> ModifierSources => _modifierSources;

        /// <summary>오른손 주력 스킬.</summary>
        public SkillInstance Primary { get; private set; }
        /// <summary>자원이 없을 때 쓰는 기본 공격 (항상 존재).</summary>
        public SkillInstance BasicAttack { get; private set; }

        /// <summary>GDD 4.2: 주력기가 [근접]인지가 이동/회피 판단의 기준입니다.</summary>
        public bool PrimaryIsMelee => (Primary ?? BasicAttack)?.Definition.IsMelee ?? true;

        public event Action Changed;

        public void Set(IEnumerable<SkillInstance> skills, SkillInstance primary, SkillInstance basicAttack, IEnumerable<ISkillModifierSource> modifierSources)
        {
            _skills.Clear();
            _skills.AddRange(skills);
            _modifierSources.Clear();
            _modifierSources.AddRange(modifierSources);
            Primary = primary;
            BasicAttack = basicAttack;
            Changed?.Invoke();
        }

        public IEnumerable<SkillInstance> ByCategory(AutoCastCategory category)
        {
            for (int i = 0; i < _skills.Count; i++)
                if (_skills[i].Definition.Category == category) yield return _skills[i];
        }

        public SkillInstance Find(Func<SkillInstance, bool> predicate)
        {
            for (int i = 0; i < _skills.Count; i++)
                if (predicate(_skills[i])) return _skills[i];
            return null;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _skills.Count; i++) _skills[i].Tick(dt);
            BasicAttack?.Tick(dt);
        }
    }
}
