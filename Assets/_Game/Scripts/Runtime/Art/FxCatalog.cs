using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>
    /// 이펙트 그림 묶음 (흰색 기반, SpriteRenderer 색으로 속성 색을 입힘).
    /// 지금은 Kenney Particle Pack(CC0)을 쓰고, 정식 이펙트(AI/손그림 프레임)로 바꿀 때 이 항목만 교체합니다.
    /// 배열은 여러 장이면 무작위로 고르거나 프레임으로 재생합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Fx Catalog", fileName = "FxCatalog")]
    public sealed class FxCatalog : ScriptableObject
    {
        public const string ResourcePath = "Art/FxCatalog";

        [Tooltip("베기 궤적 (무작위 1장)")] public Sprite[] slash;
        [Tooltip("피격 섬광")] public Sprite[] spark;
        [Tooltip("별 모양 섬광 (치명타)")] public Sprite[] star;
        [Tooltip("부드러운 빛 덩어리")] public Sprite[] glow;
        [Tooltip("충격파 고리")] public Sprite[] ring;
        [Tooltip("연기")] public Sprite[] smoke;
        [Tooltip("불꽃")] public Sprite[] flame;
        [Tooltip("마법 문양·소용돌이")] public Sprite[] magic;
        [Tooltip("궤적 (투사체 꼬리)")] public Sprite[] trace;
        [Tooltip("바닥 그을음")] public Sprite[] scorch;
        [Tooltip("캐릭터 발밑 그림자")] public Sprite groundShadow;

        public static Sprite Pick(Sprite[] list, Sprite fallback)
        {
            if (list == null || list.Length == 0) return fallback;
            var s = list[Random.Range(0, list.Length)];
            return s != null ? s : fallback;
        }

        static FxCatalog _loaded;
        public static FxCatalog Load() => _loaded != null ? _loaded : _loaded = Resources.Load<FxCatalog>(ResourcePath);
    }
}
