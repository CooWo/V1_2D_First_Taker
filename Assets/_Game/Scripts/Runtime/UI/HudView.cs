using System;
using System.Collections.Generic;
using ShadowRPG.Core.AutoBattle;
using ShadowRPG.Core.Game;
using ShadowRPG.Runtime.UI.Menus;
using ShadowRPG.Runtime.UI.Modes;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>
    /// HUD 뼈대: 상태·스킬 바·조이스틱·로그·메뉴 버튼 + 모드별 영역(던전/레이드).
    /// 각 영역의 내용은 전용 프레젠터가 그리고, 이 클래스는 배치와 갱신 주기만 맡습니다.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const int MaxLogLines = 7;
        const float TextRefreshInterval = 0.1f;

        readonly Queue<string> _log = new Queue<string>();
        readonly List<IMenu> _menus = new List<IMenu>();

        ICombatSession _session;
        MenuContext _ctx;
        StatusPanel _status;
        SkillBar _skills;
        IHudModule _module;
        UnityEngine.UI.Text _logText;
        UnityEngine.UI.Text _deathText;
        UnityEngine.UI.Text _autoMoveLabel;
        UnityEngine.UI.Text _classLabel;
        float _textTimer;

        public FloatingTextLayer FloatingText { get; private set; }
        public IMoveInput Joystick { get; private set; }

        public void Initialize(ICombatSession session, MenuContext ctx, Camera worldCamera,
            Func<RectTransform, MenuContext, IHudModule> moduleFactory, Func<RectTransform, MenuContext, IEnumerable<IMenu>> menuFactory)
        {
            _session = session;
            _ctx = ctx;
            ctx.Log = AddLog;

            var canvas = UiFactory.CreateCanvas("HUD", 10);
            canvas.transform.SetParent(transform, false);
            var root = (RectTransform)canvas.transform;

            var floatRoot = UiFactory.Stretch(root, "FloatingText");
            FloatingText = floatRoot.gameObject.AddComponent<FloatingTextLayer>();
            FloatingText.Initialize(floatRoot, worldCamera);

            _status = new StatusPanel(root, session);
            _skills = new SkillBar(root, session);
            Joystick = VirtualJoystick.Create(root);
            _logText = UiFactory.Label(UiFactory.Anchored(root, "Log", new Vector2(0f, 1f), new Vector2(24f, -225f), new Vector2(640f, 260f)),
                string.Empty, 19, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f));

            _module = moduleFactory(root, ctx);

            _menus.AddRange(menuFactory(root, ctx));
            BuildButtons(root);

            _deathText = UiFactory.Label(UiFactory.Anchored(root, "Death", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 120f)),
                string.Empty, 44, TextAnchor.MiddleCenter, new Color(0.6f, 0.75f, 1f));

            session.Log += AddLog;
        }

        void BuildButtons(RectTransform root)
        {
            // 버튼이 많으면 2열로 (가로 화면에서 우상단 정보와 겹치지 않게)
            int total = _menus.Count + 2;
            int columns = total > 6 ? 2 : 1;
            var size = new Vector2(columns == 2 ? 150f : 170f, 62f);
            int index = 0;

            Vector2 Slot()
            {
                int row = index / columns;
                int column = columns - 1 - index % columns;
                index++;
                return new Vector2(-20f - column * (size.x + 10f), 230f + row * 70f);
            }

            for (int i = _menus.Count - 1; i >= 0; i--)
            {
                var menu = _menus[i];
                UiFactory.Button(root, "Menu" + menu.ButtonLabel, new Vector2(1f, 0f), Slot(), size, menu.ButtonLabel, () => Open(menu));
            }
            _classLabel = UiFactory.Button(root, "Class", new Vector2(1f, 0f), Slot(), size, string.Empty, CycleClass).GetComponentInChildren<UnityEngine.UI.Text>();
            _autoMoveLabel = UiFactory.Button(root, "AutoMove", new Vector2(1f, 0f), Slot(), size, string.Empty, ToggleAutoMove).GetComponentInChildren<UnityEngine.UI.Text>();
        }

        void Open(IMenu menu)
        {
            bool show = !menu.Panel.Visible;
            foreach (var m in _menus) m.Panel.SetVisible(false);
            menu.Panel.SetVisible(show);
            menu.Refresh();
        }

        void ToggleAutoMove()
        {
            _session.Player.AutoMove = !_session.Player.AutoMove;
            AddLog(_session.Player.AutoMove ? "자동 이동 켜짐" : "자동 이동 꺼짐 (스킬은 계속 자동 시전)");
        }

        void CycleClass()
        {
            var classes = _ctx.Meta.Content.Classes.All;
            var current = _session.Player.Class.Current;
            int index = 0;
            for (int i = 0; i < classes.Count; i++) if (classes[i] == current) index = i;
            var next = classes[(index + 1) % classes.Count];
            _session.Player.ChangeClass(next);
            _ctx.Meta.Capture(_session.Player);
            _ctx.Meta.RequestSave();
            AddLog($"직업 변경: {next.Name} — {next.Description} (장비·스킬은 그대로)");
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
            _skills.Refresh();

            _textTimer -= Time.deltaTime;
            if (_textTimer > 0f) return;
            _textTimer = TextRefreshInterval;

            _module?.Refresh(TextRefreshInterval);
            foreach (var menu in _menus) menu.Refresh();

            var player = _session.Player;
            _autoMoveLabel.text = player.AutoMove ? "자동 이동 ON" : "자동 이동 OFF";
            _classLabel.text = $"직업: {player.Class.Current?.Name}";
            _deathText.text = _session.IsPlayerDead && _session.RespawnRemaining > 0f
                ? $"쓰러졌습니다\n<size=30>{Mathf.CeilToInt(_session.RespawnRemaining)}초 뒤 마을로</size>"
                : string.Empty;
        }

        void OnDestroy()
        {
            if (_session != null) _session.Log -= AddLog;
        }
    }
}
