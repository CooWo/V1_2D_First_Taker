using System.Linq;
using System.Text;
using ShadowRPG.Core.Raid;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>레이드 입장 창: 오늘 남은 입장 횟수, 보스 정보, 누적 체력.</summary>
    public sealed class RaidLobbyMenu : IMenu
    {
        readonly MenuContext _ctx;

        public RaidLobbyMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "RaidLobby", 0);
        }

        public string ButtonLabel => "레이드";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var meta = _ctx.Meta;
            var state = meta.Profile.Raid;
            var boss = RaidBossCatalog.ForLevel(RaidBossCatalog.CreateDefault(meta.Content.Enemies.First()), state.BossLevel);
            int remaining = meta.RaidEntries.Remaining;

            Panel.SetTitle("<b>4인 협동 레이드</b>");
            Panel.SetItems(0, _ => string.Empty);
            var sb = new StringBuilder();
            sb.AppendLine($"<size=26><color=#FF8A5A>{boss.Name}</color> Lv.{state.BossLevel}</size>");
            sb.AppendLine(state.BossLife > 0f ? $"보스 체력이 이전 공격에서 깎인 채로 남아 있습니다 (남은 {state.BossLife:N0})" : "새 보스 — 체력 가득");
            sb.AppendLine();
            sb.AppendLine($"오늘 남은 입장: <b>{remaining}/{meta.RaidEntries.MaxPerDay}</b>  (자정 초기화)");
            sb.AppendLine($"제한 시간 {RaidSession.AttemptSeconds:0}초 · 보스 체력은 처치할 때까지 누적");
            sb.AppendLine("보상: 기여 피해 비례 골드 · 처치 시 전설/세트 2개 · 막타 보너스");
            sb.AppendLine();
            sb.AppendLine("<size=19>패턴: 바닥에 예고 범위가 먼저 표시됩니다. 자동 전투도 예고 범위를 피하며,");
            sb.AppendLine("'화염 고리'는 보스 품 안쪽이 안전 지대입니다. 체력 30% 이하에서 격노.</size>");
            sb.AppendLine($"<size=18><color=#8090A8>누적 피해 {state.LifetimeDamage:N0} · 처치 {state.BossKills}회 · 동료 3명은 현재 AI (온라인 매칭은 다음 단계)</color></size>");
            Panel.SetDetail(sb.ToString());

            Panel.SetFooter(0, remaining > 0 ? $"입장 ({remaining}회 남음)" : "오늘 입장 완료", Enter, true);
            Panel.SetFooter(1, string.Empty, null, false);
            Panel.SetFooter(2, "닫기", () => Panel.SetVisible(false));
        }

        void Enter()
        {
            if (!_ctx.Modes.TryEnterRaid(out var reason)) _ctx.Log(reason);
        }
    }
}
