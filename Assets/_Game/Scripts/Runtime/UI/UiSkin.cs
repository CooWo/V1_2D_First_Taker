using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>
    /// UI 겉모양 묶음 (글꼴·패널·버튼 그림·색). 지금은 Kenney RPG UI(CC0) + Noto Sans KR.
    /// 정식 UI 아트로 바꿀 때 이 에셋의 그림만 교체하면 모든 메뉴에 반영됩니다. 위치: Resources/UI/UiSkin
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/UI Skin", fileName = "UiSkin")]
    public sealed class UiSkin : ScriptableObject
    {
        public const string ResourcePath = "UI/UiSkin";

        public Font font;
        [Tooltip("큰 창 배경 (9-slice)")] public Sprite panel;
        [Tooltip("창 안쪽 칸 (9-slice)")] public Sprite inset;
        [Tooltip("버튼 (9-slice)")] public Sprite button;
        public Sprite buttonPressed;
        [Tooltip("둥근 버튼 (하단 바)")] public Sprite roundButton;
        public Color panelTint = new Color(0.26f, 0.25f, 0.33f, 0.97f);
        public Color insetTint = new Color(0.16f, 0.15f, 0.21f, 0.95f);
        public Color buttonTint = new Color(0.27f, 0.25f, 0.34f, 1f);
        public Color accent = new Color(1f, 0.78f, 0.35f);
        public Color textMain = new Color(0.93f, 0.92f, 0.96f);
        public Color textDim = new Color(0.6f, 0.63f, 0.74f);

        static UiSkin _loaded;
        static bool _tried;

        public static UiSkin Current
        {
            get
            {
                if (_loaded != null || _tried) return _loaded;
                _tried = true;
                return _loaded = Resources.Load<UiSkin>(ResourcePath);
            }
        }
    }
}
