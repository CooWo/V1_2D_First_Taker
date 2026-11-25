using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LootHunters
{
    /// <summary>
    /// 키보드 입력 → DungeonRun. 새 Input System / 기존 Input Manager 둘 다 지원합니다.
    /// 이동: WASD 또는 방향키 (누르면 수동 모드로 전환)
    /// 1~4: 스킬 · Tab: 자동/수동 · Q: 포션 · E: 두루마리 · R: 귀환 · F: 게임 속도
    /// </summary>
    [RequireComponent(typeof(DungeonRun))]
    public class PlayerController : MonoBehaviour
    {
        DungeonRun run;
        readonly bool[] skillKeys = new bool[4];

        void Awake() => run = GetComponent<DungeonRun>();

        void Update()
        {
            Vector2 move = Vector2.zero;
            bool tab = false, potion = false, scroll = false, ret = false, speed = false;
            for (int k = 0; k < skillKeys.Length; k++) skillKeys[k] = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;   // 휴대폰에는 키보드가 없으므로 null일 수 있음
            if (kb != null)
            {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
            tab = kb.tabKey.wasPressedThisFrame;
            potion = kb.qKey.wasPressedThisFrame;
            scroll = kb.eKey.wasPressedThisFrame;
            ret = kb.rKey.wasPressedThisFrame;
            speed = kb.fKey.wasPressedThisFrame;
            skillKeys[0] = kb.digit1Key.wasPressedThisFrame;
            skillKeys[1] = kb.digit2Key.wasPressedThisFrame;
            skillKeys[2] = kb.digit3Key.wasPressedThisFrame;
            skillKeys[3] = kb.digit4Key.wasPressedThisFrame;
            }
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1;
            tab = Input.GetKeyDown(KeyCode.Tab);
            potion = Input.GetKeyDown(KeyCode.Q);
            scroll = Input.GetKeyDown(KeyCode.E);
            ret = Input.GetKeyDown(KeyCode.R);
            speed = Input.GetKeyDown(KeyCode.F);
            skillKeys[0] = Input.GetKeyDown(KeyCode.Alpha1);
            skillKeys[1] = Input.GetKeyDown(KeyCode.Alpha2);
            skillKeys[2] = Input.GetKeyDown(KeyCode.Alpha3);
            skillKeys[3] = Input.GetKeyDown(KeyCode.Alpha4);
#endif

            // 모바일: 화면을 끌어서 쓰는 가상 조이스틱이 키보드보다 우선
            if (VirtualJoystick.Value != Vector2.zero) move = VirtualJoystick.Value;

            if (speed) run.CycleSpeed();

            if (run.State == RunState.Town)
            {
                run.SetMoveInput(Vector2.zero);
                return;
            }

            run.SetMoveInput(move);
            if (tab) run.ToggleAuto();
            if (potion) run.UsePotion();
            if (scroll) run.UseScroll();
            if (ret) run.RequestReturn();
            for (int i = 0; i < skillKeys.Length; i++)
                if (skillKeys[i]) run.UseSkill(i);
        }
    }
}
