using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Tuning;

namespace ShadowRPG.Core.Growth
{
    /// <summary>
    /// 장비 세팅 슬롯 (방향성 v1 §8): 리듬 빌드 ↔ 자동 빌드 전환용 프리셋. 기본 3개, 유료로 최대 6개.
    /// 슬롯 하나를 PvP 세팅으로 지정할 수 있습니다. 아이템은 Id로 기억하고, 불러올 때 가방·창고에서 찾아 장착합니다.
    /// </summary>
    public sealed class PresetService
    {
        readonly GameProfile _profile;
        readonly ConvenienceTuning _tuning;

        public PresetService(GameProfile profile, ConvenienceTuning tuning)
        {
            _profile = profile;
            _tuning = tuning;
            var state = profile.State;
            state.PresetSlots = System.Math.Max(state.PresetSlots, tuning.PresetSlotsBase);
            while (state.Presets.Count < state.PresetSlots) state.Presets.Add(new EquipmentPresetState { Name = $"세팅 {state.Presets.Count + 1}" });
        }

        ProgressionState State => _profile.State;
        public int SlotCount => State.PresetSlots;
        public int MaxSlots => _tuning.PresetSlotsMax;
        public int Active => State.ActivePreset;
        public int PvpPreset => State.PvpPreset;
        public IReadOnlyList<EquipmentPresetState> Presets => State.Presets;

        public bool CanExpand => State.PresetSlots < _tuning.PresetSlotsMax;

        /// <summary>템포로 슬롯 +1 (최대 PresetSlotsMax).</summary>
        public string TryExpand()
        {
            if (!CanExpand) return "최대 슬롯입니다";
            if (State.Tempo < _tuning.PresetSlotTempoPrice) return $"템포 부족 (필요 {_tuning.PresetSlotTempoPrice})";
            State.Tempo -= _tuning.PresetSlotTempoPrice;
            State.PresetSlots++;
            State.Presets.Add(new EquipmentPresetState { Name = $"세팅 {State.Presets.Count + 1}" });
            return null;
        }

        /// <summary>지금 장착 상태를 슬롯에 저장.</summary>
        public void SaveCurrent(int index)
        {
            if (index < 0 || index >= State.Presets.Count) return;
            var preset = State.Presets[index];
            preset.Items = _profile.Equipped.Where(p => p.Value != null)
                .Select(p => new PresetSlotItem { Slot = (int)p.Key, ItemId = p.Value.Id }).ToList();
            State.ActivePreset = index;
        }

        public void Rename(int index, string name)
        {
            if (index >= 0 && index < State.Presets.Count && !string.IsNullOrWhiteSpace(name)) State.Presets[index].Name = name.Trim();
        }

        public void SetPvp(int index) => State.PvpPreset = index >= 0 && index < State.Presets.Count ? index : -1;

        /// <summary>
        /// 슬롯 불러오기 (마을에서만). 지금 장비는 창고(없으면 가방)로, 세팅의 장비를 찾아 장착합니다.
        /// 반환: 찾지 못한 장비 수.
        /// </summary>
        public int Load(int index, System.Func<Item, EquipSlot, bool> equip)
        {
            if (index < 0 || index >= State.Presets.Count) return 0;
            var preset = State.Presets[index];
            if (preset.Items.Count == 0) return 0;
            int missing = 0;
            foreach (var entry in preset.Items)
            {
                var slot = (EquipSlot)entry.Slot;
                if (_profile.Equipped.TryGetValue(slot, out var current) && current != null && current.Id == entry.ItemId) continue;
                var item = Find(entry.ItemId);
                if (item == null)
                {
                    missing++;
                    continue;
                }
                if (!equip(item, slot)) missing++;
            }
            State.ActivePreset = index;
            return missing;
        }

        Item Find(string id)
        {
            return _profile.Bag.Items.FirstOrDefault(i => i.Id == id)
                   ?? _profile.Stash.Items.FirstOrDefault(i => i.Id == id)
                   ?? _profile.Equipped.Values.FirstOrDefault(i => i != null && i.Id == id);
        }
    }
}
