using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Meta
{
    /// <summary>저장소 추상화 (파일/클라우드 등). 직렬화 형식과 저장 위치를 분리합니다.</summary>
    public interface IProfileStore
    {
        GameProfile Load();
        void Save(GameProfile profile);
    }

    /// <summary>프로필 ⇄ JSON. JsonUtility가 다룰 수 있는 단순 DTO로 바꿔 저장합니다.</summary>
    public static class ProfileSerializer
    {
        [Serializable] class StatDto { public int stat; public float value; }
        [Serializable] class TagDto { public long tags; public float multiplier; }

        [Serializable]
        class ItemDto
        {
            public string id, name, skillCode, supportCode, setId, power;
            public int slot, subtype, rarity, itemLevel, str, dex, intel, skillRarity, enhance;
            public bool reducesSet;
            public StatDto[] baseStats = Array.Empty<StatDto>();
            public StatDto[] affixes = Array.Empty<StatDto>();
            public TagDto[] tagMultipliers = Array.Empty<TagDto>();
        }

        [Serializable] class EquippedDto { public int slot; public ItemDto item; }

        [Serializable]
        class ProfileDto
        {
            public int version = 1;
            public long gold;
            public string classId;
            public int level;
            public float experience;
            public DungeonProgress progress;
            public ItemDto[] bag = Array.Empty<ItemDto>();
            public EquippedDto[] equipped = Array.Empty<EquippedDto>();
            public int[] treeOwned = Array.Empty<int>();
            public int treeSeed;
            public int treeSize;
            public PetState[] pets = Array.Empty<PetState>();
            public string activePetId;
            public RaidState raid;
        }

        public static string ToJson(GameProfile profile)
        {
            var dto = new ProfileDto
            {
                gold = profile.Wallet.Gold,
                classId = profile.ClassId,
                level = profile.Level,
                experience = profile.Experience,
                progress = profile.Progress,
                bag = profile.Bag.Items.Select(ToDto).ToArray(),
                equipped = profile.Equipped.Select(p => new EquippedDto { slot = (int)p.Key, item = ToDto(p.Value) }).ToArray(),
                treeOwned = profile.TreeOwned.ToArray(),
                treeSeed = profile.TreeSeed,
                treeSize = profile.TreeSize,
                pets = profile.Pets.ToArray(),
                activePetId = profile.ActivePetId,
                raid = profile.Raid
            };
            return JsonUtility.ToJson(dto);
        }

        public static GameProfile FromJson(string json)
        {
            var dto = JsonUtility.FromJson<ProfileDto>(json);
            var profile = new GameProfile(dto.gold)
            {
                ClassId = string.IsNullOrEmpty(dto.classId) ? "knight" : dto.classId,
                Level = Math.Max(1, dto.level),
                Experience = dto.experience,
                Progress = dto.progress ?? new DungeonProgress(),
                TreeSeed = dto.treeSeed != 0 ? dto.treeSeed : 20261126,
                TreeSize = dto.treeSize != 0 ? dto.treeSize : 3000,
                ActivePetId = dto.activePetId,
                Raid = dto.raid ?? new RaidState()
            };
            foreach (var item in dto.bag ?? Array.Empty<ItemDto>()) profile.Bag.TryAdd(FromDto(item));
            foreach (var e in dto.equipped ?? Array.Empty<EquippedDto>()) profile.Equipped[(EquipSlot)e.slot] = FromDto(e.item);
            foreach (var id in dto.treeOwned ?? Array.Empty<int>()) profile.TreeOwned.Add(id);
            profile.Pets.AddRange(dto.pets ?? Array.Empty<PetState>());
            return profile;
        }

        static ItemDto ToDto(Item item) => new ItemDto
        {
            id = item.Id,
            name = item.Name,
            slot = (int)item.Slot,
            subtype = (int)item.Subtype,
            rarity = (int)item.Rarity,
            itemLevel = item.ItemLevel,
            str = item.Requirements.Strength,
            dex = item.Requirements.Dexterity,
            intel = item.Requirements.Intelligence,
            skillCode = item.GrantedSkill.SkillCode,
            skillRarity = (int)item.GrantedSkill.Rarity,
            supportCode = item.SupportCode,
            setId = item.SetId,
            reducesSet = item.ReducesSetRequirement,
            power = item.PowerDescription,
            enhance = item.EnhanceLevel,
            baseStats = item.BaseStats.Select(m => new StatDto { stat = (int)m.Stat, value = m.Value }).ToArray(),
            affixes = item.Affixes.Select(m => new StatDto { stat = (int)m.Stat, value = m.Value }).ToArray(),
            tagMultipliers = item.TagMultipliers.Select(t => new TagDto { tags = (long)t.RequiredTags, multiplier = t.Multiplier }).ToArray()
        };

        static Item FromDto(ItemDto dto)
        {
            var item = new Item
            {
                Id = dto.id,
                Name = dto.name,
                Slot = (EquipSlot)dto.slot,
                Subtype = (WeaponSubtype)dto.subtype,
                Rarity = (ItemRarity)dto.rarity,
                ItemLevel = dto.itemLevel,
                Requirements = new Requirements(dto.str, dto.dex, dto.intel),
                GrantedSkill = string.IsNullOrEmpty(dto.skillCode) ? default : new SkillGrant(dto.skillCode, (SkillRarity)dto.skillRarity),
                SupportCode = string.IsNullOrEmpty(dto.supportCode) ? null : dto.supportCode,
                SetId = string.IsNullOrEmpty(dto.setId) ? null : dto.setId,
                ReducesSetRequirement = dto.reducesSet,
                PowerDescription = string.IsNullOrEmpty(dto.power) ? null : dto.power,
                EnhanceLevel = dto.enhance
            };
            item.BaseStats.AddRange((dto.baseStats ?? Array.Empty<StatDto>()).Select(s => new StatModifier((StatType)s.stat, s.value)));
            item.Affixes.AddRange((dto.affixes ?? Array.Empty<StatDto>()).Select(s => new StatModifier((StatType)s.stat, s.value)));
            item.TagMultipliers.AddRange((dto.tagMultipliers ?? Array.Empty<TagDto>()).Select(t => new TagMultiplier((SkillTag)t.tags, t.multiplier)));
            return item;
        }
    }

    /// <summary>메모리 저장소 (테스트·임시 실행용).</summary>
    public sealed class MemoryProfileStore : IProfileStore
    {
        string _json;
        public GameProfile Load() => _json == null ? new GameProfile() : ProfileSerializer.FromJson(_json);
        public void Save(GameProfile profile) => _json = ProfileSerializer.ToJson(profile);
    }
}
