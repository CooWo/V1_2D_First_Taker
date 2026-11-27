using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    public enum PropPlacement
    {
        /// <summary>방 바닥 가장자리 (통로는 피함).</summary>
        FloorEdge,
        /// <summary>방 바닥 아무 곳 (장식, 막지 않음).</summary>
        FloorScatter,
        /// <summary>뒷벽(정면이 보이는 벽) 표면.</summary>
        WallFace
    }

    /// <summary>
    /// 던전 한 종류의 그림 묶음 (바닥·벽·소품·조명 분위기). ACT마다 하나씩 지정합니다.
    /// "어두움"은 조명을 낮추는 것이 아니라 이 묶음의 에셋(검은 공허, 어두운 돌, 뼈·관·촛불)으로 만듭니다.
    /// 아트 교체 = 스프라이트만 갈아 끼우면 됩니다 (크기 규칙은 <see cref="pixelsPerUnit"/> 참고).
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Environment Theme", fileName = "EnvironmentTheme")]
    public sealed class EnvironmentTheme : ScriptableObject
    {
        [Serializable]
        public sealed class WeightedSprite
        {
            public Sprite sprite;
            [Min(0f)] public float weight = 1f;
        }

        [Serializable]
        public sealed class Prop
        {
            public string name;
            public Sprite sprite;
            public PropPlacement placement = PropPlacement.FloorEdge;
            [Min(0f)] public float weight = 1f;
            [Tooltip("이동을 막는 소품인지 (켜면 그 칸은 지나갈 수 없음)")]
            public bool blocks;
            [Tooltip("빛을 내는 소품 (횃불·촛불). a=0이면 빛 없음")]
            public Color lightColor = new Color(0f, 0f, 0f, 0f);
            public float lightRadius = 4f;
            public float lightIntensity = 1f;
            [Tooltip("빛 깜빡임 세기 (0 = 고정)")]
            public float flicker = 0.15f;
            [Tooltip("빛 위치 (스프라이트 발 기준 위로)")]
            public float lightHeight = 0.6f;
        }

        public string themeName = "Crypt";
        [Tooltip("모든 환경 스프라이트의 픽셀 밀도 (캐릭터 묶음과 같아야 크기가 맞음)")]
        public float pixelsPerUnit = 128f;

        [Header("바닥")]
        public List<WeightedSprite> floorTiles = new List<WeightedSprite>();
        [Tooltip("바닥 위 얼룩·풀·균열 (지나갈 수 있음)")]
        public List<WeightedSprite> floorDecals = new List<WeightedSprite>();
        [Range(0f, 1f)] public float decalChance = 0.08f;
        public Color floorTint = Color.white;

        [Header("벽")]
        [Tooltip("바닥 쪽을 향한 벽 정면 (한 칸 너비, 발 기준 위로 솟음)")]
        public List<WeightedSprite> wallFaces = new List<WeightedSprite>();
        [Tooltip("벽 윗면 (한 칸)")]
        public Sprite wallCap;
        public Color wallTint = Color.white;
        [Tooltip("벽 정면 높이 (게임 유닛). 벽 윗면은 이만큼 위에 그려집니다.")]
        public float wallFaceHeight = 1.1f;
        [Tooltip("벽 너머 공허 색 (배경)")]
        public Color voidColor = new Color(0.015f, 0.012f, 0.022f);

        [Header("소품")]
        public List<Prop> props = new List<Prop>();
        [Tooltip("방 가장자리 칸마다 소품이 놓일 확률")]
        [Range(0f, 1f)] public float edgePropChance = 0.12f;
        [Tooltip("뒷벽 칸마다 벽 소품(횃불·깃발)이 놓일 확률")]
        [Range(0f, 1f)] public float wallPropChance = 0.18f;
        [Range(0f, 1f)] public float scatterPropChance = 0.02f;

        [Header("빛·분위기")]
        public Color ambientColor = new Color(0.62f, 0.66f, 0.85f);
        [Range(0f, 2f)] public float ambientIntensity = 0.75f;
        public Color heroLightColor = new Color(0.55f, 0.7f, 1f);
        public float heroLightRadius = 5f;
        [Range(0f, 3f)] public float heroLightIntensity = 0.6f;

        public Sprite Pick(List<WeightedSprite> list, System.Random random)
        {
            if (list == null || list.Count == 0) return null;
            float total = 0f;
            foreach (var w in list) if (w.sprite != null) total += w.weight;
            if (total <= 0f) return null;
            float roll = (float)random.NextDouble() * total;
            foreach (var w in list)
            {
                if (w.sprite == null) continue;
                roll -= w.weight;
                if (roll <= 0f) return w.sprite;
            }
            return list[list.Count - 1].sprite;
        }

        public Prop PickProp(PropPlacement placement, System.Random random)
        {
            float total = 0f;
            foreach (var p in props) if (p.placement == placement && p.sprite != null) total += p.weight;
            if (total <= 0f) return null;
            float roll = (float)random.NextDouble() * total;
            foreach (var p in props)
            {
                if (p.placement != placement || p.sprite == null) continue;
                roll -= p.weight;
                if (roll <= 0f) return p;
            }
            return null;
        }
    }

}
