using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ParcelSort
{
    public class LevelLoader : MonoBehaviour
    {
        [SerializeField] string levelFileName = "level_01.json";
        [SerializeField] ModuleCatalog catalog;
        [SerializeField] Transform yardRoot;

        public LevelConfig LoadedConfig { get; private set; }
        public YardGraph Graph { get; private set; }
        public Bounds YardBounds { get; private set; }
        public Bounds FloorBounds { get; private set; }
        public ModuleCatalog Catalog => catalog;
        public string LevelFileName => levelFileName;

        public void SetLevelFileName(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                levelFileName = fileName;
            }
        }

        public bool TryLoad(YardDirector director)
        {
            if (catalog == null)
            {
                Debug.LogError("LevelLoader is missing ModuleCatalog.");
                return false;
            }

            if (yardRoot == null)
            {
                GameObject rootGo = new GameObject("YardRoot");
                yardRoot = rootGo.transform;
            }

            ClearYard();

            string path = Path.Combine(Application.streamingAssetsPath, "Configs", "Levels", levelFileName);
            if (!File.Exists(path))
            {
                Debug.LogError("Level JSON not found: " + path);
                return false;
            }

            string json = File.ReadAllText(path);
            LevelConfig config = LevelConfigParser.Parse(json);
            LoadedConfig = config;

            // Must land before any geometry is built: ride height and every instanced
            // visual are derived from it.
            BeltPath.VisualScale = config.grid.visualScale;
            Graph = new YardGraph();

            if (!BuildNodes(config) || !BuildBelts(config))
            {
                return false;
            }

            if (!ValidateOccupancy(config) || !ValidateTopology())
            {
                return false;
            }

            AttachNodeBehaviours(config, director);
            AttachBeltTools(config);
            BuildNodeSupports(config);

            FloorBounds = ComputeFloorBounds(config);
            YardBounds = ComputeViewBounds(config, FloorBounds);
            FitGround(FloorBounds);
            return true;
        }

        /// <summary>Speed and pause tools are whole-belt properties now.</summary>
        void AttachBeltTools(LevelConfig config)
        {
            for (int i = 0; i < config.belts.Count; i++)
            {
                BeltDef def = config.belts[i];
                if (!Graph.TryGetBelt(def.id, out BeltPath belt))
                {
                    continue;
                }

                if (def.canPause && def.canSpeed)
                {
                    Debug.LogWarning("Belt '" + def.id + "' sets canPause and canSpeed; pause wins.");
                }

                if (def.canPause)
                {
                    belt.gameObject.AddComponent<BeltPauseSwitch>().Configure();
                    continue;
                }

                if (def.canSpeed)
                {
                    belt.gameObject.AddComponent<BeltSpeedToggle>()
                        .Configure(config.rules.fastMultiplier, catalog.chevronPrefab);
                }
            }
        }

        /// <summary>
        /// Floor hugs the actual layout rather than the declared cell rectangle, which is
        /// usually far larger. The declared rectangle is only a fallback for empty levels.
        /// </summary>
        Bounds ComputeFloorBounds(LevelConfig config)
        {
            GridSettings grid = config.grid;
            Bounds content = ComputeViewBounds(config, new Bounds(Vector3.zero, Vector3.one));
            if (content.size.x > grid.size && content.size.z > grid.size)
            {
                float margin = grid.size * 0.75f;
                return new Bounds(
                    new Vector3(content.center.x, 0.5f, content.center.z),
                    new Vector3(content.size.x + margin, 2f, content.size.z + margin));
            }

            float worldW = Mathf.Max(1, grid.cellsX) * grid.size;
            float worldH = Mathf.Max(1, grid.cellsZ) * grid.size;
            Vector3 min = GridMath.CellToWorld(grid.originX, grid.originZ, grid.size);
            Vector3 center = min + new Vector3(
                worldW * 0.5f - grid.size * 0.5f,
                0.5f,
                worldH * 0.5f - grid.size * 0.5f);
            return new Bounds(center, new Vector3(worldW, 2f, worldH));
        }

        Bounds ComputeViewBounds(LevelConfig config, Bounds floorFallback)
        {
            float gridSize = config.grid.size;
            bool has = false;
            Bounds bounds = floorFallback;
            foreach (var pair in Graph.Nodes)
            {
                Vector3 center = pair.Value.GroundPos;
                var nodeBounds = new Bounds(center, new Vector3(gridSize, 2f, gridSize));
                if (!has)
                {
                    bounds = nodeBounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(nodeBounds);
                }
            }

            if (has)
            {
                float pad = gridSize * config.grid.viewPadCells;
                bounds.Expand(new Vector3(pad, 0f, pad));
            }

            return bounds;
        }

        static void FitGround(Bounds bounds)
        {
            GameObject ground = GameObject.Find("ground");
            if (ground == null)
            {
                return;
            }

            ground.transform.position = new Vector3(bounds.center.x, 0f, bounds.center.z);
            ground.transform.localScale = new Vector3(bounds.size.x, 0.1f, bounds.size.z);
        }

        void ClearYard()
        {
            if (yardRoot == null)
            {
                return;
            }

            for (int i = yardRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(yardRoot.GetChild(i).gameObject);
            }
        }

        bool BuildNodes(LevelConfig config)
        {
            for (int i = 0; i < config.nodes.Count; i++)
            {
                NodeDef def = config.nodes[i];
                if (string.IsNullOrEmpty(def.id))
                {
                    Debug.LogError("Level '" + config.id + "' has a node with no id.");
                    return false;
                }

                GameObject prefab = catalog.GetNodePrefab(def.type, def.plain);
                if (prefab == null)
                {
                    Debug.LogError("Catalog has no prefab for node type '" + def.type + "'.");
                    return false;
                }

                GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity, yardRoot);
                instance.transform.localScale = prefab.transform.localScale * config.grid.visualScale;
                var node = instance.GetComponent<YardNode>();
                if (node == null)
                {
                    node = instance.AddComponent<YardNode>();
                }

                node.Configure(def, config.grid);
                if (!Graph.AddNode(node))
                {
                    Debug.LogError("Duplicate node id '" + def.id + "'.");
                    return false;
                }

                if (def.hasFacing)
                {
                    instance.transform.rotation = Quaternion.Euler(
                        0f, GridMath.YawDegrees(GridMath.ToVector(def.facing)), 0f);
                }
            }

            return true;
        }

        bool BuildBelts(LevelConfig config)
        {
            for (int i = 0; i < config.belts.Count; i++)
            {
                BeltDef def = config.belts[i];
                if (!Graph.TryGetNode(def.from, out YardNode from))
                {
                    Debug.LogError("Belt '" + def.id + "' has unknown from node '" + def.from + "'.");
                    return false;
                }

                if (!Graph.TryGetNode(def.to, out YardNode to))
                {
                    Debug.LogError("Belt '" + def.id + "' has unknown to node '" + def.to + "'.");
                    return false;
                }

                var go = new GameObject(def.id);
                go.transform.SetParent(yardRoot, false);
                var belt = go.AddComponent<BeltPath>();
                belt.Configure(def, from, to, config.grid, config.rules);

                if (!Graph.AddBelt(belt))
                {
                    Debug.LogError("Duplicate belt id '" + def.id + "'.");
                    return false;
                }

                if (!ValidateBelt(belt, config.rules))
                {
                    return false;
                }

                if (!AssignOutput(from, belt, def))
                {
                    return false;
                }

                to.InBelts.Add(belt);
                BeltGeometryBuilder.Build(belt, catalog);
            }

            return true;
        }

        /// <summary>Places a belt at its declared output index so lever order is stable.</summary>
        static bool AssignOutput(YardNode from, BeltPath belt, BeltDef def)
        {
            if (def.output < 0)
            {
                Debug.LogError("Belt '" + def.id + "' has a negative output index.");
                return false;
            }

            while (from.OutBelts.Count <= def.output)
            {
                from.OutBelts.Add(null);
            }

            if (from.OutBelts[def.output] != null)
            {
                Debug.LogError("Node '" + from.NodeId + "' has two belts on output " + def.output + ".");
                return false;
            }

            from.OutBelts[def.output] = belt;
            return true;
        }

        static bool ValidateBelt(BeltPath belt, RuleSettings rules)
        {
            Vector3Int[] cells = belt.CellPoints;
            if (cells.Length < 2)
            {
                Debug.LogError("Belt '" + belt.BeltId + "' is degenerate (from and to share a cell).");
                return false;
            }

            for (int i = 0; i < cells.Length - 1; i++)
            {
                Vector3Int a = cells[i];
                Vector3Int b = cells[i + 1];
                var delta = new Vector2Int(b.x - a.x, b.y - a.y);
                if (!GridMath.IsAxisOrDiagonal(delta))
                {
                    Debug.LogError("Belt '" + belt.BeltId + "' segment " + i +
                                   " is neither axis aligned nor 45 degrees.");
                    return false;
                }

                int levelDelta = Mathf.Abs(b.z - a.z);
                if (levelDelta == 0)
                {
                    continue;
                }

                if (GridMath.IsDiagonal(delta))
                {
                    Debug.LogError("Belt '" + belt.BeltId + "' segment " + i +
                                   " changes level on a diagonal; ramps must be straight.");
                    return false;
                }

                int span = GridMath.CellSpan(delta);
                int needed = rules.minRampCellsPerLevel * levelDelta;
                if (span < needed)
                {
                    Debug.LogError("Belt '" + belt.BeltId + "' segment " + i + " ramps " + levelDelta +
                                   " level(s) over " + span + " cells; needs " + needed + ".");
                    return false;
                }
            }

            if (belt.CellLength < rules.minBeltCells || belt.CellLength > rules.maxBeltCells)
            {
                Debug.LogError("Belt '" + belt.BeltId + "' spans " + belt.CellLength +
                               " cells; allowed range is " + rules.minBeltCells + ".." + rules.maxBeltCells + ".");
                return false;
            }

            return true;
        }

        /// <summary>Two belts may share a cell only when their levels differ, i.e. an overpass.</summary>
        bool ValidateOccupancy(LevelConfig config)
        {
            var nodeCells = new HashSet<Vector2Int>();
            foreach (var pair in Graph.Nodes)
            {
                nodeCells.Add(new Vector2Int(pair.Value.CellX, pair.Value.CellZ));
            }

            var occupancy = new Dictionary<Vector2Int, List<KeyValuePair<float, string>>>();
            IReadOnlyList<BeltPath> belts = Graph.AllBelts;
            for (int b = 0; b < belts.Count; b++)
            {
                BeltPath belt = belts[b];
                foreach (KeyValuePair<Vector2Int, float> visit in EnumerateCells(belt))
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

                    for (int u = 0; u < users.Count; u++)
                    {
                        if (users[u].Value == belt.BeltId)
                        {
                            continue;
                        }

                        if (Mathf.Abs(users[u].Key - visit.Value) < 0.5f)
                        {
                            Debug.LogError("Belts '" + users[u].Value + "' and '" + belt.BeltId +
                                           "' overlap at cell (" + visit.Key.x + ", " + visit.Key.y +
                                           ") on the same level. Raise one of them to cross.");
                            return false;
                        }
                    }

                    users.Add(new KeyValuePair<float, string>(visit.Value, belt.BeltId));
                }
            }

            return true;
        }

        static IEnumerable<KeyValuePair<Vector2Int, float>> EnumerateCells(BeltPath belt)
        {
            Vector3Int[] cells = belt.CellPoints;
            for (int i = 0; i < cells.Length - 1; i++)
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
                    float level = Mathf.Lerp(a.z, b.z, span == 0 ? 0f : (float)s / span);
                    yield return new KeyValuePair<Vector2Int, float>(cell, level);
                }
            }
        }

        bool ValidateTopology()
        {
            foreach (var pair in Graph.Nodes)
            {
                YardNode node = pair.Value;
                for (int i = 0; i < node.OutBelts.Count; i++)
                {
                    if (node.OutBelts[i] == null)
                    {
                        Debug.LogError("Node '" + node.NodeId + "' is missing a belt on output " + i +
                                       "; output indices must start at 0 and be contiguous.");
                        return false;
                    }
                }

                if (node.Type == NodeType.Bay && node.OutBelts.Count > 0)
                {
                    Debug.LogError("Bay '" + node.NodeId + "' must not have outgoing belts.");
                    return false;
                }

                if (node.Type == NodeType.Inlet && node.OutBelts.Count == 0)
                {
                    Debug.LogError("Inlet '" + node.NodeId + "' has no outgoing belt.");
                    return false;
                }

                if (node.Type == NodeType.Junction && node.OutBelts.Count == 0)
                {
                    Debug.LogError("Junction '" + node.NodeId + "' is a dead end.");
                    return false;
                }
            }

            return ValidateReachability();
        }

        bool ValidateReachability()
        {
            if (Graph.Inlets.Count == 0)
            {
                Debug.LogError("Level has no inlet.");
                return false;
            }

            if (Graph.Bays.Count == 0)
            {
                Debug.LogError("Level has no truck bay.");
                return false;
            }

            var served = new HashSet<string>();
            for (int i = 0; i < Graph.Inlets.Count; i++)
            {
                YardNode inlet = Graph.Inlets[i];
                HashSet<string> bays = Graph.ReachableBays(inlet);
                if (bays.Count == 0)
                {
                    Debug.LogError("Inlet '" + inlet.NodeId + "' cannot reach any bay.");
                    return false;
                }

                served.UnionWith(bays);
            }

            for (int i = 0; i < Graph.Bays.Count; i++)
            {
                YardNode bay = Graph.Bays[i];
                if (!served.Contains(bay.NodeId))
                {
                    Debug.LogError("Bay '" + bay.NodeId + "' is unreachable from every inlet.");
                    return false;
                }
            }

            return true;
        }

        void AttachNodeBehaviours(LevelConfig config, YardDirector director)
        {
            TrafficSystem traffic = director != null ? director.Traffic : null;

            for (int i = 0; i < config.nodes.Count; i++)
            {
                NodeDef def = config.nodes[i];
                if (!Graph.TryGetNode(def.id, out YardNode node))
                {
                    continue;
                }

                if (node.Type == NodeType.Bay)
                {
                    var bay = node.GetComponent<TruckBay>();
                    if (bay == null)
                    {
                        bay = node.gameObject.AddComponent<TruckBay>();
                    }

                    bay.Configure(def.color, director);
                }
            }

            HashSet<DestinationColor> colors = Graph.BayColors();

            for (int i = 0; i < config.nodes.Count; i++)
            {
                NodeDef def = config.nodes[i];
                if (!Graph.TryGetNode(def.id, out YardNode node))
                {
                    continue;
                }

                if (node.Type == NodeType.Inlet)
                {
                    var inlet = node.GetComponent<Inlet>();
                    if (inlet == null)
                    {
                        inlet = node.gameObject.AddComponent<Inlet>();
                    }

                    inlet.Configure(node, catalog.parcelPrefab, traffic, director, colors);
                }
                else if (node.IsDiverter)
                {
                    var diverter = node.GetComponent<Diverter>();
                    if (diverter == null)
                    {
                        diverter = node.gameObject.AddComponent<Diverter>();
                    }

                    diverter.Configure(node);
                }
            }
        }

        /// <summary>Elevated nodes get their own pillar so they do not float.</summary>
        void BuildNodeSupports(LevelConfig config)
        {
            if (catalog.beltLegPrefab == null)
            {
                return;
            }

            foreach (var pair in Graph.Nodes)
            {
                YardNode node = pair.Value;
                float height = node.Level * config.grid.levelHeight;
                if (height < 0.9f)
                {
                    continue;
                }

                // Parented to the yard root, not the node: node prefabs carry a non-uniform
                // root scale that would squash the pillar.
                GameObject leg = Instantiate(catalog.beltLegPrefab, yardRoot);
                leg.name = node.NodeId + "_Leg";
                leg.transform.position = new Vector3(node.GroundPos.x, 0f, node.GroundPos.z);
                leg.transform.rotation = Quaternion.identity;
                float thickness = 1.4f * config.grid.visualScale;
                leg.transform.localScale = new Vector3(thickness, height, thickness);
            }
        }
    }
}
