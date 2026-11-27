using System.IO;
using ShadowRPG.Core.Tuning;
using UnityEditor;
using UnityEngine;

namespace ShadowRPG.EditorTools
{
    /// <summary>
    /// 수치 JSON 파일 만들기·초기화. 기본값은 Core/Tuning/GameTuning.cs.
    /// - Export Missing: 없는 파일만 기본값으로 생성 (이미 고친 파일은 그대로)
    /// - Reset All: 모든 파일을 기본값으로 덮어씀
    /// </summary>
    public static class TuningExporter
    {
        public const string Folder = "Assets/_Game/Resources/Tuning";

        [MenuItem("ShadowRPG/Tuning/Export Missing JSON")]
        public static void ExportMissing() => Export(false);

        [MenuItem("ShadowRPG/Tuning/Reset All JSON To Defaults")]
        public static void ResetAll()
        {
            if (!EditorUtility.DisplayDialog("수치 초기화", "모든 수치 JSON을 기본값으로 덮어씁니다. 계속할까요?", "초기화", "취소")) return;
            Export(true);
        }

        /// <summary>float 오차(1.600000023841858)를 사람이 고치기 쉬운 값(1.6)으로 정리합니다.</summary>
        static string Clean(string json) => System.Text.RegularExpressions.Regex.Replace(json, @"-?\d+\.\d{5,}", m =>
            System.Math.Round(double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture), 4).ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture));

        public static void Export(bool overwrite)
        {
            Directory.CreateDirectory(Folder);
            var defaults = new GameTuning();
            int written = 0;
            foreach (var name in GameTuning.SectionNames)
            {
                string path = $"{Folder}/{name}.json";
                if (!overwrite && File.Exists(path)) continue;
                File.WriteAllText(path, Clean(JsonUtility.ToJson(defaults.Section(name), true)), new System.Text.UTF8Encoding(false));
                written++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Tuning] JSON {written}개 작성: {Folder}");
        }
    }
}
