using ShadowRPG.Core;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Town;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Meta;
using ShadowRPG.Core.Raid;
using ShadowRPG.Runtime.Presentation;
using ShadowRPG.Runtime.UI.Menus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace ShadowRPG.Runtime
{
    /// <summary>
    /// Unity 쪽 조립 지점 (Composition Root). 프로필을 불러와 성장 계층(GameMeta)을 만들고,
    /// 던전/레이드 세션을 전환하며, 저장 시점을 관리합니다. 게임 규칙은 들고 있지 않습니다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour, IModeSwitcher
    {
        const float SaveDebounceSeconds = 2f;

        [SerializeField] SpriteLibrary spriteLibrary;
        [SerializeField] BalanceSettings balance = new BalanceSettings();
        [SerializeField] DungeonRules dungeonRules = new DungeonRules();
        [Tooltip("새 저장일 때 시작 직업: knight / mage / archer / monk")]
        [SerializeField] string startingClass = "knight";
        [Tooltip("0이면 매번 다른 시드")]
        [SerializeField] int seed;
        [SerializeField] Vector2 raidArenaSize = new Vector2(22f, 26f);
        [Tooltip("켜면 시작할 때 저장을 지우고 새로 시작 (테스트용)")]
        [SerializeField] bool resetSaveOnStart;

        FileProfileStore _store;
        GameMeta _meta;
        IRandom _random;
        ICombatSession _session;
        SessionView _view;
        Camera _camera;
        bool _saveDirty;
        float _saveTimer;
        bool _pendingTown;
        readonly IFloorLayoutSelector _layouts = FloorLayoutSelector.CreateDefault();

        public GameMeta Meta => _meta;
        public ICombatSession Session => _session;

#if UNITY_EDITOR
        public void EditorSetSpriteLibrary(SpriteLibrary library) => spriteLibrary = library;
#endif

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (spriteLibrary == null) spriteLibrary = Resources.Load<SpriteLibrary>("SpriteLibrary");

            _random = new SystemRandom(seed != 0 ? seed : (int?)null);
            _store = new FileProfileStore();
            if (resetSaveOnStart) _store.Delete();

            var profile = _store.Load();
            if (!profile.HasStarted) profile.ClassId = startingClass;
            _meta = new GameMeta(profile, GameContent.CreateDefault(), new SystemClock(), _random);
            _meta.EnsureStarted();
            _meta.SaveRequested += () => _saveDirty = true;

            _camera = EnsureCamera();
            EnsureGlobalLight();
            EnsureEventSystem();
            EnterTown();
        }

        void Update()
        {
            _session?.Tick(Time.deltaTime);
            // 층 클리어 슬로우 모션 등 세션이 요구하는 시간 배율
            Time.timeScale = _session != null ? _session.TimeScale : 1f;

            if (_pendingTown)
            {
                _pendingTown = false;
                EnterTown();
            }

            if (!_saveDirty) return;
            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer < SaveDebounceSeconds) return;
            SaveNow();
        }

        // ---------- IModeSwitcher ----------

        public void EnterTown()
        {
            Switch(new TownSession(_meta, balance, _random, new BotTownPresence()));
        }

        public void EnterDungeon(int globalDungeon)
        {
            _meta.StartRun(globalDungeon);
            var session = new DungeonSession(_meta, balance, dungeonRules, _random, _layouts, seed != 0 ? seed : _random.Range(1, int.MaxValue));
            session.ReturnToTownRequested += _ => _pendingTown = true;
            Switch(session);
        }

        public bool TryEnterRaid(out string reason)
        {
            reason = null;
            if (_session is RaidSession)
            {
                reason = "이미 레이드 중입니다.";
                return false;
            }
            if (!_meta.RaidEntries.TryConsume())
            {
                reason = "오늘 레이드 입장 횟수를 모두 사용했습니다.";
                return false;
            }
            var arena = new Rect(-raidArenaSize * 0.5f, raidArenaSize);
            Switch(new RaidSession(_meta, balance, _random, arena, new BotPartySource()));
            return true;
        }

        void Switch(ICombatSession next)
        {
            _session?.Dispose();
            if (_view != null) Destroy(_view.gameObject);

            _session = next;
            _view = new GameObject("Session").AddComponent<SessionView>();
            _view.Build(next, _meta, spriteLibrary, _camera, this, seed);
            SaveNow();
        }

        // ---------- 저장 ----------

        void SaveNow()
        {
            if (_session != null) _meta.Capture(_session.Player);
            _store.Save(_meta.Profile);
            _saveDirty = false;
            _saveTimer = 0f;
        }

        void OnDestroy() => Time.timeScale = 1f;

        void OnApplicationPause(bool paused)
        {
            if (paused && _meta != null) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (_meta != null) SaveNow();
        }

        // ---------- 장면 기본 요소 ----------

        static Camera EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.035f, 0.05f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        static void EnsureGlobalLight()
        {
            foreach (var light in FindObjectsByType<Light2D>())
            {
                if (light.lightType != Light2D.LightType.Global) continue;
                light.intensity = 0.8f;
                light.color = new Color(0.82f, 0.86f, 1f);
                return;
            }

            var go = new GameObject("Global Light 2D");
            var global = go.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.8f;
            global.color = new Color(0.82f, 0.86f, 1f);
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
