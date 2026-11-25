using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShadowRPG.Core.Tree;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>
    /// 성장 트리: 미니맵(보유=파랑, 구매 가능=노랑) + 구매 가능한 노드 목록. 살수록 비용 증가.
    /// </summary>
    public sealed class TreeMenu : IMenu
    {
        const int MapSize = 256;

        readonly MenuContext _ctx;
        readonly UnityEngine.UI.RawImage _map;
        readonly Texture2D _texture;
        readonly Color32[] _pixels = new Color32[MapSize * MapSize];
        List<int> _frontier = new List<int>();
        int _branchFilter = -1;
        int _drawnOwned = -1;
        float _extent;

        public TreeMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "TreeMenu");
            Panel.RowClicked += Buy;

            // 설명 글과 겹치지 않도록 상세 영역을 줄이고 미니맵을 오른쪽 아래에 둡니다.
            var detailSize = Panel.DetailArea.sizeDelta;
            Panel.DetailArea.sizeDelta = new Vector2(detailSize.x - 250f, detailSize.y);
            var mapRect = UiFactory.Anchored(Panel.Root, "Map", new Vector2(1f, 0f), new Vector2(-20f, 96f), new Vector2(230f, 230f));
            _map = mapRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            _texture = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _map.texture = _texture;
            _map.raycastTarget = false;
            _extent = _ctx.Meta.Tree.Nodes.Max(n => n.Position.magnitude) + 1f;
        }

        public string ButtonLabel => "트리";
        public ListPanel Panel { get; }

        PassiveTreeService Service => _ctx.Meta.TreeService;

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var tree = _ctx.Meta.Tree;
            _frontier = Service.Frontier().Where(id => _branchFilter < 0 || tree.Nodes[id].Branch == _branchFilter).ToList();

            Panel.SetTitle($"<b>성장 트리</b> {Service.OwnedCount}/{tree.Nodes.Count}  <size=22><color=#FFD84A>{_ctx.Meta.Wallet.Gold:N0} G</color></size>");
            Panel.SetItems(_frontier.Count, i => NodeLabel(tree.Nodes[_frontier[i]]));

            Panel.SetDetail(Summary());
            long next = _frontier.Count > 0 ? Service.Cost(_frontier[0]) : 0;
            Panel.SetFooter(0, _frontier.Count > 0 ? $"다음 구매 {next:N0}G" : "구매 가능 없음", () => BuyMany(1));
            Panel.SetFooter(1, "10개 연속 구매", () => BuyMany(10));
            Panel.SetFooter(2, "가지: " + (_branchFilter < 0 ? "전체" : tree.BranchNames[_branchFilter]), CycleFilter);

            if (_drawnOwned != Service.OwnedCount) DrawMap();
        }

        string NodeLabel(TreeNode node)
        {
            string tier = node.Tier == NodeTier.Keystone ? "<color=#FF8A2A>핵심</color>" : node.Tier == NodeTier.Notable ? "<color=#FFD84A>특급</color>" : "일반";
            string branch = node.Branch >= 0 ? _ctx.Meta.Tree.BranchNames[node.Branch] : "중심";
            return $"<color=#8090A8>[{branch}]</color> {ItemFormatter.StatName(node.Stat)} {ItemFormatter.FormatValue(node.Stat, node.Value)}  {tier}  <color=#FFD84A>{Service.Cost(node.Id):N0}G</color>";
        }

        string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=18>노드는 이미 가진 노드와 연결된 것만 살 수 있고, 하나 살 때마다 다음 비용이 오릅니다.</size>");
            var totals = Service.Totals().OrderByDescending(p => p.Value).Take(10);
            foreach (var pair in totals) sb.Append($"<size=18>{ItemFormatter.StatName(pair.Key)} {ItemFormatter.FormatValue(pair.Key, pair.Value)}   </size>");
            return sb.ToString();
        }

        void Buy(int index)
        {
            if (index >= _frontier.Count) return;
            int id = _frontier[index];
            long cost = Service.Cost(id);
            if (_ctx.Meta.BuyTreeNode(id, _ctx.Session.Player)) _ctx.Log($"트리 노드 구매 -{cost:N0}G");
            else _ctx.Log($"골드 부족 (필요 {cost:N0}G)");
            Refresh();
        }

        void BuyMany(int count)
        {
            int bought = 0;
            var tree = _ctx.Meta.Tree;
            for (int i = 0; i < count; i++)
            {
                bool ok = _ctx.Meta.BuyNextTreeNode(_ctx.Session.Player, n => _branchFilter < 0 || n.Branch == _branchFilter);
                if (!ok) break;
                bought++;
            }
            _ctx.Log(bought > 0 ? $"트리 노드 {bought}개 구매" : "골드가 부족합니다.");
            Refresh();
        }

        void CycleFilter()
        {
            int branches = _ctx.Meta.Tree.BranchNames.Count;
            _branchFilter = _branchFilter + 1 >= branches ? -1 : _branchFilter + 1;
            Refresh();
        }

        void DrawMap()
        {
            _drawnOwned = Service.OwnedCount;
            var background = new Color32(10, 12, 20, 255);
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = background;

            var frontier = new HashSet<int>(Service.Frontier());
            foreach (var node in _ctx.Meta.Tree.Nodes)
            {
                var color = Service.IsOwned(node.Id) ? new Color32(90, 170, 255, 255)
                    : frontier.Contains(node.Id) ? new Color32(255, 216, 74, 255)
                    : node.Tier == NodeTier.Small ? new Color32(45, 50, 66, 255) : new Color32(90, 80, 60, 255);
                int x = Mathf.Clamp(Mathf.RoundToInt((node.Position.x / _extent * 0.5f + 0.5f) * (MapSize - 1)), 0, MapSize - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt((node.Position.y / _extent * 0.5f + 0.5f) * (MapSize - 1)), 0, MapSize - 1);
                _pixels[y * MapSize + x] = color;
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply();
        }
    }
}
