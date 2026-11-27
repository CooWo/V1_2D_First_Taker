using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Status;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>좌상단: 이름·직업·레벨, 생명력/보호막/방벽, 자원, 경험치, 상태.</summary>
    public sealed class StatusPanel
    {
        readonly ICombatSession _session;
        readonly UnityEngine.UI.Text _title;
        readonly UnityEngine.UI.Image _life;
        readonly UnityEngine.UI.Image _shield;
        readonly UnityEngine.UI.Image _barrier;
        readonly UnityEngine.UI.Text _lifeText;
        readonly UnityEngine.UI.Image _resource;
        readonly UnityEngine.UI.Text _resourceText;
        readonly UnityEngine.UI.Image _xp;
        readonly UnityEngine.UI.Text _statusLine;

        public StatusPanel(Transform root, ICombatSession session)
        {
            _session = session;
            var panel = UiFactory.Anchored(root, "StatusPanel", new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(560f, 190f));
            UiFactory.Frame(panel, false, false);

            _title = UiFactory.Label(UiFactory.Anchored(panel, "Title", new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(530f, 36f)),
                string.Empty, 26, TextAnchor.MiddleLeft, Color.white);

            var lifeRect = Bar(panel, "Life", -50f, 34f, out _life, Palette.Life);
            _shield = UiFactory.Fill(UiFactory.Anchored(lifeRect, "Shield", new Vector2(0f, 1f), Vector2.zero, new Vector2(530f, 10f)), Palette.EnergyShield);
            _barrier = UiFactory.Fill(UiFactory.Anchored(lifeRect, "Barrier", new Vector2(0f, 0f), Vector2.zero, new Vector2(530f, 8f)), Palette.Barrier);
            _lifeText = UiFactory.Label(lifeRect, "Value", string.Empty, 20, TextAnchor.MiddleCenter, Color.white, 0f);

            var resourceRect = Bar(panel, "Resource", -92f, 24f, out _resource, Palette.Resource);
            _resourceText = UiFactory.Label(resourceRect, "Value", string.Empty, 18, TextAnchor.MiddleCenter, Color.white, 0f);

            Bar(panel, "Xp", -124f, 8f, out _xp, Palette.Experience);

            _statusLine = UiFactory.Label(UiFactory.Anchored(panel, "Status", new Vector2(0f, 1f), new Vector2(14f, -138f), new Vector2(530f, 44f)),
                string.Empty, 19, TextAnchor.MiddleLeft, new Color(0.75f, 0.85f, 1f));
        }

        static RectTransform Bar(RectTransform panel, string name, float y, float height, out UnityEngine.UI.Image fill, Color color)
        {
            var rect = UiFactory.Anchored(panel, name, new Vector2(0f, 1f), new Vector2(14f, y), new Vector2(530f, height));
            UiFactory.Panel(rect, new Color(0f, 0f, 0f, 0.6f));
            fill = UiFactory.Fill(UiFactory.Stretch(rect, "Fill"), color);
            return rect;
        }

        public void Refresh()
        {
            var player = _session.Player;
            var combatant = player.Combatant;
            var health = combatant.Health;

            _title.text = $"그림자 <color=#9FD3FF>{player.Class.Current?.Name}</color>  Lv.{player.Progress.Level}";
            _life.fillAmount = health.LifeFraction;
            _shield.fillAmount = health.MaxEnergyShield > 0f ? health.EnergyShield / health.MaxEnergyShield : 0f;
            _barrier.fillAmount = Mathf.Clamp01(health.Barrier / health.MaxLife);
            _lifeText.text = $"{Mathf.CeilToInt(health.Life)} / {Mathf.CeilToInt(health.MaxLife)}"
                             + (health.EnergyShield > 0.5f ? $"  <color=#7FD8FF>+{Mathf.CeilToInt(health.EnergyShield)}</color>" : string.Empty)
                             + (health.Barrier > 0.5f ? $"  <color=#FFFFFF>[{Mathf.CeilToInt(health.Barrier)}]</color>" : string.Empty);

            var resource = combatant.Resource;
            _resource.fillAmount = resource.Fraction;
            _resourceText.text = $"{Mathf.FloorToInt(resource.Current)} / {Mathf.FloorToInt(resource.Max)}"
                                 + (resource.ReservedFraction > 0f ? $"  (점유 {resource.ReservedFraction * 100f:0}%)" : string.Empty);

            _xp.fillAmount = player.Progress.Experience / player.Progress.ExperienceToNext;
            _statusLine.text = StatusLine(combatant);
        }

        static string StatusLine(Combatant combatant)
        {
            var parts = new System.Collections.Generic.List<string>();
            var fortify = combatant.Statuses.Get<ShadowFortifyStatus>();
            if (fortify != null) parts.Add($"<color=#6FA8FF>그림자 갑주 {fortify.Remaining:0.0}s</color>");
            foreach (var effect in combatant.Statuses.Effects)
            {
                if (effect is ShadowFortifyStatus) continue;
                parts.Add($"{StatusName(effect.Id)} {effect.Remaining:0.0}s");
            }
            return parts.Count == 0 ? "<color=#5A6070>상태 효과 없음</color>" : string.Join("  ", parts);
        }

        static string StatusName(string id)
        {
            switch (id)
            {
                case StatusIds.Invulnerable: return "무적";
                case "smoke_dash": return "연막";
                case "warcry_empower": return "불멸";
                case StatusIds.Slow: return "둔화";
                case StatusIds.Freeze: return "빙결";
                case StatusIds.Shock: return "감전";
                default: return id;
            }
        }
    }
}
