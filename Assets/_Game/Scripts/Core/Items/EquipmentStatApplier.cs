using ShadowRPG.Core.Actors;

namespace ShadowRPG.Core.Items
{
    /// <summary>장비 변경을 관찰해 능력치·태그 배율에 반영합니다 (Equipment는 능력치를 모릅니다).</summary>
    public sealed class EquipmentStatApplier
    {
        readonly Combatant _owner;

        public EquipmentStatApplier(Equipment equipment, Combatant owner)
        {
            _owner = owner;
            equipment.Changed += OnChanged;
        }

        void OnChanged(EquipSlot slot, Item previous, Item next)
        {
            if (previous != null)
            {
                _owner.Stats.RemoveSource(previous);
                _owner.TagMultipliers.RemoveSource(previous);
            }

            if (next == null) return;
            _owner.Stats.AddModifiers(next.AllModifiers, next);
            foreach (var multiplier in next.TagMultipliers) _owner.TagMultipliers.Add(multiplier.WithSource(next));
        }
    }
}
