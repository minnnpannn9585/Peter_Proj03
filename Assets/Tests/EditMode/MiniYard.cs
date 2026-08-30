using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Builds a small yard out of real components without going through
    /// <see cref="LevelLoader"/>, so a belt, a gate and the traffic stepper can be exercised in
    /// Edit Mode with no prefabs, no rendering and no scene.
    ///
    /// Everything is driven at an explicit timestep: <see cref="Tick"/> steps the gates and then
    /// the traffic system, which is what makes the gate/jam and speed-channel regressions
    /// reproducible rather than frame-rate dependent.
    /// </summary>
    public class MiniYard
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        public MiniYard(RuleSettings rules = null, GridSettings grid = null)
        {
            Rules = rules ?? new RuleSettings
            {
                baseSpeed = 5f,
                minGap = 1.15f,
                jamStallSeconds = 3f,
                fastMultiplier = 2f,
                minBeltCells = 2,
                maxBeltCells = 8,
                minRampCellsPerLevel = 2
            };

            Grid = grid ?? new GridSettings
            {
                size = 2f,
                levelHeight = 1.5f,
                visualScale = 2.1f,
                viewPadCells = 1.25f,
                cellsX = 36,
                cellsZ = 24
            };

            // Slot positions depend on this static, so it must match the level being imitated.
            BeltPath.VisualScale = Grid.visualScale;

            Graph = new YardGraph();

            var trafficGo = Track(new GameObject("TrafficSystem"));
            Traffic = trafficGo.AddComponent<TrafficSystem>();

            var directorGo = Track(new GameObject("YardDirector"));
            Director = directorGo.AddComponent<YardDirector>();

            Traffic.Bind(Graph, Rules, Director);
        }

        public RuleSettings Rules { get; }

        public GridSettings Grid { get; }

        public YardGraph Graph { get; }

        public TrafficSystem Traffic { get; }

        /// <summary>Real director, used only as the jam sink. Its phase stays Prep.</summary>
        public YardDirector Director { get; }

        public List<GateDevice> Gates { get; } = new List<GateDevice>();

        public List<ScannerDevice> Scanners { get; } = new List<ScannerDevice>();

        public List<ParcelRuntime> Parcels { get; } = new List<ParcelRuntime>();

        GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        public YardNode AddNode(string id, NodeType type, int cellX, int cellZ,
            DestinationColor color = DestinationColor.Red)
        {
            var go = Track(new GameObject(id));
            YardNode node = go.AddComponent<YardNode>();
            node.Configure(new NodeDef
            {
                id = id,
                type = type,
                cellX = cellX,
                cellZ = cellZ,
                level = 0,
                color = color,
                hasColor = type == NodeType.Bay
            }, Grid);

            Graph.AddNode(node);
            return node;
        }

        public BeltPath AddBelt(
            string id, YardNode from, YardNode to, int output = 0, bool allowInstall = true,
            float speed = 1f)
        {
            var go = Track(new GameObject(id));
            BeltPath belt = go.AddComponent<BeltPath>();
            belt.Configure(new BeltDef
            {
                id = id,
                from = from.NodeId,
                to = to.NodeId,
                output = output,
                allowInstall = allowInstall,
                speed = speed
            }, from, to, Grid, Rules);

            while (from.OutBelts.Count <= output)
            {
                from.OutBelts.Add(null);
            }

            from.OutBelts[output] = belt;
            to.InBelts.Add(belt);
            Graph.AddBelt(belt);
            return belt;
        }

        /// <summary>An inlet, one long belt and a bay. The simplest thing that can queue and jam.</summary>
        public BeltPath BuildStraightLine(int cells = 6, bool allowInstall = true)
        {
            YardNode inlet = AddNode("in_test", NodeType.Inlet, 2, 10);
            YardNode bay = AddNode("bay_test", NodeType.Bay, 2 + cells, 10);
            return AddBelt("b_test", inlet, bay, 0, allowInstall);
        }

        public GateDevice InstallGate(BeltPath belt, int slotIndex, float holdSeconds = 8f)
        {
            var go = Track(new GameObject("Gate"));
            GateDevice gate = go.AddComponent<GateDevice>();
            gate.Install(belt, slotIndex, holdSeconds);
            Gates.Add(gate);
            return gate;
        }

        public BoosterDevice InstallBooster(BeltPath belt, int slotIndex, float speedBonus = 1.6f)
        {
            var go = Track(new GameObject("Booster"));
            BoosterDevice booster = go.AddComponent<BoosterDevice>();
            booster.Install(belt, slotIndex, speedBonus);
            return booster;
        }

        public ScannerDevice InstallScanner(BeltPath belt, int slotIndex)
        {
            var go = Track(new GameObject("Scanner"));
            ScannerDevice scanner = go.AddComponent<ScannerDevice>();
            scanner.Install(belt, slotIndex);
            Scanners.Add(scanner);
            return scanner;
        }

        /// <summary>
        /// Puts a parcel on a belt without an Inlet or a prefab. Returns null when the belt has
        /// no room, mirroring <see cref="Inlet.TryEmit"/>.
        /// </summary>
        public ParcelRuntime Emit(BeltPath belt, DestinationColor color, bool blind = false)
        {
            var go = Track(new GameObject("Parcel"));
            Parcel parcel = go.AddComponent<Parcel>();
            parcel.Initialize(new ParcelData
            {
                destination = color,
                isRevealed = !blind,
                size = ParcelSize.Small
            });

            ParcelRuntime runtime = go.AddComponent<ParcelRuntime>();
            if (!Traffic.Spawn(runtime, belt))
            {
                Object.DestroyImmediate(go);
                spawned.Remove(go);
                return null;
            }

            Parcels.Add(runtime);
            return runtime;
        }

        public void SetRunning(bool running)
        {
            Traffic.SetRunning(running);
        }

        /// <summary>
        /// One simulation step: gates age first (so a gate that expires this tick is already open
        /// when parcels move), then traffic advances.
        /// </summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < Gates.Count; i++)
            {
                Gates[i].Advance(dt);
            }

            Traffic.Tick(dt);
        }

        /// <summary>Runs for a wall-clock duration at a fixed timestep.</summary>
        public void Run(float seconds, float dt = 1f / 60f)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
            for (int i = 0; i < steps; i++)
            {
                Tick(dt);
            }
        }

        public void Dispose()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();
        }
    }
}
