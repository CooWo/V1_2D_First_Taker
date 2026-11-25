using System.Linq;
using System.Text;
using ShadowRPG.Core.Raid;
using ShadowRPG.Runtime.UI.Menus;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Modes
{
    /// <summary>레이드 HUD: 남은 시간, 보스 체력(누적), 실시간 기여도 순위, 결과 창.</summary>
    public sealed class RaidHud : IHudModule
    {
        readonly RaidSession _session;
        readonly MenuContext _ctx;
        readonly UnityEngine.UI.Text _timer;
        readonly UnityEngine.UI.Text _ranking;
        readonly BossBar _bossBar;
        readonly ListPanel _result;

        public RaidHud(Transform root, RaidSession session, MenuContext ctx)
        {
            _session = session;
            _ctx = ctx;
            bool portrait = UiFactory.IsPortrait;
            _timer = UiFactory.Label(UiFactory.Anchored(root, "RaidTimer", new Vector2(0.5f, 1f), new Vector2(0f, portrait ? -400f : -86f), new Vector2(600f, 90f)),
                string.Empty, 40, TextAnchor.MiddleCenter, Color.white);
            _ranking = UiFactory.Label(UiFactory.Anchored(root, "RaidRanking", new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(470f, 260f)),
                string.Empty, 21, TextAnchor.UpperRight, Color.white);
            _bossBar = new BossBar(root, portrait ? -490f : -24f);

            _result = new ListPanel(root, "RaidResult", 0);
            _result.SetFooter(0, "마을로 돌아가기", () => _ctx.Modes.EnterTown());
            _result.SetFooter(1, string.Empty, null, false);
            _result.SetFooter(2, string.Empty, null, false);
            session.Finished += ShowResult;
        }

        public void Refresh(float dt)
        {
            float t = Mathf.Max(0f, _session.TimeRemaining);
            string enrage = _session.Enraged ? " <color=#FF4A3A>격노</color>" : string.Empty;
            _timer.text = $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}{enrage}\n<size=22><color=#FF8A5A>{_session.LastPattern}</color></size>";
            _bossBar.Show(_session.Boss, $"  ({_session.Boss.Health.Life:N0})");

            var sb = new StringBuilder("<b>기여도</b>\n");
            int rank = 1;
            foreach (var member in _session.Members.OrderByDescending(m => m.Damage))
            {
                string me = member.IsLocalPlayer ? "<color=#7FD8FF>" : "<color=#C8C8D0>";
                string dead = member.Character.Combatant.IsAlive ? string.Empty : " <color=#FF5A5A>✕</color>";
                sb.AppendLine($"{rank++}. {me}{member.Name}</color> {member.Damage:N0}{dead}");
            }
            sb.AppendLine($"<color=#FFD84A>{_ctx.Meta.Wallet.Gold:N0} G</color>");
            _ranking.text = sb.ToString();
        }

        void ShowResult(RaidResult result)
        {
            string outcome = result.Outcome == RaidOutcome.Victory ? "<color=#3FE06A>보스 처치!</color>"
                : result.Outcome == RaidOutcome.Wipe ? "<color=#FF5A5A>파티 전멸</color>" : "<color=#FFD84A>시간 종료</color>";
            var sb = new StringBuilder();
            sb.AppendLine($"<size=30>{outcome}</size>");
            sb.AppendLine($"보스 체력 {result.BossLifeBefore:N0} → {result.BossLifeAfter:N0} / {result.BossMaxLife:N0}");
            sb.AppendLine(result.Outcome == RaidOutcome.Victory ? "다음 입장부터 새 보스가 등장합니다." : "남은 체력은 다음 입장까지 유지됩니다.");
            sb.AppendLine();
            int rank = 1;
            foreach (var member in result.Ranking) sb.AppendLine($"{rank++}. {member.Name}  {member.Damage:N0}");
            sb.AppendLine();
            sb.AppendLine($"<color=#FFD84A>보상 +{result.GoldReward:N0} G</color>" + (result.LastHitByPlayer ? "  <color=#FF8A2A>막타 보너스!</color>" : string.Empty));
            foreach (var item in result.Rewards) sb.AppendLine("획득 " + ItemFormatter.ColoredName(item));
            _result.SetTitle("<b>레이드 결과</b>");
            _result.SetItems(0, _ => string.Empty);
            _result.SetDetail(sb.ToString());
            _result.SetVisible(true);
        }
    }
}
