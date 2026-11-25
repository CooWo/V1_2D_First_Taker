using System.Text;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>9슬롯 장비: 선택 후 강화(골드) 또는 해제(가방으로).</summary>
    public sealed class EquipmentMenu : IMenu
    {
        readonly MenuContext _ctx;
        EquipSlot? _selected;

        public EquipmentMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "EquipmentMenu");
            Panel.RowClicked += i => { _selected = SlotRules.All[i]; Refresh(); };
        }

        public string ButtonLabel => "장비";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var player = _ctx.Session.Player;
            var equipment = player.Equipment;
            Panel.SetTitle($"<b>장비 (9슬롯)</b>  <size=22><color=#FFD84A>{_ctx.Meta.Wallet.Gold:N0} G</color></size>");
            Panel.SetItems(SlotRules.All.Length, i =>
            {
                var slot = SlotRules.All[i];
                var item = equipment.Get(slot);
                string marker = _selected == slot ? "▶ " : string.Empty;
                return $"{marker}<color=#8090A8>{SlotRules.DisplayName(slot)}</color>  {ItemFormatter.ColoredName(item)} <size=17>{ItemFormatter.ShortGrant(item, _ctx.Meta.Content)}</size>";
            });

            var selectedItem = _selected.HasValue ? equipment.Get(_selected.Value) : null;
            Panel.SetDetail(selectedItem != null ? ItemFormatter.Describe(selectedItem, _ctx.Meta.Content) : StatsSummary());

            long cost = selectedItem != null ? _ctx.Meta.Enhancement.Cost(selectedItem) : 0;
            bool canEnhance = selectedItem != null && selectedItem.EnhanceLevel < Item.MaxEnhanceLevel;
            Panel.SetFooter(0, canEnhance ? $"강화 {cost:N0}G" : "강화", Enhance, selectedItem != null);
            Panel.SetFooter(1, "해제 → 가방", Unequip, selectedItem != null);
            Panel.SetFooter(2, "능력치 보기", () => { _selected = null; Refresh(); });
        }

        void Enhance()
        {
            if (!_selected.HasValue) return;
            var item = _ctx.Session.Player.Equipment.Get(_selected.Value);
            var result = _ctx.Meta.Enhance(_ctx.Session.Player, item);
            _ctx.Log(result.Message);
            Refresh();
        }

        void Unequip()
        {
            if (!_selected.HasValue) return;
            var player = _ctx.Session.Player;
            if (player.Inventory.IsFull)
            {
                _ctx.Log("가방이 가득 찼습니다.");
                return;
            }
            var item = player.Equipment.Unequip(_selected.Value);
            if (item != null) player.Inventory.TryAdd(item);
            _ctx.Meta.Capture(player);
            _ctx.Meta.RequestSave();
            Refresh();
        }

        string StatsSummary()
        {
            var player = _ctx.Session.Player;
            var stats = player.Combatant.Stats;
            var sb = new StringBuilder();
            sb.AppendLine($"<color=#FF7A6A>힘 {stats.Get(StatType.Strength):0}</color>  <color=#7AE07A>민첩 {stats.Get(StatType.Dexterity):0}</color>  <color=#7AB8FF>지능 {stats.Get(StatType.Intelligence):0}</color>   생명력 {player.Combatant.Health.MaxLife:0}");
            sb.AppendLine($"무기 공격력 {stats.Get(StatType.WeaponDamage) + stats.Get(StatType.FlatDamage):0}  치명타 {stats.Get(StatType.CritChancePct):0.#}% / +{stats.Get(StatType.CritDamagePct):0}%  회피 {stats.Get(StatType.EvasionPct):0.#}%  방어도 {stats.Get(StatType.Armor):0}");
            sb.AppendLine($"<size=18>속성 피해  불 {stats.Get(StatType.FireDamagePct):0}% · 얼음 {stats.Get(StatType.ColdDamagePct):0}% · 번개 {stats.Get(StatType.LightningDamagePct):0}% · 대지 {stats.Get(StatType.EarthDamagePct):0}% · 빛 {stats.Get(StatType.LightDamagePct):0}% · 어둠 {stats.Get(StatType.DarkDamagePct):0}%</size>");
            sb.AppendLine($"<size=18>속성 저항  불 {stats.Get(StatType.FireResistPct):0}% · 얼음 {stats.Get(StatType.ColdResistPct):0}% · 번개 {stats.Get(StatType.LightningResistPct):0}% · 대지 {stats.Get(StatType.EarthResistPct):0}% · 빛 {stats.Get(StatType.LightResistPct):0}% · 어둠 {stats.Get(StatType.DarkResistPct):0}% (+모든 {stats.Get(StatType.AllResistPct):0}%)</size>");
            sb.AppendLine($"<size=18>골드 +{stats.Get(StatType.GoldFindPct):0}% · 경험치 +{stats.Get(StatType.ExperiencePct):0}% · 펫 능력 +{stats.Get(StatType.PetPowerPct):0}%</size>");
            foreach (var bonus in player.Sets.ActiveBonuses) sb.AppendLine($"<size=18><color=#3FE06A>세트 {bonus.Pieces}: {bonus.Description}</color></size>");
            return sb.ToString();
        }
    }
}
