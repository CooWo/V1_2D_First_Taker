using System.Linq;
using NUnit.Framework;
using ShadowRPG.Core;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Status;
using ShadowRPG.Core.Supports;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Tests
{
    /// <summary>GDD 규칙이 코드에 그대로 반영됐는지 검증합니다.</summary>
    public sealed class GddRuleTests
    {
        [Test]
        public void CoreStats_Per10Points_FollowGdd()
        {
            var sheet = new StatSheet(new IStatDerivationRule[] { new CoreStatDerivationRule() });
            sheet.SetBase(StatType.Strength, 20f);
            sheet.SetBase(StatType.Dexterity, 20f);
            sheet.SetBase(StatType.Intelligence, 20f);

            Assert.AreEqual(100f, sheet.Get(StatType.MaxLife), 0.001f, "힘 10당 생명력 +50");
            Assert.AreEqual(4f, sheet.Get(StatType.MeleeDamagePct), 0.001f, "힘 10당 근접 +2%");
            Assert.AreEqual(1f, sheet.Get(StatType.AttackSpeedPct), 0.001f, "민첩 10당 공속 +0.5%");
            Assert.AreEqual(2f, sheet.Get(StatType.CritChancePct), 0.001f, "민첩 10당 치명타 +1%");
            Assert.AreEqual(40f, sheet.Get(StatType.MaxResource), 0.001f, "지능 10당 자원 +20");
            Assert.AreEqual(4f, sheet.Get(StatType.SpellDamagePct), 0.001f, "지능 10당 주문 +2%");
        }

        [Test]
        public void SkillRarity_Multipliers_FollowGdd()
        {
            Assert.AreEqual(1.0f, SkillRarityProfile.For(SkillRarity.Normal).DamageMultiplier);
            Assert.AreEqual(1.15f, SkillRarityProfile.For(SkillRarity.Magic).DamageMultiplier);
            Assert.AreEqual(1.35f, SkillRarityProfile.For(SkillRarity.Rare).DamageMultiplier);
            Assert.AreEqual(1.65f, SkillRarityProfile.For(SkillRarity.Legendary).DamageMultiplier);
            Assert.IsTrue(SkillRarityProfile.For(SkillRarity.Rare).HasGimmick);
            Assert.IsTrue(SkillRarityProfile.For(SkillRarity.Legendary).IsAwakened);
            Assert.IsFalse(SkillRarityProfile.For(SkillRarity.Rare).IsAwakened);
        }

        [Test]
        public void MeleeDamage_Is140Percent_OfRanged()
        {
            var session = TestFactory.Session();
            var attacker = new Combatant("무능력치 공격자", Team.Player);
            var target = TestFactory.Dummy(session, new Vector2(1f, 0f));
            var damage = session.World.Damage;

            float Deal(SkillTag tags) => damage.Deal(new DamageRequest
            {
                Attacker = attacker, Target = target, Tags = tags, Type = DamageType.Fire, BaseAmount = 100f, CanCrit = false
            }).Amount;

            float melee = Deal(SkillTag.Attack | SkillTag.Melee);
            float ranged = Deal(SkillTag.Attack | SkillTag.Ranged);
            Assert.AreEqual(1.4f, melee / ranged, 0.001f);
        }

        [Test]
        public void ShadowFortify_GrantedByMeleeHit_HalvedByRangedCast()
        {
            var session = TestFactory.Session();
            var player = session.Player.Combatant;
            var target = TestFactory.Dummy(session, new Vector2(1f, 0f));

            session.World.Damage.Deal(new DamageRequest { Attacker = player, Target = target, Tags = SkillTag.Attack | SkillTag.Melee, BaseAmount = 10f });
            var fortify = player.Statuses.Get<ShadowFortifyStatus>();
            Assert.NotNull(fortify, "[근접] 적중 시 그림자 갑주");
            Assert.AreEqual(4f, fortify.Remaining, 0.001f);
            Assert.AreEqual(70f, fortify.ModifyIncoming(100f), 0.001f, "받는 피해 30% 감소");

            var bolt = new SkillInstance(session.Meta.Content.Skills.Get("STR-06"), SkillRarity.Normal);
            player.Resource.Fill();
            Assert.IsTrue(session.World.Executor.TryCast(player, bolt, target, target.Position));
            Assert.AreEqual(2f, fortify.Remaining, 0.001f, "[원거리] 시전 시 남은 시간 절반");
        }

        [Test]
        public void ShadowFortify_NotGrantedByRangedHit()
        {
            var session = TestFactory.Session();
            var player = session.Player.Combatant;
            var target = TestFactory.Dummy(session, new Vector2(5f, 0f));
            session.World.Damage.Deal(new DamageRequest { Attacker = player, Target = target, Tags = SkillTag.Attack | SkillTag.Ranged, BaseAmount = 10f });
            Assert.IsNull(player.Statuses.Get<ShadowFortifyStatus>());
        }

        [Test]
        public void LifeOnHit_RangedEfficiencyIs45Percent()
        {
            var session = TestFactory.Session();
            var player = session.Player.Combatant;
            var target = TestFactory.Dummy(session, new Vector2(1f, 0f));
            player.Stats.AddModifier(new StatModifier(StatType.LifeOnHit, 99f));
            player.Health.ApplyDamage(player.Health.MaxLife * 0.8f + player.Health.EnergyShield);

            float before = player.Health.Life;
            session.World.Damage.Deal(new DamageRequest { Attacker = player, Target = target, Tags = SkillTag.Attack | SkillTag.Melee, BaseAmount = 10f });
            float meleeHeal = player.Health.Life - before;

            before = player.Health.Life;
            session.World.Damage.Deal(new DamageRequest { Attacker = player, Target = target, Tags = SkillTag.Attack | SkillTag.Ranged, BaseAmount = 10f });
            float rangedHeal = player.Health.Life - before;

            Assert.AreEqual(0.45f, rangedHeal / meleeHeal, 0.001f);
        }

        [Test]
        public void BasicProjectile_ExpiresOnFirstHit_PierceRequiresModifier()
        {
            var session = TestFactory.Session();
            var player = session.Player.Combatant;
            TestFactory.Dummy(session, player.Position + new Vector2(2f, 0f));
            TestFactory.Dummy(session, player.Position + new Vector2(3f, 0f));

            var projectile = new Projectile { Owner = player, Team = Team.Player, Position = player.Position, Direction = Vector2.right, Speed = 10f, MaxDistance = 10f };
            int hits = 0;
            projectile.OnHit = (p, t) => hits++;
            session.World.SpawnProjectile(projectile);
            for (int i = 0; i < 60; i++) session.World.Tick(0.016f);
            Assert.AreEqual(1, hits, "투사체는 기본적으로 최전열 1체 적중 시 소멸");

            var piercing = new Projectile { Owner = player, Team = Team.Player, Position = player.Position, Direction = Vector2.right, Speed = 10f, MaxDistance = 10f, PierceRemaining = 1 };
            int pierceHits = 0;
            piercing.OnHit = (p, t) => pierceHits++;
            session.World.SpawnProjectile(piercing);
            for (int i = 0; i < 60; i++) session.World.Tick(0.016f);
            Assert.AreEqual(2, pierceHits, "관통 +1이면 두 번째 적까지");
        }

        [Test]
        public void Equipment_UsesStatRequirementsOnly_NoClassRestriction()
        {
            var session = TestFactory.Session();
            var player = session.Player;
            player.ChangeClass(session.Meta.Content.Classes.Get("knight"));

            var staff = session.Meta.Loot.Create(EquipSlot.RightHand, ItemRarity.Rare, 1, "INT-03");
            staff.Requirements = new Requirements(0, 0, 20);
            Assert.IsTrue(session.Meta.Equip(session.Player, staff, EquipSlot.RightHand).Success, "기사도 지능 요구치만 맞으면 운석 지팡이 장착");

            var heavy = session.Meta.Loot.Create(EquipSlot.RightHand, ItemRarity.Rare, 1, "STR-01");
            heavy.Requirements = new Requirements(999, 0, 0);
            var result = session.Meta.Equip(session.Player, heavy, EquipSlot.RightHand);
            Assert.IsFalse(result.Success);
            StringAssert.Contains("요구 능력치", result.Reason);
        }

        [Test]
        public void SetBonus_RelicReducesRequirementByOne()
        {
            var session = TestFactory.Session();
            var player = session.Player;
            const string setId = "set_shadow_tempest";

            foreach (var slot in new[] { EquipSlot.Head, EquipSlot.Body })
            {
                var piece = session.Meta.Loot.Create(slot, ItemRarity.Set, 1);
                piece.SetId = setId;
                piece.Requirements = default;
                player.Equipment.ForceEquip(piece, slot);
            }
            Assert.AreEqual(1, player.Sets.ActiveBonuses.Count, "2세트만 활성");

            var relic = session.Meta.Loot.Create(EquipSlot.Relic, ItemRarity.Legendary, 1);
            relic.ReducesSetRequirement = true;
            player.Equipment.ForceEquip(relic, EquipSlot.Relic);
            Assert.AreEqual(3, player.Sets.EffectiveCount(setId));
            Assert.AreEqual(2, player.Sets.ActiveBonuses.Count, "성물로 3세트까지 활성");
        }

        [Test]
        public void Equipment_GrantsSkills_AndAccessoriesGrantSupports()
        {
            var session = TestFactory.Session(classId: "archer");
            var loadout = session.Player.Combatant.Loadout;
            Assert.AreEqual(5, loadout.Skills.Count, "무기·방어구 5칸에서 스킬 5개");
            Assert.IsFalse(loadout.PrimaryIsMelee);

            var amulet = session.Meta.Loot.Create(EquipSlot.Amulet, ItemRarity.Rare, 1);
            amulet.SupportCode = "SUP-03";
            amulet.Requirements = default;
            session.Player.Equipment.ForceEquip(amulet, EquipSlot.Amulet);

            Assert.AreEqual(5, loadout.Skills.Count, "장신구는 스킬 수를 늘리지 않음");
            var mods = new SkillModifiers();
            foreach (var source in loadout.ModifierSources) source.Modify(session.Player.Combatant, loadout.Primary, mods);
            Assert.AreEqual(3, mods.ExtraProjectiles);
            Assert.AreEqual(0.75f, mods.DamageMultiplier, 0.001f);
        }

        [Test]
        public void AutoBattle_SurvivalBeatsPrimary_WhenLifeAtOrBelowHalf()
        {
            var session = TestFactory.Session(classId: "mage");
            var player = session.Player;
            TestFactory.Dummy(session, player.Combatant.Position + new Vector2(2f, 0f));

            var aura = player.Combatant.Loadout.Skills.First(s => s.Definition.Category == AutoCastCategory.Aura);
            aura.IsActive = true;
            player.Combatant.Health.ApplyDamage(player.Combatant.Health.MaxLife * 0.6f + player.Combatant.Health.EnergyShield);

            player.Planner.Tick(1f);
            Assert.AreEqual("생존", player.Planner.LastDecision);
            Assert.Greater(player.Combatant.Health.Barrier, 0f, "심연의 방벽 발동");
        }

        [Test]
        public void ArcLightning_Legendary_ScalesWithUnusedChains()
        {
            var session = TestFactory.Session();
            var player = session.Player.Combatant;
            var lone = TestFactory.Dummy(session, player.Position + new Vector2(3f, 0f));

            float DamageWith(SkillRarity rarity)
            {
                lone.Statuses.Clear();
                float before = lone.Health.Life;
                var skill = new SkillInstance(session.Meta.Content.Skills.Get("INT-01"), rarity);
                session.World.Executor.TriggerCast(player, skill, lone, lone.Position, 1f);
                return before - lone.Health.Life;
            }

            float rare = DamageWith(SkillRarity.Rare);
            float legendary = DamageWith(SkillRarity.Legendary);
            float expected = 1.65f / 1.35f * Mathf.Pow(1.18f, 5);
            Assert.AreEqual(expected, legendary / rare, 0.01f, "1:1에서 남은 연쇄 5회 × 18% 곱연산");
        }
    }
}
