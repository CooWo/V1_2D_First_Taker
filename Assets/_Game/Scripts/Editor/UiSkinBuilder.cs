using System.IO;
using ShadowRPG.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowRPG.EditorTools
{
    /// <summary>Kenney RPG UI + Noto Sans KR로 기본 UI 겉모양 에셋을 만듭니다 (9-slice 경계 설정 포함).</summary>
    public static class UiSkinBuilder
    {
        const string Kenney = "Assets/_Game/Art/ThirdParty/Kenney_UI_RPG/";
        const string Path = "Assets/_Game/Resources/UI/UiSkin.asset";

        [MenuItem("ShadowRPG/UI/Build UI Skin")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/_Game/Resources/UI");
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(Path);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(skin, Path);
            }
            skin.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Resources/Fonts/NotoSansKR.ttf");
            skin.panel = Sliced("panel_brown", 14);
            skin.inset = Sliced("panelInset_brown", 12);
            skin.button = Sliced("buttonLong_grey", 14);
            skin.buttonPressed = Sliced("buttonLong_grey_pressed", 14);
            skin.roundButton = Sliced("buttonRound_grey", 0);
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            Debug.Log("[UI] UI 겉모양 생성: " + Path);
        }

        static Sprite Sliced(string name, int border)
        {
            string path = Kenney + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
