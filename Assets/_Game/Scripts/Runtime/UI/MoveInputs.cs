using ShadowRPG.Core.AutoBattle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>에디터/PC 테스트용 WASD·방향키 입력.</summary>
    public sealed class KeyboardMoveInput : IMoveInput
    {
        public Vector2 Direction
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return Vector2.zero;
                var v = Vector2.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) v.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) v.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v.y += 1f;
                return v;
            }
        }
    }

    /// <summary>여러 입력 중 먼저 값이 있는 것을 씁니다 (Composite).</summary>
    public sealed class CompositeMoveInput : IMoveInput
    {
        readonly IMoveInput[] _inputs;

        public CompositeMoveInput(params IMoveInput[] inputs) => _inputs = inputs;

        public Vector2 Direction
        {
            get
            {
                foreach (var input in _inputs)
                {
                    if (input == null) continue;
                    var d = input.Direction;
                    if (d.sqrMagnitude > 0.01f) return d;
                }
                return Vector2.zero;
            }
        }
    }
}
