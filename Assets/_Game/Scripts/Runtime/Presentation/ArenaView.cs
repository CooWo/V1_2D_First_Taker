using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>전투 구역 바닥/벽 타일맵을 만듭니다.</summary>
    public sealed class ArenaView : MonoBehaviour
    {
        public void Build(Rect bounds, SpriteLibrary library, int seed)
        {
            var grid = gameObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var floorMap = CreateLayer("Floor", SortingOrders.Floor);
            var wallMap = CreateLayer("Walls", SortingOrders.Wall);

            var random = new System.Random(seed);
            var floorTiles = CreateFloorTiles(library);
            var wallTile = ScriptableObject.CreateInstance<Tile>();
            wallTile.sprite = library != null && library.WallTile != null ? library.WallTile : PixelArtFactory.Square;
            if (library == null || library.WallTile == null) wallTile.color = new Color(0.1f, 0.11f, 0.16f);

            int xMin = Mathf.FloorToInt(bounds.xMin) - 2;
            int xMax = Mathf.CeilToInt(bounds.xMax) + 2;
            int yMin = Mathf.FloorToInt(bounds.yMin) - 2;
            int yMax = Mathf.CeilToInt(bounds.yMax) + 3;

            for (int x = xMin; x < xMax; x++)
            for (int y = yMin; y < yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool border = x < bounds.xMin - 1 || x >= bounds.xMax + 1 || y < bounds.yMin - 1 || y >= bounds.yMax + 1;
                if (border)
                {
                    wallMap.SetTile(cell, wallTile);
                    continue;
                }
                int index = random.NextDouble() < 0.82 ? 0 : random.Next(0, floorTiles.Length);
                floorMap.SetTile(cell, floorTiles[index]);
            }
        }

        Tilemap CreateLayer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            map.color = new Color(0.55f, 0.58f, 0.7f);
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
