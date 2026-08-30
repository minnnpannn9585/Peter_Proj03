using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Thin host around <see cref="SpawnSchedule"/>. All the scheduling logic and every random
    /// roll now live in that plain class so a round can be replayed without an engine; this
    /// component's only jobs are to find the inlets, pump the schedule, and turn each request
    /// into an actual parcel.
    /// </summary>
    public class SpawnDirector : MonoBehaviour
    {
        /// <summary>Guard against a pathological schedule monopolising a frame.</summary>
        const int MaxEmitsPerTick = 64;

        readonly SpawnSchedule schedule = new SpawnSchedule();
        readonly Dictionary<string, Inlet> inletsById = new Dictionary<string, Inlet>();
        readonly List<string> inletIds = new List<string>();

        bool running;

        /// <summary>Total parcels this round will release. Known during prep.</summary>
        public int Planned => schedule.Planned;

        /// <summary>Parcels already released onto a belt.</summary>
        public int Emitted => schedule.Emitted;

        /// <summary>Parcels still waiting in the schedule.</summary>
        public int Queued => schedule.Queued;

        /// <summary>True once the schedule has released everything it owed.</summary>
        public bool Finished => schedule.Finished;

        /// <summary>Blind parcels in the resolved plan, fixed by the seed.</summary>
        public int PlannedBlind => schedule.PlannedBlind;

        public SpawnSchedule Schedule => schedule;

        /// <summary>
        /// Resolves a plan against a freshly loaded yard. Called during prep so the HUD can show
        /// the parcel total before the round starts.
        /// </summary>
        public void Build(YardGraph graph, SpawnPlan plan, MissionModifiers modifiers)
        {
            Clear();
            if (graph == null)
            {
                return;
            }

            CollectInlets(graph);
            if (inletIds.Count == 0)
            {
                return;
            }

            SpawnPlan effective = plan != null && plan.HasWaves ? plan : BuildFallbackPlan();
            int seed = effective.hasSeed
                ? effective.seed
                : Random.Range(int.MinValue, int.MaxValue);

            var bayColors = new List<DestinationColor>(graph.BayColors());
            schedule.Build(inletIds, bayColors, effective, modifiers, seed);
        }

        /// <summary>Legacy overload used by levels that carry a top-level spawn block.</summary>
        public void Build(YardGraph graph, SpawnPlan plan)
        {
            Build(graph, plan, null);
        }

        /// <summary>Replays the resolved plan from the start, for a retry.</summary>
        public void Rewind()
        {
            schedule.Rewind();
        }

        public void Clear()
        {
            schedule.Clear();
            inletsById.Clear();
            inletIds.Clear();
            running = false;
        }

        public void SetRunning(bool value)
        {
            running = value;
        }

        void CollectInlets(YardGraph graph)
        {
            IReadOnlyList<YardNode> nodes = graph.Inlets;
            for (int i = 0; i < nodes.Count; i++)
            {
                var inlet = nodes[i].GetComponent<Inlet>();
                if (inlet == null)
                {
                    continue;
                }

                inletsById[nodes[i].NodeId] = inlet;
                inletIds.Add(nodes[i].NodeId);
            }
        }

        /// <summary>
        /// Used when a level file has no spawn block: one random burst per inlet, in order,
        /// with a rest between them. Keeps old levels playable without editing them.
        /// </summary>
        SpawnPlan BuildFallbackPlan()
        {
            var plan = new SpawnPlan { repeat = 4 };
            for (int i = 0; i < inletIds.Count; i++)
            {
                plan.waves.Add(new SpawnWaveDef());
            }

            return plan;
        }

        void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances the schedule. Public so it can be stepped deterministically.</summary>
        public void Tick(float dt)
        {
            if (!running || dt <= 0f)
            {
                return;
            }

            float step = dt;
            int emits = 0;
            while (emits < MaxEmitsPerTick && schedule.TryDequeue(step, out SpawnRequest request))
            {
                // Only the first call in a tick advances the clock; the rest drain whatever else
                // fell due on the same tick.
                step = 0f;
                emits++;

                if (!inletsById.TryGetValue(request.inletId, out Inlet inlet) || inlet == null)
                {
                    // The inlet vanished with the yard. Consuming the request keeps the schedule
                    // from spinning on something that can never be emitted.
                    schedule.ConfirmEmit();
                    continue;
                }

                if (inlet.TryEmit(request.color, request.blind))
                {
                    schedule.ConfirmEmit();
                }
                else
                {
                    // Belt is full. Defer rather than drop, and stop asking this tick.
                    schedule.RejectEmit();
                    break;
                }
            }
        }
    }
}
