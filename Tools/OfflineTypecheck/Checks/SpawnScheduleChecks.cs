using System;
using System.Collections.Generic;

namespace ParcelSort.Offline
{
    /// <summary>Properties 9 and 10: schedule determinism and the blind ratio's binomial bound.</summary>
    public static class SpawnScheduleChecks
    {
        static readonly List<string> TwoInlets = new List<string> { "in_a", "in_b" };

        static readonly List<DestinationColor> ThreeColors = new List<DestinationColor>
        {
            DestinationColor.Red, DestinationColor.Green, DestinationColor.Blue
        };

        public static void Run(CheckRunner r)
        {
            r.Section("SpawnSchedule (Properties 9, 10)");

            BasicShape(r);
            Determinism(r);
            BlindRatio(r);
            MissionPlanned(r);
            BackPressure(r);
        }

        static SpawnPlan Plan(int repeat, params SpawnWaveDef[] waves)
        {
            var plan = new SpawnPlan { repeat = repeat, startDelay = RandomRange.Fixed(0f) };
            plan.waves.AddRange(waves);
            return plan;
        }

        static SpawnWaveDef Wave(string inlet, int count, float spacing, bool parallel = false,
            params DestinationColor[] colors)
        {
            var wave = new SpawnWaveDef
            {
                count = RandomRange.Fixed(count),
                spacing = RandomRange.Fixed(spacing),
                delayAfter = RandomRange.Fixed(0f),
                parallel = parallel
            };

            if (!string.IsNullOrEmpty(inlet))
            {
                wave.inlets.Add(inlet);
            }

            wave.colors.AddRange(colors);
            return wave;
        }

        static void BasicShape(CheckRunner r)
        {
            var schedule = new SpawnSchedule();
            schedule.Build(TwoInlets, ThreeColors,
                Plan(3, Wave("in_a", 4, 0f, false, DestinationColor.Red)),
                new MissionModifiers(), 1234);

            r.AreEqual(12, schedule.Planned, "repeat 3 of a 4-count wave plans 12 parcels");

            List<SpawnRequest> emitted = LevelFixture.DrainAll(schedule, 1f / 60f, 100000);
            r.AreEqual(12, emitted.Count, "all 12 are released");
            r.AreEqual(12, schedule.Emitted, "Emitted matches what came out");
            r.Check(schedule.Finished, "the schedule reports finished");
            r.AreEqual(0, schedule.Queued, "nothing is left queued");

            bool allRed = true;
            bool allInletA = true;
            for (int i = 0; i < emitted.Count; i++)
            {
                if (emitted[i].color != DestinationColor.Red)
                {
                    allRed = false;
                }

                if (emitted[i].inletId != "in_a")
                {
                    allInletA = false;
                }
            }

            r.Check(allRed, "a single-colour wave stays one colour");
            r.Check(allInletA, "a named inlet is respected");

            // Unknown inlet: warn, skip, and keep Planned honest.
            UnityEngine.Debug.Clear();
            var skipped = new SpawnSchedule();
            skipped.Build(TwoInlets, ThreeColors,
                Plan(1, Wave("in_a", 5, 0f), Wave("in_ghost", 7, 0f)),
                new MissionModifiers(), 99);
            r.AreEqual(5, skipped.Planned, "an unknown inlet's wave is dropped from Planned");
            r.Check(UnityEngine.Debug.Warnings.Count > 0, "an unknown inlet logs a warning");

            // Parallel bursts pick different inlets.
            var parallel = new SpawnSchedule();
            parallel.Build(TwoInlets, ThreeColors,
                Plan(1, Wave(null, 3, 0f), Wave(null, 3, 0f, true)),
                new MissionModifiers(), 7);
            List<SpawnRequest> both = LevelFixture.DrainAll(parallel, 1f / 60f, 100000);
            var inletsSeen = new HashSet<string>();
            for (int i = 0; i < both.Count; i++)
            {
                inletsSeen.Add(both[i].inletId);
            }

            r.AreEqual(6, both.Count, "a parallel pair still releases every parcel");
            r.AreEqual(2, inletsSeen.Count, "a parallel burst avoids its group mate's inlet");

            // mixColors varies colour within one burst; without it the burst is uniform.
            var mixed = new SpawnSchedule();
            mixed.Build(TwoInlets, ThreeColors,
                Plan(1, Wave("in_a", 40, 0f, false,
                    DestinationColor.Red, DestinationColor.Green, DestinationColor.Blue)),
                new MissionModifiers { mixColors = true }, 555);
            var mixedColors = new HashSet<DestinationColor>();
            foreach (SpawnRequest request in LevelFixture.DrainAll(mixed, 1f / 60f, 100000))
            {
                mixedColors.Add(request.color);
            }

            r.Check(mixedColors.Count > 1, "mixColors produces more than one colour in a burst");

            var uniform = new SpawnSchedule();
            uniform.Build(TwoInlets, ThreeColors,
                Plan(1, Wave("in_a", 40, 0f, false,
                    DestinationColor.Red, DestinationColor.Green, DestinationColor.Blue)),
                new MissionModifiers { mixColors = false }, 555);
            var uniformColors = new HashSet<DestinationColor>();
            foreach (SpawnRequest request in LevelFixture.DrainAll(uniform, 1f / 60f, 100000))
            {
                uniformColors.Add(request.color);
            }

            r.AreEqual(1, uniformColors.Count, "without mixColors a burst is one colour");
        }

        static void Determinism(CheckRunner r)
        {
            const int iterations = 120;
            bool identical = true;
            bool monotonic = true;
            bool bounded = true;
            int badSeed = -1;

            for (int seed = 0; seed < iterations; seed++)
            {
                var rng = new Random(seed + 8080);
                SpawnPlan plan = RandomPlan(rng);
                var mods = new MissionModifiers
                {
                    mixColors = rng.Next(2) == 0,
                    blindRatio = (float)rng.NextDouble()
                };

                int planSeed = rng.Next();

                List<SpawnRequest> a = Replay(plan, mods, planSeed, out int plannedA,
                    out bool monoA, out bool boundA);
                List<SpawnRequest> b = Replay(plan, mods, planSeed, out int plannedB,
                    out bool monoB, out bool boundB);

                if (!monoA || !monoB)
                {
                    monotonic = false;
                    badSeed = seed;
                }

                if (!boundA || !boundB)
                {
                    bounded = false;
                    badSeed = seed;
                }

                if (plannedA != plannedB || a.Count != b.Count)
                {
                    identical = false;
                    badSeed = seed;
                    continue;
                }

                for (int i = 0; i < a.Count; i++)
                {
                    if (a[i].inletId != b[i].inletId || a[i].color != b[i].color ||
                        a[i].blind != b[i].blind)
                    {
                        identical = false;
                        badSeed = seed;
                        break;
                    }
                }
            }

            r.Check(identical, "same seed + plan + modifiers replays an identical request sequence " +
                              "over " + iterations + " cases (seed " + badSeed + ")");
            r.Check(monotonic, "Emitted only ever increases (seed " + badSeed + ")");
            r.Check(bounded, "Emitted never exceeds Planned (seed " + badSeed + ")");

            // A different seed should generally give a different sequence.
            var one = new SpawnSchedule();
            var two = new SpawnSchedule();
            SpawnPlan shared = Plan(2, Wave(null, 12, 0f, false,
                DestinationColor.Red, DestinationColor.Green, DestinationColor.Blue));
            var mixMods = new MissionModifiers { mixColors = true, blindRatio = 0.5f };
            one.Build(TwoInlets, ThreeColors, shared, mixMods, 1);
            two.Build(TwoInlets, ThreeColors, shared, mixMods, 2);
            List<SpawnRequest> seqOne = LevelFixture.DrainAll(one, 1f / 60f, 100000);
            List<SpawnRequest> seqTwo = LevelFixture.DrainAll(two, 1f / 60f, 100000);
            bool differs = false;
            for (int i = 0; i < seqOne.Count && i < seqTwo.Count; i++)
            {
                if (seqOne[i].color != seqTwo[i].color || seqOne[i].blind != seqTwo[i].blind)
                {
                    differs = true;
                    break;
                }
            }

            r.Check(differs, "a different seed gives a different sequence");
        }

        static List<SpawnRequest> Replay(
            SpawnPlan plan, MissionModifiers mods, int seed,
            out int planned, out bool monotonic, out bool bounded)
        {
            var schedule = new SpawnSchedule();
            schedule.Build(TwoInlets, ThreeColors, plan, mods, seed);
            planned = schedule.Planned;
            monotonic = true;
            bounded = true;

            var emitted = new List<SpawnRequest>();
            int lastEmitted = 0;
            for (int tick = 0; tick < 200000 && !schedule.Finished; tick++)
            {
                float step = 1f / 60f;
                while (schedule.TryDequeue(step, out SpawnRequest request))
                {
                    step = 0f;
                    emitted.Add(request);
                    schedule.ConfirmEmit();
                }

                if (schedule.Emitted < lastEmitted)
                {
                    monotonic = false;
                }

                if (schedule.Emitted > schedule.Planned)
                {
                    bounded = false;
                }

                lastEmitted = schedule.Emitted;
            }

            return emitted;
        }

        static SpawnPlan RandomPlan(Random rng)
        {
            var plan = new SpawnPlan
            {
                repeat = rng.Next(1, 4),
                startDelay = RandomRange.Between(0f, (float)rng.NextDouble())
            };

            int waves = rng.Next(1, 4);
            for (int i = 0; i < waves; i++)
            {
                var wave = new SpawnWaveDef
                {
                    count = RandomRange.Fixed(rng.Next(1, 12)),
                    spacing = RandomRange.Between(0f, 1.5f),
                    delayAfter = RandomRange.Between(0f, 3f),
                    parallel = i > 0 && rng.Next(2) == 0
                };

                if (rng.Next(2) == 0)
                {
                    wave.inlets.Add(TwoInlets[rng.Next(TwoInlets.Count)]);
                }

                int colors = rng.Next(0, 4);
                for (int c = 0; c < colors; c++)
                {
                    wave.colors.Add(ThreeColors[rng.Next(ThreeColors.Count)]);
                }

                plan.waves.Add(wave);
            }

            return plan;
        }

        static void BlindRatio(CheckRunner r)
        {
            var zero = new SpawnSchedule();
            zero.Build(TwoInlets, ThreeColors, Plan(4, Wave("in_a", 25, 0f)),
                new MissionModifiers { blindRatio = 0f }, 11);
            r.AreEqual(0, zero.PlannedBlind, "blindRatio 0 produces no blind parcels");

            var all = new SpawnSchedule();
            all.Build(TwoInlets, ThreeColors, Plan(4, Wave("in_a", 25, 0f)),
                new MissionModifiers { blindRatio = 1f }, 11);
            r.AreEqual(all.Planned, all.PlannedBlind, "blindRatio 1 makes every parcel blind");

            const int iterations = 200;
            bool withinBound = true;
            int badSeed = -1;
            for (int seed = 0; seed < iterations; seed++)
            {
                var rng = new Random(seed + 6060);
                float p = (float)rng.NextDouble();
                int count = rng.Next(5, 40);
                int repeat = rng.Next(1, 5);

                var schedule = new SpawnSchedule();
                schedule.Build(TwoInlets, ThreeColors, Plan(repeat, Wave("in_a", count, 0f)),
                    new MissionModifiers { blindRatio = p }, rng.Next());

                int n = schedule.Planned;
                int k = schedule.PlannedBlind;
                double mean = n * p;
                double bound = 4.0 * Math.Sqrt(n * p * (1.0 - p)) + 1.0;
                if (Math.Abs(k - mean) > bound)
                {
                    withinBound = false;
                    badSeed = seed;
                    r.Info("seed " + seed + ": n=" + n + " p=" + p.ToString("F3") +
                           " k=" + k + " bound=" + bound.ToString("F2"));
                }
            }

            r.Check(withinBound, "|k - n*p| <= 4*sqrt(n*p*(1-p)) + 1 over " + iterations +
                                 " random ratios (seed " + badSeed + ")");
        }

        static void MissionPlanned(CheckRunner r)
        {
            LevelConfig config = LevelFixture.Level01();
            int[] expected = { 18, 56, 108 };
            string[] ids = { "m1_first_shift", "m2_dual_flow", "m3_night_blind" };

            for (int i = 0; i < ids.Length; i++)
            {
                if (!config.TryGetMission(ids[i], out MissionDef mission))
                {
                    r.Check(false, "level_01 defines " + ids[i]);
                    continue;
                }

                SpawnSchedule schedule = LevelFixture.BuildSchedule(config, mission);
                r.AreEqual(expected[i], schedule.Planned,
                    ids[i] + " plans exactly " + expected[i] + " parcels");
                r.AreEqual(expected[i], mission.PlannedFromPlan(),
                    ids[i] + " PlannedFromPlan agrees with the built schedule");
                r.Check(schedule.Planned > mission.objective.targetDelivered,
                    ids[i] + " plans more parcels than the objective demands (" +
                    schedule.Planned + " > " + mission.objective.targetDelivered + ")");

                List<SpawnRequest> emitted = LevelFixture.DrainAll(schedule, 1f / 60f, 2000000);
                r.AreEqual(expected[i], emitted.Count, ids[i] + " releases every planned parcel");
            }

            // M3's blind count is what the Scanner requirement is argued from.
            if (config.TryGetMission("m3_night_blind", out MissionDef m3))
            {
                SpawnSchedule schedule = LevelFixture.BuildSchedule(config, m3);
                r.Info("m3 blind parcels: " + schedule.PlannedBlind + " of " + schedule.Planned +
                       " (expected around " + (0.35 * 108).ToString("F1") + ")");
                double bound = 4.0 * Math.Sqrt(108 * 0.35 * 0.65) + 1.0;
                r.Check(Math.Abs(schedule.PlannedBlind - 37.8) <= bound,
                    "m3 blind count sits inside the binomial bound around 37.8");
                r.Check(schedule.PlannedBlind * 2.0 / 3.0 > m3.objective.maxWrong,
                    "m3's expected misroutes with no Scanner (" +
                    (schedule.PlannedBlind * 2.0 / 3.0).ToString("F1") +
                    ") exceed maxWrong " + m3.objective.maxWrong);
            }
        }

        static void BackPressure(CheckRunner r)
        {
            // A host that refuses every other parcel must still get all of them eventually,
            // in the same order, because rejection defers rather than drops.
            LevelConfig config = LevelFixture.Level01();
            config.TryGetMission("m2_dual_flow", out MissionDef mission);

            SpawnSchedule smooth = LevelFixture.BuildSchedule(config, mission);
            List<SpawnRequest> reference = LevelFixture.DrainAll(smooth, 1f / 60f, 2000000);

            SpawnSchedule choppy = LevelFixture.BuildSchedule(config, mission);
            var got = new List<SpawnRequest>();
            bool refuseNext = true;
            for (int tick = 0; tick < 2000000 && !choppy.Finished; tick++)
            {
                float step = 1f / 60f;
                while (choppy.TryDequeue(step, out SpawnRequest request))
                {
                    step = 0f;
                    if (refuseNext)
                    {
                        refuseNext = false;
                        choppy.RejectEmit();
                        break;
                    }

                    refuseNext = true;
                    got.Add(request);
                    choppy.ConfirmEmit();
                }
            }

            r.AreEqual(reference.Count, got.Count,
                "back pressure delays parcels but never loses them");

            // Global interleaving legitimately shifts when emission is delayed, but each inlet's
            // own stream must stay in order: a deferred parcel is retried, never reordered past
            // the next parcel from the same inlet.
            r.Check(SamePerInlet(reference, got, out string mismatch),
                "back pressure preserves each inlet's own order: " + mismatch);

            // Rewind replays byte identically.
            SpawnSchedule rewound = LevelFixture.BuildSchedule(config, mission);
            LevelFixture.DrainAll(rewound, 1f / 60f, 2000000);
            rewound.Rewind();
            List<SpawnRequest> second = LevelFixture.DrainAll(rewound, 1f / 60f, 2000000);
            bool rewindMatches = second.Count == reference.Count;
            for (int i = 0; i < second.Count && rewindMatches; i++)
            {
                if (second[i].inletId != reference[i].inletId ||
                    second[i].color != reference[i].color ||
                    second[i].blind != reference[i].blind)
                {
                    rewindMatches = false;
                }
            }

            r.Check(rewindMatches, "Rewind replays the identical sequence for a retry");
        }

        static bool SamePerInlet(
            List<SpawnRequest> expected, List<SpawnRequest> actual, out string mismatch)
        {
            mismatch = "ok";
            Dictionary<string, List<SpawnRequest>> left = GroupByInlet(expected);
            Dictionary<string, List<SpawnRequest>> right = GroupByInlet(actual);

            if (left.Count != right.Count)
            {
                mismatch = "inlet count " + left.Count + " vs " + right.Count;
                return false;
            }

            foreach (KeyValuePair<string, List<SpawnRequest>> pair in left)
            {
                if (!right.TryGetValue(pair.Key, out List<SpawnRequest> other))
                {
                    mismatch = "missing inlet " + pair.Key;
                    return false;
                }

                if (pair.Value.Count != other.Count)
                {
                    mismatch = pair.Key + " count " + pair.Value.Count + " vs " + other.Count;
                    return false;
                }

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    if (pair.Value[i].color != other[i].color ||
                        pair.Value[i].blind != other[i].blind)
                    {
                        mismatch = pair.Key + " diverges at index " + i;
                        return false;
                    }
                }
            }

            return true;
        }

        static Dictionary<string, List<SpawnRequest>> GroupByInlet(List<SpawnRequest> requests)
        {
            var byInlet = new Dictionary<string, List<SpawnRequest>>();
            for (int i = 0; i < requests.Count; i++)
            {
                if (!byInlet.TryGetValue(requests[i].inletId, out List<SpawnRequest> list))
                {
                    list = new List<SpawnRequest>();
                    byInlet[requests[i].inletId] = list;
                }

                list.Add(requests[i]);
            }

            return byInlet;
        }
    }
}
