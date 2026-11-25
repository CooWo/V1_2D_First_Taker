using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Skills
{
    /// <summary>시전 1회의 문맥. 스킬 동작은 이 객체만 보고 일합니다.</summary>
    public sealed class SkillCast
    {
        public Combatant Caster;
        public SkillInstance Skill;
        public ICombatWorld World;
        public Combatant Target;
        public Vector2 Point;
        public SkillModifiers Mods;
        /// <summary>트리거·메아리 등으로 인한 피해 배율.</summary>
        public float DamageScale = 1f;
        public bool IsTriggered;

        public SkillDefinition Definition => Skill.Definition;
        public SkillRarityProfile Profile => Skill.Profile;
        public bool HasGimmick => Profile.HasGimmick;
        public bool IsAwakened => Profile.IsAwakened;
        public Team Team => Caster.Team;
        public Vector2 Origin => Caster.Position;

        public DamageType Element
            => Definition.DamageType == DamageType.Physical && Mods.ConvertPhysicalTo.HasValue
                ? Mods.ConvertPhysicalTo.Value
                : Definition.DamageType;

        public SkillTag Tags => Definition.Tags | Element.ToTag();

        public Vector2 Direction
        {
            get
            {
                var delta = Point - Caster.Position;
                return delta.sqrMagnitude > 0.0001f ? delta.normalized : Caster.Facing;
            }
        }

        public float Area(float baseRadius) => baseRadius * Mods.AreaMultiplier;

        /// <summary>기본 피해 = 무기 피해 + 고정 추가 피해.</summary>
        public float BaseDamage => Mathf.Max(1f, Caster.Stats.Get(StatType.WeaponDamage) + Caster.Stats.Get(StatType.FlatDamage));

        public float ScaledDamage(float coefficientScale)
            => BaseDamage * Definition.Coefficient * coefficientScale * Profile.DamageMultiplier * Mods.DamageMultiplier * DamageScale;

        public DamageResult Hit(Combatant target, float coefficientScale = 1f, SkillTag extraTags = SkillTag.None, bool canCrit = true)
        {
            return World.Damage.Deal(new DamageRequest
            {
                Attacker = Caster,
                Target = target,
                Tags = Tags | extraTags,
                Type = Element,
                BaseAmount = ScaledDamage(coefficientScale),
                CanCrit = canCrit
            });
        }

        public void Vfx(VfxKind kind, Vector2 position, float radius, float duration = 0.25f)
            => World.Emit(VfxEvent.At(kind, position, radius, Element, duration));
    }
}
