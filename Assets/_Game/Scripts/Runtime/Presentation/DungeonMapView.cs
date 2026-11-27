using System.Collections.Generic;
using ShadowRPG.Core.World;
using ShadowRPG.Runtime.Art;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 층 지도를 환경 묶음(EnvironmentTheme)으로 그립니다 — 3/4 시점 규칙:
    /// · 걸을 수 있는 칸 = 바닥 타일
    /// · 바닥에 닿은 막힌 칸 = 벽. 바로 아래(남쪽)가 바닥이면 정면을 그리고, 윗면은 정면 높이만큼 위에 그림
    /// · 그 밖의 막힌 칸 = 공허 (카메라 배경색)
    /// 소품: 뒷벽 횃불·깃발, 방 가장자리 상자·관·촛불, 바닥 뼈·잔해. 빛나는 소품에는 2D 조명.
    /// 막는 소품은 지도 칸을 막음으로 표시합니다 (방 가장자리에만 놓아 길을 끊지 않음).
    /// </summary>
    public sealed class DungeonMapView : MonoBehaviour
    {
        static readonly Dictionary<Sprite, Tile> TileCache = new Dictionary<Sprite, Tile>();

        readonly List<Light2D> _lights = new List<Light2D>();

        public EnvironmentTheme Theme { get; private set; }

        public void Build(GridMap map, EnvironmentTheme theme, Color actTint, int seed, Vector2 keepClear, bool solidInterior = false)
        {
            Theme = theme;
            if (theme == null)
            {
                Debug.LogWarning("[Map] 환경 묶음이 없습니다 — ShadowRPG/Art/2. Bake All 을 실행하세요.");
                return;
            }
            var random = new System.Random(seed);
            BuildFloor(map, theme, actTint, random);
            BuildWalls(map, theme, actTint, random, solidInterior);
            BuildProps(map, theme, actTint, random, keepClear);
        }

        void BuildFloor(GridMap map, EnvironmentTheme theme, Color tint, System.Random random)
        {
            // 바닥은 위에서 본 정사각 타일을 "바닥 평면" 부모 아래에 둠 → 화면에서 아이소메트릭 마름모
            var ground = WorldProjection.CreateGroundRoot(transform, "Ground");
            var go = new GameObject("Floor");
            go.transform.SetParent(ground, false);
            var grid = go.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var tilemapObject = new GameObject("Tiles");
            tilemapObject.transform.SetParent(go.transform, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = SortingOrders.Floor;
            tilemap.color = theme.floorTint * tint;
            tilemap.tileAnchor = Vector3.zero;

            var origin = Vector2Int.FloorToInt(map.Origin);
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                if (!map.IsWalkable(x, y)) continue;
                var sprite = theme.Pick(theme.floorTiles, random);
                if (sprite == null) continue;
                tilemap.SetTile(new Vector3Int(origin.x + x, origin.y + y, 0), TileFor(sprite));
            }
        }

        static Tile TileFor(Sprite sprite)
        {
            if (TileCache.TryGetValue(sprite, out var tile) && tile != null) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            TileCache[sprite] = tile;
            return tile;
        }

        /// <summary>
        /// 아이소메트릭 낮은 벽: 바닥에 닿은 막힌 칸마다 벽 블록 하나 (구운 블록 그림, 발 = 칸 중심).
        /// 정렬은 화면 높이 기준이라 앞쪽 벽이 뒤쪽 캐릭터를 자연스럽게 가립니다.
        /// </summary>
        void BuildWalls(GridMap map, EnvironmentTheme theme, Color tint, System.Random random, bool solidInterior)
        {
            var parent = new GameObject("Walls").transform;
            parent.SetParent(transform, false);
            var blockTint = theme.wallTint * tint;
            blockTint.a = 1f;

            for (int x = -1; x <= map.Width; x++)
            for (int y = -1; y <= map.Height; y++)
            {
                if (map.IsWalkable(x, y) || !(TouchesWalkable(map, x, y) || solidInterior && map.InRange(x, y))) continue;
                var block = theme.Pick(theme.wallFaces, random);
                if (block == null) continue;
                var view = WorldProjection.ToView(map.Origin + new Vector2(x + 0.5f, y + 0.5f));
                Sprite(parent, "Wall", block, view, blockTint, SortingOrders.ForY(view.y));
            }
        }

        void BuildProps(GridMap map, EnvironmentTheme theme, Color tint, System.Random random, Vector2 keepClear)
        {
            var parent = new GameObject("Props").transform;
            parent.SetParent(transform, false);
            var propTint = Color.Lerp(Color.white, tint, 0.6f);

            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Origin + new Vector2(x, y);
                var center = cell + new Vector2(0.5f, 0.5f);

                // 뒷벽(화면 위쪽 벽) 소품: 북쪽(+y) 또는 동쪽(+x) 칸이 벽이면 그 벽면에 붙임
                if (map.IsWalkable(x, y) && random.NextDouble() < theme.wallPropChance)
                {
                    bool north = !map.IsWalkable(x, y + 1), east = !map.IsWalkable(x + 1, y);
                    if (north || east)
                    {
                        var prop = theme.PickProp(PropPlacement.WallFace, random);
                        if (prop != null)
                        {
                            var face = north ? center + new Vector2(0f, 0.5f) : center + new Vector2(0.5f, 0f);
                            var view = WorldProjection.ToView(face) + new Vector3(0f, theme.wallFaceHeight * 0.55f, 0f);
                            var wallView = WorldProjection.ToView(north ? center + new Vector2(0f, 1f) : center + new Vector2(1f, 0f));
                            Place(parent, prop, view, propTint, SortingOrders.ForY(wallView.y) + 2);
                        }
                    }
                }

                if (!map.IsWalkable(x, y) || (center - keepClear).sqrMagnitude < 9f) continue;
                int open = OpenNeighbours(map, x, y);
                bool edge = open >= 5 && open < 8 && TouchesWall4(map, x, y);

                if (edge && random.NextDouble() < theme.edgePropChance)
                {
                    var prop = theme.PickProp(PropPlacement.FloorEdge, random);
                    if (prop != null)
                    {
                        var view = WorldProjection.ToView(center);
                        Place(parent, prop, view, propTint, SortingOrders.ForY(view.y));
                        if (prop.blocks) map.Set(x, y, false);
                        continue;
                    }
                }

                if (open == 8 && random.NextDouble() < theme.scatterPropChance)
                {
                    var prop = theme.PickProp(PropPlacement.FloorScatter, random);
                    if (prop != null)
                    {
                        var jitter = new Vector2((float)random.NextDouble() * 0.6f - 0.3f, (float)random.NextDouble() * 0.6f - 0.3f);
                        Place(parent, prop, WorldProjection.ToView(center + jitter), propTint, SortingOrders.Floor + 50);
                    }
                }
            }
        }

        void Place(Transform parent, EnvironmentTheme.Prop prop, Vector3 at, Color tint, int order)
        {
            var renderer = Sprite(parent, prop.name, prop.sprite, at, tint, order);
            if (prop.lightColor.a <= 0f) return;
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(renderer.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, prop.lightHeight, 0f);
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = prop.lightColor;
            light.intensity = prop.lightIntensity;
            light.pointLightOuterRadius = prop.lightRadius;
            light.pointLightInnerRadius = prop.lightRadius * 0.15f;
            light.falloffIntensity = 0.65f;
            if (prop.flicker > 0f) lightObject.AddComponent<LightFlicker>().Initialize(light, prop.flicker);
            _lights.Add(light);

            // 불꽃 자체의 빛 번짐 (블룸에 걸리도록 가산 광원)
            var fx = FxCatalog.Load();
            var glow = fx != null ? FxCatalog.Pick(fx.glow, null) : null;
            if (glow == null) return;
            var halo = Sprite(renderer.transform, "Halo", glow, Vector3.zero, new Color(prop.lightColor.r, prop.lightColor.g, prop.lightColor.b, 0.55f), order + 1);
            halo.transform.localPosition = new Vector3(0f, prop.lightHeight, 0f);
            halo.transform.localScale = Vector3.one * 1.3f;
            halo.sharedMaterial = FxMaterials.Additive;
        }

        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Vector3 position, Color tint, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = order;
            return renderer;
        }

        static bool TouchesWalkable(GridMap map, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (map.IsWalkable(x + dx, y + dy)) return true;
            return false;
        }

        static bool TouchesWall4(GridMap map, int x, int y)
            => !map.IsWalkable(x + 1, y) || !map.IsWalkable(x - 1, y) || !map.IsWalkable(x, y + 1) || !map.IsWalkable(x, y - 1);

        static int OpenNeighbours(GridMap map, int x, int y)
        {
            int n = 0;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && map.IsWalkable(x + dx, y + dy)) n++;
            return n;
        }
    }

    /// <summary>횃불·촛불 깜빡임.</summary>
    public sealed class LightFlicker : MonoBehaviour
    {
        Light2D _light;
        float _base;
        float _amount;
        float _seed;

        public void Initialize(Light2D light, float amount)
        {
            _light = light;
            _base = light.intensity;
            _amount = amount;
            _seed = Random.value * 100f;
        }

        void Update()
        {
            if (_light == null) return;
            float n = Mathf.PerlinNoise(_seed, Time.time * 6f) - 0.5f;
            _light.intensity = _base * (1f + n * _amount * 2f);
        }
    }
}
