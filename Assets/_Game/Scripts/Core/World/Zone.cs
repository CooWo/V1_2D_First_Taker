using System;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using UnityEngine;

namespace ShadowRPG.Core.World
{
    /// <summary>
    /// 지속 장판/공전체. 용암 지대, 공허의 소용돌이, 칼날 소용돌이, 회오리 잔류 폭풍 등이 이것으로 표현됩니다.
    /// </summary>
    public sealed class Zone
    {
        static int _nextId;

        public Zone()
        {
            Id = ++_nextId;
        }

        public int Id { get; }
        public Combatant Owner;
        public Vector2 Position;
        public float Radius = 1.5f;
        public float Duration = 2f;
        public float Elapsed;
        public float TickInterval = 0.5f;
        public float TickTimer;
        public DamageType Element;
        public string VisualKey = "zone";

        /// <summary>true면 주인 위치를 따라다닙니다 (칼날 소용돌이).</summary>
        public bool FollowOwner;
        /// <summary>범위 안 적을 중심으로 끌어당기는 속도 (초당 거리).</summary>
        public float PullSpeed;

        public int Counter;
        public int Stacks = 1;

        public Action<Zone> OnTick;
        public Action<Zone> OnExpire;

        public bool IsExpired { get; private set; }
        public float Remaining => Duration - Elapsed;

        public void Expire()
        {
            if (IsExpired) return;
            IsExpired = true;
            OnExpire?.Invoke(this);
        }
    }
}
