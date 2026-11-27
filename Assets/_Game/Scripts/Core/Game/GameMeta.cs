using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Character;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Growth;
using ShadowRPG.Core.Rhythm;
using ShadowRPG.Core.Tuning;
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
        public GameMeta(GameProfile profile, GameContent content, IClock clock, IRandom random, GameTuning tuning = null, IStorePurchaser store = null)
        {
            Tuning = tuning ?? GameTuning.Current;
            Profile = profile;
            Content = content;
            Random = random;
            Clock = clock;
            Loot = new LootGenerator(content.Skills, content.Supports, content.Sets, random, Tuning.Drop);
            Power = new PowerCalculator(Tuning.Item);
            StatPoints = new StatAllocationService(profile.State, profile.Wallet, Tuning.Growth);
            Training = new TrainingService(profile.State, Tuning.Training);
            Presets = new PresetService(profile, Tuning.Convenience);
            Quests = new DailyQuestService(profile.State.Quests, Tuning.Quest, clock);
            Attendance = new AttendanceService(profile.State.Attendance, Tuning.Attendance, clock);
            Shop = new ShopService(profile, Tuning, store ?? new UnavailableStorePurchaser(), clock);
            Scorer = new ItemScorer();
            Pricing = new ItemPricing();
            Enhancement = new EnhancementService();
            Pets = new PetService();
            Tree = PassiveTreeGenerator.Generate(Tuning.Tree.NodeCount > 0 ? Tuning.Tree.NodeCount : profile.TreeSize, profile.TreeSeed);
            TreeService = new PassiveTreeService(Tree, profile.TreeOwned);
            RaidEntries = new DailyEntryLimit(clock, profile.Raid, Tuning.Raid.EntriesPerDay);
        }

        public GameTuning Tuning { get; }
        public IClock Clock { get; }
        public IPowerCalculator Power { get; }
        public StatAllocationService StatPoints { get; }
        public TrainingService Training { get; }
        public PresetService Presets { get; }
        public DailyQuestService Quests { get; }
        public AttendanceService Attendance { get; }
        public ShopService Shop { get; }
        public ProgressionState State => Profile.State;
        public PlayerSettings Settings => Profile.State.Settings;
        /// <summary>열린 ACT 수 (단련 한도·출석 보상 기준).</summary>
        public int ActsOpened => Content.Acts.ActsOpened(Profile.UnlockedDungeons);
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
                // 첫 무기 = 리듬 무기 (방향성 v1 §7): 초반 구간까지 쓸 만하고, 박자는 무기마다 다름
                if (slot == EquipSlot.RightHand)
                {
                    item.BeatSeconds = StarterBeat(Profile.ClassId);
                    item.Name = "박동하는 " + item.Name;
                }
                Profile.Equipped[slot] = item;
            }
            Wallet.Earn(Tuning.Growth.StartingGold);
            State.UnspentStatPoints += Tuning.Growth.StartingStatPoints;
            Presets.SaveCurrent(0);
        }

        /// <summary>직업별 첫 리듬 무기 박자 (초). 기본값은 RhythmTuning.DefaultBeatSeconds 기준으로 조금씩 다르게.</summary>
        public float StarterBeat(string classId)
        {
            float b = Tuning.Rhythm.DefaultBeatSeconds;
            switch (classId)
            {
                case "mage": return b * 1.15f;
                case "archer": return b * 0.9f;
                case "monk": return b * 0.75f;
                default: return b;
            }
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
            StatPoints.ApplyTo(player.Progress);
            Training.ApplyTo(player.Combatant);
            player.Progress.LeveledUpBy += (level, gained) => StatPoints.GrantForLevels(gained);
            if (world is World.CombatWorld combatWorld)
                player.Rhythm = new RhythmController(player.Combatant, new EquipmentRhythmSource(player.Equipment), Tuning.Rhythm, combatWorld.Damage, () => { player.Context.Refresh(); return player.Context.NearestDistance < 3.5f ? player.Context.NearestEnemy : null; });
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

        // ---------- 성장 (방향성 v1) ----------

        public bool AllocateStat(PlayerCharacter player, Stats.CoreStat stat)
        {
            if (!StatPoints.Allocate(stat)) return false;
            if (player != null) StatPoints.ApplyTo(player.Progress);
            RequestSave();
            return true;
        }

        public bool RespecStats(PlayerCharacter player)
        {
            if (!StatPoints.Respec(Profile.Level)) return false;
            if (player != null) StatPoints.ApplyTo(player.Progress);
            RequestSave();
            return true;
        }

        public string Train(PlayerCharacter player, TrainingTrack track)
        {
            var error = Training.TryTrain(track, Wallet, ActsOpened);
            if (error != null) return error;
            if (player != null) Training.ApplyTo(player.Combatant);
            Quests.Report("train");
            RequestSave();
            return null;
        }

        /// <summary>
        /// 귀환 시 자동 장착 (옵션): 가방·창고에서 슬롯별로 지금보다 전투력이 높은 장비가 있으면 장착.
        /// 반환: 바꾼 개수.
        /// </summary>
        public int AutoEquipBetter(PlayerCharacter player)
        {
            int changed = 0;
            var candidates = Profile.Bag.Items.Concat(Profile.Stash.Items).ToList();
            foreach (var slot in SlotRules.All)
            {
                var current = player.Equipment.Get(slot);
                int currentPower = Power.ItemPower(current);
                Item best = null;
                int bestPower = currentPower;
                foreach (var item in candidates)
                {
                    if (!SlotRules.CanPlace(item.Slot, slot) || !item.Requirements.IsMetBy(player.Combatant.Stats)) continue;
                    if (current != null && current.IsRhythm && !item.IsRhythm) continue; // 리듬 무기는 자동으로 바꾸지 않음
                    int p = Power.ItemPower(item);
                    if (p <= bestPower) continue;
                    best = item;
                    bestPower = p;
                }
                if (best == null) continue;
                if (Equip(player, best, slot).Success)
                {
                    candidates.Remove(best);
                    changed++;
                }
            }
            return changed;
        }

        public int TotalPower(PlayerCharacter player) => Power.TotalPower(player.Combatant.Stats, player.Equipment);

        // ---------- 던전 진행 ----------

        /// <summary>마을에서 던전 입장: 선택한 던전의 1층부터 시작합니다.</summary>
        public void StartRun(int globalDungeon)
        {
            globalDungeon = Math.Min(globalDungeon, Profile.UnlockedDungeons - 1);
            Profile.Progress = Content.Acts.StartOf(globalDungeon, Profile.Progress.HighestGlobalFloor);
            RequestSave();
        }

        /// <summary>던전 보스 층 클리어 → 다음 던전 개방.</summary>
        public void UnlockNextDungeon(int clearedGlobalDungeon)
        {
            int total = Dungeon.ActCatalogExtensions.TotalDungeons(Content.Acts);
            Profile.UnlockedDungeons = Math.Min(total, Math.Max(Profile.UnlockedDungeons, clearedGlobalDungeon + 2));
        }

        // ---------- 가방 ↔ 창고 ----------

        public bool MoveToStash(Item item)
        {
            if (!Profile.Stash.CanFit(item) || !Profile.Bag.Remove(item)) return false;
            Profile.Stash.TryAdd(item);
            RequestSave();
            return true;
        }

        public bool MoveToBag(Item item)
        {
            if (!Profile.Bag.CanFit(item) || !Profile.Stash.Remove(item)) return false;
            Profile.Bag.TryAdd(item);
            RequestSave();
            return true;
        }

        /// <summary>창고에서 바로 판매.</summary>
        public long SellFromStash(Item item, PlayerCharacter seller)
        {
            if (!Profile.Stash.Remove(item)) return 0;
            long gold = Pricing.SellValue(item, seller?.Combatant.Stats);
            Wallet.Earn(gold);
            RequestSave();
            return gold;
        }

        // ---------- 가방 / 골드 소모 ----------

        public long Sell(Item item, PlayerCharacter seller)
        {
            if (!Profile.Bag.Remove(item)) return 0;
            long gold = Pricing.SellValue(item, seller?.Combatant.Stats);
            Wallet.Earn(gold);
            NoteSale(gold);
            RequestSave();
            return gold;
        }

        public void NoteSale(long gold)
        {
            State.Lifetime.GoldEarnedBySelling += gold;
            Quests.Report("sell_gold", (int)Math.Min(int.MaxValue, gold));
        }

        /// <summary>가방 장비 장착. 밀려난 장비는 가방으로 돌아갑니다.</summary>
        public EquipResult Equip(PlayerCharacter player, Item item, EquipSlot slot)
        {
            var source = Profile.Stash.TryGetPlacement(item, out _) ? Profile.Stash : Profile.Bag;
            var result = player.Equipment.TryEquip(item, slot, player.Combatant.Stats);
            if (!result.Success) return result;
            source.Remove(item);
            if (result.Replaced != null && !source.TryAdd(result.Replaced) && !Profile.Bag.TryAdd(result.Replaced) && !Profile.Stash.TryAdd(result.Replaced))
                Wallet.Earn(Pricing.SellValue(result.Replaced, player.Combatant.Stats));
            Capture(player);
            RequestSave();
            return result;
        }

        public EnhanceResult Enhance(PlayerCharacter player, Item item)
        {
            var result = Enhancement.TryEnhance(item, Wallet, player?.Equipment);
            if (result.Success) { Quests.Report("enhance"); RequestSave(); }
            return result;
        }

        public bool BuyTreeNode(int nodeId, PlayerCharacter player)
        {
            if (!TreeService.TryBuy(nodeId, Wallet)) return false;
            Quests.Report("tree");
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
