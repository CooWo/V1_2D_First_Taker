using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShadowRPG.ArtTest.EditorTools
{
    /// <summary>
    /// 3D 캐릭터 → 8방향 2D 스프라이트 굽기 (Hades·디아블로 2식 사전 렌더링).
    /// 2배 해상도로 렌더 후 축소(부드러운 가장자리), 알파 경계에 어두운 외곽선을 그려 2D 일러스트 느낌을 냅니다.
    /// AI로 만든 모델도 BakeSpec만 추가하면 같은 과정으로 구울 수 있습니다.
    /// </summary>
    public static class CharacterSpriteBaker
    {
        const string ModelFolder = "Assets/_Game/ArtTest/ThirdParty/Quaternius_RPGCharacters";
        const string OutputFolder = "Assets/_Game/ArtTest/Baked";
        const int RenderSize = 512;
        const int CellSize = 256;
        const int MaxFrames = 16;
        const float SampleFps = 12f;
        const float CameraPitch = 40f;
        static readonly Color OutlineColor = new Color(0.03f, 0.02f, 0.05f, 1f);

        public sealed class ClipSpec
        {
            public string Label;
            public string Source;
            public bool Loop = true;
            public float ImpactFraction = -1f;
        }

        public sealed class BakeSpec
        {
            public string Name;
            public string ModelPath;
            public string BodyTexture;
            public string WeaponTexture;
            public string WeaponKeyword;
            public Color Tint = Color.white;
            public Color RimColor = new Color(0.35f, 0.65f, 1f);
            public List<ClipSpec> Clips = new List<ClipSpec>();
        }

        public static BakeSpec[] DefaultSpecs() => new[]
        {
            new BakeSpec
            {
                Name = "Hero", ModelPath = ModelFolder + "/Rogue.fbx",
                BodyTexture = ModelFolder + "/Textures/Rogue_Texture.png", WeaponTexture = ModelFolder + "/Textures/Rogue_Dagger_Texture.png", WeaponKeyword = "Dagger",
                Tint = new Color(0.95f, 0.95f, 1f), RimColor = new Color(0.3f, 0.65f, 1f),
                Clips =
                {
                    new ClipSpec { Label = "idle", Source = "CharacterArmature|Idle" },
                    new ClipSpec { Label = "run", Source = "CharacterArmature|Run" },
                    new ClipSpec { Label = "attack", Source = "CharacterArmature|Dagger_Attack", Loop = false, ImpactFraction = 0.42f },
                    new ClipSpec { Label = "attack2", Source = "CharacterArmature|Dagger_Attack2", Loop = false, ImpactFraction = 0.4f },
                    new ClipSpec { Label = "hit", Source = "CharacterArmature|RecieveHit", Loop = false },
                    new ClipSpec { Label = "death", Source = "CharacterArmature|Death", Loop = false }
                }
            },
            new BakeSpec
            {
                Name = "Brute", ModelPath = ModelFolder + "/Warrior.fbx",
                BodyTexture = ModelFolder + "/Textures/Warrior_Texture.png", WeaponTexture = ModelFolder + "/Textures/Warrior_Sword_Texture.png", WeaponKeyword = "Sword",
                Tint = new Color(0.95f, 0.7f, 0.68f), RimColor = new Color(1f, 0.35f, 0.25f),
                Clips =
                {
                    new ClipSpec { Label = "idle", Source = "CharacterArmature|Idle_Weapon" },
                    new ClipSpec { Label = "run", Source = "CharacterArmature|Run_Weapon" },
                    new ClipSpec { Label = "attack", Source = "CharacterArmature|Sword_Attack", Loop = false, ImpactFraction = 0.5f },
                    new ClipSpec { Label = "hit", Source = "CharacterArmature|RecieveHit", Loop = false },
                    new ClipSpec { Label = "death", Source = "CharacterArmature|Death", Loop = false }
                }
            }
        };

        [MenuItem("ShadowRPG/Art Test/Bake Characters")]
        public static void BakeAll()
        {
            foreach (var spec in DefaultSpecs()) Bake(spec);
        }

        public static DirectionalSpriteSet Bake(BakeSpec spec)
        {
            Directory.CreateDirectory(OutputFolder);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(spec.ModelPath).OfType<AnimationClip>().ToDictionary(c => c.name);
            var shader = Shader.Find("ShadowRPG/ArtTest/ToonBake");
            if (modelAsset == null || shader == null) throw new InvalidOperationException("모델 또는 셰이더를 찾지 못했습니다.");

            var scene = EditorSceneManager.NewPreviewScene();
            var instance = UnityEngine.Object.Instantiate(modelAsset);
            SceneManager.MoveGameObjectToScene(instance, scene);
            ApplyToonMaterials(instance, spec, shader);
            var skins = CreateSkinProxies(instance);

            var bounds = ComputeBounds(instance);
            float height = bounds.size.y;
            var focus = new Vector3(0f, height * 0.45f, 0f);

            var cameraObject = new GameObject("BakeCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = height * 0.66f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.transform.position = focus + Quaternion.Euler(CameraPitch, 0f, 0f) * Vector3.back * 30f;
            cameraObject.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            var urp = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;

            var rt = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            var readback = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);

            FitToSilhouette(camera, instance, clips, spec, rt, readback, skins);
            var groundViewport = camera.WorldToViewportPoint(Vector3.zero);
            var set = LoadOrCreateSet(spec.Name);
            set.characterName = spec.Name;
            set.cellSize = CellSize;
            // 모델이 셀 높이의 약 75%를 차지 → 게임에서 키 약 1.7유닛이 되도록
            set.pixelsPerUnit = CellSize * 0.8f / 1.7f; // 실루엣이 셀의 80% → 키 약 1.7유닛
            set.pivot = new Vector2(groundViewport.x, groundViewport.y);
            set.clips.Clear();

            try
            {
                foreach (var clipSpec in spec.Clips)
                {
                    if (!clips.TryGetValue(clipSpec.Source, out var clip))
                    {
                        Debug.LogWarning($"[ArtTest] {spec.Name}: '{clipSpec.Source}' 없음");
                        continue;
                    }
                    int frames = Mathf.Clamp(Mathf.RoundToInt(clip.length * SampleFps), 4, MaxFrames);
                    var atlas = new Texture2D(frames * CellSize, DirectionalSpriteSet.Directions * CellSize, TextureFormat.RGBA32, false);
                    atlas.SetPixels32(new Color32[atlas.width * atlas.height]);

                    for (int d = 0; d < DirectionalSpriteSet.Directions; d++)
                    {
                        float screenAngle = d * 45f;
                        for (int f = 0; f < frames; f++)
                        {
                            float t = clipSpec.Loop ? clip.length * f / frames : clip.length * f / Mathf.Max(1, frames - 1);
                            Pose(instance, clip, t, screenAngle, skins);

                            camera.Render();
                            RenderTexture.active = rt;
                            readback.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                            readback.Apply();
                            RenderTexture.active = null;

                            var cell = Downsample(readback.GetPixels32(), RenderSize);
                            AddOutline(cell, CellSize);
                            atlas.SetPixels32(f * CellSize, atlas.height - (d + 1) * CellSize, CellSize, CellSize, cell);
                        }
                    }

                    string path = $"{OutputFolder}/{spec.Name}_{clipSpec.Label}.png";
                    File.WriteAllBytes(path, atlas.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(atlas);
                    ConfigureTexture(path);

                    set.clips.Add(new DirectionalSpriteSet.Clip
                    {
                        name = clipSpec.Label,
                        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                        frames = frames,
                        framesPerSecond = frames / clip.length,
                        loop = clipSpec.Loop,
                        impactFrame = clipSpec.ImpactFraction >= 0f ? Mathf.RoundToInt(clipSpec.ImpactFraction * (frames - 1)) : -1
                    });
                }
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(readback);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArtTest] {spec.Name}: {set.clips.Count}개 동작 × 8방향 굽기 완료");
            return set;
        }

        static void ApplyToonMaterials(GameObject instance, BakeSpec spec, Shader shader)
        {
            var body = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.BodyTexture);
            var weapon = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.WeaponTexture);
            var cache = new Dictionary<Texture, Material>();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var source = renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
                bool isWeapon = !string.IsNullOrEmpty(spec.WeaponKeyword) && renderer.name.Contains(spec.WeaponKeyword);
                var texture = source != null ? source : isWeapon && weapon != null ? weapon : body;
                if (texture == null) texture = Texture2D.whiteTexture;
                if (!cache.TryGetValue(texture, out var material))
                {
                    material = new Material(shader) { mainTexture = texture };
                    material.SetColor("_Tint", spec.Tint);
                    material.SetColor("_RimColor", spec.RimColor);
                    cache[texture] = material;
                }
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }
        }

        /// <summary>
        /// 실제 렌더된 실루엣 높이를 재서, 캐릭터가 칸 높이의 약 80%를 차지하도록 카메라를 맞춥니다.
        /// (모델 단위·뼈대 배율이 제각각이어도 같은 크기로 구워지게)
        /// </summary>
        static void FitToSilhouette(Camera camera, GameObject instance, Dictionary<string, AnimationClip> clips, BakeSpec spec, RenderTexture rt, Texture2D readback, List<(SkinnedMeshRenderer skin, Transform proxy, Mesh mesh)> skins)
        {
            var first = spec.Clips.Select(c => clips.TryGetValue(c.Source, out var clip) ? clip : null).FirstOrDefault(c => c != null);
            for (int pass = 0; pass < 2; pass++)
            {
                Pose(instance, first, 0f, 270f, skins);
                camera.Render();
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                readback.Apply();
                RenderTexture.active = null;

                var pixels = readback.GetPixels32();
                int minY = RenderSize, maxY = -1, minX = RenderSize, maxX = -1;
                for (int y = 0; y < RenderSize; y++)
                for (int x = 0; x < RenderSize; x++)
                {
                    if (pixels[y * RenderSize + x].a < 20) continue;
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                }
                if (maxY < 0) return;

                // 실루엣 중심을 화면 가운데로, 높이를 80%로
                var size = camera.orthographicSize;
                float worldPerPixel = size * 2f / RenderSize;
                var centerOffset = new Vector2((minX + maxX) * 0.5f - RenderSize * 0.5f, (minY + maxY) * 0.5f - RenderSize * 0.5f) * worldPerPixel;
                camera.transform.position += camera.transform.right * centerOffset.x + camera.transform.up * centerOffset.y;
                float silhouette = (maxY - minY) * worldPerPixel;
                camera.orthographicSize = silhouette / 0.8f * 0.5f;
            }
        }

        /// <summary>
        /// 에디터 수동 렌더에서는 스킨드 메시가 자동 변형되지 않으므로,
        /// 매 프레임 BakeMesh로 변형된 메시를 만들어 대신 그립니다.
        /// </summary>
        static List<(SkinnedMeshRenderer skin, Transform proxy, Mesh mesh)> CreateSkinProxies(GameObject instance)
        {
            var result = new List<(SkinnedMeshRenderer, Transform, Mesh)>();
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var proxy = new GameObject(skin.name + "_Baked");
                SceneManager.MoveGameObjectToScene(proxy, instance.scene);
                var mesh = new Mesh();
                proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                proxy.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
                result.Add((skin, proxy.transform, mesh));
            }
            return result;
        }

        /// <summary>
        /// 동작 샘플링 + 화면 방향으로 회전. 모델 정면은 -Z(카메라 쪽)이므로
        /// 화면 각도 θ(오른쪽 0°, 반시계)에 대한 Y회전은 -90° - θ.
        /// </summary>
        static void Pose(GameObject instance, AnimationClip clip, float time, float screenAngle, List<(SkinnedMeshRenderer skin, Transform proxy, Mesh mesh)> skins)
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clip?.SampleAnimation(instance, time);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, -90f - screenAngle, 0f));
            foreach (var (skin, proxy, mesh) in skins)
            {
                skin.BakeMesh(mesh, false); // 이 모델은 스킨 트랜스폼 배율(100)이 이미 뼈에 반영되어 있어 배율 제외
                proxy.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                proxy.localScale = Vector3.one;
            }
        }

        static Bounds ComputeBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        /// <summary>2×2 평균 축소 (알파 가중) — 계단 없는 부드러운 외곽.</summary>
        static Color32[] Downsample(Color32[] source, int size)
        {
            int half = size / 2;
            var result = new Color32[half * half];
            for (int y = 0; y < half; y++)
            for (int x = 0; x < half; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    var c = source[(y * 2 + dy) * size + x * 2 + dx];
                    float w = c.a / 255f;
                    r += c.r * w; g += c.g * w; b += c.b * w; a += w;
                }
                result[y * half + x] = a > 0.001f
                    ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / 4f * 255f))
                    : new Color32(0, 0, 0, 0);
            }
            return result;
        }

        /// <summary>실루엣 바깥 1~2픽셀에 어두운 외곽선.</summary>
        static void AddOutline(Color32[] pixels, int size)
        {
            var original = (Color32[])pixels.Clone();
            var outline = (Color32)OutlineColor;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (original[i].a > 140) continue;
                int strongest = 0;
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (dx * dx + dy * dy > 5) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                    strongest = Math.Max(strongest, original[ny * size + nx].a);
                }
                if (strongest < 160) continue;
                byte alpha = (byte)Math.Max((int)original[i].a, 235);
                pixels[i] = new Color32(outline.r, outline.g, outline.b, alpha);
            }
        }

        static void ConfigureTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
        }

        static DirectionalSpriteSet LoadOrCreateSet(string name)
        {
            string path = $"{OutputFolder}/{name}_SpriteSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSet>(path);
            if (set != null) return set;
            set = ScriptableObject.CreateInstance<DirectionalSpriteSet>();
            AssetDatabase.CreateAsset(set, path);
            return set;
        }
    }
}
