using System;
using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Skills;
using UnityEngine;

namespace ShadowRPG.Core.World
{
    /// <summary>대상 탐색 전용 창구.</summary>
    public interface ITargetQuery
    {
        IReadOnlyList<Combatant> Combatants { get; }
        void QueryEnemies(Team ofTeam, Vector2 center, float radius, List<Combatant> results);
        Combatant FindNearestEnemy(Team ofTeam, Vector2 from, float maxRange, ICollection<int> excludeIds = null);
    }

    /// <summary>투사체·장판·지연 실행·연출 생성 창구.</summary>
    public interface IEffectSpawner
    {
        void SpawnProjectile(Projectile projectile);
        void SpawnZone(Zone zone);
        void Schedule(float delay, Action action);
        void Emit(VfxEvent vfx);
    }

    /// <summary>예고 장판 생성·조회 (보스 패턴과 회피 AI가 사용).</summary>
    public interface IHazardField
    {
        IReadOnlyList<Hazard> Hazards { get; }
        void SpawnHazard(Hazard hazard);
    }

    /// <summary>
    /// 스킬·AI가 의존하는 전투 월드 추상화 (DIP). 역할별로 작은 인터페이스를 합친 형태입니다 (ISP).
    /// </summary>
    public interface ICombatWorld : ITargetQuery, IEffectSpawner, IHazardField
    {
        float Time { get; }
        IRandom Random { get; }
        BalanceSettings Balance { get; }
        DamageService Damage { get; }
        SkillExecutor Executor { get; }
        Rect Bounds { get; }
        Vector2 ClampToBounds(Vector2 position);
        /// <summary>벽·길찾기 (열린 경기장이면 null).</summary>
        IWalkability Navigation { get; }
        bool IsWalkable(Vector2 position);
    }
}
