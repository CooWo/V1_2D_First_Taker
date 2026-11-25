using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.World;

namespace ShadowRPG.Core.Supports
{
    /// <summary>
    /// GDD 5.4 보조 권능 (장신구·특수 장비칸). 스킬 수를 늘리지 않고 보유 스킬의 형태를 바꿉니다.
    /// </summary>
    public interface ISupportEffect : ISkillModifierSource
    {
        string Code { get; }
    }

    /// <summary>시전 형태가 아니라 "사건"에 반응해 스킬을 발동시키는 권능 (CoC, CwDT).</summary>
    public interface ITriggerSupport
    {
        void Bind(Combatant owner, ICombatWorld world);
        void Unbind();
    }

    public sealed class SupportDefinition
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>장비 1개당 새 인스턴스 (트리거 상태가 장비별로 분리되도록).</summary>
        public Func<ISupportEffect> Create { get; set; }
    }

    /// <summary>형태 변화만 하는 권능의 공통 기반.</summary>
    public abstract class ModifierSupport : ISupportEffect
    {
        protected ModifierSupport(string code) => Code = code;
        public string Code { get; }
        public abstract void Modify(Combatant caster, SkillInstance skill, SkillModifiers modifiers);
    }

    /// <summary>SUP-03 다중 투사체 분할: 모든 [투사체] 스킬 +N발, 단일 피해 0.75배.</summary>
    public sealed class GreaterMultipleProjectilesSupport : ModifierSupport
    {
        readonly int _extra;
        public GreaterMultipleProjectilesSupport(int extra = 3) : base("SUP-03") => _extra = extra;

        public override void Modify(Combatant caster, SkillInstance skill, SkillModifiers modifiers)
        {
            if (!skill.Tags.HasAny(SkillTag.Projectile)) return;
            modifiers.ExtraProjectiles += _extra;
            modifiers.DamageMultiplier *= 0.75f;
        }
    }

    /// <summary>SUP-04 그림자 메아리: 주력 스킬을 0.25초 뒤 무료로 1회 반복.</summary>
    public sealed class ShadowEchoSupport : ModifierSupport
    {
        public ShadowEchoSupport() : base("SUP-04") { }

        public override void Modify(Combatant caster, SkillInstance skill, SkillModifiers modifiers)
        {
            if (skill.Definition.Category != AutoCastCategory.Primary) return;
            if (!skill.Tags.HasAny(SkillTag.Attack | SkillTag.Spell)) return;
            modifiers.Echo = true;
        }
    }

    /// <summary>SUP-05 연쇄 및 갈래 분열: 투사체/연쇄 스킬 +2 연쇄, 투사체는 첫 적중 후 갈래.</summary>
    public sealed class ChainAndForkSupport : ModifierSupport
    {
        public ChainAndForkSupport() : base("SUP-05") { }

        public override void Modify(Combatant caster, SkillInstance skill, SkillModifiers modifiers)
        {
            if (!skill.Tags.HasAny(SkillTag.Projectile | SkillTag.Chain)) return;
            modifiers.ExtraChains += 2;
            if (skill.Tags.HasAny(SkillTag.Projectile)) modifiers.Fork = true;
        }
    }

    /// <summary>SUP-06 원소/심연 완전 전환: 물리 피해를 지정 속성으로 100% 전환.</summary>
    public sealed class ConversionSupport : ModifierSupport
    {
        readonly DamageType _to;
        public ConversionSupport(DamageType to) : base("SUP-06") => _to = to;

        public override void Modify(Combatant caster, SkillInstance skill, SkillModifiers modifiers)
            => modifiers.ConvertPhysicalTo = _to;
    }

    /// <summary>SUP-01 치명타 시 주문 시전 (CoC): [공격] 치명타 시 보유 [주문] 1개 자동 발동 (내부 쿨 0.25초, 80%).</summary>
    public sealed class CastOnCritSupport : ISupportEffect, ITriggerSupport, IDamageListener
    {
        const float InternalCooldown = 0.25f;
        const float DamageScale = 0.8f;

        Combatant _owner;
        ICombatWorld _world;
        float _lastTrigger = -10f;
        int _rotation;

        public string Code => "SUP-01";

        public void Modify(Combatant caster, SkillInstance skill, SkillModifiers modifiers) { }

        public void Bind(Combatant owner, ICombatWorld world)
        {
            _owner = owner;
            _world = world;
            world.Damage.AddListener(this);
        }

        public void Unbind()
        {
            _world?.Damage.RemoveListener(this);
            _owner = null;
            _world = null;
        }

        public void OnDamageResolved(in DamageResult result)
        {
            if (_owner == null || result.Attacker != _owner || !result.IsCrit || !result.Landed) return;
            if (!result.Tags.HasAny(SkillTag.Attack) || result.Tags.HasAny(SkillTag.Trigger)) return;
            if (_world.Time - _lastTrigger < InternalCooldown) return;

            var spells = _owner.Loadout?.Skills.Where(IsTriggerable).ToList();
            if (spells == null || spells.Count == 0) return;

            _lastTrigger = _world.Time;
            var spell = spells[_rotation++ % spells.Count];
            var target = result.Target;
            _world.Executor.TriggerCast(_owner, spell, target != null && target.IsAlive ? target : null,
                target != null ? target.Position : _owner.Position, DamageScale);
        }

        static bool IsTriggerable(SkillInstance s)
            => s.Definition.IsSpell && !s.Definition.IsAttack
               && s.Definition.Category != AutoCastCategory.Aura
               && s.Definition.Category != AutoCastCategory.Survival
               && s.Definition.Category != AutoCastCategory.Reactive;
    }

    /// <summary>SUP-02 피격 시 자동 시전 (CwDT): 누적 피해가 최대 생명력 25%마다 방어기/범위기 무료 발동.</summary>
    public sealed class CastWhenDamageTakenSupport : ISupportEffect, ITriggerSupport
    {
        const float Threshold = 0.25f;

        Combatant _owner;
        ICombatWorld _world;
        float _accumulated;

        public string Code => "SUP-02";

        public void Modify(Combatant caster, SkillInstance skill, SkillModifiers modifiers) { }

        public void Bind(Combatant owner, ICombatWorld world)
        {
            _owner = owner;
            _world = world;
            owner.Health.Damaged += OnDamaged;
        }

        public void Unbind()
        {
            if (_owner != null) _owner.Health.Damaged -= OnDamaged;
            _owner = null;
            _world = null;
        }

        void OnDamaged(float amount)
        {
            if (_owner == null || !_owner.IsAlive) return;
            _accumulated += amount;
            if (_accumulated < _owner.Health.MaxLife * Threshold) return;
            _accumulated = 0f;

            var loadout = _owner.Loadout;
            var skill = loadout?.Find(s => s.Definition.Category == AutoCastCategory.Survival)
                        ?? loadout?.Find(s => s.Tags.HasAny(SkillTag.Area) && s.Definition.Category != AutoCastCategory.Aura);
            if (skill == null) return;

            var target = _world.FindNearestEnemy(_owner.Team, _owner.Position, 8f);
            _world.Executor.TriggerCast(_owner, skill, target, target != null ? target.Position : _owner.Position, 1f);
        }
    }

    public interface ISupportCatalog
    {
        IReadOnlyList<SupportDefinition> All { get; }
        SupportDefinition Get(string code);
    }

    public sealed class SupportCatalog : ISupportCatalog
    {
        readonly Dictionary<string, SupportDefinition> _byCode;

        public SupportCatalog(IEnumerable<SupportDefinition> definitions)
        {
            All = definitions.ToList();
            _byCode = All.ToDictionary(d => d.Code);
        }

        public IReadOnlyList<SupportDefinition> All { get; }

        public SupportDefinition Get(string code) => code != null && _byCode.TryGetValue(code, out var def) ? def : null;

        public static SupportCatalog CreateDefault() => new SupportCatalog(new[]
        {
            new SupportDefinition { Code = "SUP-01", Name = "치명타 시 주문 시전", Description = "[공격] 치명타 시 보유 [주문] 자동 발동 (80%)", Create = () => new CastOnCritSupport() },
            new SupportDefinition { Code = "SUP-02", Name = "피격 시 자동 시전", Description = "누적 피해 25%마다 방어기/범위기 무료 발동", Create = () => new CastWhenDamageTakenSupport() },
            new SupportDefinition { Code = "SUP-03", Name = "다중 투사체 분할", Description = "[투사체] +3발, 단일 피해 0.75배", Create = () => new GreaterMultipleProjectilesSupport() },
            new SupportDefinition { Code = "SUP-04", Name = "그림자 메아리", Description = "주력 스킬 0.25초 뒤 무료 1회 반복", Create = () => new ShadowEchoSupport() },
            new SupportDefinition { Code = "SUP-05", Name = "연쇄 및 갈래 분열", Description = "투사체/연쇄 +2 연쇄, 갈래 분열", Create = () => new ChainAndForkSupport() },
            new SupportDefinition { Code = "SUP-06", Name = "심연 완전 전환", Description = "물리 피해 100% 암흑 전환", Create = () => new ConversionSupport(DamageType.Shadow) }
        });
    }
}
