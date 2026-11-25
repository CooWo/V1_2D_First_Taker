using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Status;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Skills.Behaviors
{
    /// <summary>DEX-01 점멸 타격: 적 그림자 뒤로 순간이동해 베기. 전설: 처치 시 쿨타임 초기화, 보스 치명타 시 50%.</summary>
    public sealed class FlickerStrikeBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var target = cast.Target;
            if (target == null) return;

            var caster = cast.Caster;
            var away = target.Position - caster.Position;
            var behind = target.Position + (away.sqrMagnitude > 0.0001f ? away.normalized : caster.Facing) * (target.Radius + 0.45f);
            cast.World.Emit(VfxEvent.Line(VfxKind.DashTrail, caster.Position, behind, DamageType.Shadow, 0.2f));
            caster.Teleport(cast.World.ClampToBounds(behind));
            caster.Statuses.Apply(new InvulnerableStatus(0.25f));

            bool reset = false;
            float radius = cast.Area(1.3f) * cast.Profile.GimmickArea;
            cast.Vfx(VfxKind.Slash, target.Position, radius);
            SkillPrimitives.HitCircle(cast, target.Position, radius, 1f, (hit, result) =>
            {
                if (!cast.IsAwakened) return;
                if (result.Killed) reset = true;
                else if (hit.Rank == EnemyRank.Boss && result.IsCrit && cast.World.Random.Chance(50f)) reset = true;
            });

            if (reset) cast.Skill.ResetCooldown();
        }
    }

    /// <summary>DEX-02 쌍독아 난무: 초고속 연타 + 맹독 중첩. 전설: 5중첩 시 남은 지속 피해 일괄 정산.</summary>
    public sealed class ViperFlurryBehavior : ISkillBehavior
    {
        const int Hits = 3;

        public void Execute(SkillCast cast)
        {
            var target = cast.Target;
            if (target == null) return;

            for (int i = 0; i < Hits; i++)
            {
                float delay = 0.07f * i;
                cast.World.Schedule(delay, () =>
                {
                    if (!target.IsAlive) return;
                    cast.Vfx(VfxKind.Slash, target.Position, 0.6f, 0.12f);
                    var result = cast.Hit(target, 1f / Hits, SkillTag.MultiHit);
                    if (!result.Landed || !target.IsAlive) return;

                    target.Statuses.Apply(new DamageOverTimeStatus(StatusIds.Poison, 4f, cast.ScaledDamage(0.12f), 10));
                    if (cast.HasGimmick) target.Statuses.Apply(new DamageOverTimeStatus(StatusIds.Bleed, 3f, cast.ScaledDamage(0.08f)));
                    if (cast.IsAwakened) TryRupture(cast, target);
                });
            }
        }

        static void TryRupture(SkillCast cast, Combatant target)
        {
            if (!(target.Statuses.Find(StatusIds.Poison) is DamageOverTimeStatus poison) || poison.Stacks < 5) return;
            float burst = poison.RemainingDamage;
            poison.Consume();
            cast.Vfx(VfxKind.Explosion, target.Position, 1f);
            cast.World.Damage.Deal(new DamageRequest
            {
                Attacker = cast.Caster, Target = target, Tags = cast.Tags | SkillTag.Poison,
                Type = DamageType.Physical, BaseAmount = burst, CanCrit = false
            });
        }
    }

    /// <summary>DEX-03 서리 칼날: 긴 근접 일격 + 후방 얼음 칼날 4개. 전설: 칼날 +2 연쇄, 빙결 처치 시 냉기 폭발.</summary>
    public sealed class FrostBladesBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var target = cast.Target;
            if (target == null) return;

            bool wasFrozen = target.Statuses.Has(StatusIds.Freeze);
            cast.World.Emit(VfxEvent.Line(VfxKind.Beam, cast.Origin, target.Position, DamageType.Cold, 0.12f));
            var result = cast.Hit(target, 1f);
            if (result.Landed && target.IsAlive)
            {
                if (result.IsCrit) target.Statuses.Apply(new FreezeStatus(0.6f));
                if (cast.HasGimmick) target.Statuses.Apply(new SlowStatus(1.5f, 0.65f));
            }
            if (cast.IsAwakened && result.Killed && wasFrozen) Shatter(cast, target.Position);

            int hitId = target.Id;
            var origin = target.Position;
            SkillPrimitives.FireSpread(cast, 4, 60f, cast.Direction, dir =>
            {
                var blade = SkillPrimitives.CreateProjectile(cast, origin, dir, 13f, 5f, 0.4f, "ice");
                blade.HitIds.Add(hitId);
                if (cast.IsAwakened) blade.ChainRemaining += 2;
                var baseHit = blade.OnHit;
                blade.OnHit = (p, victim) =>
                {
                    bool frozen = victim.Statuses.Has(StatusIds.Freeze);
                    baseHit(p, victim);
                    if (cast.IsAwakened && frozen && !victim.IsAlive) Shatter(cast, victim.Position);
                };
                return blade;
            });
        }

        static void Shatter(SkillCast cast, Vector2 position)
        {
            cast.Vfx(VfxKind.Explosion, position, 1.5f);
            SkillPrimitives.HitCircle(cast, position, 1.5f, 0.5f);
        }
    }

    /// <summary>DEX-04 회오리 사격: 목표 지점에서 폭발 후 2차 화살 5발. 전설: 잔류 폭풍 + 2차 화살 유도.</summary>
    public sealed class TornadoShotBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            float distance = Mathf.Clamp(Vector2.Distance(cast.Origin, cast.Point), 2f, 10f);
            SkillPrimitives.FireSpread(cast, 1, 12f, cast.Direction, dir =>
            {
                var tornado = SkillPrimitives.CreateProjectile(cast, cast.Origin, dir, 15f, distance, 1f, "tornado");
                tornado.PassThrough = true;
                tornado.OnExpire = p => Burst(cast, p.Position);
                return tornado;
            });
        }

        static void Burst(SkillCast cast, Vector2 at)
        {
            cast.Vfx(VfxKind.Explosion, at, 0.8f);
            int arrows = 5 + cast.Mods.ExtraProjectiles;
            for (int i = 0; i < arrows; i++)
            {
                var dir = CombatWorld.Rotate(Vector2.right, cast.World.Random.Range(0f, 360f));
                var arrow = SkillPrimitives.CreateProjectile(cast, at, dir, 13f, 5f, 0.45f, "arrow");
                arrow.GenerationDepth = 1;
                if (cast.HasGimmick) arrow.PierceRemaining += 1;
                if (cast.IsAwakened) arrow.HomingStrength = 6f;
                cast.World.SpawnProjectile(arrow);
            }

            if (!cast.IsAwakened) return;
            cast.World.SpawnZone(new Zone
            {
                Owner = cast.Caster, Position = at, Radius = cast.Area(1.2f), Duration = 2f, TickInterval = 0.4f,
                Element = cast.Element, VisualKey = "storm",
                OnTick = z => SkillPrimitives.HitCircle(cast, z.Position, z.Radius, 0.15f)
            });
        }
    }

    /// <summary>DEX-05 번개 화살비: 목표 지점 번개 폭우 + 감전. 전설: 감전 적에게 추가 낙뢰 3회.</summary>
    public sealed class LightningRainBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            var point = cast.Point;
            float radius = cast.Area(2.2f) * cast.Profile.GimmickArea;
            cast.Vfx(VfxKind.Mark, point, radius, 0.35f);
            cast.World.Schedule(0.35f, () =>
            {
                cast.Vfx(VfxKind.Nova, point, radius, 0.3f);
                SkillPrimitives.HitCircle(cast, point, radius, 1f, (target, _) =>
                {
                    if (!target.IsAlive) return;
                    target.Statuses.Apply(new ShockStatus(3f, 0.15f));
                    if (cast.IsAwakened) ScheduleBolts(cast, target);
                });
            });
        }

        static void ScheduleBolts(SkillCast cast, Combatant target)
        {
            for (int i = 1; i <= 3; i++)
            {
                cast.World.Schedule(0.5f * i, () =>
                {
                    if (!target.IsAlive) return;
                    cast.World.Emit(VfxEvent.Line(VfxKind.Beam, target.Position + Vector2.up * 4f, target.Position, DamageType.Lightning, 0.12f));
                    cast.Hit(target, 0.45f);
                });
            }
        }
    }

    /// <summary>DEX-06 환영 무기 투척: 왕복 관통. 전설: 복귀 후 3초간 주변 공전.</summary>
    public sealed class SpectralThrowBehavior : ISkillBehavior
    {
        public void Execute(SkillCast cast)
        {
            SkillPrimitives.FireSpread(cast, 1, 20f, cast.Direction, dir =>
            {
                var blade = SkillPrimitives.CreateProjectile(cast, cast.Origin, dir, 11f, 7f, 1f, "spectral");
                blade.Returns = true;
                blade.Radius = 0.4f * cast.Profile.GimmickArea;
                if (cast.IsAwakened)
                {
                    blade.OnExpire = p => cast.World.SpawnZone(new Zone
                    {
                        Owner = cast.Caster, FollowOwner = true, Radius = cast.Area(1.6f), Duration = 3f, TickInterval = 0.3f,
                        Element = cast.Element, VisualKey = "orbit_blades",
                        OnTick = z => SkillPrimitives.HitCircle(cast, z.Position, z.Radius, 0.35f)
                    });
                }
                return blade;
            });
        }
    }

    /// <summary>DEX-07 그림자 연막 질주: 충돌 무시 질주 + 회피·이속 버프. 전설: 출발/도착 지점 분신 미끼 폭발.</summary>
    public sealed class SmokeDashBehavior : ISkillBehavior
    {
        const float MaxDistance = 6f;

        public void Execute(SkillCast cast)
        {
            var caster = cast.Caster;
            var start = caster.Position;
            var delta = cast.Point - start;
            if (delta.magnitude > MaxDistance) delta = delta.normalized * MaxDistance;
            var end = cast.World.ClampToBounds(start + delta);

            cast.World.Emit(VfxEvent.Line(VfxKind.DashTrail, start, end, DamageType.Shadow, 0.3f));
            caster.BeginDash(end, 0.25f);
            caster.Statuses.Apply(new InvulnerableStatus(0.25f));
            caster.Statuses.Apply(new StatBuffStatus("smoke_dash", 3f,
                new StatModifier(StatType.EvasionPct, 25f),
                new StatModifier(StatType.MoveSpeedPct, 20f)));

            if (!cast.IsAwakened) return;
            SpawnDecoy(cast, start);
            SpawnDecoy(cast, end);
        }

        static void SpawnDecoy(SkillCast cast, Vector2 at)
        {
            cast.World.SpawnZone(new Zone
            {
                Owner = cast.Caster, Position = at, Radius = 2f, Duration = 2f, TickInterval = 10f,
                Element = DamageType.Shadow, VisualKey = "decoy",
                OnExpire = z =>
                {
                    cast.Vfx(VfxKind.Explosion, z.Position, z.Radius);
                    SkillPrimitives.HitCircle(cast, z.Position, z.Radius, 0.6f, (t, _) => t.Statuses.Apply(new SlowStatus(2f, 0.4f)));
                }
            });
        }
    }

    /// <summary>DEX-08 암살자의 표식: 가장 강한 적에게 치명타 취약 표식. 전설: 처치 시 회복 + 강화된 표식 전이.</summary>
    public sealed class AssassinsMarkBehavior : ISkillBehavior
    {
        public const float Duration = 8f;
        public const float CritChance = 8f;
        public const float CritDamage = 30f;

        public void Execute(SkillCast cast)
        {
            var target = cast.Target;
            if (target == null) return;
            Apply(cast.Caster, target, 0, cast.IsAwakened, cast.HasGimmick ? 1.25f : 1f);
            cast.Vfx(VfxKind.Mark, target.Position, 0.8f, 0.6f);
        }

        public static void Apply(Combatant caster, Combatant target, int empower, bool transfers, float scale = 1f)
        {
            float factor = scale * Mathf.Pow(1.2f, empower);
            target.Statuses.Remove(StatusIds.AssassinMark);
            target.Statuses.Apply(new AssassinMarkStatus(Duration, CritChance * factor, CritDamage * factor, empower)
            {
                Caster = caster,
                TransfersOnDeath = transfers
            });
        }
    }

    /// <summary>표식 대상 처치 시 회복 및 다음 적에게 강화 표식 전이 (DEX-08 전설 각성).</summary>
    public sealed class MarkTransferListener : IDamageListener
    {
        readonly ITargetQuery _query;
        readonly IEffectSpawner _effects;

        public MarkTransferListener(ITargetQuery query, IEffectSpawner effects)
        {
            _query = query;
            _effects = effects;
        }

        public void OnDamageResolved(in DamageResult result)
        {
            if (!result.Killed || result.Target == null) return;
            var mark = result.Target.Statuses.Get<AssassinMarkStatus>();
            if (mark == null || !mark.TransfersOnDeath || mark.Caster == null || !mark.Caster.IsAlive) return;

            var caster = mark.Caster;
            caster.Health.Heal(caster.Health.MaxLife * 0.15f);
            caster.Resource.Gain(caster.Resource.Max * 0.15f);
            result.Target.Statuses.Remove(StatusIds.AssassinMark);

            var exclude = new HashSet<int> { result.Target.Id };
            var next = _query.FindNearestEnemy(caster.Team, result.Target.Position, 10f, exclude);
            if (next == null) return;
            AssassinsMarkBehavior.Apply(caster, next, mark.EmpowerLevel + 1, true);
            _effects.Emit(VfxEvent.Line(VfxKind.Beam, result.Target.Position, next.Position, DamageType.Shadow, 0.3f));
        }
    }
}
