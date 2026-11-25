using System.Linq;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
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
    /// 던전 HUD: 층 정보·남은 몹 + 층 클리어 결과창
    /// (가방 칸 미리보기, 가져갈 장비 선택, 계속 진행 / 마을 귀환).
    /// </summary>
    public sealed class DungeonHud : IHudModule
    {
        readonly DungeonSession _session;
        readonly MenuContext _ctx;
        readonly UnityEngine.UI.Text _info;
        readonly UnityEngine.UI.Text _banner;
        readonly ListPanel _result;
        readonly GridView _bagPreview;
        readonly UnityEngine.UI.Text _summary;
        readonly UnityEngine.UI.Button _sellButton;
        readonly BossBar _bossBar;
        Item _focused;
        Item _bagSelected;

        public DungeonHud(Transform root, DungeonSession session, MenuContext ctx)
        {
            _session = session;
            _ctx = ctx;
            _info = UiFactory.Label(UiFactory.Anchored(root, "DungeonInfo", new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(470f, 200f)),
                string.Empty, 22, TextAnchor.UpperRight, Color.white);
            _banner = UiFactory.Label(UiFactory.Anchored(root, "ClearBanner", new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(900f, 120f)),
                string.Empty, 56, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
            _bossBar = new BossBar(root);

            _result = new ListPanel(root, "FloorResult", 5);
            _result.RowClicked += OnLootRow;

            var detail = _result.DetailArea;
            float cell = 38f;
            _bagPreview = new GridView(detail, "BagPreview", new Vector2(1f, 1f), new Vector2(-4f, -4f),
                session.Bag.Width, session.Bag.Height, cell);
            _bagPreview.ItemClicked += item => { _bagSelected = item; _focused = null; };
            _summary = UiFactory.Label(UiFactory.Anchored(detail, "Summary", new Vector2(0f, 1f), Vector2.zero,
                new Vector2(detail.sizeDelta.x - session.Bag.Width * cell - 16f, detail.sizeDelta.y)), string.Empty, 19, TextAnchor.UpperLeft, Color.white);
            _sellButton = UiFactory.Button(detail, "SellBagItem", new Vector2(1f, 1f), new Vector2(-4f, -(session.Bag.Height * cell + 10f)),
                new Vector2(session.Bag.Width * cell, 46f), "가방에서 선택한 장비 판매", SellSelected);

            session.FloorResultReady += _ => { _focused = null; _bagSelected = null; _result.SetVisible(true); };
            session.FloorStarted += _ => _result.SetVisible(false);
        }

        public void Refresh(float dt)
        {
            var plan = _session.Plan;
            var spawner = _session.Spawner;
            var bag = _session.Bag;
            _info.text =
                $"<color=#9FD3FF>ACT {plan.ActIndex + 1}</color> {_session.Act.Name}\n" +
                $"{_session.DungeonName} <b>{plan.Floor}/{_ctx.Meta.Content.Acts.FloorsPerDungeon}층</b> <size=18>[{_session.Layout.Name}]</size>{(plan.HasBoss ? " <color=#FF6A4A>보스</color>" : string.Empty)}\n" +
                $"남은 몹 <b><size=30>{spawner.Remaining}</size></b> / {plan.TotalKills}\n" +
                $"<size=19>이번 층 전리품 {_session.Loot.Found.Count} · 가방 {bag.UsedCells}/{bag.Capacity}칸</size>\n" +
                $"<color=#FFD84A>{_ctx.Meta.Wallet.Gold:N0} G</color>";

            _banner.text = _session.State == FloorState.Clearing ? "층 정복!" : string.Empty;
            _bossBar.Show(spawner.Boss != null ? spawner.Boss.Combatant : null);

            if (_session.State == FloorState.Result) RefreshResult();
            else if (_result.Visible) _result.SetVisible(false);
        }

        void RefreshResult()
        {
            if (!_result.Visible) _result.SetVisible(true);
            var loot = _session.Loot;
            var bag = _session.Bag;
            var summary = _session.Summary;

            _result.SetTitle($"<b>{_session.DungeonName} {summary.Plan.Floor}층 정복</b>");
            _result.SetItems(loot.Found.Count, i =>
            {
                var item = loot.Found[i];
                var size = ItemShapes.SizeOf(item);
                bool selected = loot.IsSelected(item);
                string state = selected ? "<color=#3FE06A>[가방]</color>"
                    : _session.CanTake(item) ? "<color=#8090A8>[판매]</color>" : "<color=#FF7A6A>[칸 부족]</color>";
                long price = _ctx.Meta.Pricing.SellValue(item, _session.Player.Combatant.Stats);
                string marker = item == _focused ? "▶ " : string.Empty;
                return $"{marker}{state} {ItemFormatter.ColoredName(item)} <size=17><color=#8090A8>{size.x}x{size.y}칸 · {price:N0}G</color> {ItemFormatter.ShortGrant(item, _ctx.Meta.Content)}</size>";
            });

            var preview = bag.PreviewPlacements(loot.Selected);
            _bagPreview.Refresh(bag, _bagSelected, preview);

            string focusText = _focused != null ? ItemFormatter.Describe(_focused, _ctx.Meta.Content)
                : _bagSelected != null ? "<size=18>가방: </size>" + ItemFormatter.Describe(_bagSelected, _ctx.Meta.Content)
                : string.Empty;
            _summary.text =
                $"<size=19>처치 {summary.Kills} · {summary.Seconds:0}초 · 경험치 +{summary.Experience:N0} · 획득 {summary.ItemsFound}</size>\n" +
                $"<size=18>가방 {bag.UsedCells + preview.Sum(p => p.Value.width * p.Value.height)}/{bag.Capacity}칸 · 판매 예정 +{_session.PreviewSaleGold:N0}G</size>\n" +
                "<size=18><color=#FF8A6A>계속 진행하다 쓰러지면 가방 아이템 절반을 잃습니다.</color></size>\n" + focusText;
            _sellButton.gameObject.SetActive(_bagSelected != null);

            _result.SetFooter(0, "다음 층 ▶ 계속", _session.ContinueToNextFloor);
            _result.SetFooter(1, "마을로 귀환", _session.ReturnToTown);
            _result.SetFooter(2, "추천 선택", _session.AutoSelectLoot);
        }

        void OnLootRow(int index)
        {
            var found = _session.Loot.Found;
            if (index >= found.Count) return;
            var item = found[index];
            _focused = item;
            _bagSelected = null;
            if (!_session.ToggleLoot(item) && !_session.Loot.IsSelected(item))
                _ctx.Log("가방 칸이 부족합니다 — 가방에서 장비를 팔거나 다른 장비를 빼세요.");
        }

        void SellSelected()
        {
            if (_bagSelected == null) return;
            long gold = _session.SellFromBag(_bagSelected);
            _ctx.Log($"판매: {_bagSelected.DisplayName} +{gold:N0}G");
            _bagSelected = null;
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
