using System;
using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using UnityEngine;

namespace ShadowRPG.Core.World
{
    /// <summary>
    /// 투사체. GDD 4.1-④: 기본적으로 첫 적중 시 소멸하며, 관통/연쇄/갈래 옵션이 있어야 다수를 처리합니다.
    /// </summary>
    public sealed class Projectile
    {
        static int _nextId;

        public Projectile()
        {
            Id = ++_nextId;
        }

        public int Id { get; }
        public Combatant Owner;
        public Team Team;
        public Vector2 Position;
        public Vector2 Direction = Vector2.right;
        public float Speed = 12f;
        public float Radius = 0.25f;
        public float MaxDistance = 10f;
        public float Travelled;
        public DamageType Element;
        public string VisualKey = "projectile";
        public float VisualScale = 1f;

        public int PierceRemaining;
        public int ChainRemaining;
        public bool CanFork;
        public float ChainRange = 6f;

        /// <summary>가장 가까운 적을 향해 방향을 서서히 트는 강도 (0이면 직선).</summary>
        public float HomingStrength;
        /// <summary>최대 사거리에 닿으면 주인에게 되돌아오는 부메랑 투사체.</summary>
        public bool Returns;
        public bool IsReturning;
        /// <summary>적에 닿아도 소멸하지 않는 투사체 (영혼 구체처럼 사거리 끝까지 비행).</summary>
        public bool PassThrough;

        /// <summary>적중 처리. 피해·상태이상은 스킬이 이 콜백에서 정의합니다.</summary>
        public Action<Projectile, Combatant> OnHit;
        /// <summary>비행 중 매 프레임 호출 (영혼 구체의 바늘 난사 등).</summary>
        public Action<Projectile, float> OnUpdate;
        /// <summary>소멸 직전 호출 (폭발, 파편 비산 등).</summary>
        public Action<Projectile> OnExpire;

        public readonly HashSet<int> HitIds = new HashSet<int>();
        public bool IsExpired { get; private set; }
        public int GenerationDepth;

        public void Expire()
        {
            if (IsExpired) return;
            IsExpired = true;
            OnExpire?.Invoke(this);
        }

        public Projectile CloneForFork(Vector2 direction)
        {
            var fork = new Projectile
            {
                Owner = Owner,
                Team = Team,
                Position = Position,
                Direction = direction,
                Speed = Speed,
                Radius = Radius,
                MaxDistance = Mathf.Max(2f, MaxDistance - Travelled),
                Element = Element,
                VisualKey = VisualKey,
                VisualScale = VisualScale,
                ChainRange = ChainRange,
                OnHit = OnHit,
                GenerationDepth = GenerationDepth + 1
            };
            foreach (var id in HitIds) fork.HitIds.Add(id);
            return fork;
        }
    }
}
