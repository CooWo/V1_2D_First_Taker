using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>ACT 순번 → 환경 묶음.</summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Environment Catalog", fileName = "EnvironmentCatalog")]
    public sealed class EnvironmentCatalog : ScriptableObject
    {
        public const string ResourcePath = "Art/EnvironmentCatalog";

        [Tooltip("ACT 1부터 순서대로. 모자라면 마지막 묶음을 반복 사용")]
        public List<EnvironmentTheme> actThemes = new List<EnvironmentTheme>();
        [Tooltip("마을 묶음")]
        public EnvironmentTheme town;
        [Tooltip("ACT별 색감 (바닥·벽 색 곱하기). 같은 그림을 ACT마다 다른 분위기로")]
        public List<Color> actTints = new List<Color>();

        public EnvironmentTheme ForAct(int actIndex)
        {
            if (actThemes.Count == 0) return null;
            return actThemes[Mathf.Clamp(actIndex, 0, actThemes.Count - 1)];
        }

        public Color TintForAct(int actIndex) => actTints.Count == 0 ? Color.white : actTints[Mathf.Clamp(actIndex, 0, actTints.Count - 1)];

        static EnvironmentCatalog _loaded;
        public static EnvironmentCatalog Load() => _loaded != null ? _loaded : _loaded = Resources.Load<EnvironmentCatalog>(ResourcePath);
    }
}
