using System.Linq;
using NUnit.Framework;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Tests
{
    /// <summary>층 흐름(슬로우 모션 → 결과 → 계속/귀환), 사망 패널티, 던전 형태, 길찾기 검증.</summary>
    public sealed class FlowAndLayoutTests
    {
        static void KillAllEnemies(DungeonSession session)
        {
            foreach (var agent in session.Spawner.Agents.ToList())
                agent.Combatant.Health.ApplyDamage(agent.Combatant.Health.MaxLife * 10f);
        }

        static void Run(DungeonSession session, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) session.Tick(0.05f);
        }

        [Test]
        public void FloorClear_SlowMotion_ThenResult_ThenContinue()
        {
            var session = TestFactory.Session();
            Assert.AreEqual(FloorState.Fighting, session.State);

            KillAllEnemies(session);
            session.Tick(0.05f);
            Assert.AreEqual(FloorState.Clearing, session.State);
            Assert.Less(session.TimeScale, 1f, "클리어 순간 슬로우 모션");

            Run(session, 1f);
            Assert.AreEqual(FloorState.Result, session.State, "포탈 없이 결과창");
            Assert.AreEqual(1f, session.TimeScale);
            Assert.AreEqual(session.Plan.TotalKills, session.Summary.Kills);

            session.ContinueToNextFloor();
            Assert.AreEqual(2, session.Plan.Floor);
            Assert.AreEqual(FloorState.Fighting, session.State);
        }

        [Test]
        public void ResultScreen_ReturnToTown_RaisesSafeReturn()
        {
            var session = TestFactory.Session();
            bool? died = null;
            session.ReturnToTownRequested += d => died = d;
            KillAllEnemies(session);
            Run(session, 1f);
            session.ReturnToTown();
            Assert.AreEqual(false, died);
        }

        [Test]
        public void DeathWhileContinuing_LosesHalfOfBag_AndReturnsToTown()
        {
            var session = TestFactory.Session();
            for (int i = 0; i < 6; i++) session.Bag.TryAdd(session.Meta.Loot.Create(EquipSlot.Ring1, ItemRarity.Magic, 1));
            bool? died = null;
            session.ReturnToTownRequested += d => died = d;

            session.Player.Combatant.Health.ApplyDamage(1e9f);
            session.Tick(0.05f);
            Assert.AreEqual(FloorState.Dead, session.State);
            Assert.AreEqual(3, session.Bag.Items.Count, "가방 6개 중 절반 손실");

            Run(session, 4f);
            Assert.IsNull(died, "사망창에서 플레이어 선택을 기다림 (자동 귀환 없음)");
            Assert.IsNotNull(session.DeathReport, "사망 원인 요약");

            int restored = session.RecoverWithAd();
            Assert.AreEqual(3, restored, "광고 회수 = 잃은 아이템 전부");
            Assert.AreEqual(6, session.Bag.Items.Count);
            Assert.AreEqual(true, died);
        }

        [Test]
        public void AllLayouts_AreConnected_AndHaveRoomsAwayFromStart()
        {
            var generators = new IFloorLayoutGenerator[]
            {
                new RoomsAndCorridorsLayout(), new CaveLayout(), new CrossLayout(), new RingLayout(), new WindingHallLayout(), new PillarHallLayout(),
                new ChamberChainLayout(), new ChamberBranchLayout()
            };
            foreach (var generator in generators)
            foreach (int enemies in new[] { 10, 60, 101 })
            {
                var layout = generator.Generate(new System.Random(enemies), enemies);
                var map = layout.Map;
                Assert.IsTrue(map.IsWalkable(layout.Start.x, layout.Start.y), $"{generator.Name}: 시작 칸");
                Assert.Greater(map.CountWalkable(), 150, $"{generator.Name}: 걸을 수 있는 칸");

                var navigator = new GridNavigator(map, () => 0f);
                var far = layout.Rooms.Select(r => map.CenterOf(new Vector2Int(r.x + r.width / 2, r.y + r.height / 2)))
                    .Where(map.IsWalkable).OrderByDescending(p => Vector2.Distance(p, layout.StartPosition)).First();
                Assert.IsTrue(Walk(navigator, layout.StartPosition, far), $"{generator.Name}({enemies}): 가장 먼 방까지 길이 이어져야 함");
            }
        }

        [Test]
        public void Navigator_SteersAroundWall()
        {
            // 가운데 세로 벽이 있고 아래쪽만 뚫린 지도
            var map = new GridMap(20, 20, Vector2.zero);
            map.Fill(new RectInt(1, 1, 18, 18), true);
            map.Fill(new RectInt(9, 4, 2, 15), false);
            var navigator = new GridNavigator(map, () => 0f);

            var from = new Vector2(4.5f, 15.5f);
            var to = new Vector2(15.5f, 15.5f);
            Assert.IsFalse(navigator.HasLineOfSight(from, to));
            var step = navigator.Steer(from, to);
            Assert.Less(step.y, -0.5f, "벽을 돌아가려면 먼저 아래로");
            Assert.IsTrue(Walk(navigator, from, to));
        }

        static bool Walk(GridNavigator navigator, Vector2 from, Vector2 to)
        {
            var p = from;
            for (int i = 0; i < 4000; i++)
            {
                if (Vector2.Distance(p, to) < 0.8f) return true;
                var next = p + navigator.Steer(p, to) * 0.25f;
                if (!navigator.IsWalkable(next)) return false;
                p = next;
            }
            return false;
        }
    }
}
