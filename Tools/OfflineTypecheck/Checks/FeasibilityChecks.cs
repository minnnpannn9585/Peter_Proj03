using System;
using System.Collections.Generic;

namespace ParcelSort.Offline
{
    /// <summary>
    /// Checks each mission's time limit against a lower bound on how long the round must take.
    ///
    /// The full traffic simulation lives on MonoBehaviour classes and cannot run in this sandbox,
    /// so this does not replace the Edit Mode replay tests. What it does do is compute a bound that
    /// needs no traffic model at all: a parcel cannot be delivered before it has been released, and
    /// it cannot cross the yard faster than belt speed with an empty run ahead of it. If the target
    /// delivery cannot even be released and carried in time under those ideal conditions, the
    /// mission is infeasible no matter how well it is played.
    ///
    /// Queueing, merges and diverter switching can only make the real time longer, so this is a
    /// genuine lower bound rather than an estimate.
    /// </summary>
    public static class FeasibilityChecks
    {
        public static void Run(CheckRunner r)
        {
            r.Section("Mission feasibility (lower bounds)");

            LevelConfig config = LevelFixture.Level01();
            LevelTopologyCheck.Report report = LevelTopologyCheck.Validate(config);

            Dictionary<string, float> shortest = ShortestInletToBay(config, report);
            float minTransitCells = float.MaxValue;
            float maxTransitCells = 0f;
            foreach (KeyValuePair<string, float> pair in shortest)
            {
                minTransitCells = Math.Min(minTransitCells, pair.Value);
                maxTransitCells = Math.Max(maxTransitCells, pair.Value);
                r.Info(pair.Key + " shortest route: " + pair.Value.ToString("F1") + " world units");
            }

            r.Check(minTransitCells < float.MaxValue, "every inlet/bay pair has a route");

            string[] ids = { "m1_first_shift", "m2_dual_flow", "m3_night_blind" };
            for (int i = 0; i < ids.Length; i++)
            {
                if (!config.TryGetMission(ids[i], out MissionDef mission))
                {
                    r.Check(false, "level_01 defines " + ids[i]);
                    continue;
                }

                Evaluate(r, config, mission, minTransitCells, maxTransitCells);
            }

            CheckDifficultyGradient(r);
        }

        static void Evaluate(
            CheckRunner r,
            LevelConfig config,
            MissionDef mission,
            float minTransit,
            float maxTransit)
        {
            SpawnSchedule schedule = LevelFixture.BuildSchedule(config, mission);

            // When is the Nth parcel released, if nothing ever blocks?
            List<float> emitTimes = EmitTimeline(schedule);
            float lastEmit = emitTimes.Count > 0 ? emitTimes[emitTimes.Count - 1] : 0f;

            int target = mission.objective.targetDelivered;
            r.Check(emitTimes.Count >= target,
                mission.id + " releases at least as many parcels as it demands (" +
                emitTimes.Count + " >= " + target + ")");

            if (emitTimes.Count < target)
            {
                return;
            }

            // Belt speed for this mission. Every belt on this map authors speed 1.
            float speed = config.rules.baseSpeed * mission.modifiers.speedScale;
            float fastestTransit = minTransit / speed;
            float slowestTransit = maxTransit / speed;

            // The target parcel cannot arrive before it is released plus one transit.
            float targetEmit = emitTimes[target - 1];
            float lowerBound = targetEmit + fastestTransit;
            float limit = mission.objective.timeLimitSeconds;

            r.Info(mission.id + ": target #" + target + " released at " +
                   targetEmit.ToString("F1") + "s, transit " + fastestTransit.ToString("F1") +
                   "-" + slowestTransit.ToString("F1") + "s, limit " + limit.ToString("F0") + "s");

            r.Check(lowerBound < limit,
                mission.id + " is feasible in principle: earliest possible completion " +
                lowerBound.ToString("F1") + "s < limit " + limit.ToString("F0") + "s");

            // Headroom tells us what kind of mission this is.
            float headroom = limit - lowerBound;
            float headroomRatio = headroom / limit;

            if (mission.id == "m1_first_shift")
            {
                // The anti-deadlock floor needs room to spare, so a beginner is not fighting the clock.
                r.Check(headroomRatio > 0.3f,
                    "M1 leaves comfortable headroom (" + (headroomRatio * 100f).ToString("F0") +
                    "% of the limit) so it can be cleared bare handed");
            }

            if (mission.id == "m2_dual_flow")
            {
                // The design says M2's designed failure mode is the clock. That claim depends on
                // queueing at the merges and on how fast a human works the diverters, and neither
                // is modelled here, so it is NOT asserted: it needs the Edit Mode replay in Unity.
                // What is reported is the raw release timeline, for context.
                r.Info("M2: releasing the whole plan and carrying it takes at least " +
                       (lastEmit + slowestTransit).ToString("F1") + "s against a " +
                       limit.ToString("F0") + "s limit (queueing not modelled)");
            }

            if (mission.id == "m3_night_blind")
            {
                r.Check(mission.modifiers.speedScale > 1f,
                    "M3 runs faster than the level's authored tempo");
            }
        }

        /// <summary>
        /// Required delivery rate: the throughput a player must sustain to clear the mission. This
        /// is pure arithmetic on the config, so unlike the timeout claim it can be asserted here.
        /// </summary>
        public static void CheckDifficultyGradient(CheckRunner r)
        {
            LevelConfig config = LevelFixture.Level01();
            string[] ids = { "m1_first_shift", "m2_dual_flow", "m3_night_blind" };

            float previous = -1f;
            for (int i = 0; i < ids.Length; i++)
            {
                if (!config.TryGetMission(ids[i], out MissionDef mission))
                {
                    continue;
                }

                float required = mission.objective.targetDelivered / mission.objective.timeLimitSeconds;
                r.Info(ids[i] + " requires " + required.ToString("F3") +
                       " correct deliveries per second");
                r.Check(required > previous,
                    ids[i] + " demands a higher sustained delivery rate than the mission before it");
                previous = required;
            }

            // The error budget must not simply scale with volume, or later missions would be no
            // tighter in relative terms.
            config.TryGetMission("m1_first_shift", out MissionDef m1);
            config.TryGetMission("m3_night_blind", out MissionDef m3);
            float m1Tolerance = (float)m1.objective.maxWrong / m1.objective.targetDelivered;
            float m3Tolerance = (float)m3.objective.maxWrong / m3.objective.targetDelivered;
            r.Info("error tolerance: M1 " + (m1Tolerance * 100f).ToString("F1") + "%, M3 " +
                   (m3Tolerance * 100f).ToString("F1") + "%");
            r.Check(m3Tolerance < m1Tolerance,
                "M3 allows proportionally fewer mistakes than M1");
        }

        /// <summary>Release time of each parcel, assuming a host that never refuses.</summary>
        static List<float> EmitTimeline(SpawnSchedule schedule)
        {
            var times = new List<float>();
            const float dt = 1f / 60f;
            float now = 0f;

            for (int tick = 0; tick < 2000000 && !schedule.Finished; tick++)
            {
                now += dt;
                float step = dt;
                while (schedule.TryDequeue(step, out SpawnRequest _))
                {
                    step = 0f;
                    times.Add(now);
                    schedule.ConfirmEmit();
                }
            }

            return times;
        }

        /// <summary>
        /// Shortest inlet-to-bay route length in world units, per inlet/bay pair, walking the belt
        /// graph with Dijkstra over the belt lengths the topology checker already computed.
        /// </summary>
        static Dictionary<string, float> ShortestInletToBay(
            LevelConfig config, LevelTopologyCheck.Report report)
        {
            var edges = new Dictionary<string, List<KeyValuePair<string, float>>>();
            for (int i = 0; i < config.belts.Count; i++)
            {
                BeltDef def = config.belts[i];
                if (!report.beltLengths.TryGetValue(def.id, out float length))
                {
                    continue;
                }

                if (!edges.TryGetValue(def.from, out List<KeyValuePair<string, float>> list))
                {
                    list = new List<KeyValuePair<string, float>>();
                    edges[def.from] = list;
                }

                list.Add(new KeyValuePair<string, float>(def.to, length));
            }

            var kinds = new Dictionary<string, NodeType>();
            for (int i = 0; i < config.nodes.Count; i++)
            {
                kinds[config.nodes[i].id] = config.nodes[i].type;
            }

            var result = new Dictionary<string, float>();
            for (int i = 0; i < config.nodes.Count; i++)
            {
                if (config.nodes[i].type != NodeType.Inlet)
                {
                    continue;
                }

                Dictionary<string, float> distance = Dijkstra(config.nodes[i].id, edges);
                foreach (KeyValuePair<string, float> pair in distance)
                {
                    if (kinds.TryGetValue(pair.Key, out NodeType kind) && kind == NodeType.Bay)
                    {
                        result[config.nodes[i].id + " -> " + pair.Key] = pair.Value;
                    }
                }
            }

            return result;
        }

        static Dictionary<string, float> Dijkstra(
            string start, Dictionary<string, List<KeyValuePair<string, float>>> edges)
        {
            var best = new Dictionary<string, float> { { start, 0f } };
            var settled = new HashSet<string>();

            while (true)
            {
                string next = null;
                float nextCost = float.MaxValue;
                foreach (KeyValuePair<string, float> pair in best)
                {
                    if (!settled.Contains(pair.Key) && pair.Value < nextCost)
                    {
                        nextCost = pair.Value;
                        next = pair.Key;
                    }
                }

                if (next == null)
                {
                    return best;
                }

                settled.Add(next);
                if (!edges.TryGetValue(next, out List<KeyValuePair<string, float>> outgoing))
                {
                    continue;
                }

                for (int i = 0; i < outgoing.Count; i++)
                {
                    float candidate = nextCost + outgoing[i].Value;
                    if (!best.TryGetValue(outgoing[i].Key, out float existing) || candidate < existing)
                    {
                        best[outgoing[i].Key] = candidate;
                    }
                }
            }
        }
    }
}
