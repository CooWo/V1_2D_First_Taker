using System.IO;
using ShadowRPG.Runtime.Art;
using UnityEditor;
using UnityEngine;

namespace ShadowRPG.EditorTools.Art
{
    /// <summary>
    /// 3D 환경 조각 → 2D 스프라이트 (바닥은 위에서, 벽·소품은 캐릭터와 같은 각도로). 결과: Baked/Environment/{이름}/
    /// </summary>
    public static class EnvironmentBaker
    {
        public const string OutputRoot = "Assets/_Game/Art/Baked/Environment";

        public static EnvironmentTheme Bake(EnvironmentBakeSpec spec, BakeSettings settings, BakeStudio studio)
        {
            string folder = $"{OutputRoot}/{spec.themeName}";
            Directory.CreateDirectory(folder);
            var theme = LoadOrCreate(folder, spec.themeName);
            theme.themeName = spec.themeName;
            theme.pixelsPerUnit = settings.pixelsPerUnit;
            theme.floorTiles.Clear();
            theme.wallFaces.Clear();
            theme.props.Clear();
            theme.floorDecals.Clear();

            float px = settings.MetersToPixels;
            int floorPx = Mathf.RoundToInt(spec.floorTileMeters * px);

            // ---- 바닥 (위에서 수직으로) ----
            int index = 0;
            foreach (var piece in spec.floors)
            {
                int variants = piece.rotateVariants ? 4 : 1;
                for (int v = 0; v < variants; v++)
                {
                    var instance = SpawnPiece(studio, piece.assetPath, Vector3.one, v * 90f, settings.environmentExposure, settings.environmentRim * 0.3f);
                    if (instance == null) break;
                    studio.Frame(90f, Vector3.zero, new Vector2(0.5f, 0.5f), spec.floorTileMeters, spec.floorTileMeters);
                    var pixels = studio.Capture(floorPx, floorPx, false, false);
                    studio.Destroy(instance);
                    for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255; // 바닥은 불투명
                    string path = $"{folder}/floor_{index++:00}.png";
                    BakeStudio.WritePng(path, pixels, floorPx, floorPx);
                    BakeStudio.ConfigureImport(path, true, settings.pixelsPerUnit, Vector2.zero);
                    theme.floorTiles.Add(new EnvironmentTheme.WeightedSprite { sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path), weight = piece.weight / variants });
                }
            }

            // ---- 벽 블록 (아이소메트릭 낮은 벽: 한 칸 크기 블록, 발 = 칸 중심) ----
            float pitch = settings.cameraPitch;
            float wallHeight = 4f * settings.wallHeightScale;
            float cellMeters = spec.floorTileMeters;
            index = 0;
            foreach (var piece in spec.wallFaces)
            {
                // KayKit 벽(가로 4m, 두께 1m)을 한 칸(2m×2m) 블록으로 늘려 굽기
                var scale = new Vector3(cellMeters / spec.wallPieceMeters, settings.wallHeightScale, cellMeters);
                var sprite = BakeUpright(studio, settings, piece.assetPath, scale, 0f, folder, $"wall_{index++:00}", false);
                if (sprite != null) theme.wallFaces.Add(new EnvironmentTheme.WeightedSprite { sprite = sprite, weight = piece.weight });
            }
            theme.wallFaceHeight = wallHeight * Mathf.Cos(pitch * Mathf.Deg2Rad) * settings.unitsPerMeter;
            theme.wallCap = null;

            // ---- 소품 (캐릭터와 같은 각도) ----
            foreach (var prop in spec.props)
            {
                string propName = Path.GetFileNameWithoutExtension(prop.assetPath).Replace(".gltf", "") + (prop.yaw != 0f ? "_r" + Mathf.RoundToInt(prop.yaw) : "");
                var sprite = BakeUpright(studio, settings, prop.assetPath, Vector3.one * prop.scale, prop.yaw, folder, "prop_" + propName, true);
                if (sprite == null) continue;
                theme.props.Add(new EnvironmentTheme.Prop
                {
                    name = Path.GetFileNameWithoutExtension(prop.assetPath).Replace(".gltf", ""),
                    sprite = sprite,
                    placement = prop.placement,
                    weight = prop.weight,
                    blocks = prop.blocks,
                    lightColor = prop.lightColor,
                    lightRadius = prop.lightRadius,
                    lightIntensity = prop.lightIntensity,
                    lightHeight = prop.lightHeight,
                    flicker = prop.flicker
                });
            }

            theme.floorTint = spec.floorTint;
            theme.wallTint = spec.wallTint;
            theme.voidColor = spec.voidColor;
            theme.ambientColor = spec.ambientColor;
            theme.ambientIntensity = spec.ambientIntensity;
            theme.edgePropChance = spec.edgePropChance;
            theme.wallPropChance = spec.wallPropChance;
            theme.scatterPropChance = spec.scatterPropChance;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Bake] 환경 {spec.themeName}: 바닥 {theme.floorTiles.Count} · 벽 {theme.wallFaces.Count} · 소품 {theme.props.Count}");
            return theme;
        }

        /// <summary>서 있는 조각(벽 블록·소품)을 환경 카메라(아이소메트릭)로 굽고, 발(바닥 중심)을 피벗으로.</summary>
        static Sprite BakeUpright(BakeStudio studio, BakeSettings settings, string assetPath, Vector3 scale, float yaw, string folder, string fileName, bool outline)
        {
            var instance = SpawnPiece(studio, assetPath, scale, yaw, settings.environmentExposure, settings.environmentRim);
            if (instance == null) return null;
            var bounds = Bounds(instance);
            float px = settings.MetersToPixels;
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.6f + 0.3f;
            float width = extent * 2f;
            float height = bounds.max.y + extent + 0.4f;
            int w = Mathf.CeilToInt(width * px), h = Mathf.CeilToInt(height * px);
            float pivotY = extent / height;
            var anchor = new Vector3(bounds.center.x, 0f, bounds.center.z);
            studio.Frame(settings.cameraPitch, anchor, new Vector2(0.5f, pivotY), width, height, settings.environmentYaw);
            var pixels = studio.Capture(w, h, true, outline);
            studio.Destroy(instance);

            var crop = BakeStudio.AlphaBounds(pixels, w, h);
            if (crop.width == 0) return null;
            int x0 = Mathf.Max(0, crop.x - 1), y0 = Mathf.Max(0, crop.y - 1);
            int x1 = Mathf.Min(w, crop.xMax + 1), y1 = Mathf.Min(h, crop.yMax + 1);
            crop = new RectInt(x0, y0, x1 - x0, y1 - y0);
            var cropped = BakeStudio.Crop(pixels, w, crop);
            var pivot = new Vector2((w * 0.5f - crop.x) / crop.width, (h * pivotY - crop.y) / crop.height);

            string path = $"{folder}/{fileName}.png";
            BakeStudio.WritePng(path, cropped, crop.width, crop.height);
            BakeStudio.ConfigureImport(path, true, settings.pixelsPerUnit, pivot);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static GameObject SpawnPiece(BakeStudio studio, string assetPath, Vector3 scale, float yaw, float exposure, float rim)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Bake] 조각 없음: " + assetPath);
                return null;
            }
            var instance = studio.Spawn(prefab);
            instance.transform.localScale = scale;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            studio.ApplyToon(instance, Color.white, new Color(0.5f, 0.6f, 0.9f), rim, exposure);
            return instance;
        }

        static Bounds Bounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static EnvironmentTheme LoadOrCreate(string folder, string name)
        {
            string path = $"{folder}/{name}_Theme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<EnvironmentTheme>(path);
            if (theme != null) return theme;
            theme = ScriptableObject.CreateInstance<EnvironmentTheme>();
            AssetDatabase.CreateAsset(theme, path);
            return theme;
        }
    }
}
