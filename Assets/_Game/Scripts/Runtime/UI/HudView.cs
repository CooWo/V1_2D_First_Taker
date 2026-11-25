using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.AutoBattle;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>
    /// 전투 HUD 조립 및 갱신 주기 관리. 각 영역은 전용 프레젠터(StatusPanel, SkillBar, …)가 그립니다.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const int MaxLogLines = 7;
        const float TextRefreshInterval = 0.1f;

        readonly Queue<string> _log = new Queue<string>();

        GameSession _session;
        StatusPanel _status;
        SkillBar _skills;
        EquipmentPanel _equipment;
        InventoryPanel _inventory;
        UnityEngine.UI.Text _info;
        UnityEngine.UI.Text _logText;
        UnityEngine.UI.Text _deathText;
        UnityEngine.UI.Text _autoMoveLabel;
        UnityEngine.UI.Text _classLabel;
        RectTransform _bossRoot;
        UnityEngine.UI.Image _bossFill;
        UnityEngine.UI.Text _bossName;
        EnemyAgent _boss;
        float _textTimer;
        float _elapsed;

        public FloatingTextLayer FloatingText { get; private set; }
        public IMoveInput Joystick { get; private set; }

        public void Initialize(GameSession session, Camera worldCamera)
        {
            _session = session;
            var canvas = UiFactory.CreateCanvas("HUD", 10);
            canvas.transform.SetParent(transform, false);
            var root = (RectTransform)canvas.transform;

            var floatRoot = UiFactory.Stretch(root, "FloatingText");
            FloatingText = floatRoot.gameObject.AddComponent<FloatingTextLayer>();
            FloatingText.Initialize(floatRoot, worldCamera);

            _status = new StatusPanel(root, session);
            _skills = new SkillBar(root, session);
            Joystick = VirtualJoystick.Create(root);

            _info = UiFactory.Label(UiFactory.Anchored(root, "Info", new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(460f, 170f)),
                string.Empty, 22, TextAnchor.UpperRight, Color.white);

            _logText = UiFactory.Label(UiFactory.Anchored(root, "Log", new Vector2(0f, 1f), new Vector2(24f, -225f), new Vector2(620f, 260f)),
                string.Empty, 19, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f));

            BuildBossBar(root);
            BuildButtons(root);

            _equipment = new EquipmentPanel(root, session);
            _inventory = new InventoryPanel(root, session, AddLog);

            _deathText = UiFactory.Label(UiFactory.Anchored(root, "Death", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 120f)),
                string.Empty, 44, TextAnchor.MiddleCenter, new Color(0.6f, 0.75f, 1f));

            session.Log += AddLog;
            session.ItemLooted += OnItemLooted;
            session.Director.WaveStarted += wave => AddLog(wave % 10 == 0 ? $"<color=#FF6A4A>웨이브 {wave} — 보스 출현!</color>" : wave % 5 == 0 ? $"웨이브 {wave} — 정예 무리" : $"웨이브 {wave}");
            session.Director.BossSpawned += boss => _boss = boss;
            AddLog("자동 사냥 시작 — 스킬은 장비에서, 직업은 패시브만 바꿉니다");
        }

        void BuildBossBar(RectTransform root)
        {
            _bossRoot = UiFactory.Anchored(root, "BossBar", new Vector2(0.5f, 1f), new Vector2(0f, -500f), new Vector2(640f, 52f));
            UiFactory.Panel(_bossRoot, new Color(0f, 0f, 0f, 0.7f));
            UiFactory.Border(_bossRoot, new Color(1f, 0.45f, 0.2f));
            _bossFill = UiFactory.Fill(UiFactory.Stretch(_bossRoot, "Fill", 4f), new Color(0.85f, 0.2f, 0.15f));
            _bossName = UiFactory.Label(_bossRoot, "Name", string.Empty, 24, TextAnchor.MiddleCenter, Color.white);
            _bossRoot.gameObject.SetActive(false);
        }

        void BuildButtons(RectTransform root)
        {
            var size = new Vector2(170f, 70f);
            var autoMove = UiFactory.Button(root, "AutoMove", new Vector2(1f, 0f), new Vector2(-20f, 470f), size, string.Empty, ToggleAutoMove);
            _autoMoveLabel = autoMove.GetComponentInChildren<UnityEngine.UI.Text>();
            var classButton = UiFactory.Button(root, "Class", new Vector2(1f, 0f), new Vector2(-20f, 390f), size, string.Empty, CycleClass);
            _classLabel = classButton.GetComponentInChildren<UnityEngine.UI.Text>();
            UiFactory.Button(root, "Equipment", new Vector2(1f, 0f), new Vector2(-20f, 310f), size, "장비", () => Toggle(true));
            UiFactory.Button(root, "Inventory", new Vector2(1f, 0f), new Vector2(-20f, 230f), size, "가방", () => Toggle(false));
        }

        void Toggle(bool equipment)
        {
            bool showEquipment = equipment && !_equipment.Visible;
            bool showInventory = !equipment && !_inventory.Visible;
            _equipment.SetVisible(showEquipment);
            _inventory.SetVisible(showInventory);
            _equipment.Refresh();
        }

        void ToggleAutoMove()
        {
            _session.Player.AutoMove = !_session.Player.AutoMove;
            AddLog(_session.Player.AutoMove ? "자동 이동 켜짐" : "자동 이동 꺼짐 (스킬은 계속 자동 시전)");
        }

        void CycleClass()
        {
            var classes = _session.Content.Classes.All;
            var current = _session.Player.Class.Current;
            int index = 0;
            for (int i = 0; i < classes.Count; i++) if (classes[i] == current) index = i;
            var next = classes[(index + 1) % classes.Count];
            _session.Player.ChangeClass(next);
            AddLog($"직업 변경: {next.Name} — {next.Description} (장비·스킬은 그대로)");
        }

        void OnItemLooted(Item item, bool equipped)
        {
            AddLog((equipped ? "자동 장착 " : "획득 ") + ItemFormatter.ColoredName(item) + " " + ItemFormatter.ShortGrant(item, _session.Content));
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
            _elapsed += Time.deltaTime;

            _status.Refresh();
            _skills.Refresh();
            RefreshBoss();

            _textTimer -= Time.deltaTime;
            if (_textTimer > 0f) return;
            _textTimer = TextRefreshInterval;

            var player = _session.Player;
            _autoMoveLabel.text = player.AutoMove ? "자동 이동 ON" : "자동 이동 OFF";
            _classLabel.text = $"직업: {player.Class.Current?.Name}";
            _info.text = $"웨이브 <b>{_session.Director.Wave}</b>   처치 <b>{_session.Kills}</b>\n" +
                         $"남은 적 {_session.Director.AliveCount}   {Mathf.FloorToInt(_elapsed / 60f):00}:{Mathf.FloorToInt(_elapsed % 60f):00}\n" +
                         $"<size=18><color=#8FA8D0>AI: {player.Planner.LastDecision} · 주력 {(player.Combatant.Loadout.PrimaryIsMelee ? "근접" : "원거리")}</color></size>";

            _deathText.text = _session.IsPlayerDead ? $"그림자가 흩어졌습니다\n<size=30>{Mathf.CeilToInt(_session.RespawnRemaining)}초 뒤 부활</size>" : string.Empty;
            _equipment.Refresh();
        }

        void RefreshBoss()
        {
            bool alive = _boss != null && _boss.Combatant.IsAlive;
            _bossRoot.gameObject.SetActive(alive);
            if (!alive) return;
            _bossFill.fillAmount = _boss.Combatant.Health.LifeFraction;
            _bossName.text = $"{_boss.Combatant.Name}  {_boss.Combatant.Health.LifeFraction * 100f:0}%";
        }
    }
}
