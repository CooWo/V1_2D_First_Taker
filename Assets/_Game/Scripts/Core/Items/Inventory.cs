using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Items
{
    /// <summary>
    /// 아이템별 차지 칸 (가로 x 세로). 큰 장비일수록 가방 공간을 많이 먹어
    /// "무엇을 들고 갈지" 선택을 강요합니다.
    /// </summary>
    public static class ItemShapes
    {
        public static Vector2Int SizeOf(Item item)
        {
            switch (item.Slot)
            {
                case EquipSlot.Ring1:
                case EquipSlot.Ring2:
                case EquipSlot.Amulet: return new Vector2Int(1, 1);
                case EquipSlot.Relic: return new Vector2Int(1, 2);
                case EquipSlot.Head:
                case EquipSlot.Boots: return new Vector2Int(2, 2);
                case EquipSlot.Body: return new Vector2Int(2, 3);
                case EquipSlot.LeftHand:
                    return item.Subtype == WeaponSubtype.Quiver ? new Vector2Int(1, 3)
                        : item.Subtype == WeaponSubtype.Focus ? new Vector2Int(1, 2)
                        : new Vector2Int(2, 2);
                default:
                    switch (item.Subtype)
                    {
                        case WeaponSubtype.Wand: return new Vector2Int(1, 2);
                        case WeaponSubtype.Sword: return new Vector2Int(1, 3);
                        case WeaponSubtype.CombatGlove: return new Vector2Int(2, 2);
                        case WeaponSubtype.Bow: return new Vector2Int(2, 3);
                        case WeaponSubtype.Staff: return new Vector2Int(1, 4);
                        case WeaponSubtype.Greatsword: return new Vector2Int(2, 4);
                        default: return new Vector2Int(1, 3);
                    }
            }
        }

        public static int Cells(Item item)
        {
            var s = SizeOf(item);
            return s.x * s.y;
        }
    }

    /// <summary>
    /// 칸(격자) 가방. 아이템은 자기 크기만큼의 빈 사각형에 놓여야 합니다.
    /// 자동 배치는 위-왼쪽부터 처음 맞는 곳 (회전 없음).
    /// </summary>
    public sealed class Inventory
    {
        readonly Item[,] _grid;
        readonly Dictionary<Item, RectInt> _placements = new Dictionary<Item, RectInt>();
        readonly List<Item> _order = new List<Item>();

        public Inventory(int width, int height)
        {
            Width = width;
            Height = height;
            _grid = new Item[width, height];
        }

        public int Width { get; }
        public int Height { get; }
        public int Capacity => Width * Height;
        public IReadOnlyList<Item> Items => _order;
        public int UsedCells => _placements.Values.Sum(r => r.width * r.height);
        public int FreeCells => Capacity - UsedCells;
        public bool IsFull => !HasSpaceFor(new Vector2Int(1, 1));

        public event Action Changed;

        public bool TryGetPlacement(Item item, out RectInt rect) => _placements.TryGetValue(item, out rect);

        public Item ItemAt(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? _grid[x, y] : null;

        public bool CanFit(Item item) => item != null && FindSpot(ItemShapes.SizeOf(item), out _);

        public bool HasSpaceFor(Vector2Int size) => FindSpot(size, out _);

        public bool TryAdd(Item item)
        {
            if (item == null || _placements.ContainsKey(item)) return false;
            var size = ItemShapes.SizeOf(item);
            if (!FindSpot(size, out var spot)) return false;
            Place(item, new RectInt(spot, size));
            Changed?.Invoke();
            return true;
        }

        /// <summary>지정 위치에 놓기 (저장 복원·수동 배치).</summary>
        public bool TryPlace(Item item, int x, int y)
        {
            if (item == null || _placements.ContainsKey(item)) return false;
            var rect = new RectInt(new Vector2Int(x, y), ItemShapes.SizeOf(item));
            if (!IsFree(rect)) return false;
            Place(item, rect);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(Item item)
        {
            if (item == null || !_placements.TryGetValue(item, out var rect)) return false;
            for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++)
                _grid[x, y] = null;
            _placements.Remove(item);
            _order.Remove(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>이 아이템들을 지금 가방에 모두 넣을 수 있는지 (실제로 넣지는 않음).</summary>
        public bool CanFitAll(IEnumerable<Item> items)
        {
            var copy = Clone();
            foreach (var item in items.OrderByDescending(ItemShapes.Cells))
                if (!copy.TryAddSilently(item)) return false;
            return true;
        }

        /// <summary>추가 아이템을 넣는다면 어디에 들어갈지 미리보기 (실제로 넣지 않음). 안 들어가는 아이템은 빠집니다.</summary>
        public Dictionary<Item, RectInt> PreviewPlacements(IEnumerable<Item> extra)
        {
            var copy = Clone();
            var result = new Dictionary<Item, RectInt>();
            foreach (var item in extra.OrderByDescending(ItemShapes.Cells))
                if (copy.TryAddSilently(item) && copy._placements.TryGetValue(item, out var rect)) result[item] = rect;
            return result;
        }

        /// <summary>큰 아이템부터 다시 채워 빈 공간을 한쪽으로 모읍니다.</summary>
        public void Compact()
        {
            var items = _order.OrderByDescending(ItemShapes.Cells).ToList();
            Array.Clear(_grid, 0, _grid.Length);
            _placements.Clear();
            _order.Clear();
            var leftovers = new List<Item>();
            foreach (var item in items)
                if (!TryAddSilently(item)) leftovers.Add(item);
            if (leftovers.Count > 0) throw new InvalidOperationException("Compact lost items");
            Changed?.Invoke();
        }

        /// <summary>무작위로 절반(올림)을 잃습니다. 잃은 아이템 목록을 돌려줍니다.</summary>
        public List<Item> LoseHalf(IRandom random)
        {
            var pool = new List<Item>(_order);
            int count = (pool.Count + 1) / 2;
            var lost = new List<Item>();
            for (int i = 0; i < count; i++)
            {
                int index = random.Range(0, pool.Count);
                lost.Add(pool[index]);
                pool.RemoveAt(index);
            }
            foreach (var item in lost) Remove(item);
            return lost;
        }

        Inventory Clone()
        {
            var copy = new Inventory(Width, Height);
            foreach (var pair in _placements) copy.Place(pair.Key, pair.Value);
            return copy;
        }

        bool TryAddSilently(Item item)
        {
            var size = ItemShapes.SizeOf(item);
            if (!FindSpot(size, out var spot)) return false;
            Place(item, new RectInt(spot, size));
            return true;
        }

        void Place(Item item, RectInt rect)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++)
                _grid[x, y] = item;
            _placements[item] = rect;
            _order.Add(item);
        }

        bool FindSpot(Vector2Int size, out Vector2Int spot)
        {
            for (int y = 0; y <= Height - size.y; y++)
            for (int x = 0; x <= Width - size.x; x++)
            {
                if (!IsFree(new RectInt(x, y, size.x, size.y))) continue;
                spot = new Vector2Int(x, y);
                return true;
            }
            spot = default;
            return false;
        }

        bool IsFree(RectInt rect)
        {
            if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > Width || rect.yMax > Height) return false;
            for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++)
                if (_grid[x, y] != null) return false;
            return true;
        }
    }

    /// <summary>장비 가치 평가 (자동 장착·추천 선택용).</summary>
    public interface IItemScorer
    {
        float Score(Item item);
    }

    public sealed class ItemScorer : IItemScorer
    {
        public float Score(Item item)
        {
            if (item == null) return 0f;
            float score = item.ItemLevel * 2f + (int)item.Rarity * 12f + item.EnhanceLevel * 6f;
            foreach (var m in item.AllModifiers) score += Weight(m.Stat) * m.Value;
            if (item.GrantedSkill.IsValid) score += 10f + (int)item.GrantedSkill.Rarity * 10f;
            if (item.SupportCode != null) score += 15f;
            if (item.SetId != null) score += 10f;
            if (item.ReducesSetRequirement) score += 20f;
            return score;
        }

        static float Weight(StatType stat)
        {
            switch (stat)
            {
                case StatType.WeaponDamage: return 2f;
                case StatType.MaxLife: return 0.15f;
                case StatType.Armor: return 0.05f;
                case StatType.EnergyShield: return 0.1f;
                case StatType.Strength:
                case StatType.Dexterity:
                case StatType.Intelligence: return 0.6f;
                case StatType.ExtraProjectiles:
                case StatType.ExtraChains:
                case StatType.ExtraPierce: return 12f;
                default: return 0.5f;
            }
        }
    }

    /// <summary>새 장비가 현재보다 좋으면 자동 장착합니다.</summary>
    public sealed class AutoEquipPolicy
    {
        readonly IItemScorer _scorer;

        public AutoEquipPolicy(IItemScorer scorer) => _scorer = scorer;

        public bool TryAutoEquip(Equipment equipment, Item item, IStatReader stats, out EquipSlot slot, out Item replaced)
        {
            replaced = null;
            slot = ChooseSlot(equipment, item);
            var current = equipment.Get(slot);
            if (current != null && _scorer.Score(current) >= _scorer.Score(item)) return false;
            if (!equipment.CanEquip(item, slot, stats, out _)) return false;

            var result = equipment.TryEquip(item, slot, stats);
            replaced = result.Replaced;
            return result.Success;
        }

        EquipSlot ChooseSlot(Equipment equipment, Item item)
        {
            if (!SlotRules.IsRing(item.Slot)) return item.Slot;
            var ring1 = equipment.Get(EquipSlot.Ring1);
            var ring2 = equipment.Get(EquipSlot.Ring2);
            if (ring1 == null) return EquipSlot.Ring1;
            if (ring2 == null) return EquipSlot.Ring2;
            return _scorer.Score(ring1) <= _scorer.Score(ring2) ? EquipSlot.Ring1 : EquipSlot.Ring2;
        }
    }

    public static class ItemRarityExtensions
    {
        public static SkillRarity ToSkillRarity(this ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return SkillRarity.Magic;
                case ItemRarity.Rare:
                case ItemRarity.Set: return SkillRarity.Rare;
                case ItemRarity.Legendary: return SkillRarity.Legendary;
                default: return SkillRarity.Normal;
            }
        }

        public static string DisplayName(this ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return "마법";
                case ItemRarity.Rare: return "희귀";
                case ItemRarity.Legendary: return "전설";
                case ItemRarity.Set: return "세트";
                default: return "일반";
            }
        }
    }
}
