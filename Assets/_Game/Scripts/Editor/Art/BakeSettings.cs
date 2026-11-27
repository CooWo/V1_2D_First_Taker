using UnityEngine;

namespace ShadowRPG.EditorTools.Art
{
    /// <summary>
    /// 3D → 2D 굽기 공통 설정 (카메라 각도·픽셀 밀도·명암·선). 모든 캐릭터·환경이 같은 값을 써야 화풍이 하나로 맞습니다.
    /// 위치: Assets/_Game/Art/Bake/BakeSettings.asset
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Bake Settings", fileName = "BakeSettings")]
    public sealed class BakeSettings : ScriptableObject
    {
        [Header("카메라")]
        [Tooltip("내려다보는 각도 (도). 캐릭터·벽·소품 공통")]
        [Range(20f, 80f)] public float cameraPitch = 45f;
        [Tooltip("3D 1미터 = 게임 몇 유닛 (KayKit 작은 바닥 2m = 1칸)")]
        public float unitsPerMeter = 0.5f;
        [Tooltip("게임 1유닛 = 픽셀 수 (모든 스프라이트 공통 밀도)")]
        public float pixelsPerUnit = 128f;

        [Header("캐릭터")]
        [Tooltip("캐릭터 한 칸 크기 (픽셀). 224 = 1.75유닛 = 3.5m 범위")]
        public int characterCell = 224;
        [Tooltip("셀 안 발 위치 (아래에서 비율)")]
        [Range(0f, 0.5f)] public float characterPivotY = 0.2f;
        [Tooltip("초당 샘플 프레임")]
        public float sampleFps = 12f;
        public int maxFrames = 12;
        public int minFrames = 4;
        [Tooltip("5방향만 굽고 3방향은 좌우 반전 (용량 37% 절감)")]
        public bool mirrorDirections = true;

        [Header("품질")]
        [Tooltip("확대 렌더 배수 (클수록 가장자리가 부드럽지만 느림)")]
        [Range(1, 4)] public int supersample = 3;
        [Tooltip("바깥 외곽선 색")]
        public Color outlineColor = new Color(0.035f, 0.025f, 0.05f, 1f);
        [Tooltip("바깥 외곽선 두께 (최종 픽셀)")]
        [Range(0, 3)] public int outlineWidth = 1;
        [Tooltip("안쪽 선 (접힘·겹침 경계) 진하기 0~1")]
        [Range(0f, 1f)] public float innerLineStrength = 0.55f;
        [Tooltip("안쪽 선: 노멀 각도 차이 기준 (도)")]
        public float innerLineAngle = 55f;
        [Tooltip("안쪽 선: 깊이 차이 기준 (미터)")]
        public float innerLineDepth = 0.18f;

        [Header("명암 (ToonBake 셰이더)")]
        public Color shadowColor = new Color(0.30f, 0.27f, 0.42f);
        public Color midColor = new Color(0.70f, 0.66f, 0.80f);
        public Vector3 lightDirection = new Vector3(-0.45f, 0.75f, -0.55f);
        public Color skyAmbient = new Color(0.20f, 0.24f, 0.36f);
        public Color groundAmbient = new Color(0.06f, 0.04f, 0.05f);
        [Range(0f, 1f)] public float topHighlight = 0.18f;
        public float saturation = 1.12f;
        public float exposure = 1.1f;

        [Header("환경")]
        [Tooltip("벽 높이 배율 (KayKit 벽 4m는 너무 높아 줄임)")]
        public float wallHeightScale = 0.55f;
        [Tooltip("환경 명암 노출")]
        public float environmentExposure = 1.0f;
        [Tooltip("환경 림라이트 세기")]
        public float environmentRim = 0.15f;

        public float MetersToPixels => unitsPerMeter * pixelsPerUnit;

        public void Apply(Material toon)
        {
            toon.SetColor("_ShadowColor", shadowColor);
            toon.SetColor("_MidColor", midColor);
            toon.SetVector("_LightDir", lightDirection);
            toon.SetColor("_SkyColor", skyAmbient);
            toon.SetColor("_GroundColor", groundAmbient);
            toon.SetFloat("_HighlightAmount", topHighlight);
            toon.SetFloat("_Saturation", saturation);
            toon.SetFloat("_Exposure", exposure);
        }
    }
}
