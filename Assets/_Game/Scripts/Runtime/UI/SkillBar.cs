using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Skills;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>하단 스킬 표시줄: 기본 공격 + 장비 5칸의 내장 스킬 (쿨타임·활성 상태).</summary>
    public sealed class SkillBar
    {
        const float SlotSize = 118f;
        const float Spacing = 132f;

        static readonly EquipSlot?[] Order = { null, EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.Head, EquipSlot.Body, EquipSlot.Boots };

        readonly GameSession _session;
        readonly Slot[] _slots;

        sealed class Slot
        {
            public UnityEngine.UI.Image Frame;
            public UnityEngine.UI.Text Name;
            public UnityEngine.UI.Text Caption;
            public UnityEngine.UI.Image Cooldown;
        }

        public SkillBar(Transform root, GameSession session)
        {
            _session = session;
            var bar = UiFactory.Anchored(root, "SkillBar", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(Spacing * Order.Length, SlotSize + 30f));
            _slots = new Slot[Order.Length];

            for (int i = 0; i < Order.Length; i++)
            {
                float x = (i - (Order.Length - 1) * 0.5f) * Spacing;
                var rect = UiFactory.Anchored(bar, "Slot" + i, new Vector2(0.5f, 0f), new Vector2(x, 26f), new Vector2(SlotSize, SlotSize));
                var slot = new Slot { Frame = UiFactory.Panel(rect, Color.white) };
                UiFactory.Panel(UiFactory.Stretch(rect, "Inner", 4f), new Color(0.04f, 0.05f, 0.08f, 0.95f));

                slot.Name = UiFactory.Label(rect, "Name", string.Empty, 20, TextAnchor.MiddleCenter, Color.white, 8f);
                slot.Cooldown = UiFactory.Fill(UiFactory.Stretch(rect, "Cooldown"), new Color(0f, 0f, 0f, 0.65f), UnityEngine.UI.Image.FillMethod.Radial360);
                slot.Cooldown.fillClockwise = false;
                slot.Cooldown.fillOrigin = 2;

                var caption = UiFactory.Anchored(rect, "Caption", new Vector2(0.5f, 0f), new Vector2(0f, -28f), new Vector2(Spacing, 26f));
                slot.Caption = UiFactory.Label(caption, Order[i].HasValue ? SlotRules.DisplayName(Order[i].Value) : "기본", 17, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));
                _slots[i] = slot;
            }
        }

        public void Refresh()
        {
            var player = _session.Player;
            var loadout = player.Combatant.Loadout;

            for (int i = 0; i < Order.Length; i++)
            {
                var skill = Order[i].HasValue ? Find(loadout, player.Equipment.Get(Order[i].Value)) : loadout.BasicAttack;
                var slot = _slots[i];
                if (skill == null)
                {
                    slot.Name.text = "<color=#4A5060>-</color>";
                    slot.Frame.color = new Color(0.25f, 0.28f, 0.35f, 0.6f);
                    slot.Cooldown.fillAmount = 0f;
                    continue;
                }

                string state = skill.Definition.Category == AutoCastCategory.Aura ? (skill.IsActive ? "\n<color=#7FD8FF>ON</color>" : "\n<color=#5A6070>OFF</color>")
                    : skill.Definition.Category == AutoCastCategory.Reactive ? "\n<color=#C8A0FF>반응</color>"
                    : skill.CooldownRemaining > 0.05f ? $"\n<color=#FFD84A>{skill.CooldownRemaining:0.0}</color>" : string.Empty;
                slot.Name.text = skill.Definition.Name + state;
                slot.Frame.color = Palette.For(skill.Rarity);
                slot.Cooldown.fillAmount = skill.CooldownFraction;
            }
        }

        static SkillInstance Find(SkillLoadout loadout, Item item)
        {
            if (item == null) return null;
            foreach (var skill in loadout.Skills)
                if (skill.Source == item) return skill;
            return null;
        }
    }
}
