using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Runs a level's authored spawn plan. Waves are resolved into concrete bursts the
    /// moment the round starts, so the total parcel count is known up front and the
    /// schedule is reproducible from the level seed.
    /// </summary>
    public class SpawnDirector : MonoBehaviour
    {
        /// <summary>One resolved run of same-colour parcels out of one inlet.</summary>
        class Burst
        {
            public Inlet inlet;
            public DestinationColor color;
            public int total;
            public int emitted;
            public float spacing;
            public float timer;

            public bool Done => emitted >= total;
        }

        /// <summary>Bursts that run at the same time, followed by a rest.</summary>
        class Group
        {
            public readonly List<Burst> bursts = new List<Burst>();
            public float restAfter;
        }

        readonly List<Group> groups = new List<Group>();
        readonly List<Inlet> inlets = new List<Inlet>();
        readonly Dictionary<string, Inlet> inletsById = new Dictionary<string, Inlet>();
        readonly List<DestinationColor> fallbackColors = new List<DestinationColor>();
        System.Random rng;
        int groupIndex;
        float leadIn;
        float rest;
        bool running;

        /// <summary>Total parcels this level will release.</summary>
        public int Planned { get; private set; }

        /// <summary>Parcels already released onto a belt.</summary>
        public int Emitted { get; private set; }

        /// <summary>Parcels still waiting in the schedule.</summary>
        public int Queued => Mathf.Max(0, Planned - Emitted);

        /// <summary>True once the last group has released everything it owed.</summary>
        public bool Finished => groupIndex >= groups.Count;

        /// <summary>
        /// Resolves the plan against a freshly loaded yard. Call once per level load so the
        /// prep HUD can already show the parcel total.
        /// </summary>
        public void Build(YardGraph graph, SpawnPlan plan)
        {
            Clear();
            if (graph == null)
            {
                return;
            }

            CollectInlets(graph);
            if (inlets.Count == 0)
            {
                return;
            }

            SpawnPlan effective = plan != null && plan.HasWaves ? plan : BuildFallbackPlan();
            int seed = effective.hasSeed ? effective.seed : Random.Range(int.MinValue, int.MaxValue);
            rng = new System.Random(seed);
            leadIn = Mathf.Max(0f, effective.startDelay.NextFloat(rng));
            BuildGroups(effective);

            Planned = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                List<Burst> bursts = groups[g].bursts;
                for (int b = 0; b < bursts.Count; b++)
                {
                    Planned += bursts[b].total;
                }
            }
        }

        void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Clear()
        {
            groups.Clear();
            inlets.Clear();
            inletsById.Clear();
            fallbackColors.Clear();
            groupIndex = 0;
            leadIn = 0f;
            rest = 0f;
            running = false;
            Planned = 0;
            Emitted = 0;
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

                inlets.Add(inlet);
                inletsById[nodes[i].NodeId] = inlet;
            }

            fallbackColors.AddRange(graph.BayColors());
            if (fallbackColors.Count == 0)
            {
                fallbackColors.Add(DestinationColor.Red);
            }
        }

        /// <summary>
        /// Used when a level file has no spawn block: one random burst per inlet, in order,
        /// with a rest between them. Keeps old levels playable without editing them.
        /// </summary>
        SpawnPlan BuildFallbackPlan()
        {
            var plan = new SpawnPlan { repeat = 4 };
            for (int i = 0; i < inlets.Count; i++)
            {
                plan.waves.Add(new SpawnWaveDef());
            }

            return plan;
        }

        void BuildGroups(SpawnPlan plan)
        {
            for (int pass = 0; pass < plan.repeat; pass++)
            {
                for (int w = 0; w < plan.waves.Count; w++)
                {
                    SpawnWaveDef wave = plan.waves[w];
                    Group joinTarget = wave.parallel && groups.Count > 0 ? groups[groups.Count - 1] : null;
                    Burst burst = ResolveBurst(wave, joinTarget);
                    if (burst == null)
                    {
                        continue;
                    }

                    float restAfter = Mathf.Max(0f, wave.delayAfter.NextFloat(rng));
                    if (joinTarget != null)
                    {
                        joinTarget.bursts.Add(burst);
                        joinTarget.restAfter = Mathf.Max(joinTarget.restAfter, restAfter);
                        continue;
                    }

                    var group = new Group { restAfter = restAfter };
                    group.bursts.Add(burst);
                    groups.Add(group);
                }
            }
        }

        /// <summary>
        /// Turns one authored wave into a concrete burst. <paramref name="joinTarget"/> is the
        /// group this burst will share, so a random pick avoids an inlet already busy there.
        /// </summary>
        Burst ResolveBurst(SpawnWaveDef wave, Group joinTarget)
        {
            Inlet inlet = PickInlet(wave, joinTarget);
            if (inlet == null)
            {
                return null;
            }

            int count = Mathf.Max(0, wave.count.NextInt(rng));
            if (count == 0)
            {
                return null;
            }

            return new Burst
            {
                inlet = inlet,
                color = PickColor(wave),
                total = count,
                spacing = Mathf.Max(0f, wave.spacing.NextFloat(rng))
            };
        }

        Inlet PickInlet(SpawnWaveDef wave, Group joinTarget)
        {
            var candidates = new List<Inlet>();
            if (wave.inlets.Count > 0)
            {
                for (int i = 0; i < wave.inlets.Count; i++)
                {
                    if (inletsById.TryGetValue(wave.inlets[i], out Inlet named))
                    {
                        candidates.Add(named);
                    }
                    else
                    {
                        Debug.LogWarning("Spawn plan names unknown inlet '" + wave.inlets[i] + "'.");
                    }
                }
            }
            else
            {
                candidates.AddRange(inlets);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            if (joinTarget != null && candidates.Count > 1)
            {
                for (int i = candidates.Count - 1; i >= 0; i--)
                {
                    if (UsesInlet(joinTarget, candidates[i]))
                    {
                        candidates.RemoveAt(i);
                    }
                }

                if (candidates.Count == 0)
                {
                    return null;
                }
            }

            return candidates[rng.Next(candidates.Count)];
        }

        DestinationColor PickColor(SpawnWaveDef wave)
        {
            if (wave.colors.Count == 1)
            {
                return wave.colors[0];
            }

            if (wave.colors.Count > 1)
            {
                return wave.colors[rng.Next(wave.colors.Count)];
            }

            return fallbackColors[rng.Next(fallbackColors.Count)];
        }

        static bool UsesInlet(Group group, Inlet inlet)
        {
            for (int i = 0; i < group.bursts.Count; i++)
            {
                if (group.bursts[i].inlet == inlet)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Advances the schedule. Public so it can be stepped deterministically.</summary>
        public void Tick(float dt)
        {
            if (!running || dt <= 0f)
            {
                return;
            }

            if (leadIn > 0f)
            {
                leadIn -= dt;
                return;
            }

            if (rest > 0f)
            {
                rest -= dt;
                return;
            }

            if (groupIndex >= groups.Count)
            {
                running = false;
                return;
            }

            Group group = groups[groupIndex];
            if (StepGroup(group, dt))
            {
                rest = group.restAfter;
                groupIndex++;
            }
        }

        /// <summary>Returns true once every burst in the group has released all its parcels.</summary>
        bool StepGroup(Group group, float dt)
        {
            bool done = true;
            for (int i = 0; i < group.bursts.Count; i++)
            {
                Burst burst = group.bursts[i];
                if (burst.Done)
                {
                    continue;
                }

                done = false;
                if (burst.timer > 0f)
                {
                    burst.timer -= dt;
                    continue;
                }

                if (burst.inlet == null)
                {
                    burst.emitted = burst.total;
                    continue;
                }

                if (burst.inlet.TryEmit(burst.color))
                {
                    burst.emitted++;
                    Emitted++;
                    burst.timer = burst.spacing;
                }
            }

            return done;
        }
    }
}
