using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>
    /// 시점 설정 (아트 디렉션 §8.1): 굽기와 게임 화면이 같은 값을 써야 그림이 맞습니다.
    /// 위치: Resources/Art/Projection.asset — 없으면 기본값(아이소메트릭 30°).
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Projection Settings", fileName = "Projection")]
    public sealed class ProjectionSettings : ScriptableObject
    {
        public const string ResourcePath = "Art/Projection";

        [Tooltip("켜면 아이소메트릭(바닥 마름모), 끄면 정면 3/4")]
        public bool isometric = true;
        [Tooltip("내려다보는 각도 (도). 30° = 바닥 마름모 2:1")]
        [Range(20f, 80f)] public float pitch = 30f;
    }
}
