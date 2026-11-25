using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Status;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Skills.Behaviors
{
    /// <summary>INT-01 연쇄 번개: 최대 5회 연쇄. 전설: 남은 연쇄 1회당 최종 피해 +18% 곱연산.</summary>
    public sealed class ArcLightningBehavior : ISkillBehavior
    {
        const int BaseChains = 5;
        const float ChainRange = 4.5f;

        public void Execute(SkillCast cast)
        {
            var first = cast.Target;
            if (first == null) return;

            int maxChains = BaseChains + cast.Mods.ExtraChains;
            var targets = new List<Combatant> { first };
            var exclude = new HashSet<int> { first.Id };
            var current = first;
            while (targets.Count < maxChains + 1)
            {
                var next = cast.World.FindNearestEnemy(cast.Team, current.Position, ChainRange, exclude);
                if (next == null) break;
                targets.Add(next);
                exclude.Add(next.Id);
                current = next;
            }

            int unused = maxChains + 1 - targets.Count;
            float scale = cast.IsAwakened ? Mathf.Pow(1.18f, unused) : 1f;

            var from = cast.Origin;
            foreach (var target in targets)
            {
                cast.World.Emit(VfxEvent.Line(VfxKind.Beam, from, target.Position, DamageType.Lightning, 0.15f));
                from = target.Position;
                var result = cast.Hit(target, scale);
                if (cast.HasGimmick && result.Landed && target.IsAlive) target.Statuses.Apply(new ShockStatus(2f, 0.1f));
            }
        }
    }

    /// <summary>INT-02 공허의 소용돌이: 4초 블랙홀 + 끌어당김. 전설: 종료 시 타격 횟수 비례 붕괴 폭발.</summary>
    public sealed class VoidVortexBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            float radius = cast.Area(2.2f) * cast.Profile.GimmickArea;
            var zone = new Zone
            {
                Owner = cast.Caster, Position = cast.Point, Radius = radius, Duration = 4f, TickInterval = 0.5f,
                PullSpeed = 2.5f, Element = cast.Element, VisualKey = "void"
            };
            zone.OnTick = z => z.Counter += SkillPrimitives.HitCircle(cast, z.Position, z.Radius, 0.25f);
            if (cast.IsAwakened)
            {
                zone.OnExpire = z =>
                {
                    float blast = cast.Area(3.5f);
                    cast.Vfx(VfxKind.Explosion, z.Position, blast, 0.4f);
                    SkillPrimitives.HitCircle(cast, z.Position, blast, 0.25f + 0.05f * Mathf.Min(z.Counter, 40));
                };
            }
            cast.World.SpawnZone(zone);
        }
    }

    /// <summary>INT-03 운석 낙하: 거대 운석 + 점화. 전설: 소형 유성우 5발, 점화 2배 속도.</summary>
    public sealed class MeteorShowerBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var point = cast.Point;
            float radius = cast.Area(2.5f) * cast.Profile.GimmickArea;
            cast.Vfx(VfxKind.Mark, point, radius, 0.7f);
            cast.World.Emit(VfxEvent.Line(VfxKind.Meteor, point + new Vector2(-3f, 7f), point, DamageType.Fire, 0.7f));
            cast.World.Schedule(0.7f, () => Impact(cast, point, radius, 1f));

            if (!cast.IsAwakened) return;
            for (int i = 0; i < 5; i++)
            {
                var small = cast.World.ClampToBounds(point + SkillPrimitives.RandomOffset(cast.World.Random, 3f));
                float delay = 0.9f + 0.1f * i;
                cast.World.Emit(VfxEvent.Line(VfxKind.Meteor, small + new Vector2(-1.5f, 5f), small, DamageType.Fire, delay));
                cast.World.Schedule(delay, () => Impact(cast, small, 1f, 0.25f));
            }
        }

        static void Impact(SkillCast cast, Vector2 point, float radius, float coefficientScale)
        {
            cast.Vfx(VfxKind.Explosion, point, radius, 0.35f);
            SkillPrimitives.HitCircle(cast, point, radius, coefficientScale, (target, _) =>
            {
                if (!target.IsAlive) return;
                var ignite = SkillPrimitives.Ignite(cast, 3f, 0.12f * coefficientScale);
                if (cast.IsAwakened) ignite.SpeedMultiplier = 2f;
                target.Statuses.Apply(ignite);
            });
        }
    }

    /// <summary>INT-04 빙결 영혼 구체: 느리게 유영하며 얼음 바늘 난사. 전설: 관통할수록 커지고 끝에서 빙창 12개.</summary>
    public sealed class FreezingOrbBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            SkillPrimitives.FireSpread(cast, 1, 25f, cast.Direction, dir =>
            {
                var orb = SkillPrimitives.CreateProjectile(cast, cast.Origin, dir, 3.5f, 8f, 0.3f, "orb");
                orb.PassThrough = true;
                orb.Radius = 0.5f;
                orb.VisualScale = 1.2f;

                float timer = 0f;
                orb.OnUpdate = (p, dt) =>
                {
                    timer -= dt;
                    if (timer > 0f) return;
                    timer = 0.2f;
                    var victim = cast.World.FindNearestEnemy(cast.Team, p.Position, 2.4f * p.VisualScale / 1.2f);
                    if (victim == null) return;
                    cast.World.Emit(VfxEvent.Line(VfxKind.Beam, p.Position, victim.Position, DamageType.Cold, 0.08f));
                    var result = cast.Hit(victim, 0.2f);
                    if (cast.HasGimmick && result.Landed && victim.IsAlive) victim.Statuses.Apply(new SlowStatus(1f, 0.7f));
                };

                if (cast.IsAwakened)
                {
                    var baseHit = orb.OnHit;
                    orb.OnHit = (p, victim) =>
                    {
                        baseHit(p, victim);
                        p.VisualScale = Mathf.Min(2.4f, p.VisualScale + 0.12f);
                        p.Radius = Mathf.Min(1f, p.Radius + 0.05f);
                    };
                    orb.OnExpire = p =>
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            var spear = SkillPrimitives.CreateProjectile(cast, p.Position, CombatWorld.Rotate(Vector2.right, 30f * i), 12f, 5f, 0.5f, "ice");
                            spear.GenerationDepth = 1;
                            cast.World.SpawnProjectile(spear);
                        }
                    };
                }
                return orb;
            });
        }
    }

    /// <summary>INT-05 칼날 소용돌이: 캐릭터 주변 공전 칼날 (최대 5중첩). 전설: 5중첩 시 1.5배 + 보호막 흡수.</summary>
    public sealed class BladeVortexBehavior : ISkillBehavior, IPersistentSkillBehavior
    {
        const int MaxStacks = 5;
        const float Duration = 5f;

        public void Execute(SkillCast cast)
        {
            if (cast.Skill.State is Zone existing && !existing.IsExpired)
            {
                existing.Stacks = Mathf.Min(MaxStacks, existing.Stacks + 1);
                existing.Elapsed = 0f;
                return;
            }

            float baseRadius = cast.Area(1.4f) * cast.Profile.GimmickArea;
            var zone = new Zone
            {
                Owner = cast.Caster, FollowOwner = true, Radius = baseRadius, Duration = Duration, TickInterval = 0.35f,
                Element = cast.Element, VisualKey = "blade_vortex", Stacks = 1
            };
            zone.OnTick = z =>
            {
                bool empowered = cast.IsAwakened && z.Stacks >= MaxStacks;
                z.Radius = baseRadius * (empowered ? 1.5f : 1f);
                z.TickInterval = empowered ? 0.35f / 1.5f : 0.35f;
                SkillPrimitives.HitCircle(cast, z.Position, z.Radius, 0.3f * z.Stacks, (target, result) =>
                {
                    if (empowered) cast.Caster.Health.AddEnergyShield(result.Amount * 0.05f);
                });
            };
            cast.Skill.State = zone;
            cast.World.SpawnZone(zone);
        }

        public void Deactivate(SkillInstance skill, Combatant caster, ICombatWorld world)
        {
            if (skill.State is Zone zone) zone.Expire();
            skill.State = null;
        }
    }

    /// <summary>INT-06 비전 마검격: 주문 피해 증가가 그대로 적용되는 근접 베기. 전설: 3타마다 다른 주문 무료 자동 발사.</summary>
    public sealed class ArcaneSpellbladeBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            float radius = cast.Area(2f) * cast.Profile.GimmickArea;
            var dir = cast.Direction;
            cast.World.Emit(new VfxEvent { Kind = VfxKind.Cone, Position = cast.Origin, To = cast.Origin + dir * radius, Radius = radius, AngleDegrees = 110f, Element = cast.Element, Duration = 0.2f });
            SkillPrimitives.HitCone(cast, cast.Origin, dir, radius, 110f, 1f);

            if (!cast.IsAwakened || cast.IsTriggered) return;
            cast.Skill.Counter++;
            if (cast.Skill.Counter % 3 != 0) return;

            var spell = cast.Caster.Loadout?.Find(s => s != cast.Skill && s.Definition.IsSpell
                && s.Definition.Category != AutoCastCategory.Aura
                && s.Definition.Category != AutoCastCategory.Survival
                && s.Definition.Category != AutoCastCategory.Reactive);
            if (spell == null) return;
            var target = cast.Target;
            cast.World.Executor.TriggerCast(cast.Caster, spell, target, target != null ? target.Position : cast.Point, 1f);
        }
    }

    /// <summary>INT-07 심연의 방벽: 최대 자원 비례 흡수 방벽. 전설: 파괴/만료 시 반경 5m 빙결 충격파.</summary>
    public sealed class AbyssalAegisBehavior : ISkillBehavior
    {
        const float Duration = 5f;

        public void Execute(SkillCast cast)
        {
            var caster = cast.Caster;
            float amount = (caster.Resource.Total * 1.2f + caster.Health.MaxLife * 0.1f) * (cast.HasGimmick ? 1.2f : 1f) * cast.Profile.DamageMultiplier;
            caster.Health.AddBarrier(amount, Duration);
            cast.Vfx(VfxKind.Shield, caster.Position, 1.2f, Duration);

            if (!cast.IsAwakened) return;
            cast.World.SpawnZone(new Zone
            {
                Owner = caster, FollowOwner = true, Radius = 1.2f, Duration = Duration, TickInterval = 0.1f,
                Element = DamageType.Cold, VisualKey = "aegis",
                OnTick = z => { if (caster.Health.Barrier <= 0f) z.Expire(); },
                OnExpire = z =>
                {
                    float radius = cast.Area(5f);
                    cast.Vfx(VfxKind.Nova, z.Position, radius, 0.4f);
                    SkillPrimitives.HitCircle(cast, z.Position, radius, 0.4f, (t, _) =>
                    {
                        if (t.IsAlive) t.Statuses.Apply(new FreezeStatus(2f));
                    });
                }
            });
        }
    }

    /// <summary>INT-08 그림자 전령: 상시 오라 (자원 15% 점유). 상태이상 적 처치 시 시체 폭발. 전설: 폭발 사슬 반응.</summary>
    public sealed class HeraldOfShadowBehavior : ISkillBehavior, IPersistentSkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var skill = cast.Skill;
            if (skill.IsActive) return;

            skill.IsActive = true;
            cast.Caster.Resource.Reserve(skill, skill.Definition.Reservation);
            cast.Caster.Stats.AddModifier(new StatModifier(StatType.AllDamagePct, 12f, skill));
            var effect = new HeraldExplosionListener(cast);
            skill.State = effect;
            cast.World.Damage.AddListener(effect);
            cast.Vfx(VfxKind.Warcry, cast.Origin, 1.5f, 0.4f);
        }

        public void Deactivate(SkillInstance skill, Combatant caster, ICombatWorld world)
        {
            if (!skill.IsActive) return;
            skill.IsActive = false;
            caster.Resource.Release(skill);
            caster.Stats.RemoveSource(skill);
            if (skill.State is IDamageListener listener) world.Damage.RemoveListener(listener);
            skill.State = null;
        }

        sealed class HeraldExplosionListener : IDamageListener
        {
            readonly SkillCast _cast;

            public HeraldExplosionListener(SkillCast cast) => _cast = cast;

            public void OnDamageResolved(in DamageResult result)
            {
                if (!result.Killed || result.Attacker != _cast.Caster || result.Target == null) return;

                bool fromExplosion = result.Tags.HasAll(SkillTag.Shadow | SkillTag.Trigger);
                bool qualifies = result.Target.Statuses.HasAilment || (_cast.IsAwakened && fromExplosion);
                if (!qualifies) return;

                var at = result.Target.Position;
                float radius = _cast.Area(1.8f) * _cast.Profile.GimmickArea;
                _cast.Vfx(VfxKind.Explosion, at, radius, 0.3f);
                SkillPrimitives.HitCircle(_cast, at, radius, 0.6f, null, SkillTag.Trigger);
            }
        }
    }
}
