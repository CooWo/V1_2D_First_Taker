using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
using ShadowRPG.Runtime.Presentation;
using ShadowRPG.Runtime.UI.Menus;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Modes
{
    /// <summary>HUD에 붙는 모드별 영역.</summary>
    public interface IHudModule
    {
        void Refresh(float dt);
    }

    /// <summary>
    /// 던전 모드 HUD: ACT/던전/층, 남은 몹 수, 이번 층 전리품, 보스 바,
    /// 층 클리어 시 "가져갈 템 선택" 창 (나머지는 자동 판매).
    /// </summary>
    public sealed class DungeonHud : IHudModule
    {
        readonly DungeonSession _session;
        readonly MenuContext _ctx;
        readonly UnityEngine.UI.Text _info;
        readonly ListPanel _lootPanel;
        readonly BossBar _bossBar;
        Item _focused;

        public DungeonHud(Transform root, DungeonSession session, MenuContext ctx)
        {
            _session = session;
            _ctx = ctx;
            _info = UiFactory.Label(UiFactory.Anchored(root, "DungeonInfo", new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(470f, 200f)),
                string.Empty, 22, TextAnchor.UpperRight, Color.white);
            _bossBar = new BossBar(root);

            _lootPanel = new ListPanel(root, "LootChoice");
            _lootPanel.RowClicked += OnLootRow;
            session.FloorCleared += _ => { _focused = null; _lootPanel.SetVisible(true); };
            session.FloorStarted += _ => _lootPanel.SetVisible(false);
        }

        public void Refresh(float dt)
        {
            var plan = _session.Plan;
            var spawner = _session.Spawner;
            string state = _session.State == FloorState.Portal ? "\n<color=#9FD3FF>포탈로 이동 중…</color>" : string.Empty;
            _info.text =
                $"<color=#9FD3FF>ACT {plan.ActIndex + 1}</color> {_session.Act.Name}\n" +
                $"{_session.DungeonName} <b>{plan.Floor}/{_ctx.Meta.Content.Acts.FloorsPerDungeon}층</b>{(plan.HasBoss ? " <color=#FF6A4A>보스</color>" : string.Empty)}\n" +
                $"남은 몹 <b><size=30>{spawner.Remaining}</size></b> / {plan.TotalKills}\n" +
                $"<size=19>이번 층 전리품 {_session.Loot.Found.Count} (반출 {_session.KeepLimit}개) · 가방 {_session.Player.Inventory.Items.Count}/{_session.Player.Inventory.Capacity}</size>\n" +
                $"<color=#FFD84A>{_ctx.Meta.Wallet.Gold:N0} G</color>{state}";

            _bossBar.Show(spawner.Boss != null ? spawner.Boss.Combatant : null);

            if (_session.State == FloorState.LootChoice) RefreshLoot();
            else if (_lootPanel.Visible) _lootPanel.SetVisible(false);
        }

        void RefreshLoot()
        {
            if (!_lootPanel.Visible) _lootPanel.SetVisible(true);
            var loot = _session.Loot;
            int allowed = loot.AllowedPicks(_session.Player.Inventory.FreeSlots);
            string timer = _session.LootTimer > 0f ? $" <size=20>자동 확정 {Mathf.CeilToInt(_session.LootTimer)}초</size>" : string.Empty;
            _lootPanel.SetTitle($"<b>층 클리어!</b> 반출 {loot.Selected.Count}/{allowed}{timer}");
            _lootPanel.SetItems(loot.Found.Count, i =>
            {
                var item = loot.Found[i];
                string check = loot.IsSelected(item) ? "<color=#3FE06A>[가져감]</color>" : "<color=#8090A8>[판매]</color>";
                long price = _ctx.Meta.Pricing.SellValue(item, _session.Player.Combatant.Stats);
                return $"{check} {ItemFormatter.ColoredName(item)} <size=17><color=#8090A8>{SlotRules.DisplayName(item.Slot)} · {price:N0}G</color> {ItemFormatter.ShortGrant(item, _ctx.Meta.Content)}</size>";
            });
            _lootPanel.SetDetail(_focused != null
                ? ItemFormatter.Describe(_focused, _ctx.Meta.Content)
                : $"<size=19>한 층에서 가져갈 수 있는 장비는 {loot.KeepLimit}개, 가방 여유 {_session.Player.Inventory.FreeSlots}칸.\n선택하지 않은 장비는 자동 판매되어 골드(강화·트리·펫 재료)가 됩니다.</size>");
            _lootPanel.SetFooter(0, $"확정 (판매 +{_session.PreviewSaleGold:N0}G)", _session.ConfirmLoot);
            _lootPanel.SetFooter(1, "추천 자동 선택", () => loot.AutoSelect(_ctx.Meta.Scorer, _session.Player.Inventory.FreeSlots));
            _lootPanel.SetFooter(2, "가방 열기", null, false);
        }

        void OnLootRow(int index)
        {
            var found = _session.Loot.Found;
            if (index >= found.Count) return;
            var item = found[index];
            _focused = item;
            if (!_session.ToggleLoot(item) && !_session.Loot.IsSelected(item))
                _ctx.Log("반출 한도 또는 가방 공간이 부족합니다.");
        }
    }

    /// <summary>화면 상단 보스 체력 바.</summary>
    public sealed class BossBar
    {
        readonly RectTransform _root;
        readonly UnityEngine.UI.Image _fill;
        readonly UnityEngine.UI.Text _name;

        /// <summary>세로 화면은 상태창 아래, 가로 화면은 상단 중앙 (좌우 패널 사이).</summary>
        public static float DefaultY => UiFactory.IsPortrait ? -500f : -24f;

        public BossBar(Transform parent) : this(parent, DefaultY) { }

        public BossBar(Transform parent, float y)
        {
            _root = UiFactory.Anchored(parent, "BossBar", new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(680f, 56f));
            UiFactory.Panel(_root, new Color(0f, 0f, 0f, 0.7f));
            UiFactory.Border(_root, new Color(1f, 0.45f, 0.2f));
            _fill = UiFactory.Fill(UiFactory.Stretch(_root, "Fill", 4f), new Color(0.85f, 0.2f, 0.15f));
            _name = UiFactory.Label(_root, "Name", string.Empty, 24, TextAnchor.MiddleCenter, Color.white);
            _root.gameObject.SetActive(false);
        }

        public void Show(Core.Actors.Combatant boss, string suffix = "")
        {
            bool alive = boss != null && boss.IsAlive;
            _root.gameObject.SetActive(alive);
            if (!alive) return;
            _fill.fillAmount = boss.Health.LifeFraction;
            _name.text = $"{boss.Name}  {boss.Health.LifeFraction * 100f:0.0}%{suffix}";
        }
    }
}
