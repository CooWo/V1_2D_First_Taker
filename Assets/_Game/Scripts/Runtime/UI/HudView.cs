using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.AutoBattle;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Town;
using ShadowRPG.Runtime.UI.Menus;
using ShadowRPG.Runtime.UI.Modes;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>
    /// HUD 뼈대 (세로 화면 기본, Menu_Structure_Draft §1·§3):
    /// · 마을: 좌상단 상태 · 우상단 재화 · 하단 메인 바 5개(캐릭터·가방·성장·던전·☰) → 묶음 창 → 메뉴
    /// · 던전: 좌상단 상태 · 우상단 층 정보 · 하단 스킬 · 오른쪽 조작(배속·자동 이동·가방·메뉴) · 리듬 원
    /// 각 영역 내용은 전용 프레젠터가 그리고, 이 클래스는 배치와 갱신 주기만 맡습니다.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const int MaxLogLines = 6;
        const float TextRefreshInterval = 0.1f;

        /// <summary>하단 메인 바 묶음 (버튼 이름, 묶음 안 메뉴 이름들).</summary>
        static readonly (string label, string[] menus)[] TownBar =
        {
            ("캐릭터", new[] { "장비", "세팅 슬롯", "스탯" }),
            ("가방", new[] { "가방", "창고" }),
            ("성장", new[] { "단련", "트리", "펫" }),
            ("던전", new[] { "던전" }),
            ("☰", new[] { "레이드", "PvP", "길드", "도감", "퀘스트", "출석", "업적", "상점", "시즌 패스", "꾸미기", "친구", "랭킹", "우편함", "설정", "공지", "고객지원" })
        };

        static readonly (string label, string[] menus)[] DungeonSide =
        {
            ("가방", new[] { "가방" }),
            ("☰", new[] { "장비", "스탯", "퀘스트", "설정" })
        };

        readonly Queue<string> _log = new Queue<string>();
        readonly List<IMenu> _menus = new List<IMenu>();

        ICombatSession _session;
        MenuContext _ctx;
        StatusPanel _status;
        SkillBar _skills;
        IHudModule _module;
        RectTransform _root;
        RectTransform _hub;
        UnityEngine.UI.Text _logText;
        UnityEngine.UI.Text _currency;
        UnityEngine.UI.Text _speedLabel;
        UnityEngine.UI.Text _autoLabel;
        UnityEngine.UI.Text _pointsBadge;
        float _textTimer;

        public FloatingTextLayer FloatingText { get; private set; }
        public IMoveInput Joystick { get; private set; }
        public RectTransform Root => _root;

        public void Initialize(ICombatSession session, MenuContext ctx, Camera worldCamera,
            Func<RectTransform, MenuContext, IHudModule> moduleFactory, Func<RectTransform, MenuContext, IEnumerable<IMenu>> menuFactory)
        {
            _session = session;
            _ctx = ctx;
            ctx.Log = AddLog;
            ItemFormatter.Power = ctx.Meta.Power.ItemPower;

            var canvas = UiFactory.CreateCanvas("HUD", 10);
            canvas.transform.SetParent(transform, false);
            _root = (RectTransform)canvas.transform;

            var floatRoot = UiFactory.Stretch(_root, "FloatingText");
            FloatingText = floatRoot.gameObject.AddComponent<FloatingTextLayer>();
            FloatingText.Initialize(floatRoot, worldCamera);

            bool town = session is TownSession;
            _status = new StatusPanel(_root, session);
            if (!town) _skills = new SkillBar(_root, session);
            Joystick = VirtualJoystick.Create(_root);

            _logText = UiFactory.Label(UiFactory.Anchored(_root, "Log", new Vector2(0f, 1f), new Vector2(24f, -225f), new Vector2(640f, 220f)),
                string.Empty, 19, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f));

            _module = moduleFactory(_root, ctx);
            _menus.AddRange(menuFactory(_root, ctx));

            if (town) BuildTownBar();
            else BuildDungeonControls();

            if (!town && session.Player.Rhythm != null)
                RhythmRingView.Create(_root, session.Player.Rhythm, ctx.Meta.Tuning.Rhythm, ctx.Meta.Settings, () => !(session is DungeonSession d) || d.State == FloorState.Fighting);

            session.Log += AddLog;
        }

        // ---------- 마을: 하단 메인 바 ----------

        void BuildTownBar()
        {
            var bar = UiFactory.Anchored(_root, "MainBar", new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(1080f, 170f));
            UiFactory.Frame(bar, true, true);
            float width = 1040f / TownBar.Length;
            for (int i = 0; i < TownBar.Length; i++)
            {
                var group = TownBar[i];
                var button = UiFactory.Button(bar, "Bar_" + group.label, new Vector2(0f, 0.5f), new Vector2(20f + i * width + 6f, 0f), new Vector2(width - 12f, 130f), group.label, () => OpenGroup(group.menus));
                var label = button.GetComponentInChildren<UnityEngine.UI.Text>();
                label.fontSize = 32;
                if (group.label == "캐릭터")
                    _pointsBadge = UiFactory.Label(UiFactory.Anchored(button.transform, "Badge", new Vector2(1f, 1f), new Vector2(-6f, -4f), new Vector2(60f, 40f)), string.Empty, 22, TextAnchor.MiddleRight, new Color(1f, 0.8f, 0.3f));
            }

            _currency = UiFactory.Label(UiFactory.Anchored(_root, "Currency", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(420f, 90f)),
                string.Empty, 26, TextAnchor.UpperRight, Color.white);
            // 우측 상단 빠른 버튼: 우편함 · 퀘스트
            UiFactory.Button(_root, "Quick_Quest", new Vector2(1f, 1f), new Vector2(-24f, -120f), new Vector2(150f, 64f), "퀘스트", () => OpenGroup(new[] { "퀘스트" }));
            UiFactory.Button(_root, "Quick_Attendance", new Vector2(1f, 1f), new Vector2(-184f, -120f), new Vector2(150f, 64f), "출석", () => OpenGroup(new[] { "출석" }));
        }

        // ---------- 던전: 오른쪽 조작 ----------

        void BuildDungeonControls()
        {
            var size = new Vector2(170f, 74f);
            float y = 260f;
            _speedLabel = UiFactory.Button(_root, "Speed", new Vector2(1f, 0f), new Vector2(-20f, y + 3 * 84f), size, string.Empty, CycleSpeed).GetComponentInChildren<UnityEngine.UI.Text>();
            _autoLabel = UiFactory.Button(_root, "AutoMove", new Vector2(1f, 0f), new Vector2(-20f, y + 2 * 84f), size, string.Empty, ToggleAutoMove).GetComponentInChildren<UnityEngine.UI.Text>();
            for (int i = 0; i < DungeonSide.Length; i++)
            {
                var group = DungeonSide[i];
                UiFactory.Button(_root, "Side_" + group.label, new Vector2(1f, 0f), new Vector2(-20f, y + (1 - i) * 84f), size, group.label, () => OpenGroup(group.menus));
            }
        }

        void CycleSpeed()
        {
            var meta = _ctx.Meta;
            var speeds = meta.Tuning.Convenience.Speeds;
            int index = Array.IndexOf(speeds, meta.Settings.Speed);
            float next = speeds[(index + 1) % speeds.Length];
            if (next > meta.Tuning.Convenience.FreeMaxSpeed && !meta.Shop.Speed3Active)
            {
                AddLog($"배속 ×{next:0}은 이용권이 필요합니다 (상점 · 편의)");
                next = speeds[0];
            }
            meta.Settings.Speed = next;
            meta.RequestSave();
        }

        void ToggleAutoMove()
        {
            _session.Player.AutoMove = !_session.Player.AutoMove;
            _ctx.Meta.Settings.AutoMove = _session.Player.AutoMove;
            AddLog(_session.Player.AutoMove ? "자동 이동 켜짐" : "자동 이동 꺼짐 (스킬은 계속 자동 시전)");
        }

        // ---------- 묶음 창 ----------

        void OpenGroup(string[] labels)
        {
            CloseAll();
            var menus = labels.Select(l => _menus.FirstOrDefault(m => m.ButtonLabel == l)).Where(m => m != null).ToList();
            if (menus.Count == 1)
            {
                Open(menus[0]);
                return;
            }
            _hub = UiFactory.Anchored(_root, "Hub", new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1000f, 40f + Mathf.CeilToInt(menus.Count / 4f) * 112f + 70f));
            UiFactory.Frame(_hub);
            UiFactory.Button(_hub, "Close", new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(70f, 56f), "X", CloseAll);
            for (int i = 0; i < menus.Count; i++)
            {
                var menu = menus[i];
                int row = i / 4, col = i % 4;
                UiFactory.Button(_hub, "Hub_" + menu.ButtonLabel, new Vector2(0f, 1f), new Vector2(24f + col * 240f, -80f - row * 112f), new Vector2(224f, 96f), menu.ButtonLabel, () => Open(menu));
            }
        }

        void Open(IMenu menu)
        {
            CloseAll();
            menu.Panel.SetVisible(true);
            menu.Refresh();
        }

        void CloseAll()
        {
            if (_hub != null) Destroy(_hub.gameObject);
            _hub = null;
            foreach (var m in _menus) m.Panel.SetVisible(false);
        }

        void AddLog(string line)
        {
            _log.Enqueue(line);
            while (_log.Count > MaxLogLines) _log.Dequeue();
            if (_logText != null) _logText.text = string.Join("\n", _log);
        }

        void Update()
        {
            if (_session == null) return;
            _status.Refresh();
            _skills?.Refresh();

            _textTimer -= Time.unscaledDeltaTime;
            if (_textTimer > 0f) return;
            _textTimer = TextRefreshInterval;

            _module?.Refresh(TextRefreshInterval);
            foreach (var menu in _menus) menu.Refresh();

            var meta = _ctx.Meta;
            if (_currency != null)
                _currency.text = $"<color=#FFD84A>{meta.Wallet.Gold:N0} G</color>\n<color=#9FD3FF>템포 {meta.State.Tempo:N0}</color>  <size=20>전투력 {meta.TotalPower(_session.Player):N0}</size>";
            if (_pointsBadge != null) _pointsBadge.text = meta.StatPoints.Unspent > 0 ? $"+{meta.StatPoints.Unspent}" : string.Empty;
            if (_speedLabel != null) _speedLabel.text = $"배속 ×{meta.Settings.Speed:0}";
            if (_autoLabel != null) _autoLabel.text = _session.Player.AutoMove ? "자동 이동" : "수동 이동";
        }

        void OnDestroy()
        {
            if (_session != null) _session.Log -= AddLog;
        }
    }
}
