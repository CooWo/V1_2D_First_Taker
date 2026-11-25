using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Status;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Skills.Behaviors
{
    /// <summary>STR-01 대지 분쇄: 부채꼴 충격파 + 둔화. 전설: 균열 끝 그림자 바위 기둥 3개 연쇄 폭발.</summary>
    public sealed class EarthshatterBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            float radius = cast.Area(2.6f) * cast.Profile.GimmickArea;
            var dir = cast.Direction;
            cast.World.Emit(new VfxEvent { Kind = VfxKind.Cone, Position = cast.Origin, To = cast.Origin + dir * radius, Radius = radius, AngleDegrees = 100f, Element = cast.Element, Duration = 0.3f });
            SkillPrimitives.HitCone(cast, cast.Origin, dir, radius, 100f, 1f,
                (target, _) => target.Statuses.Apply(new SlowStatus(1.5f, 0.6f)));

            if (!cast.IsAwakened) return;
            for (int i = -1; i <= 1; i++)
            {
                var pillar = cast.Origin + CombatWorld.Rotate(dir, 30f * i) * radius;
                cast.Vfx(VfxKind.Pillar, pillar, 0.6f, 1f);
                cast.World.Schedule(1f, () =>
                {
                    cast.Vfx(VfxKind.Explosion, pillar, 1.3f);
                    SkillPrimitives.HitCircle(cast, pillar, 1.3f, 0.6f);
                });
            }
        }
    }

    /// <summary>STR-02 소용돌이 참격: 회전하며 적을 통과. 전설: 반경 +35%, 중력 소용돌이로 끌어당김.</summary>
    public sealed class CycloneCleaveBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            float radius = cast.Area(1.7f) * cast.Profile.GimmickArea * (cast.IsAwakened ? 1.35f : 1f);
            cast.Vfx(VfxKind.Nova, cast.Origin, radius, 0.3f);
            SkillPrimitives.HitCircle(cast, cast.Origin, radius, 1f);

            var through = cast.Origin + cast.Direction * 1.6f;
            cast.Caster.BeginDash(cast.World.ClampToBounds(through), 0.25f);

            if (cast.IsAwakened)
                cast.World.SpawnZone(new Zone { Owner = cast.Caster, FollowOwner = true, Radius = radius * 1.6f, Duration = 0.5f, PullSpeed = 4f, TickInterval = 10f, Element = cast.Element, VisualKey = "pull" });
        }
    }

    /// <summary>STR-03 용암 파쇄격: 근접 타격 후 화염 구체 3개 포물선 낙하. 전설: +3개, 용암 지대.</summary>
    public sealed class MoltenStrikeBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var target = cast.Target;
            if (target != null) cast.Hit(target, 1f);
            var center = target != null ? target.Position : cast.Origin + cast.Direction;
            cast.Vfx(VfxKind.Slash, center, 0.8f);

            int balls = 3 + cast.Mods.ExtraProjectiles + (cast.IsAwakened ? 3 : 0);
            float blast = cast.Area(0.9f) * cast.Profile.GimmickArea;
            for (int i = 0; i < balls; i++)
            {
                var landing = cast.World.ClampToBounds(center + SkillPrimitives.RandomOffset(cast.World.Random, 2.5f));
                float delay = 0.35f + 0.05f * i;
                cast.World.Emit(VfxEvent.Line(VfxKind.Beam, center, landing, DamageType.Fire, delay));
                cast.World.Schedule(delay, () =>
                {
                    cast.Vfx(VfxKind.Explosion, landing, blast);
                    SkillPrimitives.HitCircle(cast, landing, blast, 0.4f, null, SkillTag.Projectile);
                    if (!cast.IsAwakened) return;
                    cast.World.SpawnZone(new Zone
                    {
                        Owner = cast.Caster, Position = landing, Radius = blast, Duration = 3f, TickInterval = 0.5f,
                        Element = DamageType.Fire, VisualKey = "lava",
                        OnTick = z => SkillPrimitives.HitCircle(cast, z.Position, z.Radius, 0.12f)
                    });
                });
            }
        }
    }

    /// <summary>STR-04 선조의 그림자 망치: 주변 적 2체 추가 강타. 전설: 1:1이면 분신이 60% 합동 타격.</summary>
    public sealed class AncestralCallBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var main = cast.Target;
            if (main == null) return;

            cast.Vfx(VfxKind.Slash, main.Position, 1f);
            var hit = cast.Hit(main, 1f);
            if (cast.HasGimmick && hit.Landed) main.Statuses.Apply(new SlowStatus(1.2f, 0.6f));

            var exclude = new System.Collections.Generic.HashSet<int> { main.Id };
            int extra = 0;
            for (int i = 0; i < 2; i++)
            {
                var other = cast.World.FindNearestEnemy(cast.Team, cast.Origin, cast.Area(3.2f), exclude);
                if (other == null) break;
                exclude.Add(other.Id);
                extra++;
                cast.World.Emit(VfxEvent.Line(VfxKind.Beam, cast.Origin, other.Position, DamageType.Shadow));
                cast.Hit(other, 1f, SkillTag.Clone);
            }

            if (extra == 0 && cast.IsAwakened)
            {
                cast.World.Schedule(0.15f, () =>
                {
                    if (!main.IsAlive) return;
                    cast.Vfx(VfxKind.Slash, main.Position, 1.1f);
                    cast.Hit(main, 0.6f, SkillTag.Clone);
                });
            }
        }
    }

    /// <summary>STR-05 지각 변동파: 장거리 관통 파동. 전설: 관통 시 좌우 갈래 균열.</summary>
    public sealed class TectonicWaveBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            SkillPrimitives.FireSpread(cast, 1, 20f, cast.Direction, dir =>
            {
                var wave = SkillPrimitives.CreateProjectile(cast, cast.Origin, dir, 10f, 8f, 1f, "wave");
                wave.PassThrough = true;
                wave.Radius = 0.6f * cast.Profile.GimmickArea * cast.Mods.AreaMultiplier;
                wave.VisualScale = 1.6f;
                if (cast.IsAwakened)
                {
                    var baseHit = wave.OnHit;
                    wave.OnHit = (p, target) =>
                    {
                        baseHit(p, target);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            var side = SkillPrimitives.CreateProjectile(cast, target.Position, CombatWorld.Rotate(p.Direction, 70f * s), 9f, 3.5f, 0.5f, "wave");
                            side.PassThrough = true;
                            side.GenerationDepth = 1;
                            foreach (var id in p.HitIds) side.HitIds.Add(id);
                            cast.World.SpawnProjectile(side);
                        }
                    };
                }
                return wave;
            });
        }
    }

    /// <summary>STR-06 투척 분쇄창: 무거운 투창 + 밀쳐내기. 전설: 명중/충돌 시 8방향 관통 파편.</summary>
    public sealed class ShatteringJavelinBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            SkillPrimitives.FireSpread(cast, 1, 15f, cast.Direction, dir =>
            {
                var javelin = SkillPrimitives.CreateProjectile(cast, cast.Origin, dir, 14f, 10f, 1f, "javelin");
                if (cast.HasGimmick) javelin.PierceRemaining += 1;
                var baseHit = javelin.OnHit;
                javelin.OnHit = (p, target) =>
                {
                    baseHit(p, target);
                    SkillPrimitives.Knockback(target, p.Position - p.Direction, 0.7f);
                };
                if (cast.IsAwakened)
                {
                    javelin.OnExpire = p =>
                    {
                        cast.Vfx(VfxKind.Explosion, p.Position, 0.9f);
                        for (int i = 0; i < 8; i++)
                        {
                            var shard = SkillPrimitives.CreateProjectile(cast, p.Position, CombatWorld.Rotate(Vector2.right, 45f * i), 12f, 4f, 0.4f, "shard");
                            shard.PierceRemaining = 1;
                            shard.ChainRemaining = 0;
                            shard.GenerationDepth = 1;
                            cast.World.SpawnProjectile(shard);
                        }
                    };
                }
                return javelin;
            });
        }
    }

    /// <summary>STR-07 불멸의 외침: 적 수 비례 보호막 + 적 약화. 전설: 보호막 동안 [강타] 피해 1.3배.</summary>
    public sealed class ImmortalWarcryBehavior : ISkillBehavior
    {
        const float Duration = 6f;

        public void Execute(SkillCast cast)
        {
            var caster = cast.Caster;
            var nearby = ListPool<Combatant>.Get();
            cast.World.QueryEnemies(cast.Team, caster.Position, cast.Area(4f), nearby);

            float fraction = Mathf.Clamp(0.15f + 0.05f * nearby.Count, 0.15f, 0.40f);
            caster.Health.AddBarrier(caster.Health.MaxLife * fraction, Duration);
            foreach (var enemy in nearby)
                enemy.Statuses.Apply(new StatBuffStatus("warcry_weaken", 4f, new StatModifier(StatType.AllDamagePct, -20f)));
            ListPool<Combatant>.Release(nearby);

            cast.Vfx(VfxKind.Warcry, caster.Position, cast.Area(4f), 0.5f);
            if (cast.IsAwakened) caster.Statuses.Apply(new TagEmpowerStatus("warcry_empower", Duration, SkillTag.Slam, 1.3f));
        }
    }

    /// <summary>STR-08 복수의 가시 갑주: 피격/막기 시 자동 360° 반격. 전설: 막기 시 확정 + 방어도 합산.</summary>
    public sealed class VengeanceBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            float radius = cast.Area(2f) * cast.Profile.GimmickArea;
            cast.Vfx(VfxKind.Nova, cast.Origin, radius, 0.25f);
            SkillPrimitives.HitCircle(cast, cast.Origin, radius, 1f, (target, _) =>
            {
                if (!cast.IsAwakened || !target.IsAlive) return;
                float armor = cast.Caster.Stats.Get(StatType.Armor);
                if (armor <= 0f) return;
                cast.World.Damage.Deal(new DamageRequest
                {
                    Attacker = cast.Caster, Target = target, Tags = cast.Tags, Type = DamageType.Physical,
                    BaseAmount = armor * 0.3f * cast.DamageScale, CanCrit = false
                });
            });
        }
    }

    /// <summary>지속시간 동안 특정 태그 스킬에 최종 곱연산을 부여하는 버프.</summary>
    public sealed class TagEmpowerStatus : TimedStatus
    {
        readonly SkillTag _tags;
        readonly float _multiplier;

        public TagEmpowerStatus(string id, float duration, SkillTag tags, float multiplier) : base(id, duration)
        {
            _tags = tags;
            _multiplier = multiplier;
        }

        public override void OnApplied(Combatant owner) => owner.TagMultipliers.Add(new TagMultiplier(_tags, _multiplier, this));
        public override void OnRemoved(Combatant owner) => owner.TagMultipliers.RemoveSource(this);
    }
}
