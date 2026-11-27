using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>스탯: 레벨업 포인트를 힘/민첩/지능에 분배, 저렴한 재분배, 상세 능력치·전투력.</summary>
    public sealed class StatsMenu : IMenu
    {
        static readonly (CoreStat stat, string name, string effect)[] Rows =
        {
            (CoreStat.Strength, "힘", "생명력·방어력·근접 피해"),
            (CoreStat.Dexterity, "민첩", "공격 속도·치명타·회피"),
            (CoreStat.Intelligence, "지능", "자원·주문 피해·보호막")
        };

        readonly MenuContext _ctx;
        bool _details;

        public StatsMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "StatsMenu", 3);
            Panel.RowClicked += OnRow;
        }

        public string ButtonLabel => "스탯";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var meta = _ctx.Meta;
            var player = _ctx.Session.Player;
            var stats = player.Combatant.Stats;
            int unspent = meta.StatPoints.Unspent;
            Panel.SetTitle($"<b>스탯</b>  남은 포인트 <color=#FFD84A>{unspent}</color>  <size=20>전투력 {meta.TotalPower(player):N0}</size>");
            Panel.SetItems(Rows.Length, i =>
            {
                var r = Rows[i];
                int allocated = r.stat == CoreStat.Strength ? meta.State.AllocatedStrength : r.stat == CoreStat.Dexterity ? meta.State.AllocatedDexterity : meta.State.AllocatedIntelligence;
                var type = r.stat == CoreStat.Strength ? StatType.Strength : r.stat == CoreStat.Dexterity ? StatType.Dexterity : StatType.Intelligence;
                return $"<b>{r.name}</b> {stats.Get(type):0} <size=17><color=#8090A8>(분배 {allocated}) · {r.effect}</color></size>{(unspent > 0 ? "   <color=#3FE06A>[+1]</color>" : string.Empty)}";
            });
            Panel.SetDetail(_details ? Detail(stats) :
                "<size=19>레벨이 오를 때마다 포인트 1을 받아 직접 분배합니다.\n재분배는 가벼운 골드로 언제든 가능 — 빌드를 자유롭게 실험하세요.</size>");
            Panel.SetFooter(0, $"재분배 {meta.StatPoints.RespecCost(player.Progress.Level):N0}G", () =>
            {
                if (!meta.RespecStats(player)) _ctx.Log("재분배할 포인트가 없거나 골드가 부족합니다.");
            });
            Panel.SetFooter(1, _details ? "간단히" : "상세 능력치", () => { _details = !_details; Refresh(); });
            Panel.SetFooter(2, "닫기", () => Panel.SetVisible(false));
        }

        static string Detail(IStatReader s) =>
            $"<size=18>생명 {s.Get(StatType.MaxLife):0} · 방어 {s.Get(StatType.Armor):0} · 회피 {s.Get(StatType.EvasionPct):0}% · 막기 {s.Get(StatType.BlockChancePct):0}%\n" +
            $"무기 피해 {s.Get(StatType.WeaponDamage):0} · 공격 속도 {s.Get(StatType.AttacksPerSecond):0.00} (+{s.Get(StatType.AttackSpeedPct):0}%) · 치명타 {s.Get(StatType.CritChancePct):0}% ×{1.5f + s.Get(StatType.CritDamagePct) / 100f:0.00}\n" +
            $"모든 피해 +{s.Get(StatType.AllDamagePct):0}% · 쿨감 {s.Get(StatType.CooldownReductionPct):0}% · 범위 +{s.Get(StatType.AreaPct):0}%\n" +
            $"저항 — 모든 {s.Get(StatType.AllResistPct):0}% · 불 {s.Get(StatType.FireResistPct):0} · 얼음 {s.Get(StatType.ColdResistPct):0} · 번개 {s.Get(StatType.LightningResistPct):0} · 대지 {s.Get(StatType.EarthResistPct):0} · 빛 {s.Get(StatType.LightResistPct):0} · 어둠 {s.Get(StatType.DarkResistPct):0}\n" +
            $"이동 속도 +{s.Get(StatType.MoveSpeedPct):0}% · 골드 +{s.Get(StatType.GoldFindPct):0}% · 경험치 +{s.Get(StatType.ExperiencePct):0}%</size>";

        void OnRow(int index)
        {
            if (index < 0 || index >= Rows.Length) return;
            if (!_ctx.Meta.AllocateStat(_ctx.Session.Player, Rows[index].stat)) _ctx.Log("남은 스탯 포인트가 없습니다.");
            Refresh();
        }
    }
}
