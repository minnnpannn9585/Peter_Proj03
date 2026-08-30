using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>One parcel the schedule wants released right now.</summary>
    public struct SpawnRequest
    {
        public string inletId;
        public DestinationColor color;

        /// <summary>True spawns the parcel unrevealed, so its destination is unreadable.</summary>
        public bool blind;

        public override string ToString()
        {
            return inletId + ":" + color + (blind ? ":blind" : string.Empty);
        }
    }

    /// <summary>
    /// The deterministic half of parcel production, lifted out of <see cref="SpawnDirector"/> so
    /// a whole round can be replayed without an engine.
    ///
    /// Everything random is rolled once, at <see cref="Build"/> time: which inlet a wave uses,
    /// how many parcels it carries, its rhythm, each parcel's colour and whether each parcel is
    /// blind. That means the emitted sequence depends only on the seed and the authored plan,
    /// never on how belt back pressure happened to fall during the round.
    ///
    /// The host owns actual emission. <see cref="TryDequeue"/> hands out a reservation, and the
    /// host answers with <see cref="ConfirmEmit"/> or <see cref="RejectEmit"/>; a rejected
    /// parcel is retried on a later tick instead of being lost.
    /// </summary>
    public class SpawnSchedule
    {
        /// <summary>One resolved run of parcels out of one inlet.</summary>
        class Burst
        {
            public string inletId = string.Empty;
            public readonly List<SpawnRequest> items = new List<SpawnRequest>();
            public float spacing;

            /// <summary>How many items have been handed to the host at least once.</summary>
            public int offered;

            /// <summary>How many items the host has actually put on a belt.</summary>
            public int emitted;

            public float timer;

            public int Total => items.Count;
        }

        /// <summary>Bursts that run at the same time, followed by a rest.</summary>
        class Group
        {
            public readonly List<Burst> bursts = new List<Burst>();
            public float restAfter;
        }

        readonly List<Group> groups = new List<Group>();

        /// <summary>Items offered to the host and awaiting a verdict order.</summary>
        readonly List<Reservation> ready = new List<Reservation>();

        /// <summary>Items the host could not place this tick; merged back on the next tick.</summary>
        readonly List<Reservation> blocked = new List<Reservation>();

        struct Reservation
        {
            public Burst burst;
            public SpawnRequest request;
        }

        bool hasOutstanding;
        Reservation outstanding;

        int groupIndex;
        float leadIn;
        float rest;

        public int Planned { get; private set; }

        public int Emitted { get; private set; }

        public int Queued => Mathf.Max(0, Planned - Emitted);

        /// <summary>True once every parcel the plan owed has actually been released.</summary>
        public bool Finished =>
            groupIndex >= groups.Count && ready.Count == 0 && blocked.Count == 0 && !hasOutstanding;

        /// <summary>Blind parcels in the resolved plan. Fixed by the seed, so tests can assert it.</summary>
        public int PlannedBlind { get; private set; }

        public void Clear()
        {
            groups.Clear();
            ready.Clear();
            blocked.Clear();
            hasOutstanding = false;
            outstanding = default;
            groupIndex = 0;
            leadIn = 0f;
            rest = 0f;
            Planned = 0;
            Emitted = 0;
            PlannedBlind = 0;
        }

        /// <summary>
        /// Resolves an authored plan into a concrete parcel list.
        /// </summary>
        /// <param name="inletIds">Inlet ids in graph order. A wave naming none picks from these.</param>
        /// <param name="bayColors">Colours to fall back to when a wave names none.</param>
        /// <param name="plan">The authored waves.</param>
        /// <param name="modifiers">Mission twists: colour mixing and blind ratio.</param>
        /// <param name="seed">Fixes the whole sequence.</param>
        public void Build(
            IReadOnlyList<string> inletIds,
            IReadOnlyList<DestinationColor> bayColors,
            SpawnPlan plan,
            MissionModifiers modifiers,
            int seed)
        {
            Clear();
            if (inletIds == null || inletIds.Count == 0 || plan == null)
            {
                return;
            }

            var fallbackColors = new List<DestinationColor>();
            if (bayColors != null)
            {
                fallbackColors.AddRange(bayColors);
            }

            if (fallbackColors.Count == 0)
            {
                fallbackColors.Add(DestinationColor.Red);
            }

            MissionModifiers mods = modifiers ?? new MissionModifiers();
            var rng = new System.Random(seed);
            leadIn = Mathf.Max(0f, plan.startDelay.NextFloat(rng));

            int repeat = Mathf.Max(1, plan.repeat);
            for (int pass = 0; pass < repeat; pass++)
            {
                for (int w = 0; w < plan.waves.Count; w++)
                {
                    SpawnWaveDef wave = plan.waves[w];
                    Group joinTarget = wave.parallel && groups.Count > 0 ? groups[groups.Count - 1] : null;
                    Burst burst = ResolveBurst(wave, joinTarget, inletIds, fallbackColors, mods, rng);
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

            for (int g = 0; g < groups.Count; g++)
            {
                List<Burst> bursts = groups[g].bursts;
                for (int b = 0; b < bursts.Count; b++)
                {
                    Burst burst = bursts[b];
                    Planned += burst.Total;
                    for (int i = 0; i < burst.items.Count; i++)
                    {
                        if (burst.items[i].blind)
                        {
                            PlannedBlind++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Rewinds to the start of the resolved plan without re-rolling anything, so a retry
        /// replays a byte-identical parcel sequence.
        /// </summary>
        public void Rewind()
        {
            ready.Clear();
            blocked.Clear();
            hasOutstanding = false;
            outstanding = default;
            groupIndex = 0;
            rest = 0f;
            Emitted = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                List<Burst> bursts = groups[g].bursts;
                for (int b = 0; b < bursts.Count; b++)
                {
                    bursts[b].offered = 0;
                    bursts[b].emitted = 0;
                    bursts[b].timer = 0f;
                }
            }
        }

        Burst ResolveBurst(
            SpawnWaveDef wave,
            Group joinTarget,
            IReadOnlyList<string> inletIds,
            List<DestinationColor> fallbackColors,
            MissionModifiers mods,
            System.Random rng)
        {
            string inletId = PickInletId(wave, joinTarget, inletIds, rng);
            if (inletId == null)
            {
                return null;
            }

            int count = Mathf.Max(0, wave.count.NextInt(rng));
            if (count == 0)
            {
                return null;
            }

            var burst = new Burst
            {
                inletId = inletId,
                spacing = Mathf.Max(0f, wave.spacing.NextFloat(rng))
            };

            List<DestinationColor> palette = wave.colors.Count > 0 ? wave.colors : fallbackColors;

            // Without mixColors the whole burst shares one colour, decided once here.
            DestinationColor uniform = palette.Count == 1
                ? palette[0]
                : palette[rng.Next(palette.Count)];

            for (int i = 0; i < count; i++)
            {
                DestinationColor color = mods.mixColors && palette.Count > 1
                    ? palette[rng.Next(palette.Count)]
                    : uniform;

                burst.items.Add(new SpawnRequest
                {
                    inletId = inletId,
                    color = color,
                    blind = RollBlind(mods.blindRatio, rng)
                });
            }

            return burst;
        }

        /// <summary>
        /// One coin flip per parcel. The two extremes never touch the generator, so a mission
        /// with no blind parcels produces exactly the sequence it would without the mechanic.
        /// </summary>
        static bool RollBlind(float blindRatio, System.Random rng)
        {
            if (blindRatio <= 0f)
            {
                return false;
            }

            if (blindRatio >= 1f)
            {
                return true;
            }

            return rng.NextDouble() < blindRatio;
        }

        static string PickInletId(
            SpawnWaveDef wave,
            Group joinTarget,
            IReadOnlyList<string> inletIds,
            System.Random rng)
        {
            var candidates = new List<string>();
            if (wave.inlets.Count > 0)
            {
                for (int i = 0; i < wave.inlets.Count; i++)
                {
                    if (Contains(inletIds, wave.inlets[i]))
                    {
                        candidates.Add(wave.inlets[i]);
                    }
                    else
                    {
                        // Matches the previous behaviour: warn, drop the wave, and let Planned
                        // reflect what can actually be emitted so the progress bar still fills.
                        Debug.LogWarning("Spawn plan names unknown inlet '" + wave.inlets[i] + "'.");
                    }
                }
            }
            else
            {
                for (int i = 0; i < inletIds.Count; i++)
                {
                    candidates.Add(inletIds[i]);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            // A parallel burst avoids an inlet its group mates already occupy, so "run these two
            // waves at once" really does mean two streams rather than one contended one.
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

            return candidates.Count == 1 ? candidates[0] : candidates[rng.Next(candidates.Count)];
        }

        static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id))
                {
                    return true;
                }
            }

            return false;
        }

        static bool UsesInlet(Group group, string inletId)
        {
            for (int i = 0; i < group.bursts.Count; i++)
            {
                if (string.Equals(group.bursts[i].inletId, inletId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Advances the schedule and offers one parcel.
        ///
        /// Call with the real delta once per tick; if it returns true, emit the parcel and answer
        /// with <see cref="ConfirmEmit"/> or <see cref="RejectEmit"/>, then call again with
        /// <c>dt = 0</c> to drain any other parcels due on the same tick.
        /// </summary>
        public bool TryDequeue(float dt, out SpawnRequest request)
        {
            // An unanswered reservation means the host dropped it; treat that as a rejection so
            // the parcel is retried rather than silently lost.
            if (hasOutstanding)
            {
                RejectEmit();
            }

            if (dt > 0f)
            {
                // Anything the host could not place last tick gets another chance first.
                if (blocked.Count > 0)
                {
                    ready.InsertRange(0, blocked);
                    blocked.Clear();
                }

                Advance(dt);
            }

            if (ready.Count == 0)
            {
                request = default;
                return false;
            }

            outstanding = ready[0];
            ready.RemoveAt(0);
            hasOutstanding = true;
            request = outstanding.request;
            return true;
        }

        /// <summary>The host placed the parcel: count it and start the intra-burst spacing timer.</summary>
        public void ConfirmEmit()
        {
            if (!hasOutstanding)
            {
                return;
            }

            Burst burst = outstanding.burst;
            burst.emitted++;
            burst.timer = burst.spacing;
            Emitted++;
            hasOutstanding = false;
            outstanding = default;
        }

        /// <summary>The belt was full: hold the parcel over for a later tick.</summary>
        public void RejectEmit()
        {
            if (!hasOutstanding)
            {
                return;
            }

            blocked.Add(outstanding);
            hasOutstanding = false;
            outstanding = default;
        }

        void Advance(float dt)
        {
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
                return;
            }

            Group group = groups[groupIndex];
            if (StepGroup(group, dt))
            {
                rest = group.restAfter;
                groupIndex++;
            }
        }

        /// <summary>Queues everything the group owes this tick. True once it owes nothing more.</summary>
        bool StepGroup(Group group, float dt)
        {
            bool allEmitted = true;
            for (int i = 0; i < group.bursts.Count; i++)
            {
                Burst burst = group.bursts[i];
                if (burst.emitted < burst.Total)
                {
                    allEmitted = false;
                }

                if (burst.offered >= burst.Total)
                {
                    continue;
                }

                if (burst.timer > 0f)
                {
                    burst.timer -= dt;
                    continue;
                }

                ready.Add(new Reservation
                {
                    burst = burst,
                    request = burst.items[burst.offered]
                });
                burst.offered++;
            }

            return allEmitted;
        }
    }
}
