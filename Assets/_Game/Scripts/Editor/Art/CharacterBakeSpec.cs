using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.EditorTools.Art
{
    /// <summary>
    /// 캐릭터 한 외형의 굽기 설명서. 모델(FBX/GLB) + 보일 부품 + 붙일 장비 + 동작 목록.
    /// 같은 모델로 무기·투구만 바꾼 외형을 여러 개 만들 수 있습니다 (장비 외형 변경).
    /// AI로 만든 모델로 바꿀 때: modelPath와 동작 이름(source)만 새 모델에 맞게 고치면 됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Character Bake Spec", fileName = "CharacterBakeSpec")]
    public sealed class CharacterBakeSpec : ScriptableObject
    {
        [Serializable]
        public sealed class ClipSpec
        {
            [Tooltip("게임 동작 이름: idle, run, attack, attack2, cast, dodge, hit, death, spawn")]
            public string label;
            [Tooltip("모델 안 애니메이션 이름")]
            public string source;
            public bool loop = true;
            [Tooltip("타격 시점 (0~1, -1 = 없음)")]
            public float impact = -1f;
            [Tooltip("재생 속도 배율 (원본보다 빠르게 → 타격감)")]
            public float speed = 1f;
            [Tooltip("원본 동작 중 사용할 구간 (0~1)")]
            public Vector2 range = new Vector2(0f, 1f);
        }

        [Serializable]
        public sealed class Attachment
        {
            [Tooltip("붙일 모델 경로 (무기 GLB 등)")]
            public string assetPath;
            [Tooltip("붙일 뼈 이름 (예: handslot.r)")]
            public string bone = "handslot.r";
            public Vector3 localPosition;
            public Vector3 localEuler;
            public float scale = 1f;
        }

        [Tooltip("외형 키 (AppearanceCatalog에 등록되는 이름, 예: knight/sword_shield)")]
        public string key;
        public string modelPath;
        [Tooltip("모델 정면 보정 (도). +Z 정면 = 0, -Z 정면 = 180")]
        public float forwardYaw;
        [Tooltip("켜고 끌 수 있는 부품 이름 목록 (무기·방패·투구·망토 등)")]
        public List<string> optionalParts = new List<string>();
        [Tooltip("optionalParts 중 이 외형에서 보일 부품")]
        public List<string> showParts = new List<string>();
        public List<Attachment> attachments = new List<Attachment>();
        public Color tint = Color.white;
        public Color rimColor = new Color(0.35f, 0.6f, 1f);
        [Range(0f, 2f)] public float rimAmount = 1f;
        [Tooltip("게임 안 표시 배율 (보스 등)")]
        public float displayScale = 1f;
        public List<ClipSpec> clips = new List<ClipSpec>();
    }
}
