using System.Text;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Stats;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>9슬롯 장비, 세트 효과, 주요 능력치 요약 창.</summary>
    public sealed class EquipmentPanel
    {
        readonly GameSession _session;
        readonly RectTransform _root;
        readonly UnityEngine.UI.Text _text;

        public EquipmentPanel(Transform parent, GameSession session)
        {
            _session = session;
            _root = UiFactory.Anchored(parent, "EquipmentPanel", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(760f, 860f));
            UiFactory.Panel(_root, new Color(0.04f, 0.05f, 0.08f, 0.94f), true);
            UiFactory.Border(_root, Palette.PanelBorder);
            _text = UiFactory.Label(_root, "Text", string.Empty, 21, TextAnchor.UpperLeft, Color.white, 18f);
            _root.gameObject.SetActive(false);
        }

        public bool Visible => _root.gameObject.activeSelf;

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        public void Refresh()
        {
            if (!Visible) return;
            var player = _session.Player;
            var content = _session.Content;
            var sb = new StringBuilder();

            sb.AppendLine("<size=28><b>장비 (9슬롯)</b></size>");
            foreach (var slot in SlotRules.All)
            {
                var item = player.Equipment.Get(slot);
                sb.AppendLine($"<color=#8090A8>{SlotRules.DisplayName(slot),-4}</color> {ItemFormatter.ColoredName(item)}");
                string grant = ItemFormatter.ShortGrant(item, content);
                if (grant.Length > 0) sb.AppendLine($"<size=18>      {grant}</size>");
            }

            sb.AppendLine();
            sb.AppendLine("<size=24><b>세트 효과</b></size>");
            bool anySet = false;
            foreach (var pair in player.Sets.PieceCounts)
            {
                var set = content.Sets.Get(pair.Key);
                if (set == null) continue;
                anySet = true;
                int effective = player.Sets.EffectiveCount(pair.Key);
                sb.AppendLine($"<color=#3FE06A>{set.Name}</color> {pair.Value}/4" + (effective != pair.Value ? $" (실효 {effective})" : string.Empty));
                foreach (var bonus in set.Bonuses)
                {
                    bool on = bonus.Pieces <= effective;
                    sb.AppendLine($"<size=18>  <color=#{(on ? "C8FFC8" : "5A6070")}>{bonus.Pieces}세트: {bonus.Description}</color></size>");
                }
            }
            if (!anySet) sb.AppendLine("<size=18><color=#5A6070>머리·몸·신발·목걸이 세트 장비를 모으면 활성화</color></size>");

            var stats = player.Combatant.Stats;
            sb.AppendLine();
            sb.AppendLine("<size=24><b>능력치</b></size>");
            sb.AppendLine($"<size=19><color=#FF7A6A>힘 {stats.Get(StatType.Strength):0}</color>  <color=#7AE07A>민첩 {stats.Get(StatType.Dexterity):0}</color>  <color=#7AB8FF>지능 {stats.Get(StatType.Intelligence):0}</color></size>");
            sb.AppendLine($"<size=19>무기 공격력 {stats.Get(StatType.WeaponDamage) + stats.Get(StatType.FlatDamage):0}  치명타 {stats.Get(StatType.CritChancePct):0.#}% / +{stats.Get(StatType.CritDamagePct):0}%  공속 +{stats.Get(StatType.AttackSpeedPct):0.#}%</size>");
            sb.AppendLine($"<size=19>방어도 {stats.Get(StatType.Armor):0}  막기 {stats.Get(StatType.BlockChancePct):0}%  회피 {stats.Get(StatType.EvasionPct):0}%  저항 {stats.Get(StatType.AllResistPct):0}%</size>");
            sb.AppendLine($"<size=19>쿨감 {stats.Get(StatType.CooldownReductionPct):0}%  범위 +{stats.Get(StatType.AreaPct):0}%  투사체 +{stats.Get(StatType.ExtraProjectiles):0}  연쇄 +{stats.Get(StatType.ExtraChains):0}</size>");
            _text.text = sb.ToString();
        }
    }
}
