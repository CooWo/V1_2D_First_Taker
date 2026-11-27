using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowRPG.Runtime.Art;
using UnityEditor;
using UnityEngine;

namespace ShadowRPG.EditorTools.Art
{
    /// <summary>
    /// 아트 파이프라인 메뉴: 기본 설명서 생성 → 전부 굽기 → 카탈로그(외형·환경·이펙트) 갱신.
    /// 아트를 바꿀 때는 설명서(Specs 폴더) 또는 카탈로그 항목만 고치면 됩니다.
    /// </summary>
    public static class ArtPipelineMenu
    {
        public const string BakeFolder = "Assets/_Game/Art/Bake";
        public const string SpecFolder = BakeFolder + "/Specs";
        public const string ResourcesFolder = "Assets/_Game/Resources/Art";
        const string KayKit = "Assets/_Game/Art/ThirdParty/KayKit";
        const string Heroes = KayKit + "/Adventurers/Characters/gltf/";
        const string Skeletons = KayKit + "/Skeletons/Characters/gltf/";
        const string SkeletonGear = KayKit + "/Skeletons/Assets/gltf/";
        const string Dungeon = KayKit + "/Dungeon/gltf/";
        const string Halloween = KayKit + "/Halloween/Assets/gltf/";
        const string Particles = "Assets/_Game/Art/ThirdParty/Kenney_Particles/";

        [MenuItem("ShadowRPG/Art/1. Create Default Specs")]
        public static void CreateDefaultSpecs()
        {
            Directory.CreateDirectory(SpecFolder);
            Settings();
            foreach (var spec in DefaultCharacterSpecs()) SaveSpec(spec, $"{SpecFolder}/Char_{CharacterBaker.SafeName(spec.key)}.asset");
            foreach (var spec in DefaultEnvironmentSpecs()) SaveSpec(spec, $"{SpecFolder}/Env_{spec.themeName}.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[Art] 기본 굽기 설명서 생성: " + SpecFolder);
        }

        [MenuItem("ShadowRPG/Art/2. Bake All (Characters + Environment)")]
        public static void BakeAll()
        {
            BakeCharacters(null);
            BakeEnvironments();
            BuildFxCatalog();
        }

        /// <summary>filter가 있으면 키에 그 문자열이 들어간 캐릭터만 굽습니다.</summary>
        public static void BakeCharacters(string filter)
        {
            var settings = Settings();
            var specs = LoadAll<CharacterBakeSpec>(SpecFolder);
            if (specs.Count == 0)
            {
                CreateDefaultSpecs();
                specs = LoadAll<CharacterBakeSpec>(SpecFolder);
            }
            var catalog = LoadOrCreate<AppearanceCatalog>($"{ResourcesFolder}/AppearanceCatalog.asset");
            using (var studio = new BakeStudio(settings))
            {
                foreach (var spec in specs)
                {
                    if (!string.IsNullOrEmpty(filter) && !spec.key.Contains(filter)) continue;
                    var set = CharacterBaker.Bake(spec, settings, studio);
                    if (set != null) catalog.Set(spec.key, set, spec.displayScale, Color.white);
                }
            }
            catalog.fallbackKey = "knight/sword_shield";
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void BakeEnvironments()
        {
            var settings = Settings();
            var specs = LoadAll<EnvironmentBakeSpec>(SpecFolder);
            if (specs.Count == 0)
            {
                CreateDefaultSpecs();
                specs = LoadAll<EnvironmentBakeSpec>(SpecFolder);
            }
            var catalog = LoadOrCreate<EnvironmentCatalog>($"{ResourcesFolder}/EnvironmentCatalog.asset");
            using (var studio = new BakeStudio(settings))
            {
                foreach (var spec in specs)
                {
                    var theme = EnvironmentBaker.Bake(spec, settings, studio);
                    if (spec.themeName == "Town") catalog.town = theme;
                    else if (!catalog.actThemes.Contains(theme)) catalog.actThemes.Add(theme);
                }
            }
            if (catalog.actTints.Count == 0)
                catalog.actTints = new List<Color>
                {
                    new Color(0.78f, 0.80f, 0.92f), // ACT 1 차가운 회청
                    new Color(0.80f, 0.74f, 0.70f), // ACT 2 잿빛 갈색
                    new Color(0.66f, 0.78f, 0.92f), // ACT 3 얼음 푸름
                    new Color(0.92f, 0.66f, 0.58f)  // ACT 4 타는 붉음
                };
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("ShadowRPG/Art/3. Build Fx Catalog")]
        public static void BuildFxCatalog()
        {
            EnsureMaterials();
            var catalog = LoadOrCreate<FxCatalog>($"{ResourcesFolder}/FxCatalog.asset");
            catalog.slash = Sprites("slash_01", "slash_02", "slash_03", "slash_04");
            catalog.spark = Sprites("spark_01", "spark_02", "spark_03", "spark_04", "spark_07");
            catalog.star = Sprites("star_04", "star_06", "star_07", "star_08");
            catalog.glow = Sprites("light_01", "light_02", "circle_05");
            catalog.ring = Sprites("circle_01", "circle_02", "circle_03");
            catalog.smoke = Sprites("smoke_01", "smoke_02", "smoke_03", "smoke_04", "smoke_05", "smoke_06", "smoke_07");
            catalog.flame = Sprites("flame_01", "flame_02", "flame_03", "flame_04", "fire_01", "fire_02");
            catalog.magic = Sprites("magic_01", "magic_02", "magic_03", "magic_04", "magic_05", "twirl_01", "twirl_02", "twirl_03");
            catalog.trace = Sprites("trace_01", "trace_02", "trace_03", "trace_04", "trace_05");
            catalog.scorch = Sprites("scorch_01", "scorch_02", "scorch_03");
            catalog.groundShadow = Sprites("circle_05").FirstOrDefault();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[Art] 이펙트 카탈로그 갱신 (Kenney Particle Pack)");
        }

        /// <summary>이펙트·플래시 재질을 Resources에 만들어 빌드에 셰이더가 포함되게 합니다.</summary>
        public static void EnsureMaterials()
        {
            Directory.CreateDirectory(ResourcesFolder + "/Materials");
            foreach (var (name, shader) in new[] { ("Additive", "ShadowRPG/AdditiveSprite"), ("Flash", "ShadowRPG/SpriteFlash") })
            {
                string path = $"{ResourcesFolder}/Materials/{name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;
                AssetDatabase.CreateAsset(new Material(Shader.Find(shader)), path);
            }
        }

        static Sprite[] Sprites(params string[] names)
        {
            var list = new List<Sprite>();
            foreach (var name in names)
            {
                string path = Particles + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 512f)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 512f;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) list.Add(sprite);
            }
            return list.ToArray();
        }

        // ---------------- 기본 설명서 ----------------

        static BakeSettings Settings() => LoadOrCreate<BakeSettings>($"{BakeFolder}/BakeSettings.asset");

        static CharacterBakeSpec.ClipSpec C(string label, string source, bool loop = true, float impact = -1f, float speed = 1f, float start = 0f, float end = 1f)
            => new CharacterBakeSpec.ClipSpec { label = label, source = source, loop = loop, impact = impact, speed = speed, range = new Vector2(start, end) };

        static List<CharacterBakeSpec.ClipSpec> Common(string idle, string attack, float attackImpact, string attack2, float attack2Impact, bool skeleton = false)
        {
            var list = new List<CharacterBakeSpec.ClipSpec>
            {
                C("idle", idle),
                C("run", skeleton ? "Running_C" : "Running_A", speed: 1.1f),
                C("attack", attack, false, attackImpact, 1.45f),
                C("attack2", attack2, false, attack2Impact, 1.45f),
                C("cast", "Spellcast_Shoot", false, 0.35f, 1.3f),
                C("dodge", "Dodge_Forward", false, -1f, 1.2f),
                C("hit", "Hit_A", false, -1f, 1.6f),
                C("death", skeleton ? "Death_C_Skeletons" : "Death_A", false)
            };
            if (skeleton) list.Add(C("spawn", "Spawn_Ground_Skeletons", false, -1f, 1.3f));
            return list;
        }

        static readonly string[] KnightParts = { "1H_Sword_Offhand", "Badge_Shield", "Rectangle_Shield", "Round_Shield", "Spike_Shield", "1H_Sword", "2H_Sword", "Knight_Helmet", "Knight_Cape" };
        static readonly string[] MageParts = { "Spellbook", "Spellbook_open", "1H_Wand", "2H_Staff", "Mage_Hat", "Mage_Cape" };
        static readonly string[] RogueParts = { "Knife_Offhand", "1H_Crossbow", "2H_Crossbow", "Knife", "Throwable", "Rogue_Cape" };
        static readonly string[] BarbarianParts = { "1H_Axe_Offhand", "Barbarian_Round_Shield", "1H_Axe", "2H_Axe", "Mug", "Barbarian_Hat", "Barbarian_Cape" };

        static CharacterBakeSpec Hero(string key, string model, string[] optional, string[] show, Color rim, List<CharacterBakeSpec.ClipSpec> clips)
        {
            var spec = ScriptableObject.CreateInstance<CharacterBakeSpec>();
            spec.key = key;
            spec.modelPath = Heroes + model;
            spec.optionalParts = optional.ToList();
            spec.showParts = show.ToList();
            spec.rimColor = rim;
            spec.clips = clips;
            return spec;
        }

        static CharacterBakeSpec Monster(string key, string model, Color tint, Color rim, float scale, List<CharacterBakeSpec.ClipSpec> clips, params (string asset, string bone)[] gear)
        {
            var spec = ScriptableObject.CreateInstance<CharacterBakeSpec>();
            spec.key = key;
            spec.modelPath = Skeletons + model;
            spec.tint = tint;
            spec.rimColor = rim;
            spec.rimAmount = 1.2f;
            spec.displayScale = scale;
            spec.clips = clips;
            spec.attachments = gear.Select(g => new CharacterBakeSpec.Attachment { assetPath = SkeletonGear + g.asset, bone = g.bone }).ToList();
            return spec;
        }

        public static IEnumerable<CharacterBakeSpec> DefaultCharacterSpecs()
        {
            var blue = new Color(0.35f, 0.62f, 1f);
            var violet = new Color(0.62f, 0.45f, 1f);
            var teal = new Color(0.3f, 0.9f, 0.8f);
            var amber = new Color(1f, 0.62f, 0.3f);
            var red = new Color(1f, 0.32f, 0.22f);

            yield return Hero("knight/sword_shield", "Knight.glb", KnightParts, new[] { "1H_Sword", "Round_Shield", "Knight_Helmet", "Knight_Cape" }, blue,
                Common("Idle", "1H_Melee_Attack_Slice_Diagonal", 0.45f, "1H_Melee_Attack_Chop", 0.48f));
            yield return Hero("knight/2h_sword", "Knight.glb", KnightParts, new[] { "2H_Sword", "Knight_Helmet", "Knight_Cape" }, blue,
                Common("2H_Melee_Idle", "2H_Melee_Attack_Slice", 0.42f, "2H_Melee_Attack_Chop", 0.5f));
            yield return Hero("mage/staff", "Mage.glb", MageParts, new[] { "2H_Staff", "Mage_Hat", "Mage_Cape" }, violet,
                Common("Idle", "Spellcast_Shoot", 0.4f, "1H_Ranged_Shoot", 0.4f));
            yield return Hero("mage/wand_book", "Mage.glb", MageParts, new[] { "1H_Wand", "Spellbook_open", "Mage_Hat", "Mage_Cape" }, violet,
                Common("Idle", "1H_Ranged_Shoot", 0.4f, "Spellcast_Shoot", 0.4f));
            yield return Hero("archer/crossbow", "Rogue_Hooded.glb", RogueParts, new[] { "2H_Crossbow", "Rogue_Cape" }, teal,
                Common("Idle", "2H_Ranged_Shoot", 0.3f, "2H_Ranged_Shooting", 0.3f));
            yield return Hero("archer/daggers", "Rogue_Hooded.glb", RogueParts, new[] { "Knife", "Knife_Offhand", "Rogue_Cape" }, teal,
                Common("Idle", "Dualwield_Melee_Attack_Slice", 0.42f, "Dualwield_Melee_Attack_Chop", 0.46f));
            yield return Hero("monk/fists", "Barbarian.glb", BarbarianParts, new[] { "Barbarian_Cape" }, amber,
                Common("Unarmed_Idle", "Unarmed_Melee_Attack_Punch_A", 0.4f, "Unarmed_Melee_Attack_Kick", 0.45f));
            yield return Hero("monk/axes", "Barbarian.glb", BarbarianParts, new[] { "1H_Axe", "1H_Axe_Offhand", "Barbarian_Hat", "Barbarian_Cape" }, amber,
                Common("Idle", "Dualwield_Melee_Attack_Slice", 0.42f, "Dualwield_Melee_Attack_Chop", 0.46f));

            var bone = new Color(0.86f, 0.84f, 0.8f);
            yield return Monster("skeleton_minion", "Skeleton_Minion.glb", bone, red, 1f,
                Common("Idle_Combat", "1H_Melee_Attack_Chop", 0.48f, "1H_Melee_Attack_Slice_Horizontal", 0.45f, true), ("Skeleton_Blade.gltf", "handslot.r"));
            yield return Monster("skeleton_warrior", "Skeleton_Warrior.glb", bone, red, 1.1f,
                Common("Idle_Combat", "1H_Melee_Attack_Chop", 0.5f, "Block_Attack", 0.45f, true), ("Skeleton_Axe.gltf", "handslot.r"), ("Skeleton_Shield_Large_A.gltf", "handslot.l"));
            yield return Monster("skeleton_rogue", "Skeleton_Rogue.glb", bone, red, 1f,
                Common("Idle_Combat", "1H_Ranged_Shoot", 0.35f, "1H_Ranged_Shoot", 0.35f, true), ("Skeleton_Crossbow.gltf", "handslot.r"));
            yield return Monster("skeleton_mage", "Skeleton_Mage.glb", bone, violet, 1f,
                Common("Idle_Combat", "Spellcast_Shoot", 0.4f, "Spellcast_Summon", 0.6f, true), ("Skeleton_Staff.gltf", "handslot.r"));
            yield return Monster("skeleton_boss", "Skeleton_Warrior.glb", new Color(0.95f, 0.72f, 0.66f), red, 1.9f,
                Common("Idle_Combat", "2H_Melee_Attack_Chop", 0.5f, "2H_Melee_Attack_Spin", 0.4f, true), ("Skeleton_Axe.gltf", "handslot.r"));
        }

        static EnvironmentBakeSpec.Piece F(string path, float weight, bool rotate = true) => new EnvironmentBakeSpec.Piece { assetPath = path, weight = weight, rotateVariants = rotate };

        static EnvironmentBakeSpec.PropPiece P(string path, PropPlacement placement, float weight, bool blocks, float yaw = 0f, float scale = 1f)
            => new EnvironmentBakeSpec.PropPiece { assetPath = path, placement = placement, weight = weight, blocks = blocks, yaw = yaw, scale = scale };

        static EnvironmentBakeSpec.PropPiece L(EnvironmentBakeSpec.PropPiece p, Color color, float radius, float intensity, float height, float flicker = 0.18f)
        {
            p.lightColor = color;
            p.lightRadius = radius;
            p.lightIntensity = intensity;
            p.lightHeight = height;
            p.flicker = flicker;
            return p;
        }

        public static IEnumerable<EnvironmentBakeSpec> DefaultEnvironmentSpecs()
        {
            var fire = new Color(1f, 0.62f, 0.32f, 1f);
            var candle = new Color(1f, 0.75f, 0.45f, 1f);
            var ghost = new Color(0.45f, 0.75f, 1f, 1f);

            var crypt = ScriptableObject.CreateInstance<EnvironmentBakeSpec>();
            crypt.themeName = "Crypt";
            crypt.floors = new List<EnvironmentBakeSpec.Piece>
            {
                F(Dungeon + "floor_tile_small.gltf.glb", 6f), F(Dungeon + "floor_tile_small_broken_A.gltf.glb", 1.2f), F(Dungeon + "floor_tile_small_broken_B.gltf.glb", 1.2f),
                F(Dungeon + "floor_tile_small_decorated.gltf.glb", 0.5f, false), F(Dungeon + "floor_tile_small_weeds_A.gltf.glb", 0.35f), F(Dungeon + "floor_tile_small_weeds_B.gltf.glb", 0.35f)
            };
            crypt.wallFaces = new List<EnvironmentBakeSpec.Piece> { F(Dungeon + "wall.gltf.glb", 5f, false), F(Dungeon + "wall_cracked.gltf.glb", 2f, false), F(Dungeon + "wall_window_closed.gltf.glb", 0.6f, false) };
            crypt.wallCapPath = Dungeon + "wall.gltf.glb";
            crypt.props = new List<EnvironmentBakeSpec.PropPiece>
            {
                P(Dungeon + "barrel_large.gltf.glb", PropPlacement.FloorEdge, 1f, true),
                P(Dungeon + "barrel_small_stack.gltf.glb", PropPlacement.FloorEdge, 1f, true),
                P(Dungeon + "box_stacked.gltf.glb", PropPlacement.FloorEdge, 1f, true),
                P(Dungeon + "crates_stacked.gltf.glb", PropPlacement.FloorEdge, 0.8f, true),
                P(Dungeon + "trunk_large_A.gltf.glb", PropPlacement.FloorEdge, 0.5f, true),
                P(Dungeon + "rubble_large.gltf.glb", PropPlacement.FloorEdge, 0.8f, true),
                P(Dungeon + "table_medium_broken.gltf.glb", PropPlacement.FloorEdge, 0.4f, true),
                P(Halloween + "coffin.gltf", PropPlacement.FloorEdge, 0.8f, true, 90f),
                P(Halloween + "coffin_decorated.gltf", PropPlacement.FloorEdge, 0.5f, true, 90f),
                P(Dungeon + "pillar.gltf.glb", PropPlacement.FloorEdge, 0.6f, true, 0f, 0.55f),
                L(P(Dungeon + "torch_lit.gltf.glb", PropPlacement.FloorEdge, 0.5f, true), fire, 5f, 1.1f, 0.9f),
                L(P(Halloween + "skull_candle.gltf", PropPlacement.FloorEdge, 0.5f, false), candle, 2.6f, 0.8f, 0.4f),
                L(P(Halloween + "candle_triple.gltf", PropPlacement.FloorEdge, 0.5f, false), candle, 2.8f, 0.8f, 0.4f),
                L(P(Halloween + "shrine_candles.gltf", PropPlacement.FloorEdge, 0.25f, true), ghost, 3.5f, 0.9f, 0.6f),
                P(Halloween + "bone_A.gltf", PropPlacement.FloorScatter, 1f, false),
                P(Halloween + "bone_B.gltf", PropPlacement.FloorScatter, 1f, false),
                P(Halloween + "bone_C.gltf", PropPlacement.FloorScatter, 1f, false),
                P(Halloween + "skull.gltf", PropPlacement.FloorScatter, 0.8f, false),
                P(Halloween + "ribcage.gltf", PropPlacement.FloorScatter, 0.5f, false),
                P(Dungeon + "rubble_half.gltf.glb", PropPlacement.FloorScatter, 0.8f, false),
                P(Halloween + "candle_melted.gltf", PropPlacement.FloorScatter, 0.4f, false),
                L(P(Dungeon + "torch_mounted.gltf.glb", PropPlacement.WallFace, 2.5f, false, 180f), fire, 5.5f, 1.15f, 0.55f),
                P(Dungeon + "banner_patternA_red.gltf.glb", PropPlacement.WallFace, 0.8f, false, 180f),
                P(Dungeon + "banner_thin_red.gltf.glb", PropPlacement.WallFace, 0.6f, false, 180f),
                P(Dungeon + "banner_shield_brown.gltf.glb", PropPlacement.WallFace, 0.5f, false, 180f),
                L(P(Halloween + "plaque_candles.gltf", PropPlacement.WallFace, 0.6f, false, 180f), candle, 3f, 0.8f, 0.5f),
                P(Dungeon + "sword_shield_broken.gltf.glb", PropPlacement.WallFace, 0.4f, false, 180f),
                P(Dungeon + "keyring_hanging.gltf.glb", PropPlacement.WallFace, 0.3f, false, 180f)
            };
            crypt.floorTint = new Color(0.62f, 0.62f, 0.72f);
            crypt.wallTint = new Color(0.72f, 0.72f, 0.82f);
            yield return crypt;

            var town = ScriptableObject.CreateInstance<EnvironmentBakeSpec>();
            town.themeName = "Town";
            town.floors = new List<EnvironmentBakeSpec.Piece>
            {
                F(Dungeon + "floor_dirt_small_A.gltf.glb", 3f), F(Dungeon + "floor_dirt_small_B.gltf.glb", 2f), F(Dungeon + "floor_dirt_small_C.gltf.glb", 2f),
                F(Dungeon + "floor_dirt_small_D.gltf.glb", 1f), F(Dungeon + "floor_dirt_small_weeds.gltf.glb", 1f), F(Dungeon + "floor_tile_small.gltf.glb", 1.5f)
            };
            town.wallFaces = new List<EnvironmentBakeSpec.Piece> { F(Dungeon + "wall.gltf.glb", 4f, false), F(Dungeon + "wall_window_open.gltf.glb", 1f, false), F(Dungeon + "wall_arched.gltf.glb", 1f, false) };
            town.wallCapPath = Dungeon + "wall.gltf.glb";
            town.props = new List<EnvironmentBakeSpec.PropPiece>
            {
                P(Halloween + "tree_dead_large.gltf", PropPlacement.FloorEdge, 0.8f, true, 0f, 0.6f),
                P(Halloween + "tree_dead_medium.gltf", PropPlacement.FloorEdge, 1f, true, 0f, 0.7f),
                P(Halloween + "gravestone.gltf", PropPlacement.FloorEdge, 1f, true),
                P(Halloween + "grave_A.gltf", PropPlacement.FloorEdge, 0.8f, true),
                P(Halloween + "fence.gltf", PropPlacement.FloorEdge, 0.8f, true),
                P(Halloween + "bench.gltf", PropPlacement.FloorEdge, 0.6f, true),
                P(Dungeon + "barrel_large_decorated.gltf.glb", PropPlacement.FloorEdge, 0.8f, true),
                P(Dungeon + "keg_decorated.gltf.glb", PropPlacement.FloorEdge, 0.6f, true),
                L(P(Halloween + "lantern_standing.gltf", PropPlacement.FloorEdge, 1.2f, true), candle, 5f, 1.1f, 1.1f, 0.1f),
                L(P(Halloween + "post_lantern.gltf", PropPlacement.FloorEdge, 1f, true), candle, 5.5f, 1.1f, 1.4f, 0.1f),
                P(Halloween + "pumpkin_orange_small.gltf", PropPlacement.FloorScatter, 0.4f, false),
                P(Halloween + "skull.gltf", PropPlacement.FloorScatter, 0.3f, false),
                L(P(Dungeon + "torch_mounted.gltf.glb", PropPlacement.WallFace, 2f, false, 180f), fire, 5.5f, 1.1f, 0.55f),
                P(Dungeon + "banner_patternB_blue.gltf.glb", PropPlacement.WallFace, 1f, false, 180f),
                P(Dungeon + "wall_shelves.gltf.glb", PropPlacement.WallFace, 0.0f, false, 180f)
            };
            town.floorTint = new Color(0.7f, 0.68f, 0.72f);
            town.wallTint = new Color(0.75f, 0.73f, 0.8f);
            town.ambientColor = new Color(0.7f, 0.68f, 0.85f);
            town.ambientIntensity = 0.8f;
            town.edgePropChance = 0.08f;
            yield return town;
        }

        // ---------------- 도우미 ----------------

        static void SaveSpec(ScriptableObject spec, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(spec, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(spec);
                return;
            }
            AssetDatabase.CreateAsset(spec, path);
        }

        static List<T> LoadAll<T>(string folder) where T : Object
            => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(a => a != null).ToList();

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
