using System.Linq;
using NUnit.Framework;
using ShadowRPG.Core;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Growth;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Rhythm;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tuning;

namespace ShadowRPG.Tests
{
    /// <summary>방향성 v1 성장 규칙: 스탯 포인트·단련 한도·드랍 상한·세팅 슬롯·퀘스트·리듬.</summary>
    public sealed class GrowthRuleTests
    {
        [Test]
        public void LevelUp_GivesOneStatPoint_AndRespecReturnsAll()
        {
            var session = TestFactory.Session();
            var meta = session.Meta;
            var player = session.Player;
            float str = player.Combatant.Stats.Get(StatType.Strength);
            player.Progress.Gain(player.Progress.ExperienceToNext);
            Assert.AreEqual(1, meta.StatPoints.Unspent);
            Assert.IsTrue(meta.AllocateStat(player, CoreStat.Strength));
            Assert.AreEqual(str + 1f, player.Combatant.Stats.Get(StatType.Strength), 0.01f);
            Assert.IsTrue(meta.RespecStats(player));
            Assert.AreEqual(1, meta.StatPoints.Unspent);
        }

        [Test]
        public void Training_IsCappedPerOpenedAct()
        {
            var meta = TestFactory.Meta();
            meta.Wallet.Earn(100_000_000);
            int cap = meta.Training.Cap(meta.ActsOpened);
            for (int i = 0; i < cap; i++) Assert.IsNull(meta.Train(null, TrainingTrack.Attack));
            Assert.IsNotNull(meta.Train(null, TrainingTrack.Attack), "ACT 한도에서 막힘");
            Assert.Greater(meta.Training.Cost(TrainingTrack.Life), 0);
        }

        [Test]
        public void Drops_RespectActRarityCap_AndItemLevelFormula()
        {
            var meta = TestFactory.Meta(0f);
            var loot = (LootGenerator)meta.Loot;
            for (int i = 0; i < 30; i++)
            {
                var item = loot.Generate(new DropContext(0, 0, 9, EnemyRank.Normal));
                Assert.LessOrEqual(item.Rarity, ItemRarity.Rare, "ACT 1은 희귀까지");
            }
            Assert.AreEqual(25 + 2 * 4 + 5, loot.ItemLevel(new DropContext(1, 1, 10, EnemyRank.Normal)));
        }

        [Test]
        public void StarterWeapon_IsRhythm_AndPerfectTapGivesExtraHitsAndSpeed()
        {
            var session = TestFactory.Session();
            var weapon = session.Player.Equipment.Get(EquipSlot.RightHand);
            Assert.IsTrue(weapon.IsRhythm, "첫 무기 = 리듬 무기");
            var rhythm = session.Player.Rhythm;
            Assert.IsTrue(rhythm.IsActive);

            var dummy = TestFactory.Dummy(session, session.Player.Combatant.Position + UnityEngine.Vector2.right);
            float life = dummy.Health.Life;
            rhythm.Tick(rhythm.BeatSeconds);
            Assert.AreEqual(RhythmJudgement.Perfect, rhythm.Tap());
            Assert.Less(dummy.Health.Life, life, "추가 타격");
            Assert.AreEqual(1, rhythm.Stacks);
            Assert.AreEqual(RhythmJudgement.Miss, rhythm.Tap(), "같은 박 두 번은 무효");
        }

        [Test]
        public void Presets_StartAtThree_ExpandWithTempoToSix()
        {
            var meta = TestFactory.Meta();
            Assert.AreEqual(3, meta.Presets.SlotCount);
            meta.State.Tempo = 10_000;
            for (int i = 0; i < 3; i++) Assert.IsNull(meta.Presets.TryExpand());
            Assert.AreEqual(6, meta.Presets.SlotCount);
            Assert.IsNotNull(meta.Presets.TryExpand());
        }

        [Test]
        public void DailyQuests_PickFive_AndClaimRewards()
        {
            var meta = TestFactory.Meta();
            var active = meta.Quests.Active;
            Assert.AreEqual(GameTuning.Current.Quest.DailyCount, active.Count);
            var q = active.First(x => meta.Quests.Template(x.Id).Category != "반납");
            meta.Quests.Report(q.Id, 100_000);
            long gold = meta.Wallet.Gold;
            Assert.IsTrue(meta.Quests.Claim(q, meta.Wallet, meta.State));
            Assert.Greater(meta.Wallet.Gold, gold);
        }
    }
}
