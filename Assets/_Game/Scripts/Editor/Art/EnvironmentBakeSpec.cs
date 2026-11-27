using System;
using System.Collections.Generic;
using ShadowRPG.Runtime.Art;
using UnityEngine;

namespace ShadowRPG.EditorTools.Art
{
    /// <summary>
    /// 환경 묶음 굽기 설명서: 어떤 3D 조각을 바닥·벽·소품으로 쓸지. 결과는 EnvironmentTheme 에셋.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Environment Bake Spec", fileName = "EnvironmentBakeSpec")]
    public sealed class EnvironmentBakeSpec : ScriptableObject
    {
        [Serializable]
        public sealed class Piece
        {
            public string assetPath;
            public float weight = 1f;
            [Tooltip("가로세로 회전 변형 (바닥 타일 반복 티 줄이기)")]
            public bool rotateVariants;
        }

        [Serializable]
        public sealed class PropPiece
        {
            public string assetPath;
            public PropPlacement placement = PropPlacement.FloorEdge;
            public float weight = 1f;
            public bool blocks = true;
            public float scale = 1f;
            [Tooltip("Y축 회전 (도)")]
            public float yaw;
            public Color lightColor = new Color(0f, 0f, 0f, 0f);
            public float lightRadius = 4f;
            public float lightIntensity = 1f;
            public float lightHeight = 0.6f;
            public float flicker = 0.15f;
        }

        public string themeName = "Crypt";
        [Tooltip("바닥 한 칸 크기 (미터) — KayKit 작은 타일 2m")]
        public float floorTileMeters = 2f;
        public List<Piece> floors = new List<Piece>();
        [Tooltip("벽 정면 조각 (너비 4m 조각을 2칸으로 자름)")]
        public List<Piece> wallFaces = new List<Piece>();
        [Tooltip("벽 정면 조각 너비 (미터)")]
        public float wallPieceMeters = 4f;
        [Tooltip("벽 윗면으로 쓸 조각 (위에서 찍음)")]
        public string wallCapPath;
        public List<PropPiece> props = new List<PropPiece>();

        [Header("런타임 기본값 (EnvironmentTheme로 복사)")]
        public Color floorTint = Color.white;
        public Color wallTint = Color.white;
        public Color voidColor = new Color(0.015f, 0.012f, 0.022f);
        public Color ambientColor = new Color(0.62f, 0.66f, 0.85f);
        public float ambientIntensity = 0.75f;
        public float edgePropChance = 0.06f;
        public float wallPropChance = 0.18f;
        public float scatterPropChance = 0.02f;
    }
}
