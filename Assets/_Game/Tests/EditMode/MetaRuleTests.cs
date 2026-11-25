using System.Linq;
using NUnit.Framework;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tree;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Tests
{
    /// <summary>던전 구조·전리품 제한·골드 소모처·펫·레이드 제한·저장 규칙 검증.</summary>
    public sealed class MetaRuleTests
    {
        [Test]
        public void FloorEnemyCount_GrowsByAct_From10To100()
        {
            var acts = ActCatalog.CreateDefault();
            var progress = new DungeonProgress();
            int first = acts.Plan(progress).EnemyCount;
            int previousActMax = 0;
            int last = 0;

            for (int act = 0; act < acts.Acts.Count; act++)
            {
                int actMax = 0;
                for (int d = 0; d < acts.DungeonsPerAct; d++)
                for (int f = 1; f <= acts.FloorsPerDungeon; f++)
                {
                    var plan = acts.Plan(new DungeonProgress { ActIndex = act, DungeonIndex = d, Floor = f });
                    Assert.That(plan.EnemyCount, Is.InRange(10, 100));
                    actMax = Mathf.Max(actMax, plan.EnemyCount);
                    last = plan.EnemyCount;
                }
                Assert.Greater(actMax, previousActMax, $"ACT {act + 1}은 이전 ACT보다 층당 몹이 많아야 함");
                previousActMax = actMax;
            }
            Assert.AreEqual(10, first);
            Assert.AreEqual(100, last);
        }

        [Test]
        public void DungeonProgress_ClearsFloorsThenDungeonsThenActs()
        {
            var acts = ActCatalog.CreateDefault();
            var progress = new DungeonProgress();
            for (int i = 0; i < acts.FloorsPerDungeon; i++) progress.Advance(acts);
            Assert.AreEqual(1, progress.DungeonIndex);
            Assert.AreEqual(1, progress.Floor);
            for (int i = 0; i < acts.FloorsPerDungeon * (acts.DungeonsPerAct - 1); i++) progress.Advance(acts);
            Assert.AreEqual(1, progress.ActIndex);
        }

        [Test]
        public void FloorLoot_TakesOnlyWhatFitsInBagCells_RestIsAutoSold()
        {
            var meta = TestFactory.Meta();
            var loot = new FloorLoot();
            for (int i = 0; i < 3; i++) loot.Add(meta.Loot.Create(EquipSlot.Head, ItemRarity.Rare, 5)); // 2x2 각 4칸

            var bag = new Inventory(2, 3); // 6칸 → 투구 하나만 들어감
            loot.AutoSelect(meta.Scorer, bag);
            Assert.AreEqual(1, loot.Selected.Count);
            Assert.IsFalse(loot.Toggle(loot.Found.First(i => !loot.IsSelected(i)), bag), "칸 부족이면 선택 불가");

            long before = meta.Wallet.Gold;
            var (kept, gold) = loot.Resolve(bag, meta.Wallet, meta.Pricing, null);
            Assert.AreEqual(1, kept);
            Assert.Greater(gold, 0);
            Assert.AreEqual(before + gold, meta.Wallet.Gold, "나머지 2개는 판매 → 골드");
        }

        [Test]
        public void GridBag_ItemsOccupyTheirShape()
        {
            var meta = TestFactory.Meta();
            var bag = new Inventory(8, 5);
            int bodies = 0;
            for (int i = 0; i < 6; i++)
                if (bag.TryAdd(meta.Loot.Create(EquipSlot.Body, ItemRarity.Magic, 1))) bodies++;
            Assert.AreEqual(4, bodies, "2x3 갑주는 8x5 가방에 4개");
            Assert.IsTrue(bag.TryAdd(meta.Loot.Create(EquipSlot.Ring1, ItemRarity.Magic, 1)), "남은 칸에 반지는 들어감");
            Assert.AreEqual(25, bag.UsedCells);
        }

        [Test]
        public void GridBag_LoseHalf_RemovesHalfRoundedUp()
        {
            var meta = TestFactory.Meta();
            var bag = new Inventory(8, 5);
            for (int i = 0; i < 7; i++) bag.TryAdd(meta.Loot.Create(EquipSlot.Ring1, ItemRarity.Magic, 1));
            var lost = bag.LoseHalf(meta.Random);
            Assert.AreEqual(4, lost.Count);
            Assert.AreEqual(3, bag.Items.Count);
        }
        [Test]
        public void Enhancement_CostRises_AndStatsScale()
        {
            var meta = TestFactory.Meta();
            meta.Wallet.Earn(1_000_000);
            var item = meta.Loot.Create(EquipSlot.Body, ItemRarity.Rare, 5);
            float armor = item.AllModifiers.Where(m => m.Stat == StatType.Armor).Sum(m => m.Value);

            long first = meta.Enhancement.Cost(item);
            Assert.IsTrue(meta.Enhancement.TryEnhance(item, meta.Wallet, null).Success);
            Assert.Greater(meta.Enhancement.Cost(item), first);
            float enhanced = item.AllModifiers.Where(m => m.Stat == StatType.Armor).Sum(m => m.Value);
            Assert.AreEqual(armor * 1.08f, enhanced, 0.01f);
        }

        [Test]
        public void PassiveTree_HasThousandsOfNodes_RequiresConnection_CostGrows()
        {
            var tree = PassiveTreeGenerator.Generate(5000, 7);
            Assert.AreEqual(5000, tree.Nodes.Count);

            var owned = new System.Collections.Generic.HashSet<int>();
            var service = new PassiveTreeService(tree, owned);
            var wallet = new Wallet(10_000_000);

            int far = tree.Nodes.OrderByDescending(n => n.Position.magnitude).First().Id;
            Assert.IsFalse(service.CanReach(far), "연결되지 않은 노드는 구매 불가");

            int next = service.Frontier()[0];
            long cost1 = service.Cost(next);
            Assert.IsTrue(service.TryBuy(next, wallet));
            int after = service.Frontier().First(id => tree.Nodes[id].Tier == tree.Nodes[next].Tier);
            Assert.Greater(service.Cost(after), cost1, "살수록 비용 증가");

            var player = new ShadowRPG.Core.Actors.Combatant("p", ShadowRPG.Core.Actors.Team.Player);
            service.ApplyTo(player);
            var node = tree.Nodes[next];
            Assert.GreaterOrEqual(player.Stats.Get(node.Stat), node.Value - 0.001f);
        }

        [Test]
        public void PassiveTree_CoversSixElementsAndMetaStats()
        {
            var stats = PassiveTreeGenerator.Generate(3000, 1).Nodes.Select(n => n.Stat).Distinct().ToList();
            foreach (var s in new[] { StatType.FireDamagePct, StatType.LightningDamagePct, StatType.ColdDamagePct, StatType.EarthDamagePct,
                         StatType.LightDamagePct, StatType.DarkDamagePct, StatType.GoldFindPct, StatType.ExperiencePct, StatType.PetPowerPct,
                         StatType.CritChancePct, StatType.EvasionPct, StatType.Armor })
                CollectionAssert.Contains(stats, s);
        }

        [Test]
        public void Pet_GradeUpUnlocksSkills_AndCostsGold()
        {
            var meta = TestFactory.Meta();
            var definition = meta.Content.Pets.Get("spirit");
            var state = meta.Profile.GetOrAddPet("spirit");
            Assert.AreEqual(1, definition.Skills.Count(s => s.UnlockGrade <= state.Grade));

            meta.Wallet.Earn(10_000_000);
            Assert.IsFalse(meta.GradeUpPet(state), "최대 레벨 전에는 등급업 불가");
            while (state.Level < PetService.MaxLevel(state)) Assert.IsTrue(meta.LevelUpPet(state));
            for (int grade = 2; grade <= 3; grade++)
            {
                while (state.Level < PetService.MaxLevel(state)) meta.LevelUpPet(state);
                Assert.IsTrue(meta.GradeUpPet(state));
            }
            Assert.AreEqual(3, state.Grade);
            Assert.AreEqual(2, definition.Skills.Count(s => s.UnlockGrade <= state.Grade), "★3에 치유의 빛 개방");
        }

        [Test]
        public void RaidEntries_AreLimitedPerDay_AndResetNextDay()
        {
            var clock = new FixedClock();
            var meta = TestFactory.Meta(clock: clock);
            for (int i = 0; i < 3; i++) Assert.IsTrue(meta.RaidEntries.TryConsume());
            Assert.IsFalse(meta.RaidEntries.TryConsume());
            Assert.AreEqual(0, meta.RaidEntries.Remaining);

            clock.Now = clock.Now.AddDays(1);
            Assert.AreEqual(3, meta.RaidEntries.Remaining);
        }

        [Test]
        public void Hazard_AutoMovementDodgesTelegraph()
        {
            var session = TestFactory.Session();
            var player = session.Player.Combatant;
            session.World.SpawnHazard(new Hazard { Team = ShadowRPG.Core.Actors.Team.Enemy, Shape = HazardShape.Circle, Center = player.Position + new Vector2(0.5f, 0f), Radius = 2f, Delay = 5f });
            session.Player.Context.Refresh();
            var move = session.Player.Movement.Decide(session.Player.Context, 0.016f);
            Assert.Less(move.x, -0.5f, "장판 중심 반대 방향으로 피함");
        }

        [Test]
        public void Profile_RoundTripsThroughJson()
        {
            var meta = TestFactory.Meta();
            meta.Wallet.Earn(1234);
            var item = meta.Loot.Create(EquipSlot.Amulet, ItemRarity.Legendary, 9);
            item.EnhanceLevel = 4;
            meta.Profile.Bag.TryAdd(item);
            meta.Profile.TreeOwned.Add(5);
            meta.Profile.Raid.BossLevel = 3;

            var store = new MemoryProfileStore();
            store.Save(meta.Profile);
            var loaded = store.Load();

            Assert.AreEqual(meta.Wallet.Gold, loaded.Wallet.Gold);
            Assert.AreEqual(1, loaded.Bag.Items.Count);
            var copy = loaded.Bag.Items[0];
            Assert.AreEqual(item.Name, copy.Name);
            Assert.AreEqual(4, copy.EnhanceLevel);
            Assert.AreEqual(item.Affixes.Count, copy.Affixes.Count);
            Assert.AreEqual(5, loaded.Equipped.Count, "시작 장비 5칸");
            Assert.IsTrue(loaded.TreeOwned.Contains(5));
            Assert.AreEqual(3, loaded.Raid.BossLevel);
        }
    }
}
