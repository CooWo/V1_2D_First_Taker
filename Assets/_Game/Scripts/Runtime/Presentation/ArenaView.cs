using ShadowRPG.Core.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>층 지도를 타일맵으로 그립니다: 걸을 수 있는 칸은 바닥, 그 둘레의 막힌 칸은 벽.</summary>
    public sealed class ArenaView : MonoBehaviour
    {
        /// <summary>벽이 있는 지도.</summary>
        public void Build(GridMap map, SpriteLibrary library, int seed, Color tint, bool solidWalls = false)
        {
            Setup(library, tint, out var floorMap, out var wallMap, out var floorTiles, out var wallTile);
            var random = new System.Random(seed);
            var origin = Vector2Int.FloorToInt(map.Origin);

            for (int x = -1; x <= map.Width; x++)
            for (int y = -1; y <= map.Height; y++)
            {
                var cell = new Vector3Int(origin.x + x, origin.y + y, 0);
                if (map.IsWalkable(x, y))
                {
                    floorMap.SetTile(cell, floorTiles[random.NextDouble() < 0.82 ? 0 : random.Next(0, floorTiles.Length)]);
                    continue;
                }
                // 던전은 벽 둘레만(바깥은 어둠), 마을은 건물 블록을 통째로 채움
                if (TouchesWalkable(map, x, y) || (solidWalls && map.InRange(x, y))) wallMap.SetTile(cell, wallTile);
            }
        }

        /// <summary>벽 없는 사각 경기장 (레이드).</summary>
        public void Build(Rect bounds, SpriteLibrary library, int seed, Color tint)
        {
            int width = Mathf.CeilToInt(bounds.width) + 2;
            int height = Mathf.CeilToInt(bounds.height) + 2;
            var map = new GridMap(width, height, bounds.min - Vector2.one);
            map.Fill(new RectInt(1, 1, width - 2, height - 2), true);
            Build(map, library, seed, tint);
        }

        static bool TouchesWalkable(GridMap map, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (map.IsWalkable(x + dx, y + dy)) return true;
            return false;
        }

        void Setup(SpriteLibrary library, Color tint, out Tilemap floorMap, out Tilemap wallMap, out Tile[] floorTiles, out Tile wallTile)
        {
            var grid = gameObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            floorMap = CreateLayer("Floor", SortingOrders.Floor, tint);
            wallMap = CreateLayer("Walls", SortingOrders.Wall, tint * 0.9f);
            floorTiles = CreateFloorTiles(library);
            wallTile = ScriptableObject.CreateInstance<Tile>();
            wallTile.sprite = library != null && library.WallTile != null ? library.WallTile : PixelArtFactory.Square;
            if (library == null || library.WallTile == null) wallTile.color = new Color(0.1f, 0.11f, 0.16f);
        }

        Tilemap CreateLayer(string name, int order, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            map.color = tint;
            return map;
        }

        static Tile[] CreateFloorTiles(SpriteLibrary library)
        {
            if (library != null && library.FloorTiles.Count > 0)
            {
                var tiles = new Tile[library.FloorTiles.Count];
                for (int i = 0; i < tiles.Length; i++)
                {
                    tiles[i] = ScriptableObject.CreateInstance<Tile>();
                    tiles[i].sprite = library.FloorTiles[i];
                }
                return tiles;
            }

            var fallback = ScriptableObject.CreateInstance<Tile>();
            fallback.sprite = PixelArtFactory.Square;
            fallback.color = new Color(0.16f, 0.17f, 0.22f);
            return new[] { fallback };
        }
    }
}
