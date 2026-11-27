using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShadowRPG.EditorTools.Art
{
    /// <summary>
    /// 굽기 작업실: 미리보기 장면 + 정사영 카메라 + 툰/노멀 두 번 렌더 → 안쪽 선 → 축소 → 외곽선.
    /// 캐릭터·환경 굽기가 함께 씁니다.
    /// </summary>
    public sealed class BakeStudio : IDisposable
    {
        readonly BakeSettings _settings;
        readonly Shader _toonShader;
        readonly Material _normalMaterial;
        readonly Dictionary<Renderer, Material[]> _toonMaterials = new Dictionary<Renderer, Material[]>();
        readonly List<(SkinnedMeshRenderer skin, Transform proxy, Mesh mesh)> _skins = new List<(SkinnedMeshRenderer, Transform, Mesh)>();
        RenderTexture _rt;
        Texture2D _readback;

        public BakeStudio(BakeSettings settings)
        {
            _settings = settings;
            _toonShader = Shader.Find("ShadowRPG/ToonBake");
            var normalShader = Shader.Find("ShadowRPG/BakeNormalDepth");
            if (_toonShader == null || normalShader == null) throw new InvalidOperationException("굽기 셰이더(ShadowRPG/ToonBake, BakeNormalDepth)를 찾지 못했습니다.");
            _normalMaterial = new Material(normalShader);

            Scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("BakeCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, Scene);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.scene = Scene;
            Camera.enabled = false;
            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            Camera.nearClipPlane = 20f;
            Camera.farClipPlane = 40f;
            var urp = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;
            urp.antialiasing = AntialiasingMode.None;
        }

        public Scene Scene { get; }
        public Camera Camera { get; }
        public BakeSettings Settings => _settings;
        const float CameraDistance = 30f;

        public GameObject Spawn(GameObject prefab)
        {
            var instance = (GameObject)Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, Scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return instance;
        }

        public void Destroy(GameObject instance)
        {
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true)) _toonMaterials.Remove(r);
            _skins.RemoveAll(s => s.skin == null || s.skin.transform.IsChildOf(instance.transform));
            Object.DestroyImmediate(instance);
        }

        /// <summary>렌더러마다 툰 재질을 입힙니다 (원본 텍스처 유지).</summary>
        public void ApplyToon(GameObject instance, Color tint, Color rim, float rimAmount, float exposure)
        {
            var cache = new Dictionary<Texture, Material>();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials.Select(source =>
                {
                    var texture = SourceTexture(source) ?? Texture2D.whiteTexture;
                    var color = SourceColor(source);
                    if (cache.TryGetValue(texture, out var cached) && color == Color.white) return cached;
                    var material = new Material(_toonShader) { mainTexture = texture };
                    _settings.Apply(material);
                    material.SetColor("_Tint", tint * color);
                    material.SetColor("_RimColor", rim);
                    material.SetFloat("_RimAmount", rimAmount);
                    material.SetFloat("_Exposure", exposure);
                    if (color == Color.white) cache[texture] = material;
                    return material;
                }).ToArray();
                renderer.sharedMaterials = materials;
                _toonMaterials[renderer] = materials;
            }
        }

        static Texture SourceTexture(Material m)
        {
            if (m == null) return null;
            foreach (var name in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                if (m.HasProperty(name) && m.GetTexture(name) != null) return m.GetTexture(name);
            return null;
        }

        static Color SourceColor(Material m)
        {
            if (m == null) return Color.white;
            foreach (var name in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                if (m.HasProperty(name)) return m.GetColor(name);
            return Color.white;
        }

        /// <summary>에디터 수동 렌더에서는 스킨드 메시가 변형되지 않으므로 BakeMesh 대리 렌더러를 만듭니다.</summary>
        public void CreateSkinProxies(GameObject instance)
        {
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.gameObject.activeInHierarchy) continue;
                var proxy = new GameObject(skin.name + "_Baked");
                SceneManager.MoveGameObjectToScene(proxy, Scene);
                proxy.transform.SetParent(instance.transform, true);
                var mesh = new Mesh();
                proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = proxy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = skin.sharedMaterials;
                _toonMaterials[renderer] = skin.sharedMaterials;
                skin.enabled = false;
                _skins.Add((skin, proxy.transform, mesh));
            }
        }

        public void UpdateSkinProxies()
        {
            foreach (var (skin, proxy, mesh) in _skins)
            {
                if (skin == null) continue;
                skin.BakeMesh(mesh, true);
                proxy.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                var parentScale = proxy.parent != null ? proxy.parent.lossyScale : Vector3.one;
                proxy.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            }
        }

        /// <summary>
        /// 카메라를 pitch로 기울이고, 월드 점 anchor가 화면(viewport)의 anchorViewport에 오도록 놓습니다.
        /// widthMeters × heightMeters 영역을 widthPx × heightPx로 렌더합니다.
        /// </summary>
        public void Frame(float pitch, Vector3 anchor, Vector2 anchorViewport, float widthMeters, float heightMeters)
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            Camera.transform.rotation = rotation;
            Camera.orthographicSize = heightMeters * 0.5f;
            Camera.aspect = widthMeters / heightMeters;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;
            // anchor가 viewport (ax, ay)에 오려면 카메라 중심은 anchor + right*(0.5-ax)*w + up*(0.5-ay)*h
            var center = anchor + right * ((0.5f - anchorViewport.x) * widthMeters) + up * ((0.5f - anchorViewport.y) * heightMeters);
            Camera.transform.position = center - forward * CameraDistance;
        }

        /// <summary>
        /// 툰 렌더 + 노멀/깊이 렌더 → 안쪽 선 → 축소 → 바깥 외곽선. 결과는 width×height 픽셀.
        /// </summary>
        public Color32[] Capture(int width, int height, bool innerLines, bool outline)
        {
            int ss = Mathf.Max(1, _settings.supersample);
            int w = width * ss, h = height * ss;
            EnsureTargets(w, h);
            UpdateSkinProxies();

            var color = RenderPixels(w, h);
            if (innerLines && _settings.innerLineStrength > 0f)
            {
                SwapMaterials(true);
                var normals = RenderPixels(w, h);
                SwapMaterials(false);
                DrawInnerLines(color, normals, w, h, ss);
            }

            var result = Downsample(color, w, h, ss);
            if (outline && _settings.outlineWidth > 0) AddOutline(result, width, height, _settings.outlineWidth, _settings.outlineColor);
            return result;
        }

        void SwapMaterials(bool normals)
        {
            _normalMaterial.SetFloat("_Near", Camera.nearClipPlane);
            _normalMaterial.SetFloat("_Far", Camera.farClipPlane);
            foreach (var pair in _toonMaterials)
            {
                if (pair.Key == null) continue;
                pair.Key.sharedMaterials = normals ? Enumerable.Repeat(_normalMaterial, pair.Value.Length).ToArray() : pair.Value;
            }
        }

        void EnsureTargets(int w, int h)
        {
            if (_rt != null && _rt.width == w && _rt.height == h) return;
            Release();
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            _readback = new Texture2D(w, h, TextureFormat.RGBA32, false);
        }

        Color32[] RenderPixels(int w, int h)
        {
            Camera.targetTexture = _rt;
            Camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _rt;
            _readback.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            _readback.Apply();
            RenderTexture.active = previous;
            Camera.targetTexture = null;
            return _readback.GetPixels32();
        }

        /// <summary>노멀 각도·깊이가 크게 바뀌는 경계를 어둡게 (펜 선 느낌).</summary>
        void DrawInnerLines(Color32[] color, Color32[] normals, int w, int h, int ss)
        {
            float cosLimit = Mathf.Cos(_settings.innerLineAngle * Mathf.Deg2Rad);
            float depthRange = Camera.farClipPlane - Camera.nearClipPlane;
            float depthLimit = _settings.innerLineDepth / depthRange * 255f;
            float strength = _settings.innerLineStrength;
            var line = _settings.outlineColor;
            int step = Mathf.Max(1, ss / 2);
            var mark = new bool[color.Length];

            for (int y = step; y < h - step; y++)
            for (int x = step; x < w - step; x++)
            {
                int i = y * w + x;
                if (color[i].a < 128) continue;
                var n = normals[i];
                if (Edge(n, normals[i + step], cosLimit, depthLimit) || Edge(n, normals[i - step], cosLimit, depthLimit)
                    || Edge(n, normals[i + step * w], cosLimit, depthLimit) || Edge(n, normals[i - step * w], cosLimit, depthLimit))
                    mark[i] = true;
            }

            for (int i = 0; i < color.Length; i++)
            {
                if (!mark[i]) continue;
                var c = color[i];
                color[i] = new Color32(
                    (byte)Mathf.Lerp(c.r, line.r * 255f, strength),
                    (byte)Mathf.Lerp(c.g, line.g * 255f, strength),
                    (byte)Mathf.Lerp(c.b, line.b * 255f, strength), c.a);
            }
        }

        static bool Edge(Color32 a, Color32 b, float cosLimit, float depthLimit)
        {
            if (b.a == 0 && a.a == 0) return false;
            if (Mathf.Abs(a.a - b.a) > depthLimit) return a.a < b.a; // 앞쪽 픽셀만 선 (두께 1)
            float ax = a.r / 127.5f - 1f, ay = a.g / 127.5f - 1f, az = a.b / 127.5f - 1f;
            float bx = b.r / 127.5f - 1f, by = b.g / 127.5f - 1f, bz = b.b / 127.5f - 1f;
            float dot = (ax * bx + ay * by + az * bz) / Mathf.Max(0.001f, Mathf.Sqrt((ax * ax + ay * ay + az * az) * (bx * bx + by * by + bz * bz)));
            return dot < cosLimit;
        }

        /// <summary>ss×ss 평균 축소 (알파 가중) — 계단 없는 부드러운 가장자리.</summary>
        static Color32[] Downsample(Color32[] source, int w, int h, int ss)
        {
            int ow = w / ss, oh = h / ss;
            var result = new Color32[ow * oh];
            float count = ss * ss;
            for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int dy = 0; dy < ss; dy++)
                for (int dx = 0; dx < ss; dx++)
                {
                    var c = source[(y * ss + dy) * w + x * ss + dx];
                    float wgt = c.a / 255f;
                    r += c.r * wgt; g += c.g * wgt; b += c.b * wgt; a += wgt;
                }
                result[y * ow + x] = a > 0.001f
                    ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / count * 255f))
                    : new Color32(0, 0, 0, 0);
            }
            return result;
        }

        /// <summary>실루엣 바깥에 어두운 외곽선.</summary>
        public static void AddOutline(Color32[] pixels, int w, int h, int width, Color color)
        {
            var original = (Color32[])pixels.Clone();
            var outline = (Color32)color;
            int r2 = width * width + width;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (original[i].a > 200) continue;
                int strongest = 0;
                for (int dy = -width; dy <= width; dy++)
                for (int dx = -width; dx <= width; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    strongest = Math.Max(strongest, original[ny * w + nx].a);
                }
                if (strongest < 120) continue;
                // 반투명 가장자리 픽셀은 외곽선 색과 섞음
                float keep = original[i].a / 255f;
                byte alpha = (byte)Math.Max((int)original[i].a, Mathf.RoundToInt(strongest * 0.95f));
                pixels[i] = new Color32(
                    (byte)Mathf.Lerp(outline.r, original[i].r, keep * 0.5f),
                    (byte)Mathf.Lerp(outline.g, original[i].g, keep * 0.5f),
                    (byte)Mathf.Lerp(outline.b, original[i].b, keep * 0.5f), alpha);
            }
        }

        public static RectInt AlphaBounds(Color32[] pixels, int w, int h, byte threshold = 8)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a <= threshold) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            return maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public static Color32[] Crop(Color32[] pixels, int w, RectInt rect)
        {
            var result = new Color32[rect.width * rect.height];
            for (int y = 0; y < rect.height; y++)
                Array.Copy(pixels, (rect.y + y) * w + rect.x, result, y * rect.width, rect.width);
            return result;
        }

        public static void WritePng(string path, Color32[] pixels, int w, int h)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>구운 PNG 가져오기 설정. sprite = 스프라이트로 바로 쓸지 (환경), 아니면 텍스처 (캐릭터 시트).</summary>
        public static void ConfigureImport(string path, bool sprite, float pixelsPerUnit, Vector2 pivot, bool repeat = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                var ts = new TextureImporterSettings();
                importer.ReadTextureSettings(ts);
                ts.spriteAlignment = (int)SpriteAlignment.Custom;
                ts.spritePivot = pivot;
                ts.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(ts);
            }
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        void Release()
        {
            if (_rt != null)
            {
                _rt.Release();
                Object.DestroyImmediate(_rt);
                _rt = null;
            }
            if (_readback != null)
            {
                Object.DestroyImmediate(_readback);
                _readback = null;
            }
        }

        public void Dispose()
        {
            Release();
            Object.DestroyImmediate(_normalMaterial);
            EditorSceneManager.ClosePreviewScene(Scene);
        }
    }
}
