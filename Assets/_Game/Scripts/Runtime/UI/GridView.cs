using System;
using System.Collections.Generic;
using ShadowRPG.Core.Items;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>
    /// 칸 가방 그리기: 빈 칸 격자 + 아이템 블록(등급 색, 크기만큼). 블록을 누르면 ItemClicked.
    /// 미리보기 블록(가져갈 예정)은 반투명으로 표시합니다.
    /// </summary>
    public sealed class GridView
    {
        readonly RectTransform _root;
        readonly float _cell;
        readonly int _width;
        readonly int _height;
        readonly List<UnityEngine.UI.Button> _blocks = new List<UnityEngine.UI.Button>();
        readonly List<Item> _blockItems = new List<Item>();
        int _used;

        public GridView(Transform parent, string name, Vector2 anchor, Vector2 position, int width, int height, float cell)
        {
            _cell = cell;
            _width = width;
            _height = height;
            _root = UiFactory.Anchored(parent, name, anchor, position, new Vector2(width * cell, height * cell));
            UiFactory.Panel(_root, new Color(0f, 0f, 0f, 0.5f));
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var c = UiFactory.Anchored(_root, "Cell", new Vector2(0f, 1f), new Vector2(x * cell + 1f, -(y * cell + 1f)), new Vector2(cell - 2f, cell - 2f));
                UiFactory.Panel(c, new Color(0.12f, 0.14f, 0.2f, 0.9f));
            }
        }

        public RectTransform Root => _root;
        public event Action<Item> ItemClicked;

        public void Refresh(Inventory inventory, Item selected, IReadOnlyDictionary<Item, RectInt> preview = null)
        {
            _used = 0;
            foreach (var item in inventory.Items)
                if (inventory.TryGetPlacement(item, out var rect)) Draw(item, rect, item == selected, false);
            if (preview != null)
                foreach (var pair in preview) Draw(pair.Key, pair.Value, pair.Key == selected, true);
            for (int i = _used; i < _blocks.Count; i++) _blocks[i].gameObject.SetActive(false);
        }

        void Draw(Item item, RectInt rect, bool selected, bool ghost)
        {
            var button = _used < _blocks.Count ? _blocks[_used] : CreateBlock();
            _blockItems[_used] = item;
            _used++;

            button.gameObject.SetActive(true);
            var rt = (RectTransform)button.transform;
            rt.anchoredPosition = new Vector2(rect.x * _cell + 2f, -(rect.y * _cell + 2f));
            rt.sizeDelta = new Vector2(rect.width * _cell - 4f, rect.height * _cell - 4f);

            var color = Palette.For(item.Rarity);
            var image = button.targetGraphic as UnityEngine.UI.Image;
            image.color = ghost ? new Color(color.r, color.g, color.b, 0.35f)
                : selected ? Color.Lerp(color, Color.white, 0.45f)
                : new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 0.95f);

            var label = button.GetComponentInChildren<UnityEngine.UI.Text>();
            label.text = (ghost ? "+" : string.Empty) + ShortName(item);
            label.fontSize = Mathf.RoundToInt(Mathf.Clamp(_cell * 0.28f, 12f, 20f));
        }

        UnityEngine.UI.Button CreateBlock()
        {
            int index = _blocks.Count;
            var button = UiFactory.Button(_root, "Item", new Vector2(0f, 1f), Vector2.zero, new Vector2(_cell, _cell), string.Empty,
                () => ItemClicked?.Invoke(_blockItems[index]));
            _blocks.Add(button);
            _blockItems.Add(null);
            return button;
        }

        static string ShortName(Item item)
        {
            string name = item.DisplayName;
            int space = name.LastIndexOf(' ');
            string core = space >= 0 ? name.Substring(space + 1) : name;
            return item.EnhanceLevel > 0 ? $"+{item.EnhanceLevel}\n{core}" : core;
        }
    }
}
