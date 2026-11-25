using System;
using System.Collections.Generic;
using ShadowRPG.Core.Character;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.Tree;
using ShadowRPG.Core.World;

namespace ShadowRPG.Core.Game
{
    /// <summary>
    /// 세션 밖 성장 계층: 프로필 + 골드 소모처(강화·트리·펫) + 가방 판매.
    /// 전투 세션은 여기서 캐릭터를 만들어 받고, 끝나면 Capture로 돌려놓습니다.
    /// </summary>
    public sealed class GameMeta
    {
        public const int RaidEntriesPerDay = 3;

        public GameMeta(GameProfile profile, GameContent content, IClock clock, IRandom random)
        {
            Profile = profile;
            Content = content;
            Random = random;
            Loot = new LootGenerator(content.Skills, content.Supports, content.Sets, random);
            Scorer = new ItemScorer();
            Pricing = new ItemPricing();
            Enhancement = new EnhancementService();
            Pets = new PetService();
            Tree = PassiveTreeGenerator.Generate(profile.TreeSize, profile.TreeSeed);
            TreeService = new PassiveTreeService(Tree, profile.TreeOwned);
            RaidEntries = new DailyEntryLimit(clock, profile.Raid, RaidEntriesPerDay);
        }

        public GameProfile Profile { get; }
        public GameContent Content { get; }
        public IRandom Random { get; }
        public ILootGenerator Loot { get; }
        public IItemScorer Scorer { get; }
        public IItemPricing Pricing { get; }
        public EnhancementService Enhancement { get; }
        public PetService Pets { get; }
        public PassiveTree Tree { get; }
        public PassiveTreeService TreeService { get; }
        public DailyEntryLimit RaidEntries { get; }
        public Wallet Wallet => Profile.Wallet;

        /// <summary>저장이 필요한 변화가 생겼을 때 (층 클리어, 구매 등).</summary>
        public event Action SaveRequested;

        public void RequestSave() => SaveRequested?.Invoke();

        /// <summary>처음 시작이면 직업별 시작 장비와 펫을 지급합니다.</summary>
        public void EnsureStarted()
        {
            if (Profile.Pets.Count == 0)
            {
                foreach (var pet in Content.Pets.All) Profile.GetOrAddPet(pet.Id);
                Profile.ActivePetId = Content.Pets.All[0].Id;
            }
            if (Profile.HasStarted) return;

            foreach (var (slot, skill) in StarterKit(Profile.ClassId))
            {
                var item = Loot.Create(slot, ItemRarity.Normal, 1, skill);
                item.Requirements = default;
                Profile.Equipped[slot] = item;
            }
            Wallet.Earn(200);
        }

        public static IEnumerable<(EquipSlot, string)> StarterKit(string classId)
        {
            switch (classId)
            {
                case "mage":
                    return new[] { (EquipSlot.RightHand, "INT-01"), (EquipSlot.LeftHand, "INT-08"), (EquipSlot.Head, "DEX-08"), (EquipSlot.Body, "INT-07"), (EquipSlot.Boots, "DEX-07") };
                case "archer":
                    return new[] { (EquipSlot.RightHand, "DEX-04"), (EquipSlot.LeftHand, "DEX-05"), (EquipSlot.Head, "DEX-08"), (EquipSlot.Body, "INT-07"), (EquipSlot.Boots, "DEX-07") };
                case "monk":
                    return new[] { (EquipSlot.RightHand, "DEX-01"), (EquipSlot.LeftHand, "DEX-02"), (EquipSlot.Head, "DEX-08"), (EquipSlot.Body, "STR-08"), (EquipSlot.Boots, "DEX-07") };
                default:
                    return new[] { (EquipSlot.RightHand, "STR-01"), (EquipSlot.LeftHand, "INT-05"), (EquipSlot.Head, "STR-07"), (EquipSlot.Body, "STR-08"), (EquipSlot.Boots, "DEX-07") };
            }
        }

        /// <summary>프로필로부터 전투용 캐릭터를 만듭니다 (직업·레벨·장비·트리 반영).</summary>
        public PlayerCharacter CreatePlayer(ICombatWorld world)
        {
            var player = new PlayerCharacter(world, Content.Skills, Content.Supports, Content.Sets, Profile.Bag);
            var characterClass = Content.Classes.Get(Profile.ClassId) ?? Content.Classes.All[0];
            player.ChangeClass(characterClass);
            player.Progress.Restore(Profile.Level, Profile.Experience);
            foreach (var pair in Profile.Equipped) player.Equipment.ForceEquip(pair.Value, pair.Key);
            TreeService.ApplyTo(player.Combatant);
            player.Combatant.Health.ResetToFull();
            player.Combatant.Resource.Fill();
            return player;
        }

        public PetCompanion CreatePet(PlayerCharacter owner, IEffectSpawner effects)
        {
            var state = Profile.ActivePet;
            var definition = state != null ? Content.Pets.Get(state.PetId) : null;
            return definition == null ? null : new PetCompanion(definition, state, owner.Combatant, effects);
        }

        /// <summary>전투 중 바뀐 캐릭터 상태를 프로필에 기록합니다.</summary>
        public void Capture(PlayerCharacter player)
        {
            Profile.Equipped.Clear();
            foreach (var pair in player.Equipment.Equipped)
                if (pair.Value != null) Profile.Equipped[pair.Key] = pair.Value;
            Profile.Level = player.Progress.Level;
            Profile.Experience = player.Progress.Experience;
            Profile.ClassId = player.Class.Current?.Id ?? Profile.ClassId;
        }

        // ---------- 가방 / 골드 소모 ----------

        public long Sell(Item item, PlayerCharacter seller)
        {
            if (!Profile.Bag.Remove(item)) return 0;
            long gold = Pricing.SellValue(item, seller?.Combatant.Stats);
            Wallet.Earn(gold);
            RequestSave();
            return gold;
        }

        /// <summary>가방 장비 장착. 밀려난 장비는 가방으로 돌아갑니다.</summary>
        public EquipResult Equip(PlayerCharacter player, Item item, EquipSlot slot)
        {
            var result = player.Equipment.TryEquip(item, slot, player.Combatant.Stats);
            if (!result.Success) return result;
            Profile.Bag.Remove(item);
            if (result.Replaced != null) Profile.Bag.TryAdd(result.Replaced);
            Capture(player);
            RequestSave();
            return result;
        }

        public EnhanceResult Enhance(PlayerCharacter player, Item item)
        {
            var result = Enhancement.TryEnhance(item, Wallet, player?.Equipment);
            if (result.Success) RequestSave();
            return result;
        }

        public bool BuyTreeNode(int nodeId, PlayerCharacter player)
        {
            if (!TreeService.TryBuy(nodeId, Wallet)) return false;
            if (player != null) TreeService.ApplyTo(player.Combatant);
            RequestSave();
            return true;
        }

        /// <summary>도달 가능한 노드 중 가장 가까운(저렴한 등급 우선) 노드를 하나 삽니다.</summary>
        public bool BuyNextTreeNode(PlayerCharacter player, Predicate<TreeNode> filter = null)
        {
            foreach (var id in TreeService.Frontier())
            {
                var node = Tree.Nodes[id];
                if (filter != null && !filter(node)) continue;
                return BuyTreeNode(id, player);
            }
            return false;
        }

        public bool LevelUpPet(PetState state)
        {
            bool ok = Pets.TryLevelUp(state, Wallet);
            if (ok) RequestSave();
            return ok;
        }

        public bool GradeUpPet(PetState state)
        {
            bool ok = Pets.TryGradeUp(state, Wallet);
            if (ok) RequestSave();
            return ok;
        }
    }
}
