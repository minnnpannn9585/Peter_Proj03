using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Validates a <see cref="LevelConfig"/> before any GameObject is spawned.
    ///
    /// <see cref="LevelLoader"/> historically checked all of this while building, which meant a
    /// bad level file could only be diagnosed by opening Unity. Running the same rules on the
    /// plain config first means a level can be verified from a test or a command line, and the
    /// loader aborts before it has half built a yard.
    /// </summary>
    public static class LevelTopologyCheck
    {
        /// <summary>Everything the checker learned about a level, valid or not.</summary>
        public class Report
        {
            public readonly List<string> errors = new List<string>();

            /// <summary>Belt id to arc length in world units.</summary>
            public readonly Dictionary<string, float> beltLengths = new Dictionary<string, float>();

            /// <summary>Belt id to cell span.</summary>
            public readonly Dictionary<string, int> beltCells = new Dictionary<string, int>();

            /// <summary>Belt id to the number of install slots it generates.</summary>
            public readonly Dictionary<string, int> beltSlots = new Dictionary<string, int>();

            /// <summary>Node ids that carry a device slot, i.e. junctions with 2+ outputs.</summary>
            public readonly List<string> deviceNodes = new List<string>();

            /// <summary>Inlet node id to the bay node ids it can reach.</summary>
            public readonly Dictionary<string, HashSet<string>> reachableBays =
                new Dictionary<string, HashSet<string>>();

            public bool Ok => errors.Count == 0;

            public int TotalBeltSlots
            {
                get
                {
                    int total = 0;
                    foreach (KeyValuePair<string, int> pair in beltSlots)
                    {
                        total += pair.Value;
                    }

                    return total;
                }
            }

            public int TotalNodeSlots => deviceNodes.Count;

            public int TotalInstallSlots => TotalBeltSlots + TotalNodeSlots;
        }

        public static Report Validate(LevelConfig config)
        {
            var report = new Report();
            if (config == null)
            {
                report.errors.Add("Level config is null.");
                return report;
            }

            Dictionary<string, NodeDef> nodes = IndexNodes(config, report);
            var outputs = new Dictionary<string, SortedDictionary<int, string>>();
            var inEdges = new Dictionary<string, List<string>>();
            var adjacency = new Dictionary<string, List<string>>();

            float deckWidth = BeltSlotMath.DeckWidth(config.grid.visualScale);

            var seenBelts = new HashSet<string>();
            for (int i = 0; i < config.belts.Count; i++)
            {
                BeltDef def = config.belts[i];
                if (string.IsNullOrEmpty(def.id))
                {
                    report.errors.Add("Belt " + i + " has no id.");
                    continue;
                }

                if (!seenBelts.Add(def.id))
                {
                    report.errors.Add("Duplicate belt id '" + def.id + "'.");
                    continue;
                }

                if (!nodes.TryGetValue(def.from, out NodeDef from))
                {
                    report.errors.Add("Belt '" + def.id + "' has unknown from node '" + def.from + "'.");
                    continue;
                }

                if (!nodes.TryGetValue(def.to, out NodeDef to))
                {
                    report.errors.Add("Belt '" + def.id + "' has unknown to node '" + def.to + "'.");
                    continue;
                }

                List<Vector3Int> cells = BuildCellPath(def, from, to);
                if (cells.Count < 2)
                {
                    report.errors.Add("Belt '" + def.id + "' is degenerate (from and to share a cell).");
                    continue;
                }

                if (!ValidateShape(def, cells, config.grid, config.rules, report,
                    out int cellSpan, out float length))
                {
                    continue;
                }

                report.beltCells[def.id] = cellSpan;
                report.beltLengths[def.id] = length;
                report.beltSlots[def.id] =
                    BeltSlotMath.Count(length, config.grid.size, deckWidth, def.allowInstall);

                RegisterOutput(def, report, outputs);
                Add(inEdges, def.to, def.id);
                Add(adjacency, def.from, def.to);
            }

            ValidateOutputContinuity(outputs, report);
            ValidateNodeRoles(config, nodes, outputs, report);
            ValidateOccupancy(config, nodes, report);
            ValidateReachability(config, nodes, adjacency, report);
            return report;
        }

        static Dictionary<string, NodeDef> IndexNodes(LevelConfig config, Report report)
        {
            var nodes = new Dictionary<string, NodeDef>();
            for (int i = 0; i < config.nodes.Count; i++)
            {
                NodeDef def = config.nodes[i];
                if (string.IsNullOrEmpty(def.id))
                {
                    report.errors.Add("Node " + i + " has no id.");
                    continue;
                }

                if (nodes.ContainsKey(def.id))
                {
                    report.errors.Add("Duplicate node id '" + def.id + "'.");
                    continue;
                }

                nodes.Add(def.id, def);
            }

            return nodes;
        }

        /// <summary>Mirrors <see cref="BeltPath"/>: endpoints plus authored waypoints, deduped.</summary>
        static List<Vector3Int> BuildCellPath(BeltDef def, NodeDef from, NodeDef to)
        {
            var cells = new List<Vector3Int>(def.path.Count + 2);
            cells.Add(new Vector3Int(from.cellX, from.cellZ, from.level));

            int lastLevel = from.level;
            for (int i = 0; i < def.path.Count; i++)
            {
                PathPoint point = def.path[i];
                int level = point.hasLevel ? point.level : lastLevel;
                lastLevel = level;
                cells.Add(new Vector3Int(point.cellX, point.cellZ, level));
            }

            cells.Add(new Vector3Int(to.cellX, to.cellZ, to.level));

            for (int i = cells.Count - 1; i > 0; i--)
            {
                if (cells[i] == cells[i - 1])
                {
                    cells.RemoveAt(i);
                }
            }

            return cells;
        }

        static bool ValidateShape(
            BeltDef def,
            List<Vector3Int> cells,
            GridSettings grid,
            RuleSettings rules,
            Report report,
            out int cellSpan,
            out float length)
        {
            cellSpan = 0;
            length = 0f;
            bool ok = true;
            float gridSize = grid.size;
            float levelHeight = grid.levelHeight;

            for (int i = 0; i < cells.Count - 1; i++)
            {
                Vector3Int a = cells[i];
                Vector3Int b = cells[i + 1];
                var delta = new Vector2Int(b.x - a.x, b.y - a.y);

                if (!GridMath.IsAxisOrDiagonal(delta))
                {
                    report.errors.Add("Belt '" + def.id + "' segment " + i +
                                      " is neither axis aligned nor 45 degrees.");
                    ok = false;
                    continue;
                }

                int span = GridMath.CellSpan(delta);
                cellSpan += span;

                int levelDelta = Mathf.Abs(b.z - a.z);
                if (levelDelta > 0)
                {
                    if (GridMath.IsDiagonal(delta))
                    {
                        report.errors.Add("Belt '" + def.id + "' segment " + i +
                                          " changes level on a diagonal; ramps must be straight.");
                        ok = false;
                        continue;
                    }

                    int needed = rules.minRampCellsPerLevel * levelDelta;
                    if (span < needed)
                    {
                        report.errors.Add("Belt '" + def.id + "' segment " + i + " ramps " +
                                          levelDelta + " level(s) over " + span + " cells; needs " +
                                          needed + ".");
                        ok = false;
                    }
                }

                // World length, matching BeltPath: one straight run per authored waypoint pair,
                // so a 4 cell diagonal is 4*sqrt(2) cells long rather than 4.
                float dx = delta.x * gridSize;
                float dz = delta.y * gridSize;
                float dy = (b.z - a.z) * levelHeight;
                length += Mathf.Sqrt(dx * dx + dz * dz + dy * dy);
            }

            if (!ok)
            {
                return false;
            }

            if (cellSpan < rules.minBeltCells || cellSpan > rules.maxBeltCells)
            {
                report.errors.Add("Belt '" + def.id + "' spans " + cellSpan +
                                  " cells; allowed range is " + rules.minBeltCells + ".." +
                                  rules.maxBeltCells + ".");
                return false;
            }

            return true;
        }

        static void RegisterOutput(
            BeltDef def, Report report, Dictionary<string, SortedDictionary<int, string>> outputs)
        {
            if (def.output < 0)
            {
                report.errors.Add("Belt '" + def.id + "' has a negative output index.");
                return;
            }

            if (!outputs.TryGetValue(def.from, out SortedDictionary<int, string> byIndex))
            {
                byIndex = new SortedDictionary<int, string>();
                outputs[def.from] = byIndex;
            }

            if (byIndex.ContainsKey(def.output))
            {
                report.errors.Add("Node '" + def.from + "' has two belts on output " + def.output + ".");
                return;
            }

            byIndex.Add(def.output, def.id);
        }

        static void ValidateOutputContinuity(
            Dictionary<string, SortedDictionary<int, string>> outputs, Report report)
        {
            foreach (KeyValuePair<string, SortedDictionary<int, string>> pair in outputs)
            {
                int expected = 0;
                foreach (KeyValuePair<int, string> entry in pair.Value)
                {
                    if (entry.Key != expected)
                    {
                        report.errors.Add("Node '" + pair.Key + "' is missing a belt on output " +
                                          expected + "; output indices must start at 0 and be contiguous.");
                        break;
                    }

                    expected++;
                }
            }
        }

        static void ValidateNodeRoles(
            LevelConfig config,
            Dictionary<string, NodeDef> nodes,
            Dictionary<string, SortedDictionary<int, string>> outputs,
            Report report)
        {
            foreach (KeyValuePair<string, NodeDef> pair in nodes)
            {
                NodeDef def = pair.Value;
                int outCount = outputs.TryGetValue(def.id, out SortedDictionary<int, string> byIndex)
                    ? byIndex.Count
                    : 0;

                switch (def.type)
                {
                    case NodeType.Bay:
                        if (outCount > 0)
                        {
                            report.errors.Add("Bay '" + def.id + "' must not have outgoing belts.");
                        }

                        if (!def.hasColor)
                        {
                            report.errors.Add("Bay '" + def.id + "' has no accepted colour.");
                        }

                        break;
                    case NodeType.Inlet:
                        if (outCount == 0)
                        {
                            report.errors.Add("Inlet '" + def.id + "' has no outgoing belt.");
                        }

                        break;
                    case NodeType.Junction:
                        if (outCount == 0)
                        {
                            report.errors.Add("Junction '" + def.id + "' is a dead end.");
                        }

                        // Two or more outputs makes it a diverter, which is where an AutoArm goes.
                        if (outCount > 1)
                        {
                            report.deviceNodes.Add(def.id);
                        }

                        break;
                }
            }

            report.deviceNodes.Sort(System.StringComparer.Ordinal);

            if (config.nodes.Count == 0)
            {
                report.errors.Add("Level has no nodes.");
            }
        }

        /// <summary>Two belts may share a cell only when their levels differ, i.e. an overpass.</summary>
        static void ValidateOccupancy(LevelConfig config, Dictionary<string, NodeDef> nodes, Report report)
        {
            var nodeCells = new HashSet<Vector2Int>();
            foreach (KeyValuePair<string, NodeDef> pair in nodes)
            {
                nodeCells.Add(new Vector2Int(pair.Value.cellX, pair.Value.cellZ));
            }

            var occupancy = new Dictionary<Vector2Int, List<KeyValuePair<float, string>>>();
            for (int i = 0; i < config.belts.Count; i++)
            {
                BeltDef def = config.belts[i];
                if (!nodes.TryGetValue(def.from, out NodeDef from) ||
                    !nodes.TryGetValue(def.to, out NodeDef to))
                {
                    continue;
                }

                List<Vector3Int> cells = BuildCellPath(def, from, to);
                foreach (KeyValuePair<Vector2Int, float> visit in EnumerateCells(cells))
                {
                    if (nodeCells.Contains(visit.Key))
                    {
                        continue;
                    }

                    if (!occupancy.TryGetValue(visit.Key, out List<KeyValuePair<float, string>> users))
                    {
                        users = new List<KeyValuePair<float, string>>();
                        occupancy[visit.Key] = users;
                    }

                    bool clash = false;
                    for (int u = 0; u < users.Count; u++)
                    {
                        if (users[u].Value == def.id)
                        {
                            continue;
                        }

                        if (Mathf.Abs(users[u].Key - visit.Value) < 0.5f)
                        {
                            report.errors.Add("Belts '" + users[u].Value + "' and '" + def.id +
                                              "' overlap at cell (" + visit.Key.x + ", " + visit.Key.y +
                                              ") on the same level. Raise one of them to cross.");
                            clash = true;
                            break;
                        }
                    }

                    if (!clash)
                    {
                        users.Add(new KeyValuePair<float, string>(visit.Value, def.id));
                    }
                }
            }
        }

        static IEnumerable<KeyValuePair<Vector2Int, float>> EnumerateCells(List<Vector3Int> cells)
        {
            for (int i = 0; i < cells.Count - 1; i++)
            {
                Vector3Int a = cells[i];
                Vector3Int b = cells[i + 1];
                var delta = new Vector2Int(b.x - a.x, b.y - a.y);
                int span = GridMath.CellSpan(delta);
                if (span == 0)
                {
                    continue;
                }

                int stepX = delta.x == 0 ? 0 : (delta.x > 0 ? 1 : -1);
                int stepZ = delta.y == 0 ? 0 : (delta.y > 0 ? 1 : -1);
                for (int s = 0; s <= span; s++)
                {
                    var cell = new Vector2Int(a.x + stepX * s, a.y + stepZ * s);
                    float level = Mathf.Lerp(a.z, b.z, (float)s / span);
                    yield return new KeyValuePair<Vector2Int, float>(cell, level);
                }
            }
        }

        static void ValidateReachability(
            LevelConfig config,
            Dictionary<string, NodeDef> nodes,
            Dictionary<string, List<string>> adjacency,
            Report report)
        {
            var inlets = new List<string>();
            var bays = new List<string>();
            foreach (KeyValuePair<string, NodeDef> pair in nodes)
            {
                if (pair.Value.type == NodeType.Inlet)
                {
                    inlets.Add(pair.Key);
                }
                else if (pair.Value.type == NodeType.Bay)
                {
                    bays.Add(pair.Key);
                }
            }

            if (inlets.Count == 0)
            {
                report.errors.Add("Level has no inlet.");
            }

            if (bays.Count == 0)
            {
                report.errors.Add("Level has no truck bay.");
            }

            var served = new HashSet<string>();
            for (int i = 0; i < inlets.Count; i++)
            {
                HashSet<string> reached = Walk(inlets[i], nodes, adjacency);
                report.reachableBays[inlets[i]] = reached;
                if (reached.Count == 0)
                {
                    report.errors.Add("Inlet '" + inlets[i] + "' cannot reach any bay.");
                }

                served.UnionWith(reached);
            }

            for (int i = 0; i < bays.Count; i++)
            {
                if (!served.Contains(bays[i]))
                {
                    report.errors.Add("Bay '" + bays[i] + "' is unreachable from every inlet.");
                }
            }
        }

        static HashSet<string> Walk(
            string start,
            Dictionary<string, NodeDef> nodes,
            Dictionary<string, List<string>> adjacency)
        {
            var found = new HashSet<string>();
            var seen = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                string id = stack.Pop();
                if (!seen.Add(id))
                {
                    continue;
                }

                if (nodes.TryGetValue(id, out NodeDef def) && def.type == NodeType.Bay)
                {
                    found.Add(id);
                    continue;
                }

                if (adjacency.TryGetValue(id, out List<string> next))
                {
                    for (int i = 0; i < next.Count; i++)
                    {
                        stack.Push(next[i]);
                    }
                }
            }

            return found;
        }

        static void Add(Dictionary<string, List<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out List<string> list))
            {
                list = new List<string>();
                map[key] = list;
            }

            list.Add(value);
        }
    }
}
