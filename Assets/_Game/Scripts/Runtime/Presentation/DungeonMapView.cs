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
            var grid = gameObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var go = new GameObject("Floor");
            go.transform.SetParent(transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
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

        void BuildWalls(GridMap map, EnvironmentTheme theme, Color tint, System.Random random, bool solidInterior)
        {
            var parent = new GameObject("Walls").transform;
            parent.SetParent(transform, false);
            var faceTint = theme.wallTint * tint;
            var capTint = faceTint * 0.72f;
            capTint.a = 1f;

            for (int x = -1; x <= map.Width; x++)
            for (int y = -1; y <= map.Height; y++)
            {
                if (map.IsWalkable(x, y) || !(TouchesWalkable(map, x, y) || solidInterior && map.InRange(x, y))) continue;
                var cell = map.Origin + new Vector2(x, y);
                int order = SortingOrders.ForY(cell.y);

                if (theme.wallCap != null)
                    Sprite(parent, "Cap", theme.wallCap, new Vector3(cell.x, cell.y + theme.wallFaceHeight), capTint, order);

                if (map.IsWalkable(x, y - 1))
                {
                    var face = theme.Pick(theme.wallFaces, random);
                    if (face != null) Sprite(parent, "Face", face, new Vector3(cell.x, cell.y), faceTint, order + 1);
                }
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

                // 뒷벽 (정면이 보이는 벽) 소품: 위쪽 칸이 벽이고 지금 칸이 바닥
                if (map.IsWalkable(x, y) && !map.IsWalkable(x, y + 1) && random.NextDouble() < theme.wallPropChance)
                {
                    var prop = theme.PickProp(PropPlacement.WallFace, random);
                    if (prop != null)
                    {
                        var at = new Vector3(center.x, cell.y + 1f + theme.wallFaceHeight * 0.45f);
                        Place(parent, prop, at, propTint, SortingOrders.ForY(cell.y + 1f) + 2);
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
                        Place(parent, prop, new Vector3(center.x, cell.y + 0.25f), propTint, SortingOrders.ForY(cell.y + 0.25f));
                        if (prop.blocks) map.Set(x, y, false);
                        continue;
                    }
                }

                if (open == 8 && random.NextDouble() < theme.scatterPropChance)
                {
                    var prop = theme.PickProp(PropPlacement.FloorScatter, random);
                    if (prop != null)
                    {
                        var jitter = new Vector3((float)random.NextDouble() * 0.6f - 0.3f, (float)random.NextDouble() * 0.6f - 0.3f);
                        Place(parent, prop, (Vector3)center + jitter, propTint, SortingOrders.Floor + 50);
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
