using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Replays a whole round at a fixed timestep with no rendering, driving the real pipeline:
    /// <see cref="SpawnSchedule.TryDequeue"/> -> emit -> <see cref="TrafficSystem.Tick"/> ->
    /// <see cref="MissionRuntime.Tick"/>.
    ///
    /// Two things make this worth having. It proves a mission's outcome is reproducible from its
    /// seed, and it lets balance claims ("M2 times out bare handed", "M3 needs two Scanners") be
    /// measured instead of asserted in prose.
    ///
    /// The yard is assembled from the level config directly rather than through
    /// <see cref="LevelLoader"/>, because the loader needs prefabs and a scene; the belts, nodes,
    /// gates, scanners and traffic stepper are all the real components.
    /// </summary>
    public class HeadlessRoundSim
    {
        public const float FixedStep = 1f / 60f;

        readonly List<GameObject> spawned = new List<GameObject>();
        readonly Dictionary<string, YardNode> nodes = new Dictionary<string, YardNode>();
        readonly SpawnSchedule schedule = new SpawnSchedule();

        public HeadlessRoundSim(LevelConfig config, MissionDef mission)
        {
            Config = config;
            Mission = mission;

            BeltPath.VisualScale = config.grid.visualScale;
            Graph = new YardGraph();

            var trafficGo = Track(new GameObject("Traffic"));
            Traffic = trafficGo.AddComponent<TrafficSystem>();

            var directorGo = Track(new GameObject("Director"));
            Director = directorGo.AddComponent<YardDirector>();

            BuildYard();
            Traffic.Bind(Graph, config.rules, Director);

            ApplySpeedScale(mission.modifiers.speedScale);

            schedule.Build(
                Graph.InletIds(),
                new List<DestinationColor>(Graph.BayColors()),
                mission.spawn,
                mission.modifiers,
                mission.spawn.hasSeed ? mission.spawn.seed : 0);

            Runtime = new MissionRuntime(mission, schedule.Planned);
        }

        public LevelConfig Config { get; }

        public MissionDef Mission { get; }

        public YardGraph Graph { get; }

        public TrafficSystem Traffic { get; }

        public YardDirector Director { get; }

        public MissionRuntime Runtime { get; }

        public SpawnSchedule Schedule => schedule;

        public int Planned => schedule.Planned;

        public int PlannedBlind => schedule.PlannedBlind;

        public int Emitted => schedule.Emitted;

        /// <summary>Total parcels revealed by installed scanners during the replay.</summary>
        public int RevealedTotal
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Scanners.Count; i++)
                {
                    total += Scanners[i].RevealedCount;
                }

                return total;
            }
        }

        /// <summary>
        /// Blind parcels that were never revealed, and so had to be routed by guesswork. This is
        /// the quantity the Scanner requirement is argued from.
        /// </summary>
        public int NeverRevealed => Mathf.Max(0, PlannedBlind - RevealedTotal);

        public int Ticks { get; private set; }

        public List<GateDevice> Gates { get; } = new List<GateDevice>();

        public List<ScannerDevice> Scanners { get; } = new List<ScannerDevice>();

        GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        void BuildYard()
        {
            for (int i = 0; i < Config.nodes.Count; i++)
            {
                NodeDef def = Config.nodes[i];
                var go = Track(new GameObject(def.id));
                YardNode node = go.AddComponent<YardNode>();
                node.Configure(def, Config.grid);
                Graph.AddNode(node);
                nodes[def.id] = node;

                if (def.type == NodeType.Bay)
                {
                    go.AddComponent<TruckBay>().Configure(def.color, Director);
                }
            }

            for (int i = 0; i < Config.belts.Count; i++)
            {
                BeltDef def = Config.belts[i];
                YardNode from = nodes[def.from];
                YardNode to = nodes[def.to];

                var go = Track(new GameObject(def.id));
                BeltPath belt = go.AddComponent<BeltPath>();
                belt.Configure(def, from, to, Config.grid, Config.rules);

                while (from.OutBelts.Count <= def.output)
                {
                    from.OutBelts.Add(null);
                }

                from.OutBelts[def.output] = belt;
                to.InBelts.Add(belt);
                Graph.AddBelt(belt);
            }
        }

        void ApplySpeedScale(float scale)
        {
            IReadOnlyList<BeltPath> belts = Graph.AllBelts;
            for (int i = 0; i < belts.Count; i++)
            {
                belts[i].SetMissionSpeedScale(scale);
            }
        }

        public BeltPath Belt(string beltId)
        {
            Graph.TryGetBelt(beltId, out BeltPath belt);
            return belt;
        }

        public YardNode Node(string nodeId)
        {
            return nodes.TryGetValue(nodeId, out YardNode node) ? node : null;
        }

        public GateDevice InstallGate(string beltId, int slotIndex)
        {
            BeltPath belt = Belt(beltId);
            if (belt == null)
            {
                return null;
            }

            var go = Track(new GameObject("Gate_" + beltId));
            GateDevice gate = go.AddComponent<GateDevice>();
            gate.Install(belt, slotIndex, LevelFixtures.Spec(DeviceType.Gate).holdSeconds);
            Gates.Add(gate);
            return gate;
        }

        public BoosterDevice InstallBooster(string beltId, int slotIndex)
        {
            BeltPath belt = Belt(beltId);
            if (belt == null)
            {
                return null;
            }

            var go = Track(new GameObject("Booster_" + beltId));
            BoosterDevice booster = go.AddComponent<BoosterDevice>();
            booster.Install(belt, slotIndex, LevelFixtures.Spec(DeviceType.Booster).speedBonus);
            return booster;
        }

        public ScannerDevice InstallScanner(string beltId, int slotIndex)
        {
            BeltPath belt = Belt(beltId);
            if (belt == null)
            {
                return null;
            }

            var go = Track(new GameObject("Scanner_" + beltId));
            ScannerDevice scanner = go.AddComponent<ScannerDevice>();
            scanner.Install(belt, slotIndex);
            Scanners.Add(scanner);
            return scanner;
        }

        /// <summary>
        /// A stand-in for the player's hands: whenever a diverter's front parcel is readable, throw
        /// the lever to the branch that reaches its colour. Perfect play, so a failure in a replay
        /// means the round is genuinely infeasible rather than badly driven.
        /// </summary>
        public bool AutoRouteRevealedParcels { get; set; } = true;

        Dictionary<YardNode, Dictionary<DestinationColor, int>> routing;

        void BuildRouting()
        {
            routing = new Dictionary<YardNode, Dictionary<DestinationColor, int>>();
            Dictionary<string, DestinationColor> bayColors = Graph.BayColorsById();

            foreach (KeyValuePair<string, YardNode> pair in nodes)
            {
                YardNode node = pair.Value;
                if (!node.IsDiverter)
                {
                    continue;
                }

                var table = new Dictionary<DestinationColor, int>();
                for (int output = 0; output < node.OutBelts.Count; output++)
                {
                    BeltPath belt = node.OutBelts[output];
                    if (belt == null || belt.To == null)
                    {
                        continue;
                    }

                    foreach (string bayId in Graph.ReachableBays(belt.To))
                    {
                        if (bayColors.TryGetValue(bayId, out DestinationColor color) &&
                            !table.ContainsKey(color))
                        {
                            table.Add(color, output);
                        }
                    }
                }

                routing[node] = table;
            }
        }

        void RouteDiverters()
        {
            if (routing == null)
            {
                BuildRouting();
            }

            foreach (KeyValuePair<YardNode, Dictionary<DestinationColor, int>> pair in routing)
            {
                YardNode node = pair.Key;
                Parcel next = FrontParcel(node);
                if (next == null || !next.IsRevealed || next.Data == null)
                {
                    continue;
                }

                if (pair.Value.TryGetValue(next.Data.destination, out int output))
                {
                    node.SelectedOutput = output;
                }
            }
        }

        static Parcel FrontParcel(YardNode node)
        {
            Parcel best = null;
            float bestGap = float.MaxValue;

            for (int i = 0; i < node.InBelts.Count; i++)
            {
                BeltPath belt = node.InBelts[i];
                if (belt == null || belt.Parcels.Count == 0)
                {
                    continue;
                }

                ParcelRuntime front = belt.Parcels[0];
                float gap = belt.Length - front.Distance;
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = front.Parcel;
                }
            }

            return best;
        }

        public void Begin()
        {
            Traffic.SetRunning(true);
            Runtime.Begin();
        }

        /// <summary>
        /// One step of the whole pipeline, in the order the running game uses.
        /// </summary>
        public void Tick(float dt)
        {
            Ticks++;

            for (int i = 0; i < Gates.Count; i++)
            {
                Gates[i].Advance(dt);
            }

            if (AutoRouteRevealedParcels)
            {
                RouteDiverters();
            }

            PumpSchedule(dt);
            Traffic.Tick(dt);
            Runtime.Tick(dt);
        }

        void PumpSchedule(float dt)
        {
            float step = dt;
            int guard = 0;
            while (guard++ < 64 && schedule.TryDequeue(step, out SpawnRequest request))
            {
                step = 0f;
                if (!Emit(request))
                {
                    schedule.RejectEmit();
                    break;
                }

                schedule.ConfirmEmit();
            }
        }

        /// <summary>Puts one parcel on its inlet's outgoing belt. False when the belt is full.</summary>
        bool Emit(SpawnRequest request)
        {
            if (!nodes.TryGetValue(request.inletId, out YardNode inlet))
            {
                return true;
            }

            BeltPath belt = inlet.SelectedBelt;
            if (belt == null || !Traffic.CanEnter(belt))
            {
                return false;
            }

            var go = Track(new GameObject("Parcel"));
            Parcel parcel = go.AddComponent<Parcel>();
            parcel.Initialize(new ParcelData
            {
                destination = request.color,
                isRevealed = !request.blind,
                size = ParcelSize.Small
            });

            ParcelRuntime runtime = go.AddComponent<ParcelRuntime>();
            if (!Traffic.Spawn(runtime, belt))
            {
                Object.DestroyImmediate(go);
                spawned.Remove(go);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Runs until the mission resolves, the schedule empties and the yard drains, or the
        /// budget expires. Returns the settled outcome.
        /// </summary>
        public MissionOutcome Run(MissionRecord record = null, float maxSeconds = 400f)
        {
            Begin();
            int maxTicks = Mathf.CeilToInt(maxSeconds / FixedStep);

            for (int i = 0; i < maxTicks; i++)
            {
                Tick(FixedStep);
                CountDelivered();

                if (Runtime.IsOver)
                {
                    break;
                }

                if (schedule.Finished && Traffic.LiveParcelCount == 0)
                {
                    // Nothing left to deliver: settle on whatever the objective says.
                    Runtime.ForceTimeout();
                    break;
                }
            }

            return Runtime.Resolve(record ?? new MissionRecord());
        }

        int lastCorrect;
        int lastWrong;

        /// <summary>
        /// Forwards bay results into the mission runtime. TruckBay reports to the director, which
        /// is not phase-running here, so the counters are transferred by difference instead.
        /// </summary>
        void CountDelivered()
        {
            while (lastCorrect < Director.Correct)
            {
                lastCorrect++;
                Runtime.ReportCorrect();
            }

            while (lastWrong < Director.Wrong)
            {
                lastWrong++;
                Runtime.ReportWrong();
            }
        }

        /// <summary>Jams observed by the traffic system during the replay.</summary>
        public int Jams => Director.Jams;

        public int Correct => Director.Correct;

        public int Wrong => Director.Wrong;

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
            nodes.Clear();
        }
    }
}
