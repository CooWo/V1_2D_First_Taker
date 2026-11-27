using ShadowRPG.Core.Items;

namespace ShadowRPG.Core.Rhythm
{
    /// <summary>주무기(오른손)가 리듬 무기면 그 박자를 씁니다.</summary>
    public sealed class EquipmentRhythmSource : IRhythmSource
    {
        readonly Equipment _equipment;

        public EquipmentRhythmSource(Equipment equipment) => _equipment = equipment;

        public float BeatSeconds
        {
            get
            {
                var weapon = _equipment.Get(EquipSlot.RightHand);
                return weapon != null && weapon.IsRhythm ? weapon.BeatSeconds : 0f;
            }
        }
    }
}
