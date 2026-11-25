using System;
using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Skills;
using UnityEngine;

namespace ShadowRPG.Core.World
{
    /// <summary>
    /// 순수 C# 전투 시뮬레이션. 전투원·투사체·장판을 시간에 따라 진행시키고 변화를 이벤트로 알립니다.
    /// Unity 표현 계층은 이 이벤트만 구독해 화면을 그립니다.
    /// </summary>
    public sealed class CombatWorld : ICombatWorld
    {
        struct Scheduled
        {
            public float At;
            public Action Action;
        }

        readonly List<Combatant> _combatants = new List<Combatant>();
        readonly List<Projectile> _projectiles = new List<Projectile>();
        readonly List<Zone> _zones = new List<Zone>();
        readonly List<Scheduled> _scheduled = new List<Scheduled>();
        readonly List<Scheduled> _dueBuffer = new List<Scheduled>();
        readonly HashSet<int> _reportedDead = new HashSet<int>();
        readonly List<Hazard> _hazards = new List<Hazard>();

        public CombatWorld(BalanceSettings balance, IRandom random, DamageService damage, Rect bounds)
        {
            Balance = balance;
            Random = random;
            Damage = damage;
            Bounds = bounds;
        }

        public float Time { get; private set; }
        public IRandom Random { get; }
        public BalanceSettings Balance { get; }
        public DamageService Damage { get; }
        public SkillExecutor Executor { get; set; }
        public Rect Bounds { get; private set; }

        /// <summary>벽이 있는 층의 지형·길찾기 (열린 경기장이면 null).</summary>
        public IWalkability Navigation { get; private set; }

        readonly Dictionary<Combatant, Vector2> _lastValid = new Dictionary<Combatant, Vector2>();

        /// <summary>새 층 지도로 교체합니다.</summary>
        public void SetNavigation(IWalkability navigation)
        {
            Navigation = navigation;
            if (navigation != null) Bounds = navigation.Bounds;
            _lastValid.Clear();
            foreach (var c in _combatants) _lastValid[c] = c.Position;
        }

        public bool IsWalkable(Vector2 position) => Bounds.Contains(position) && (Navigation == null || Navigation.IsWalkable(position));

        /// <summary>벽 속으로 들어간 이동을 벽을 따라 미끄러지게 되돌립니다.</summary>
        void ResolveWalls(Combatant c)
        {
            var position = ClampToBounds(c.Position);
            if (!_lastValid.TryGetValue(c, out var last)) last = position;
            if (Navigation == null || Navigation.IsWalkable(position))
            {
                c.Position = position;
                _lastValid[c] = position;
                return;
            }
            var slideX = new Vector2(position.x, last.y);
            var slideY = new Vector2(last.x, position.y);
            c.Position = Navigation.IsWalkable(slideX) ? slideX : Navigation.IsWalkable(slideY) ? slideY : last;
            _lastValid[c] = c.Position;
        }

        public IReadOnlyList<Combatant> Combatants => _combatants;
        public IReadOnlyList<Projectile> Projectiles => _projectiles;
        public IReadOnlyList<Zone> Zones => _zones;

        public event Action<Combatant> CombatantAdded;
        public event Action<Combatant> CombatantRemoved;
        public event Action<Combatant> CombatantDied;
        public event Action<Projectile> ProjectileSpawned;
        public event Action<Projectile> ProjectileRemoved;
        public event Action<Zone> ZoneSpawned;
        public event Action<Zone> ZoneRemoved;
        public event Action<VfxEvent> VfxEmitted;
        public event Action<Hazard> HazardSpawned;
        public event Action<Hazard> HazardRemoved;

        public IReadOnlyList<Hazard> Hazards => _hazards;

        public void SpawnHazard(Hazard hazard)
        {
            _hazards.Add(hazard);
            HazardSpawned?.Invoke(hazard);
        }

        void TickHazards(float dt)
        {
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                var hazard = _hazards[i];
                hazard.Elapsed += dt;
                if (hazard.Elapsed < hazard.Delay) continue;

                hazard.MarkResolved();
                for (int c = 0; c < _combatants.Count; c++)
                {
                    var target = _combatants[c];
                    if (target.Team == hazard.Team || !target.IsAlive) continue;
                    if (hazard.Contains(target.Position, target.Radius * 0.5f)) hazard.OnHit?.Invoke(hazard, target);
                }
                _hazards.RemoveAt(i);
                HazardRemoved?.Invoke(hazard);
            }
        }

        public void Add(Combatant combatant)
        {
            combatant.Position = ClampToBounds(combatant.Position);
            _combatants.Add(combatant);
            _lastValid[combatant] = combatant.Position;
            CombatantAdded?.Invoke(combatant);
        }

        public void Remove(Combatant combatant)
        {
            _lastValid.Remove(combatant);
            if (_combatants.Remove(combatant)) CombatantRemoved?.Invoke(combatant);
        }

        /// <summary>사망 처리된 전투원을 되살립니다 (플레이어 부활).</summary>
        public void Revive(Combatant combatant)
        {
            _reportedDead.Remove(combatant.Id);
            combatant.Statuses.Clear();
            combatant.Health.ResetToFull();
            combatant.Resource.Fill();
        }

        public Vector2 ClampToBounds(Vector2 p)
            => new Vector2(Mathf.Clamp(p.x, Bounds.xMin, Bounds.xMax), Mathf.Clamp(p.y, Bounds.yMin, Bounds.yMax));

        // ---------- ITargetQuery ----------

        public void QueryEnemies(Team ofTeam, Vector2 center, float radius, List<Combatant> results)
        {
            for (int i = 0; i < _combatants.Count; i++)
            {
                var c = _combatants[i];
                if (c.Team == ofTeam || !c.IsAlive) continue;
                float reach = radius + c.Radius;
                if ((c.Position - center).sqrMagnitude <= reach * reach) results.Add(c);
            }
        }

        public Combatant FindNearestEnemy(Team ofTeam, Vector2 from, float maxRange, ICollection<int> excludeIds = null)
        {
            Combatant best = null;
            float bestSqr = maxRange * maxRange;
            for (int i = 0; i < _combatants.Count; i++)
            {
                var c = _combatants[i];
                if (c.Team == ofTeam || !c.IsAlive) continue;
                if (excludeIds != null && excludeIds.Contains(c.Id)) continue;
                float sqr = (c.Position - from).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = c;
            }
            return best;
        }

        // ---------- IEffectSpawner ----------

        public void SpawnProjectile(Projectile projectile)
        {
            _projectiles.Add(projectile);
            ProjectileSpawned?.Invoke(projectile);
        }

        public void SpawnZone(Zone zone)
        {
            zone.TickTimer = 0f;
            _zones.Add(zone);
            ZoneSpawned?.Invoke(zone);
        }

        public void Schedule(float delay, Action action) => _scheduled.Add(new Scheduled { At = Time + delay, Action = action });

        public void Emit(VfxEvent vfx) => VfxEmitted?.Invoke(vfx);

        // ---------- 시뮬레이션 ----------

        public void Tick(float dt)
        {
            Time += dt;
            RunScheduled();

            for (int i = 0; i < _combatants.Count; i++)
            {
                var c = _combatants[i];
                c.Tick(dt);
                ResolveWalls(c);
            }

            TickProjectiles(dt);
            TickZones(dt);
            TickHazards(dt);
            CollectDead();
        }

        public void ClearEffects()
        {
            foreach (var p in _projectiles) ProjectileRemoved?.Invoke(p);
            foreach (var z in _zones) ZoneRemoved?.Invoke(z);
            foreach (var h in _hazards) HazardRemoved?.Invoke(h);
            _projectiles.Clear();
            _zones.Clear();
            _hazards.Clear();
            _scheduled.Clear();
        }

        void RunScheduled()
        {
            _dueBuffer.Clear();
            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                if (_scheduled[i].At > Time) continue;
                _dueBuffer.Add(_scheduled[i]);
                _scheduled.RemoveAt(i);
            }
            for (int i = _dueBuffer.Count - 1; i >= 0; i--) _dueBuffer[i].Action?.Invoke();
        }

        void TickProjectiles(float dt)
        {
            int count = _projectiles.Count;
            for (int i = 0; i < count; i++)
            {
                var p = _projectiles[i];
                if (p.IsExpired) continue;
                StepProjectile(p, dt);
            }

            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                if (!_projectiles[i].IsExpired) continue;
                var p = _projectiles[i];
                _projectiles.RemoveAt(i);
                ProjectileRemoved?.Invoke(p);
            }
        }

        void StepProjectile(Projectile p, float dt)
        {
            p.OnUpdate?.Invoke(p, dt);
            if (p.IsExpired) return;

            if (p.IsReturning && p.Owner != null)
            {
                var toOwner = p.Owner.Position - p.Position;
                if (toOwner.magnitude < 0.5f)
                {
                    p.Expire();
                    return;
                }
                p.Direction = toOwner.normalized;
            }
            else if (p.HomingStrength > 0f)
            {
                var target = FindNearestEnemy(p.Team, p.Position, 7f, p.HitIds);
                if (target != null)
                {
                    var desired = (target.Position - p.Position).normalized;
                    p.Direction = Vector2.Lerp(p.Direction, desired, Mathf.Clamp01(p.HomingStrength * dt)).normalized;
                }
            }

            float step = p.Speed * dt;
            p.Position += p.Direction * step;
            p.Travelled += step;

            if (!p.IsReturning && p.Travelled >= p.MaxDistance)
            {
                if (p.Returns)
                {
                    p.IsReturning = true;
                    p.HitIds.Clear();
                }
                else
                {
                    p.Expire();
                    return;
                }
            }

            if (!p.IsReturning && !IsWalkable(p.Position))
            {
                p.Expire();
                return;
            }

            var hit = FindCollision(p);
            if (hit == null) return;

            p.HitIds.Add(hit.Id);
            p.OnHit?.Invoke(p, hit);
            if (p.IsExpired) return;

            ResolveAfterHit(p, hit);
        }

        Combatant FindCollision(Projectile p)
        {
            for (int i = 0; i < _combatants.Count; i++)
            {
                var c = _combatants[i];
                if (c.Team == p.Team || !c.IsAlive || p.HitIds.Contains(c.Id)) continue;
                float reach = p.Radius + c.Radius;
                if ((c.Position - p.Position).sqrMagnitude <= reach * reach) return c;
            }
            return null;
        }

        void ResolveAfterHit(Projectile p, Combatant hit)
        {
            if (p.PassThrough || p.Returns) return;

            if (p.PierceRemaining > 0)
            {
                p.PierceRemaining--;
                return;
            }

            if (p.ChainRemaining > 0)
            {
                var next = FindNearestEnemy(p.Team, hit.Position, p.ChainRange, p.HitIds);
                if (next != null)
                {
                    p.ChainRemaining--;
                    p.Direction = (next.Position - p.Position).normalized;
                    p.MaxDistance = p.Travelled + p.ChainRange + 1f;
                    return;
                }
            }

            if (p.CanFork && p.GenerationDepth == 0)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    var dir = Rotate(p.Direction, 25f * s);
                    SpawnProjectile(p.CloneForFork(dir));
                }
            }

            p.Expire();
        }

        void TickZones(float dt)
        {
            int count = _zones.Count;
            for (int i = 0; i < count; i++)
            {
                var z = _zones[i];
                if (z.IsExpired) continue;

                z.Elapsed += dt;
                if (z.FollowOwner && z.Owner != null) z.Position = z.Owner.Position;
                if (z.PullSpeed > 0f) PullTowards(z, dt);

                z.TickTimer -= dt;
                if (z.TickTimer <= 0f)
                {
                    z.TickTimer += Mathf.Max(0.05f, z.TickInterval);
                    z.OnTick?.Invoke(z);
                }

                bool ownerGone = z.FollowOwner && (z.Owner == null || !z.Owner.IsAlive);
                if (z.Elapsed >= z.Duration || ownerGone) z.Expire();
            }

            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                if (!_zones[i].IsExpired) continue;
                var z = _zones[i];
                _zones.RemoveAt(i);
                ZoneRemoved?.Invoke(z);
            }
        }

        void PullTowards(Zone z, float dt)
        {
            var team = z.Owner != null ? z.Owner.Team : Team.Player;
            for (int i = 0; i < _combatants.Count; i++)
            {
                var c = _combatants[i];
                if (c.Team == team || !c.IsAlive) continue;
                var delta = z.Position - c.Position;
                float dist = delta.magnitude;
                if (dist > z.Radius || dist < 0.2f) continue;
                float strength = c.Rank == EnemyRank.Boss ? 0.2f : c.Rank == EnemyRank.Elite ? 0.5f : 1f;
                c.Position += delta / dist * Mathf.Min(dist, z.PullSpeed * strength * dt);
            }
        }

        void CollectDead()
        {
            for (int i = _combatants.Count - 1; i >= 0; i--)
            {
                var c = _combatants[i];
                if (c.IsAlive || _reportedDead.Contains(c.Id)) continue;
                _reportedDead.Add(c.Id);
                CombatantDied?.Invoke(c);
                if (c.Team == Team.Enemy)
                {
                    _combatants.Remove(c);
                    CombatantRemoved?.Invoke(c);
                }
            }
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
