using ShadowRPG.Core;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Game;
using ShadowRPG.Runtime.Presentation;
using ShadowRPG.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace ShadowRPG.Runtime
{
    /// <summary>
    /// Unity 쪽 조립 지점 (Composition Root). Core 세션을 만들고, 표현·입력·UI를 연결한 뒤
    /// 매 프레임 세션을 진행시킵니다. 게임 규칙은 하나도 들고 있지 않습니다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] SpriteLibrary spriteLibrary;
        [SerializeField] BalanceSettings balance = new BalanceSettings();
        [Tooltip("knight / mage / archer / monk")]
        [SerializeField] string startingClass = "knight";
        [Tooltip("0이면 매번 다른 시드")]
        [SerializeField] int seed;
        [Tooltip("세로형 모바일 화면 기준 전투 구역 (가로 x 세로, 유닛)")]
        [SerializeField] Vector2 arenaSize = new Vector2(24f, 34f);

        GameSession _session;

        public GameSession Session => _session;

#if UNITY_EDITOR
        public void EditorSetSpriteLibrary(SpriteLibrary library) => spriteLibrary = library;
#endif

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (spriteLibrary == null) spriteLibrary = Resources.Load<SpriteLibrary>("SpriteLibrary");

            var random = new SystemRandom(seed != 0 ? seed : (int?)null);
            var arena = new Rect(-arenaSize * 0.5f, arenaSize);
            _session = new GameSession(GameContent.CreateDefault(), balance, random, arena);

            var camera = EnsureCamera();
            EnsureGlobalLight();
            EnsureEventSystem();

            new GameObject("Arena").AddComponent<ArenaView>().Build(arena, spriteLibrary, seed);

            var hud = new GameObject("Hud").AddComponent<HudView>();
            hud.Initialize(_session, camera);

            var presenter = new GameObject("World").AddComponent<WorldPresenter>();
            presenter.Initialize(_session.World, _session.Player.Combatant, spriteLibrary, hud.FloatingText);

            _session.Player.MoveInput = new CompositeMoveInput(hud.Joystick, new KeyboardMoveInput());
            _session.GiveStarterKit(startingClass);

            if (!camera.TryGetComponent<CameraFollow>(out var follow)) follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.SetTarget(presenter.PlayerTransform);
        }

        void Update() => _session?.Tick(Time.deltaTime);

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
            foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
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
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
