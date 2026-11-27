using System.Linq;
using System.Text;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Growth;
using ShadowRPG.Runtime.Services;
using ShadowRPG.Runtime.UI.Menus;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Modes
{
    /// <summary>
    /// 사망창 (Menu_Structure §3-5): 잃은 아이템(절반) + 사망 원인 요약
    /// (피해 유형별 비율 막대, 몬스터별 피해, 회피·막기, 마지막 일격, 취약 방어 힌트)
    /// → 광고 보고 전부 회수 (횟수 제한 없음) / 그대로 귀환.
    /// </summary>
    public sealed class DeathPanel
    {
        static readonly DamageType[] Types = { DamageType.Physical, DamageType.Fire, DamageType.Cold, DamageType.Lightning, DamageType.Earth, DamageType.Light, DamageType.Shadow };

        readonly DungeonSession _session;
        readonly MenuContext _ctx;
        readonly ListPanel _panel;
        bool _adPending;

        public DeathPanel(Transform root, DungeonSession session, MenuContext ctx)
        {
            _session = session;
            _ctx = ctx;
            _panel = new ListPanel(root, "DeathPanel", 0);
            session.PlayerDied += _ => _panel.SetVisible(true);
            session.FloorStarted += _ => _panel.SetVisible(false);
        }

        public void Refresh()
        {
            if (_session.State != FloorState.Dead)
            {
                if (_panel.Visible) _panel.SetVisible(false);
                return;
            }
            if (!_panel.Visible) _panel.SetVisible(true);
            var report = _session.DeathReport;
            var lost = _session.LostItems;
            _panel.SetTitle($"<color=#FF5A5A><b>쓰러졌습니다</b></color>  <size=22>{_session.DungeonName} {_session.Plan.Floor}층</size>");
            _panel.SetItems(0, _ => string.Empty);

            var sb = new StringBuilder();
            sb.Append($"<size=21><b>잃은 아이템 {lost.Count}개</b></size>  <size=18>");
            sb.Append(lost.Count == 0 ? "<color=#8090A8>없음</color>" : string.Join(", ", lost.Take(6).Select(ItemFormatter.ColoredName)) + (lost.Count > 6 ? " …" : string.Empty));
            sb.Append("</size>\n\n");

            if (report != null && report.Total > 0f)
            {
                sb.Append("<size=21><b>받은 피해 유형</b></size>\n<size=18>");
                foreach (var type in Types)
                {
                    float share = report.ShareOf(type);
                    if (share <= 0.005f) continue;
                    int bars = Mathf.Max(1, Mathf.RoundToInt(share * 20f));
                    var color = ColorUtility.ToHtmlStringRGB(Presentation.Palette.For(type));
                    sb.Append($"{DamageTypes.Korean(type),-3} <color=#{color}>{new string('█', bars)}</color> {share:P0}\n");
                }
                sb.Append("</size>\n<size=21><b>몬스터별 피해</b></size>\n<size=18>");
                foreach (var line in report.TopSources)
                    sb.Append($"{line.Name}  {line.Damage:N0} ({line.Share:P0}) · 주로 {DamageTypes.Korean(line.MainType)}\n");
                sb.Append($"</size><size=18><color=#8090A8>피격 {report.HitsTaken} · 회피 {report.Evaded} · 막기 {report.Blocked} · 마지막 일격: {report.LastHitBy} {report.LastHitAmount:N0}</color></size>\n\n");
                sb.Append("<size=21><b>보강 힌트</b></size>\n<size=19><color=#FFB070>");
                foreach (var hint in report.Hints) sb.Append("· ").Append(hint).Append('\n');
                sb.Append("</color></size>");
            }
            _panel.SetDetail(sb.ToString());

            _panel.SetFooter(0, lost.Count > 0 ? "광고 보고 전부 회수" : "마을로", () =>
            {
                if (lost.Count == 0)
                {
                    _session.ReturnAfterDeath();
                    return;
                }
                if (_adPending) return;
                _adPending = true;
                RewardedAds.Current.Show("death_recover", ok =>
                {
                    _adPending = false;
                    if (ok) _session.RecoverWithAd();
                    else _ctx.Log("광고를 끝까지 보지 않아 회수하지 못했습니다.");
                });
            });
            _panel.SetFooter(1, string.Empty, null, false);
            _panel.SetFooter(2, "그대로 귀환", _session.ReturnAfterDeath);
        }
    }
}
