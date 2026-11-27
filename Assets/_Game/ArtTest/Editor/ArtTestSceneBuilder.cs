using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowRPG.ArtTest.EditorTools
{
    /// <summary>아트 시험 장면 생성 (본 게임 빌드 씬 목록에는 넣지 않습니다).</summary>
    public static class ArtTestSceneBuilder
    {
        const string Folder = "Assets/_Game/ArtTest";
        const string ScenePath = Folder + "/ArtTest_Combat.unity";

        [MenuItem("ShadowRPG/Art Test/Build Combat Scene")]
        public static void Build()
        {
            var hero = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSet>(Folder + "/Baked/Hero_SpriteSet.asset");
            var brute = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSet>(Folder + "/Baked/Brute_SpriteSet.asset");
            if (hero == null || brute == null)
            {
                CharacterSpriteBaker.BakeAll();
                hero = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSet>(Folder + "/Baked/Hero_SpriteSet.asset");
                brute = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSet>(Folder + "/Baked/Brute_SpriteSet.asset");
            }

            var additive = MaterialAsset("Additive", "ShadowRPG/ArtTest/AdditiveSprite");
            var flash = MaterialAsset("Flash", "ShadowRPG/ArtTest/SpriteFlash");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.transform.position = new Vector3(0f, 0.6f, -10f);

            var demo = new GameObject("ArtTestDemo").AddComponent<ArtTestDemo>();
            demo.EditorAssign(hero, brute, additive, flash);
            EditorUtility.SetDirty(demo);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[ArtTest] 시험 장면 생성: " + ScenePath);
        }

        static Material MaterialAsset(string name, string shaderName)
        {
            Directory.CreateDirectory(Folder + "/Materials");
            string path = $"{Folder}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
