using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Runtime.Art;
using UnityEditor;
using UnityEngine;

namespace ShadowRPG.EditorTools.Art
{
    /// <summary>
    /// 캐릭터 3D 모델 → 8방향(5방향 + 반전) 스프라이트 시트. 결과: Baked/Characters/{키}/*.png + SpriteSet.asset
    /// </summary>
    public static class CharacterBaker
    {
        public const string OutputRoot = "Assets/_Game/Art/Baked/Characters";

        public static DirectionalSpriteSet Bake(CharacterBakeSpec spec, BakeSettings settings, BakeStudio studio)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.modelPath);
            if (model == null)
            {
                Debug.LogError($"[Bake] {spec.key}: 모델 없음 {spec.modelPath}");
                return null;
            }
            var clips = AssetDatabase.LoadAllAssetsAtPath(spec.modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());

            var instance = studio.Spawn(model);
            try
            {
                foreach (var animator in instance.GetComponentsInChildren<Animator>()) animator.enabled = false;
                ApplyParts(instance, spec);
                Attach(instance, spec, studio);
                studio.ApplyToon(instance, spec.tint, spec.rimColor, spec.rimAmount, settings.exposure);
                studio.CreateSkinProxies(instance);

                int cell = settings.characterCell;
                float frameMeters = cell / settings.MetersToPixels;
                var rows = settings.mirrorDirections ? DirectionalSpriteSet.MirroredRows : Enumerable.Range(0, DirectionalSpriteSet.Directions).ToArray();
                string folder = $"{OutputRoot}/{SafeName(spec.key)}";

                var set = LoadOrCreateSet(folder, spec.key);
                set.characterName = spec.key;
                set.cellSize = cell;
                set.pixelsPerUnit = settings.pixelsPerUnit;
                set.pivot = new Vector2(0.5f, settings.characterPivotY);
                set.mirrored = settings.mirrorDirections;
                set.clips.Clear();
                float tallest = 0f;

                foreach (var clipSpec in spec.clips)
                {
                    if (!clips.TryGetValue(clipSpec.source, out var clip))
                    {
                        Debug.LogWarning($"[Bake] {spec.key}: 동작 '{clipSpec.source}' 없음");
                        continue;
                    }
                    float start = clip.length * Mathf.Clamp01(clipSpec.range.x);
                    float end = clip.length * Mathf.Clamp01(clipSpec.range.y);
                    float length = Mathf.Max(0.05f, end - start);
                    float playLength = length / Mathf.Max(0.1f, clipSpec.speed);
                    int frames = Mathf.Clamp(Mathf.RoundToInt(playLength * settings.sampleFps), settings.minFrames, settings.maxFrames);
                    var atlas = new Color32[frames * cell * rows.Length * cell];
                    int atlasWidth = frames * cell, atlasHeight = rows.Length * cell;

                    for (int r = 0; r < rows.Length; r++)
                    {
                        float screenAngle = rows[r] * 45f;
                        for (int f = 0; f < frames; f++)
                        {
                            float t = start + (clipSpec.loop ? length * f / frames : length * f / Mathf.Max(1, frames - 1));
                            Pose(instance, clip, t, screenAngle, spec.forwardYaw);
                            studio.Frame(settings.cameraPitch, Vector3.zero, new Vector2(0.5f, settings.characterPivotY), frameMeters, frameMeters);
                            var pixels = studio.Capture(cell, cell, true, true);
                            var bounds = BakeStudio.AlphaBounds(pixels, cell, cell);
                            tallest = Mathf.Max(tallest, bounds.yMax - cell * settings.characterPivotY);
                            Blit(pixels, cell, atlas, atlasWidth, f * cell, atlasHeight - (r + 1) * cell);
                        }
                    }

                    string path = $"{folder}/{clipSpec.label}.png";
                    BakeStudio.WritePng(path, atlas, atlasWidth, atlasHeight);
                    BakeStudio.ConfigureImport(path, false, settings.pixelsPerUnit, Vector2.zero);
                    set.clips.Add(new DirectionalSpriteSet.Clip
                    {
                        name = clipSpec.label,
                        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                        frames = frames,
                        framesPerSecond = frames / playLength,
                        loop = clipSpec.loop,
                        impactFrame = clipSpec.impact >= 0f ? Mathf.Clamp(Mathf.RoundToInt(clipSpec.impact * (frames - 1)), 0, frames - 1) : -1
                    });
                }

                set.heightUnits = tallest / settings.pixelsPerUnit;
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Bake] {spec.key}: 동작 {set.clips.Count}개 × {rows.Length}방향 (키 {set.heightUnits:0.00}유닛)");
                return set;
            }
            finally
            {
                studio.Destroy(instance);
            }
        }

        static void ApplyParts(GameObject instance, CharacterBakeSpec spec)
        {
            var optional = new HashSet<string>(spec.optionalParts);
            var show = new HashSet<string>(spec.showParts);
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (!optional.Contains(t.name)) continue;
                t.gameObject.SetActive(show.Contains(t.name));
            }
        }

        static void Attach(GameObject instance, CharacterBakeSpec spec, BakeStudio studio)
        {
            foreach (var attachment in spec.attachments)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(attachment.assetPath);
                var bone = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == attachment.bone);
                if (prefab == null || bone == null)
                {
                    Debug.LogWarning($"[Bake] {spec.key}: 부착 실패 {attachment.assetPath} → {attachment.bone}");
                    continue;
                }
                var item = studio.Spawn(prefab);
                item.transform.SetParent(bone, false);
                item.transform.localPosition = attachment.localPosition;
                item.transform.localRotation = Quaternion.Euler(attachment.localEuler);
                item.transform.localScale = Vector3.one * attachment.scale;
            }
        }

        /// <summary>동작 샘플링 + 화면 방향 회전. 화면 각도 θ(오른쪽 0°, 반시계) → +Z 정면 모델의 Y회전 = 90° − θ.</summary>
        static void Pose(GameObject instance, AnimationClip clip, float time, float screenAngle, float forwardYaw)
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clip.SampleAnimation(instance, time);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f - screenAngle + forwardYaw, 0f));
        }

        static void Blit(Color32[] cell, int size, Color32[] atlas, int atlasWidth, int x, int y)
        {
            for (int row = 0; row < size; row++)
                System.Array.Copy(cell, row * size, atlas, (y + row) * atlasWidth + x, size);
        }

        public static string SafeName(string key) => key.Replace('/', '_');

        static DirectionalSpriteSet LoadOrCreateSet(string folder, string key)
        {
            System.IO.Directory.CreateDirectory(folder);
            string path = $"{folder}/{SafeName(key)}_SpriteSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSet>(path);
            if (set != null) return set;
            set = ScriptableObject.CreateInstance<DirectionalSpriteSet>();
            AssetDatabase.CreateAsset(set, path);
            return set;
        }
    }
}
