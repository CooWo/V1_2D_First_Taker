using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Supports;
using ShadowRPG.Core.World;

namespace ShadowRPG.Core.Character
{
    /// <summary>
    /// 장비 → 스킬 묶음 변환. 무기·방어구 5칸에서 스킬을, 장신구 4칸에서 보조 권능을 모읍니다.
    /// 같은 장비가 유지되면 스킬 인스턴스(쿨타임·오라 상태)를 그대로 재사용합니다.
    /// </summary>
    public sealed class LoadoutBuilder
    {
        readonly Equipment _equipment;
        readonly Combatant _owner;
        readonly ICombatWorld _world;
        readonly ISkillCatalog _skills;
        readonly ISupportCatalog _supports;

        readonly Dictionary<Item, SkillInstance> _instances = new Dictionary<Item, SkillInstance>();
        readonly List<ITriggerSupport> _boundTriggers = new List<ITriggerSupport>();
        readonly List<ReactiveSkillTrigger> _reactiveTriggers = new List<ReactiveSkillTrigger>();
        readonly SkillInstance _basicMelee;
        readonly SkillInstance _basicRanged;

        public LoadoutBuilder(Equipment equipment, Combatant owner, ICombatWorld world, ISkillCatalog skills, ISupportCatalog supports)
        {
            _equipment = equipment;
            _owner = owner;
            _world = world;
            _skills = skills;
            _supports = supports;
            _basicMelee = new SkillInstance(skills.BasicMelee, SkillRarity.Normal);
            _basicRanged = new SkillInstance(skills.BasicRanged, SkillRarity.Normal);

            if (owner.Loadout == null) owner.Loadout = new SkillLoadout();
            equipment.Changed += (_, __, ___) => Rebuild();
        }

        public void Rebuild()
        {
            var skills = new List<SkillInstance>();
            var keep = new HashSet<Item>();
            SkillInstance primary = null;

            foreach (var slot in SlotRules.All)
            {
                if (!SlotRules.GrantsSkill(slot)) continue;
                var item = _equipment.Get(slot);
                if (item == null || !item.GrantedSkill.IsValid) continue;
                var definition = _skills.Get(item.GrantedSkill.SkillCode);
                if (definition == null) continue;

                if (!_instances.TryGetValue(item, out var instance))
                {
                    instance = new SkillInstance(definition, item.GrantedSkill.Rarity, item);
                    _instances[item] = instance;
                }
                keep.Add(item);
                skills.Add(instance);
                if (slot == EquipSlot.RightHand && definition.Category == AutoCastCategory.Primary) primary = instance;
            }

            RemoveStaleInstances(keep);

            if (primary == null)
                foreach (var s in skills)
                    if (s.Definition.Category == AutoCastCategory.Primary) { primary = s; break; }

            var basic = primary == null || primary.Definition.IsMelee ? _basicMelee : _basicRanged;

            var modifierSources = RebindSupports();
            RebindReactive(skills);

            _owner.Loadout.Set(skills, primary, basic, modifierSources);
        }

        void RemoveStaleInstances(HashSet<Item> keep)
        {
            var stale = new List<Item>();
            foreach (var pair in _instances)
                if (!keep.Contains(pair.Key)) stale.Add(pair.Key);

            foreach (var item in stale)
            {
                var instance = _instances[item];
                if (instance.Definition.Behavior is IPersistentSkillBehavior persistent)
                    persistent.Deactivate(instance, _owner, _world);
                _instances.Remove(item);
            }
        }

        List<ISkillModifierSource> RebindSupports()
        {
            foreach (var trigger in _boundTriggers) trigger.Unbind();
            _boundTriggers.Clear();

            var sources = new List<ISkillModifierSource>();
            foreach (var slot in SlotRules.All)
            {
                var item = _equipment.Get(slot);
                var definition = _supports.Get(item?.SupportCode);
                if (definition == null) continue;

                var effect = definition.Create();
                sources.Add(effect);
                if (effect is ITriggerSupport trigger)
                {
                    trigger.Bind(_owner, _world);
                    _boundTriggers.Add(trigger);
                }
            }
            return sources;
        }

        void RebindReactive(List<SkillInstance> skills)
        {
            foreach (var trigger in _reactiveTriggers) _world.Damage.RemoveListener(trigger);
            _reactiveTriggers.Clear();

            foreach (var skill in skills)
            {
                if (skill.Definition.Category != AutoCastCategory.Reactive) continue;
                var trigger = new ReactiveSkillTrigger(_owner, skill, _world);
                _world.Damage.AddListener(trigger);
                _reactiveTriggers.Add(trigger);
            }
        }
    }

    /// <summary>
    /// 피격/막기 반응 스킬 발동기 (STR-08 복수의 가시 갑주): 35% 확률, 내부 쿨 0.8초.
    /// 전설 각성 시 막기 성공은 100% 발동.
    /// </summary>
    public sealed class ReactiveSkillTrigger : IDamageListener
    {
        const float Chance = 35f;
        const float InternalCooldown = 0.8f;

        readonly Combatant _owner;
        readonly SkillInstance _skill;
        readonly ICombatWorld _world;
        float _last = -10f;

        public ReactiveSkillTrigger(Combatant owner, SkillInstance skill, ICombatWorld world)
        {
            _owner = owner;
            _skill = skill;
            _world = world;
        }

        public void OnDamageResolved(in DamageResult result)
        {
            if (result.Target != _owner || result.Evaded || !_owner.IsAlive) return;
            if (_world.Time - _last < InternalCooldown) return;

            float chance = result.Blocked && _skill.Profile.IsAwakened ? 100f : Chance;
            if (!_world.Random.Chance(chance)) return;

            _last = _world.Time;
            var attacker = result.Attacker;
            _world.Executor.TriggerCast(_owner, _skill, attacker, attacker != null ? attacker.Position : _owner.Position, 1f);
        }
    }
}
