using System;
using System.Collections.Generic;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>가방: 페이지 단위 장비 목록. 항목을 누르면 상세를 보여주고, 다시 누르면 장착합니다.</summary>
    public sealed class InventoryPanel
    {
        const int PageSize = 8;

        readonly GameSession _session;
        readonly Action<string> _log;
        readonly RectTransform _root;
        readonly List<UnityEngine.UI.Button> _rows = new List<UnityEngine.UI.Button>();
        readonly List<UnityEngine.UI.Text> _rowLabels = new List<UnityEngine.UI.Text>();
        readonly UnityEngine.UI.Text _header;
        readonly UnityEngine.UI.Text _detail;
        int _page;
        Item _selected;

        public InventoryPanel(Transform parent, GameSession session, Action<string> log)
        {
            _session = session;
            _log = log;
            _root = UiFactory.Anchored(parent, "InventoryPanel", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(760f, 860f));
            UiFactory.Panel(_root, new Color(0.04f, 0.05f, 0.08f, 0.94f), true);
            UiFactory.Border(_root, Palette.PanelBorder);

            _header = UiFactory.Label(UiFactory.Anchored(_root, "Header", new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(720f, 40f)),
                string.Empty, 26, TextAnchor.MiddleLeft, Color.white);

            for (int i = 0; i < PageSize; i++)
            {
                int index = i;
                var button = UiFactory.Button(_root, "Row" + i, new Vector2(0.5f, 1f), new Vector2(0f, -62f - i * 62f), new Vector2(720f, 56f), string.Empty, () => OnRow(index));
                var label = button.GetComponentInChildren<UnityEngine.UI.Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 20;
                _rows.Add(button);
                _rowLabels.Add(label);
            }

            UiFactory.Button(_root, "Prev", new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(120f, 56f), "◀", () => Page(-1));
            UiFactory.Button(_root, "Next", new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(120f, 56f), "▶", () => Page(1));
            UiFactory.Button(_root, "AutoEquip", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(300f, 56f), string.Empty, ToggleAutoEquip);

            _detail = UiFactory.Label(UiFactory.Anchored(_root, "Detail", new Vector2(0f, 1f), new Vector2(18f, -566f), new Vector2(720f, 210f)),
                string.Empty, 20, TextAnchor.UpperLeft, Color.white);

            _session.Player.Inventory.Changed += Refresh;
            _root.gameObject.SetActive(false);
        }

        public bool Visible => _root.gameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
            _selected = null;
            Refresh();
        }

        void Page(int delta)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(_session.Player.Inventory.Items.Count / (float)PageSize));
            _page = (_page + delta + pages) % pages;
            Refresh();
        }

        void ToggleAutoEquip()
        {
            _session.AutoEquipEnabled = !_session.AutoEquipEnabled;
            Refresh();
        }

        void OnRow(int index)
        {
            var items = _session.Player.Inventory.Items;
            int i = _page * PageSize + index;
            if (i >= items.Count) return;
            var item = items[i];

            if (_selected != item)
            {
                _selected = item;
                Refresh();
                return;
            }

            var slot = TargetSlot(item);
            var result = _session.Equip(item, slot);
            _log?.Invoke(result.Success ? $"장착: {item.Name}" : $"장착 실패: {result.Reason}");
            _selected = null;
            Refresh();
        }

        EquipSlot TargetSlot(Item item)
        {
            if (!SlotRules.IsRing(item.Slot)) return item.Slot;
            var equipment = _session.Player.Equipment;
            if (equipment.Get(EquipSlot.Ring1) == null) return EquipSlot.Ring1;
            if (equipment.Get(EquipSlot.Ring2) == null) return EquipSlot.Ring2;
            return _session.Scorer.Score(equipment.Get(EquipSlot.Ring1)) <= _session.Scorer.Score(equipment.Get(EquipSlot.Ring2)) ? EquipSlot.Ring1 : EquipSlot.Ring2;
        }

        public void Refresh()
        {
            if (!Visible) return;
            var items = _session.Player.Inventory.Items;
            int pages = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            _header.text = $"<b>가방</b>  {items.Count}/{_session.Player.Inventory.Capacity}   <size=20>페이지 {_page + 1}/{pages} · 눌러서 상세, 한 번 더 누르면 장착</size>";

            for (int r = 0; r < PageSize; r++)
            {
                int i = _page * PageSize + r;
                bool has = i < items.Count;
                _rows[r].gameObject.SetActive(has);
                if (!has) continue;
                var item = items[i];
                string marker = item == _selected ? "▶ " : string.Empty;
                _rowLabels[r].text = $"  {marker}{ItemFormatter.ColoredName(item)} <size=17><color=#8090A8>{SlotRules.DisplayName(item.Slot)} · Lv.{item.ItemLevel}</color></size>";
            }

            _detail.text = _selected != null ? ItemFormatter.Describe(_selected, _session.Content) : "<color=#5A6070>항목을 선택하세요</color>";
            var autoLabel = _root.Find("AutoEquip").GetComponentInChildren<UnityEngine.UI.Text>();
            autoLabel.text = _session.AutoEquipEnabled ? "자동 장착: ON" : "자동 장착: OFF";
        }
    }
}
