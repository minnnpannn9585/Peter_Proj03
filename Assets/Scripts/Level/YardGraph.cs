using System.Collections.Generic;

namespace ParcelSort
{
    public class YardGraph
    {
        readonly Dictionary<string, YardNode> nodes = new Dictionary<string, YardNode>();
        readonly Dictionary<string, BeltPath> belts = new Dictionary<string, BeltPath>();
        readonly List<BeltPath> beltOrder = new List<BeltPath>();
        readonly List<YardNode> inlets = new List<YardNode>();
        readonly List<YardNode> bays = new List<YardNode>();

        public IReadOnlyDictionary<string, YardNode> Nodes => nodes;
        public IReadOnlyDictionary<string, BeltPath> Belts => belts;
        public IReadOnlyList<BeltPath> AllBelts => beltOrder;
        public IReadOnlyList<YardNode> Inlets => inlets;
        public IReadOnlyList<YardNode> Bays => bays;

        public bool AddNode(YardNode node)
        {
            if (node == null || nodes.ContainsKey(node.NodeId))
            {
                return false;
            }

            nodes.Add(node.NodeId, node);
            if (node.Type == NodeType.Inlet)
            {
                inlets.Add(node);
            }
            else if (node.Type == NodeType.Bay)
            {
                bays.Add(node);
            }

            return true;
        }

        public bool AddBelt(BeltPath belt)
        {
            if (belt == null || belts.ContainsKey(belt.BeltId))
            {
                return false;
            }

            belts.Add(belt.BeltId, belt);
            beltOrder.Add(belt);
            return true;
        }

        public bool TryGetNode(string id, out YardNode node)
        {
            node = null;
            return !string.IsNullOrEmpty(id) && nodes.TryGetValue(id, out node);
        }

        public bool TryGetBelt(string id, out BeltPath belt)
        {
            belt = null;
            return !string.IsNullOrEmpty(id) && belts.TryGetValue(id, out belt);
        }

        /// <summary>All bay node ids reachable from an inlet by following every branch.</summary>
        public HashSet<string> ReachableBays(YardNode start)
        {
            var found = new HashSet<string>();
            var seen = new HashSet<string>();
            var stack = new Stack<YardNode>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                YardNode node = stack.Pop();
                if (node == null || !seen.Add(node.NodeId))
                {
                    continue;
                }

                if (node.Type == NodeType.Bay)
                {
                    found.Add(node.NodeId);
                    continue;
                }

                for (int i = 0; i < node.OutBelts.Count; i++)
                {
                    BeltPath belt = node.OutBelts[i];
                    if (belt != null)
                    {
                        stack.Push(belt.To);
                    }
                }
            }

            return found;
        }

        public HashSet<DestinationColor> BayColors()
        {
            var colors = new HashSet<DestinationColor>();
            for (int i = 0; i < bays.Count; i++)
            {
                var bay = bays[i].GetComponent<TruckBay>();
                if (bay != null)
                {
                    colors.Add(bay.AcceptsColor);
                }
            }

            return colors;
        }

        /// <summary>
        /// Bay node id to the colour it accepts. <see cref="AutoArmDevice"/> needs this to turn
        /// "which bays does this branch reach" into "which colours should go down it".
        /// </summary>
        public Dictionary<string, DestinationColor> BayColorsById()
        {
            var byId = new Dictionary<string, DestinationColor>();
            for (int i = 0; i < bays.Count; i++)
            {
                var bay = bays[i].GetComponent<TruckBay>();
                if (bay != null)
                {
                    byId[bays[i].NodeId] = bay.AcceptsColor;
                }
            }

            return byId;
        }

        /// <summary>Inlet node ids in graph order, for building a spawn schedule.</summary>
        public List<string> InletIds()
        {
            var ids = new List<string>(inlets.Count);
            for (int i = 0; i < inlets.Count; i++)
            {
                ids.Add(inlets[i].NodeId);
            }

            return ids;
        }
    }
}
