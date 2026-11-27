using ShadowRPG.Core.Tuning;
using UnityEngine;

namespace ShadowRPG.Runtime.Tuning
{
    /// <summary>
    /// 수치 JSON 불러오기: Resources/Tuning/{섹션}.json → GameTuning.
    /// 파일에 없는 항목은 코드 기본값 유지 (FromJsonOverwrite). 문서: Docs/Tuning_Guide.md
    /// </summary>
    public static class TuningLoader
    {
        public const string ResourceFolder = "Tuning";

        public static GameTuning Load()
        {
            var tuning = new GameTuning();
            foreach (var name in GameTuning.SectionNames)
            {
                var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{name}");
                if (asset == null) continue;
                var section = tuning.Section(name);
                if (section == null) continue;
                try
                {
                    JsonUtility.FromJsonOverwrite(asset.text, section);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Tuning] {name}.json 읽기 실패 — 기본값 사용: {e.Message}");
                }
            }
            GameTuning.Current = tuning;
            return tuning;
        }
    }
}
