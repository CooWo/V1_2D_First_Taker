using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Core.Tree
{
    public enum NodeTier
    {
        Small,
        Notable,
        Keystone
    }

    /// <summary>트리 노드 하나: 능력치 한 줄 + 위치 + 연결.</summary>
    public sealed class TreeNode
    {
        public int Id;
        public Vector2 Position;
        public NodeTier Tier;
        public StatType Stat;
        public float Value;
        public int Branch;
        public readonly List<int> Links = new List<int>();
    }

    /// <summary>
    /// 플레이어 성장 트리 (수천~1만 노드). 중심(루트)에서 가지별 테마로 뻗어 나가며,
    /// 이미 가진 노드와 연결된 노드만 살 수 있습니다.
    /// </summary>
    public sealed class PassiveTree
    {
        public PassiveTree(List<TreeNode> nodes, string[] branchNames)
        {
            Nodes = nodes;
            BranchNames = branchNames;
        }

        public IReadOnlyList<TreeNode> Nodes { get; }
        public IReadOnlyList<string> BranchNames { get; }
        public const int RootId = 0;
    }

    /// <summary>가지 테마 하나 (이 가지에서 나오는 능력치 목록).</summary>
    public sealed class TreeBranchTheme
    {
        public string Name;
        public StatType[] Stats;
    }

    /// <summary>시드 기반 결정적 트리 생성기. 같은 시드·크기면 항상 같은 트리가 나옵니다.</summary>
    public static class PassiveTreeGenerator
    {
        public static readonly TreeBranchTheme[] DefaultThemes =
        {
            new TreeBranchTheme { Name = "힘·생명", Stats = new[] { StatType.Strength, StatType.MaxLife, StatType.LifePct, StatType.Armor } },
            new TreeBranchTheme { Name = "민첩·회피", Stats = new[] { StatType.Dexterity, StatType.EvasionPct, StatType.AttackSpeedPct, StatType.MoveSpeedPct } },
            new TreeBranchTheme { Name = "지능·보호막", Stats = new[] { StatType.Intelligence, StatType.EnergyShieldPct, StatType.MaxResource, StatType.CooldownReductionPct } },
            new TreeBranchTheme { Name = "치명타", Stats = new[] { StatType.CritChancePct, StatType.CritDamagePct } },
            new TreeBranchTheme { Name = "물리 방어", Stats = new[] { StatType.Armor, StatType.ArmorEffectPct, StatType.BlockChancePct, StatType.EliteDamageReductionPct } },
            new TreeBranchTheme { Name = "불", Stats = new[] { StatType.FireDamagePct, StatType.FireResistPct } },
            new TreeBranchTheme { Name = "번개", Stats = new[] { StatType.LightningDamagePct, StatType.LightningResistPct } },
            new TreeBranchTheme { Name = "얼음", Stats = new[] { StatType.ColdDamagePct, StatType.ColdResistPct } },
            new TreeBranchTheme { Name = "대지", Stats = new[] { StatType.EarthDamagePct, StatType.EarthResistPct } },
            new TreeBranchTheme { Name = "빛", Stats = new[] { StatType.LightDamagePct, StatType.LightResistPct } },
            new TreeBranchTheme { Name = "어둠", Stats = new[] { StatType.DarkDamagePct, StatType.DarkResistPct } },
            new TreeBranchTheme { Name = "골드·경험치", Stats = new[] { StatType.GoldFindPct, StatType.ExperiencePct } },
            new TreeBranchTheme { Name = "펫", Stats = new[] { StatType.PetPowerPct, StatType.PetCooldownPct } },
            new TreeBranchTheme { Name = "전투 기술", Stats = new[] { StatType.AllDamagePct, StatType.AreaPct, StatType.LifeOnHit } }
        };

        /// <summary>작은 노드 1개의 기본 수치. 특급(Notable)은 4배, 핵심(Keystone)은 12배.</summary>
        public static float SmallValue(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:
                case StatType.Dexterity:
                case StatType.Intelligence: return 3f;
                case StatType.MaxLife: return 12f;
                case StatType.MaxResource: return 5f;
                case StatType.Armor: return 15f;
                case StatType.LifeOnHit: return 1f;
                case StatType.CritChancePct:
                case StatType.EvasionPct:
                case StatType.BlockChancePct:
                case StatType.CooldownReductionPct:
                case StatType.PetCooldownPct:
                case StatType.MoveSpeedPct:
                case StatType.AttackSpeedPct: return 0.4f;
                case StatType.EliteDamageReductionPct:
                case StatType.LifePct:
                case StatType.EnergyShieldPct:
                case StatType.AllDamagePct:
                case StatType.GoldFindPct:
                case StatType.ExperiencePct:
                case StatType.AreaPct: return 1f;
                case StatType.FireResistPct:
                case StatType.ColdResistPct:
                case StatType.LightningResistPct:
                case StatType.EarthResistPct:
                case StatType.LightResistPct:
                case StatType.DarkResistPct: return 1.5f;
                default: return 2f;
            }
        }

        public static PassiveTree Generate(int nodeCount, int seed, TreeBranchTheme[] themes = null)
        {
            themes = themes ?? DefaultThemes;
            nodeCount = Mathf.Clamp(nodeCount, 50, 10000);
            var random = new System.Random(seed);
            var nodes = new List<TreeNode>(nodeCount);
            var root = new TreeNode { Id = 0, Position = Vector2.zero, Tier = NodeTier.Notable, Stat = StatType.AllDamagePct, Value = 2f, Branch = -1 };
            nodes.Add(root);

            int branches = themes.Length;
            var branchTips = new List<int>[branches];
            var branchCounts = new int[branches];

            // 각 가지의 시작 노드는 루트와 연결
            for (int b = 0; b < branches; b++)
            {
                float angle = 360f / branches * b;
                var node = NewNode(nodes.Count, b, Polar(angle, 1.5f), themes[b], 0, random);
                Link(root, node);
                nodes.Add(node);
                branchTips[b] = new List<int> { node.Id };
                branchCounts[b] = 1;
            }

            while (nodes.Count < nodeCount)
            {
                int b = nodes.Count % branches;
                var tips = branchTips[b];
                // 바깥쪽 노드를 우선 골라 가지가 넓게 뻗도록
                int pick = tips[Mathf.Max(0, tips.Count - 1 - (int)(Math.Pow(random.NextDouble(), 3) * tips.Count))];
                var parent = nodes[pick];

                float baseAngle = 360f / branches * b;
                float spread = 360f / branches * 0.45f;
                float radius = parent.Position.magnitude + 0.6f + (float)random.NextDouble() * 0.5f;
                float angle = baseAngle + ((float)random.NextDouble() * 2f - 1f) * spread;

                var node = NewNode(nodes.Count, b, Polar(angle, radius), themes[b], branchCounts[b], random);
                Link(parent, node);
                nodes.Add(node);
                tips.Add(node.Id);
                branchCounts[b]++;

                // 가끔 이웃 노드와 교차 연결 (우회 경로)
                if (random.NextDouble() < 0.08 && tips.Count > 4)
                {
                    var other = nodes[tips[random.Next(Math.Max(0, tips.Count - 6), tips.Count - 1)]];
                    if (other != parent && other != node) Link(node, other);
                }
            }

            return new PassiveTree(nodes, themes.Select(t => t.Name).ToArray());
        }

        static TreeNode NewNode(int id, int branch, Vector2 position, TreeBranchTheme theme, int indexInBranch, System.Random random)
        {
            var tier = indexInBranch > 0 && indexInBranch % 60 == 0 ? NodeTier.Keystone
                : indexInBranch > 0 && indexInBranch % 8 == 0 ? NodeTier.Notable
                : NodeTier.Small;
            var stat = theme.Stats[random.Next(theme.Stats.Length)];
            float multiplier = tier == NodeTier.Keystone ? 12f : tier == NodeTier.Notable ? 4f : 1f;
            return new TreeNode { Id = id, Branch = branch, Position = position, Tier = tier, Stat = stat, Value = SmallValue(stat) * multiplier };
        }

        static void Link(TreeNode a, TreeNode b)
        {
            if (!a.Links.Contains(b.Id)) a.Links.Add(b.Id);
            if (!b.Links.Contains(a.Id)) b.Links.Add(a.Id);
        }

        static Vector2 Polar(float degrees, float radius)
            => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;
    }

    /// <summary>
    /// 트리 구매·적용. 노드를 살 때마다 다음 노드 비용이 계속 오릅니다.
    /// </summary>
    public sealed class PassiveTreeService
    {
        readonly PassiveTree _tree;
        readonly HashSet<int> _owned;

        public PassiveTreeService(PassiveTree tree, HashSet<int> owned)
        {
            _tree = tree;
            _owned = owned;
            _owned.Add(PassiveTree.RootId);
        }

        public PassiveTree Tree => _tree;
        public IReadOnlyCollection<int> Owned => _owned;
        public int OwnedCount => _owned.Count;

        public event Action Changed;

        public bool IsOwned(int id) => _owned.Contains(id);

        public bool CanReach(int id)
        {
            if (_owned.Contains(id) || id < 0 || id >= _tree.Nodes.Count) return false;
            foreach (var link in _tree.Nodes[id].Links)
                if (_owned.Contains(link)) return true;
            return false;
        }

        /// <summary>비용 = 25 × (보유 수 + 1)^1.35 × 등급 배율. 살수록 계속 비싸집니다.</summary>
        public long Cost(int id)
        {
            var tier = _tree.Nodes[id].Tier;
            float tierMultiplier = tier == NodeTier.Keystone ? 8f : tier == NodeTier.Notable ? 3f : 1f;
            return (long)Mathf.Round(Tuning.GameTuning.Current.Tree.CostBase * Mathf.Pow(_owned.Count + 1, Tuning.GameTuning.Current.Tree.CostExponent) * tierMultiplier);
        }

        public List<int> Frontier()
        {
            var result = new HashSet<int>();
            foreach (var id in _owned)
                foreach (var link in _tree.Nodes[id].Links)
                    if (!_owned.Contains(link)) result.Add(link);
            return result.OrderBy(i => _tree.Nodes[i].Position.magnitude).ToList();
        }

        public bool TryBuy(int id, Wallet wallet)
        {
            if (!CanReach(id)) return false;
            if (!wallet.TrySpend(Cost(id))) return false;
            _owned.Add(id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>보유 노드 능력치 합계.</summary>
        public Dictionary<StatType, float> Totals()
        {
            var totals = new Dictionary<StatType, float>();
            foreach (var id in _owned)
            {
                var node = _tree.Nodes[id];
                totals[node.Stat] = (totals.TryGetValue(node.Stat, out var v) ? v : 0f) + node.Value;
            }
            return totals;
        }

        public void ApplyTo(Combatant combatant)
        {
            combatant.Stats.RemoveSource(this);
            foreach (var pair in Totals()) combatant.Stats.AddModifier(new StatModifier(pair.Key, pair.Value, this));
        }
    }
}
