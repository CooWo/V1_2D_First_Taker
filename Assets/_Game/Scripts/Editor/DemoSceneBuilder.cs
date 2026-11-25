using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowRPG.Core.Enemies;
using ShadowRPG.Runtime;
using ShadowRPG.Runtime.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowRPG.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 스프라이트 라이브러리(0x72 CC0 타일셋)와 전투 데모 씬을 만듭니다.
    /// 다시 실행해도 같은 결과가 나오도록 설계했습니다.
    /// </summary>
    public static class DemoSceneBuilder
    {
        const string FramesFolder = "Assets/Art/0x72/frames";
        const string DataFolder = "Assets/_Game/Resources";
        const string LegacyLibraryPath = "Assets/_Game/Data/SpriteLibrary.asset";
        const string SceneFolder = "Assets/_Game/Scenes";
        const string LibraryPath = DataFolder + "/SpriteLibrary.asset";
        const string ScenePath = SceneFolder + "/ShadowBattle.unity";

        [MenuItem("ShadowRPG/Build Demo Scene")]
        public static void BuildAll()
        {
            var library = BuildSpriteLibrary();
            BuildScene(library);
        }

        [MenuItem("ShadowRPG/Rebuild Sprite Library")]
        public static SpriteLibrary BuildSpriteLibrary()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            if (AssetDatabase.LoadAssetAtPath<SpriteLibrary>(LegacyLibraryPath) != null && AssetDatabase.LoadAssetAtPath<SpriteLibrary>(LibraryPath) == null)
            {
                AssetDatabase.MoveAsset(LegacyLibraryPath, LibraryPath);
                AssetDatabase.DeleteAsset("Assets/_Game/Data");
            }
            var library = AssetDatabase.LoadAssetAtPath<SpriteLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SpriteLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            // 몬스터 + 레이드 동료(knight_m 등) + 펫(angel 등)
            var keys = EnemyRoster.CreateDefault().Concat(EnemyRoster.CreateElites()).Append(EnemyRoster.CreateBoss())
                .Select(a => a.VisualKey)
                .Concat(new[] { "knight_m", "knight_f", "wizzard_f", "wizzard_m", "elf_m", "elf_f", "lizard_m", "dwarf_m", "angel", "pumpkin_dude", "tiny_slug" })
                .Distinct();

            var entries = new List<SpriteLibrary.Entry>();
            foreach (var key in keys)
            {
                var idle = LoadFrames(key + "_idle_anim_f");
                var run = LoadFrames(key + "_run_anim_f");
                var plain = LoadFrames(key + "_anim_f");
                if (idle.Length == 0) idle = plain.Length > 0 ? plain : run;
                if (idle.Length == 0)
                {
                    Debug.LogWarning($"[ShadowRPG] '{key}' 스프라이트를 찾지 못했습니다.");
                    continue;
                }
                entries.Add(new SpriteLibrary.Entry { key = key, frames = idle, framesPerSecond = 8f });
                if (run.Length > 0) entries.Add(new SpriteLibrary.Entry { key = key + "_run", frames = run, framesPerSecond = 10f });
            }

            var stairs = Load("floor_stairs");
            if (stairs != null) entries.Add(new SpriteLibrary.Entry { key = "portal", frames = new[] { stairs }, framesPerSecond = 1f });

            var floors = Enumerable.Range(1, 8).Select(i => Load("floor_" + i)).Where(s => s != null).ToArray();
            library.EditorSet(entries, floors, Load("wall_mid"));
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowRPG] SpriteLibrary: {entries.Count}개 항목, 바닥 {floors.Length}종");
            return library;
        }

        static void BuildScene(SpriteLibrary library)
        {
            Directory.CreateDirectory(SceneFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.035f, 0.05f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<CameraFollow>();

            var bootstrapObject = new GameObject("GameBootstrap");
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.EditorSetSpriteLibrary(library);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);

            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"[ShadowRPG] 데모 씬 생성: {ScenePath} (빌드 첫 씬으로 등록)");
        }

        static Sprite[] LoadFrames(string prefix)
        {
            var frames = new List<Sprite>();
            for (int i = 0; i < 12; i++)
            {
                var sprite = Load(prefix + i);
                if (sprite != null) frames.Add(sprite);
                else if (frames.Count > 0) break;
            }
            return frames.ToArray();
        }

        static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{FramesFolder}/{name}.png");
    }
}
