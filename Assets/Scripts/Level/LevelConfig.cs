using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    public class LevelConfig
    {
        public string id = string.Empty;
        public string displayName = string.Empty;

        /// <summary>Level unlocked once every mission here is cleared. Empty means "last level".</summary>
        public string nextLevel = string.Empty;

        public GridSettings grid = new GridSettings();
        public RuleSettings rules = new RuleSettings();
        public List<LoadoutEntry> loadout = new List<LoadoutEntry>();
        public GateSettings gate = new GateSettings();
        public List<NodeDef> nodes = new List<NodeDef>();
        public List<BeltDef> belts = new List<BeltDef>();
        public SpawnPlan spawn = new SpawnPlan();

        /// <summary>Shop offer for this level. Empty when the level authors no "devices" block.</summary>
        public DeviceCatalogConfig devices = new DeviceCatalogConfig();

        /// <summary>Selectable rounds. Empty when the level authors no "missions" block.</summary>
        public List<MissionDef> missions = new List<MissionDef>();

        public bool TryGetMission(string missionId, out MissionDef mission)
        {
            for (int i = 0; i < missions.Count; i++)
            {
                if (string.Equals(missions[i].id, missionId))
                {
                    mission = missions[i];
                    return true;
                }
            }

            mission = null;
            return false;
        }

        public int LoadoutCount(DeviceType device)
        {
            for (int i = 0; i < loadout.Count; i++)
            {
                if (loadout[i].device == device)
                {
                    return loadout[i].count;
                }
            }

            return 0;
        }
    }

    public class GridSettings
    {
        public float size = 2f;
        public float levelHeight = 1.5f;

        /// <summary>Uniform size multiplier for every yard visual. 1 is the authored prefab size.</summary>
        public float visualScale = 2f;

        /// <summary>Extra cells of empty margin kept around the content when framing the camera.</summary>
        public float viewPadCells = 1.25f;

        public int cellsX;
        public int cellsZ;
        public int originX;
        public int originZ;
    }

    public class RuleSettings
    {
        public float baseSpeed = 5f;
        public float minGap = 0.75f;
        public float jamStallSeconds = 3f;
        public float fastMultiplier = 2f;
        public int minBeltCells = 2;
        public int maxBeltCells = 6;
        public int minRampCellsPerLevel = 2;
    }

    public class GateSettings
    {
        public float holdSeconds = 8f;
    }

    public class LoadoutEntry
    {
        public DeviceType device = DeviceType.Gate;
        public int count;
    }

    public class NodeDef
    {
        public string id = string.Empty;
        public NodeType type = NodeType.Junction;
        public int cellX;
        public int cellZ;
        public int level;
        public Vector2Int facing = new Vector2Int(1, 0);
        public bool hasFacing;
        public DestinationColor color = DestinationColor.Red;
        public bool hasColor;
        public float spawnInterval = 2f;
        public bool plain;
    }

    public class PathPoint
    {
        public int cellX;
        public int cellZ;
        public int level;

        /// <summary>False when the JSON omitted the level, meaning "keep the previous level".</summary>
        public bool hasLevel;
    }

    public class BeltDef
    {
        public string id = string.Empty;
        public string from = string.Empty;
        public string to = string.Empty;
        public int output;
        public List<PathPoint> path = new List<PathPoint>();
        public float speed = 1f;
        public bool canPause;
        public bool canSpeed;
        public bool allowInstall = true;
    }

    public static class LevelConfigParser
    {
        public static LevelConfig Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException("Level JSON is empty.");
            }

            JsonValue root = MiniJson.Parse(json);
            if (root.Kind != JsonKind.Object)
            {
                throw new InvalidOperationException("Level JSON root must be an object.");
            }

            var config = new LevelConfig
            {
                id = root["id"].AsString(),
                displayName = root["displayName"].AsString(),
                nextLevel = root["nextLevel"].AsString()
            };

            ReadGrid(root["grid"], config.grid);
            ReadRules(root["rules"], config.rules);
            ReadGate(root["gate"], config.gate);
            ReadLoadout(root["loadout"], config.loadout);
            ReadDevices(root["devices"], config.devices, config.gate);
            ReadNodes(root["nodes"], config.nodes);
            ReadBelts(root["belts"], config.belts);
            SpawnPlanParser.Read(root["spawn"], config.spawn);
            MissionParser.Read(root["missions"], config.missions);
            return config;
        }

        /// <summary>
        /// Reads the "devices" block: what the shop sells and how each device behaves. A missing
        /// block leaves the catalog empty, which renders as an empty shop rather than an error,
        /// so a level can still be played bare handed.
        /// </summary>
        static void ReadDevices(JsonValue value, DeviceCatalogConfig catalog, GateSettings gate)
        {
            catalog.Clear();
            if (value.Kind != JsonKind.Object)
            {
                return;
            }

            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                DeviceType device = DeviceTypes.All[i];
                JsonValue item = value[DeviceTypes.JsonKey(device)];
                if (item.Kind != JsonKind.Object)
                {
                    continue;
                }

                var spec = new DeviceSpec
                {
                    device = device,
                    price = Mathf.Max(0, item["price"].AsInt()),
                    cap = Mathf.Max(0, item["cap"].AsInt())
                };

                if (item.Has("target") && !InstallTargets.TryParse(item["target"].AsString(), out spec.target))
                {
                    throw new InvalidOperationException(
                        "Device '" + DeviceTypes.JsonKey(device) + "' has unknown target '" +
                        item["target"].AsString() + "'; expected \"beltSlot\" or \"node\".");
                }

                spec.holdSeconds = Mathf.Max(0.5f, item["holdSeconds"].AsFloat(gate.holdSeconds));
                spec.speedBonus = Mathf.Max(1f, item["speedBonus"].AsFloat(spec.speedBonus));
                spec.switchCooldown = Mathf.Max(0f, item["switchCooldown"].AsFloat(spec.switchCooldown));
                spec.skipBlind = item["skipBlind"].AsBool(spec.skipBlind);

                catalog.Add(spec);

                // Keep the legacy gate block and the new devices block from disagreeing.
                if (device == DeviceType.Gate && item.Has("holdSeconds"))
                {
                    gate.holdSeconds = spec.holdSeconds;
                }
            }
        }

        static void ReadGrid(JsonValue value, GridSettings grid)
        {
            if (!value.Exists)
            {
                return;
            }

            grid.size = Mathf.Max(0.1f, value["size"].AsFloat(grid.size));
            grid.levelHeight = Mathf.Max(0.1f, value["levelHeight"].AsFloat(grid.levelHeight));
            grid.visualScale = Mathf.Clamp(value["visualScale"].AsFloat(grid.visualScale), 0.25f, 4f);
            grid.viewPadCells = Mathf.Clamp(value["viewPadCells"].AsFloat(grid.viewPadCells), 0f, 6f);

            JsonValue cells = value["cells"];
            if (cells.Kind == JsonKind.Array && cells.Count >= 2)
            {
                grid.cellsX = cells[0].AsInt();
                grid.cellsZ = cells[1].AsInt();
            }

            JsonValue origin = value["origin"];
            if (origin.Kind == JsonKind.Array && origin.Count >= 2)
            {
                grid.originX = origin[0].AsInt();
                grid.originZ = origin[1].AsInt();
            }
        }

        static void ReadRules(JsonValue value, RuleSettings rules)
        {
            if (!value.Exists)
            {
                return;
            }

            rules.baseSpeed = Mathf.Max(0.1f, value["baseSpeed"].AsFloat(rules.baseSpeed));
            rules.minGap = Mathf.Max(0.05f, value["minGap"].AsFloat(rules.minGap));
            rules.jamStallSeconds = Mathf.Max(0.5f, value["jamStallSeconds"].AsFloat(rules.jamStallSeconds));
            rules.fastMultiplier = Mathf.Max(1.05f, value["fastMultiplier"].AsFloat(rules.fastMultiplier));
            rules.minBeltCells = Mathf.Max(1, value["minBeltCells"].AsInt(rules.minBeltCells));
            rules.maxBeltCells = Mathf.Max(rules.minBeltCells, value["maxBeltCells"].AsInt(rules.maxBeltCells));
            rules.minRampCellsPerLevel = Mathf.Max(1, value["minRampCellsPerLevel"].AsInt(rules.minRampCellsPerLevel));
        }

        static void ReadGate(JsonValue value, GateSettings gate)
        {
            if (!value.Exists)
            {
                return;
            }

            gate.holdSeconds = Mathf.Max(0.5f, value["holdSeconds"].AsFloat(gate.holdSeconds));
        }

        static void ReadLoadout(JsonValue value, List<LoadoutEntry> loadout)
        {
            if (value.Kind != JsonKind.Array)
            {
                return;
            }

            for (int i = 0; i < value.Count; i++)
            {
                JsonValue item = value[i];
                string deviceName = item["device"].AsString();
                if (!Enum.TryParse(deviceName, true, out DeviceType device))
                {
                    throw new InvalidOperationException("Unknown loadout device '" + deviceName + "'.");
                }

                loadout.Add(new LoadoutEntry
                {
                    device = device,
                    count = Mathf.Max(0, item["count"].AsInt())
                });
            }
        }

        static void ReadNodes(JsonValue value, List<NodeDef> nodes)
        {
            if (value.Kind != JsonKind.Array)
            {
                throw new InvalidOperationException("Level JSON needs a 'nodes' array.");
            }

            for (int i = 0; i < value.Count; i++)
            {
                JsonValue item = value[i];
                var node = new NodeDef
                {
                    id = item["id"].AsString(),
                    level = item["level"].AsInt(),
                    plain = item["plain"].AsBool(),
                    spawnInterval = Mathf.Max(0.2f, item["interval"].AsFloat(2f))
                };

                string typeName = item["type"].AsString();
                if (!Enum.TryParse(typeName, true, out node.type))
                {
                    throw new InvalidOperationException(
                        "Node '" + node.id + "' has unknown type '" + typeName + "'.");
                }

                JsonValue cell = item["cell"];
                if (cell.Kind != JsonKind.Array || cell.Count < 2)
                {
                    throw new InvalidOperationException("Node '" + node.id + "' needs cell [x, z].");
                }

                node.cellX = cell[0].AsInt();
                node.cellZ = cell[1].AsInt();
                if (cell.Count >= 3)
                {
                    node.level = cell[2].AsInt();
                }

                if (item.Has("facing"))
                {
                    node.hasFacing = GridMath.TryParseFacing(item["facing"].AsString(), out node.facing);
                }

                if (item.Has("color"))
                {
                    node.hasColor = DestinationPalette.TryParse(item["color"].AsString(), out node.color);
                    if (!node.hasColor)
                    {
                        throw new InvalidOperationException(
                            "Node '" + node.id + "' has unknown color '" + item["color"].AsString() + "'.");
                    }
                }

                nodes.Add(node);
            }
        }

        static void ReadBelts(JsonValue value, List<BeltDef> belts)
        {
            if (value.Kind != JsonKind.Array)
            {
                throw new InvalidOperationException("Level JSON needs a 'belts' array.");
            }

            for (int i = 0; i < value.Count; i++)
            {
                JsonValue item = value[i];
                var belt = new BeltDef
                {
                    id = item["id"].AsString(),
                    from = item["from"].AsString(),
                    to = item["to"].AsString(),
                    output = item["output"].AsInt(),
                    speed = Mathf.Max(0.1f, item["speed"].AsFloat(1f)),
                    canPause = item["canPause"].AsBool(),
                    canSpeed = item["canSpeed"].AsBool(),
                    allowInstall = item.Has("allowInstall") ? item["allowInstall"].AsBool(true) : true
                };

                JsonValue path = item["path"];
                if (path.Kind == JsonKind.Array)
                {
                    for (int p = 0; p < path.Count; p++)
                    {
                        JsonValue point = path[p];
                        if (point.Kind != JsonKind.Array || point.Count < 2)
                        {
                            throw new InvalidOperationException(
                                "Belt '" + belt.id + "' path point " + p + " must be [x, z] or [x, z, level].");
                        }

                        belt.path.Add(new PathPoint
                        {
                            cellX = point[0].AsInt(),
                            cellZ = point[1].AsInt(),
                            level = point.Count >= 3 ? point[2].AsInt() : 0,
                            hasLevel = point.Count >= 3
                        });
                    }
                }

                belts.Add(belt);
            }
        }
    }
}
