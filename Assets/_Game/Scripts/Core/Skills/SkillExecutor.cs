using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Skills
{
    /// <summary>
    /// 시전 절차 (조건 확인 → 자원 소모 → 쿨타임 → 형태 변화 수집 → 동작 실행 → 관찰자 통지).
    /// 스킬이 "무엇을 하는지"는 모르고 "어떤 순서로 하는지"만 책임집니다 (SRP).
    /// </summary>
    public sealed class SkillExecutor
    {
        const float EchoDelay = 0.25f;

        readonly ICombatWorld _world;
        readonly List<ISkillModifierSource> _globalSources;
        readonly List<ICastListener> _listeners = new List<ICastListener>();
        readonly SkillModifiers _modifiers = new SkillModifiers();

        public SkillExecutor(ICombatWorld world, IEnumerable<ISkillModifierSource> globalSources)
        {
            _world = world;
            _globalSources = new List<ISkillModifierSource>(globalSources);
        }

        public void AddListener(ICastListener listener)
        {
            if (!_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void RemoveListener(ICastListener listener) => _listeners.Remove(listener);

        public bool CanCast(Combatant caster, SkillInstance skill)
            => skill != null && caster.CanAct && skill.IsReady && caster.Resource.CanAfford(skill.GetCost(caster.Stats));

        /// <summary>일반 시전: 자원·쿨타임·후딜레이를 모두 적용합니다.</summary>
        public bool TryCast(Combatant caster, SkillInstance skill, Combatant target, Vector2 point)
        {
            if (!CanCast(caster, skill)) return false;

            caster.Resource.TrySpend(skill.GetCost(caster.Stats));
            float cooldown = skill.GetCooldown(caster.Stats);
            if (cooldown > 0f) skill.StartCooldown(cooldown);
            caster.LockActions(GetActionTime(caster, skill));

            var delta = point - caster.Position;
            if (delta.sqrMagnitude > 0.0001f) caster.Facing = delta.normalized;

            Execute(caster, skill, target, point, 1f, false);
            return true;
        }

        /// <summary>
        /// 트리거 시전 (치명타 시 시전, 피격 시 시전, 메아리, 마검 연계): 자원·쿨타임·후딜레이 없이 발동합니다.
        /// </summary>
        public void TriggerCast(Combatant caster, SkillInstance skill, Combatant target, Vector2 point, float damageScale)
        {
            if (skill == null || !caster.IsAlive) return;
            Execute(caster, skill, target, point, damageScale, true);
        }

        public float GetActionTime(Combatant caster, SkillInstance skill)
        {
            var def = skill.Definition;
            if (def.IsAttack)
            {
                float aps = Mathf.Max(0.3f, caster.Stats.Get(StatType.AttacksPerSecond));
                return def.CastTime * (1.2f / aps) / caster.AttackSpeedMultiplier;
            }
            return def.CastTime / caster.CastSpeedMultiplier;
        }

        void Execute(Combatant caster, SkillInstance skill, Combatant target, Vector2 point, float damageScale, bool triggered)
        {
            var mods = new SkillModifiers();
            for (int i = 0; i < _globalSources.Count; i++) _globalSources[i].Modify(caster, skill, mods);
            var sources = caster.Loadout?.ModifierSources;
            if (sources != null)
                for (int i = 0; i < sources.Count; i++) sources[i].Modify(caster, skill, mods);

            var cast = new SkillCast
            {
                Caster = caster,
                Skill = skill,
                World = _world,
                Target = target,
                Point = point,
                Mods = mods,
                DamageScale = damageScale,
                IsTriggered = triggered
            };

            skill.Definition.Behavior?.Execute(cast);

            for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnSkillCast(cast);

            if (!triggered && mods.Echo)
            {
                var echoTarget = target;
                var echoPoint = point;
                _world.Emit(VfxEvent.At(VfxKind.Blink, caster.Position, 0.6f, cast.Element, EchoDelay));
                _world.Schedule(EchoDelay, () =>
                {
                    if (!caster.IsAlive) return;
                    var aim = echoTarget != null && echoTarget.IsAlive ? echoTarget.Position : echoPoint;
                    TriggerCast(caster, skill, echoTarget, aim, 1f);
                });
            }
        }
    }
}
