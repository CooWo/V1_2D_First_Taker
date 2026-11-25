using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Classes;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Supports;

namespace ShadowRPG.Core.Game
{
    /// <summary>게임 정적 데이터 묶음 (카탈로그). 교체·확장 시 이 묶음만 바꿔 주입합니다.</summary>
    public sealed class GameContent
    {
        public ISkillCatalog Skills;
        public ISupportCatalog Supports;
        public ISetCatalog Sets;
        public IClassCatalog Classes;
        public IActCatalog Acts;
        public IPetCatalog Pets;
        public List<EnemyArchetype> Enemies;
        public List<EnemyArchetype> Elites;
        public EnemyArchetype Boss;

        public IReadOnlyList<EnemyArchetype> EnemiesFor(ActDefinition act)
        {
            var pool = Enemies.Where(e => act.EnemyIds.Contains(e.Id)).ToList();
            return pool.Count > 0 ? pool : Enemies;
        }

        public static GameContent CreateDefault() => new GameContent
        {
            Skills = SkillCatalog.CreateDefault(),
            Supports = SupportCatalog.CreateDefault(),
            Sets = SetCatalog.CreateDefault(),
            Classes = ClassCatalog.CreateDefault(),
            Acts = ActCatalog.CreateDefault(),
            Pets = PetCatalog.CreateDefault(),
            Enemies = EnemyRoster.CreateDefault(),
            Elites = EnemyRoster.CreateElites(),
            Boss = EnemyRoster.CreateBoss()
        };
    }
}
