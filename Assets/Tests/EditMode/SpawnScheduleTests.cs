using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Feature: stage1-hud-device-mission-system.
    /// Property 9: schedule determinism - the same seed, plan and modifiers replay an identical
    /// request sequence, Emitted only rises, and Emitted never exceeds Planned.
    /// Property 10: the blind parcel count sits inside a binomial bound around n*p.
    /// </summary>
    [TestFixture]
    public class SpawnScheduleTests
    {
        static readonly List<string> TwoInlets = new List<string> { "in_a", "in_b" };

        static readonly List<DestinationColor> ThreeColors = new List<DestinationColor>
        {
            DestinationColor.Red, DestinationColor.Green, DestinationColor.Blue
        };

        static SpawnWaveDef Wave(
            string inlet, int count, float spacing, bool parallel = false,
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

        static SpawnPlan Plan(int repeat, params SpawnWaveDef[] waves)
        {
            var plan = new SpawnPlan { repeat = repeat, startDelay = RandomRange.Fixed(0f) };
            plan.waves.AddRange(waves);
            return plan;
        }

        static SpawnSchedule Build(SpawnPlan plan, MissionModifiers mods, int seed)
        {
            var schedule = new SpawnSchedule();
            schedule.Build(TwoInlets, ThreeColors, plan, mods ?? new MissionModifiers(), seed);
            return schedule;
        }

        // ------------------------------------------------------------------ shape

        [Test]
        public void RepeatMultipliesTheWaveList()
        {
            SpawnSchedule schedule = Build(
                Plan(3, Wave("in_a", 4, 0f, false, DestinationColor.Red)), null, 1234);

            Assert.AreEqual(12, schedule.Planned);

            List<SpawnRequest> emitted = LevelFixtures.DrainAll(schedule);
            Assert.AreEqual(12, emitted.Count);
            Assert.AreEqual(12, schedule.Emitted);
            Assert.AreEqual(0, schedule.Queued);
            Assert.IsTrue(schedule.Finished);
        }

        [Test]
        public void ANamedInletAndASingleColourAreRespected()
        {
            SpawnSchedule schedule = Build(
                Plan(2, Wave("in_a", 5, 0f, false, DestinationColor.Green)), null, 77);

            foreach (SpawnRequest request in LevelFixtures.DrainAll(schedule))
            {
                Assert.AreEqual("in_a", request.inletId);
                Assert.AreEqual(DestinationColor.Green, request.color);
                Assert.IsFalse(request.blind, "no blind ratio means no blind parcels");
            }
        }

        [Test]
        public void AnUnknownInletIsSkippedAndPlannedStaysHonest()
        {
            // Planned must reflect what can actually be emitted, or the progress bar would have a
            // denominator it can never reach.
            SpawnSchedule schedule = Build(
                Plan(1, Wave("in_a", 5, 0f), Wave("in_ghost", 7, 0f)), null, 99);

            Assert.AreEqual(5, schedule.Planned);
            Assert.AreEqual(5, LevelFixtures.DrainAll(schedule).Count);
        }

        [Test]
        public void ParallelBurstsUseDifferentInlets()
        {
            SpawnSchedule schedule = Build(
                Plan(1, Wave(null, 3, 0f), Wave(null, 3, 0f, true)), null, 7);

            var inlets = new HashSet<string>();
            List<SpawnRequest> emitted = LevelFixtures.DrainAll(schedule);
            for (int i = 0; i < emitted.Count; i++)
            {
                inlets.Add(emitted[i].inletId);
            }

            Assert.AreEqual(6, emitted.Count);
            Assert.AreEqual(2, inlets.Count,
                "a parallel burst avoids the inlet its group mate already occupies");
        }

        [Test]
        public void MixColorsVariesColourWithinABurst()
        {
            SpawnWaveDef wave = Wave("in_a", 40, 0f, false,
                DestinationColor.Red, DestinationColor.Green, DestinationColor.Blue);

            SpawnSchedule mixed = Build(Plan(1, wave), new MissionModifiers { mixColors = true }, 555);
            var mixedColors = new HashSet<DestinationColor>();
            foreach (SpawnRequest request in LevelFixtures.DrainAll(mixed))
            {
                mixedColors.Add(request.color);
            }

            Assert.Greater(mixedColors.Count, 1, "mixColors should mix within one burst");

            SpawnSchedule uniform = Build(Plan(1, wave), new MissionModifiers { mixColors = false }, 555);
            var uniformColors = new HashSet<DestinationColor>();
            foreach (SpawnRequest request in LevelFixtures.DrainAll(uniform))
            {
                uniformColors.Add(request.color);
            }

            Assert.AreEqual(1, uniformColors.Count, "without mixColors a burst is one colour");
        }

        [Test]
        public void SpacingAndDelayAfterAcceptRangeSyntaxWithoutChangingTheYield()
        {
            // Ranges are allowed on rhythm, never on count. That is what keeps Planned fixed
            // while every run still feels different.
            var wave = new SpawnWaveDef
            {
                count = RandomRange.Fixed(6),
                spacing = RandomRange.Between(0.5f, 1.5f),
                delayAfter = RandomRange.Between(2f, 4f)
            };
            wave.inlets.Add("in_a");

            SpawnPlan plan = Plan(3, wave);

            for (int seed = 0; seed < 20; seed++)
            {
                SpawnSchedule schedule = Build(plan, null, seed);
                Assert.AreEqual(18, schedule.Planned,
                    "Planned must be seed independent when counts are constants");
            }
        }

        [Test]
        public void RangeSyntaxIsParsedForSpacingAndDelayAfter()
        {
            RandomRange range = SpawnPlanParser.ReadRange(
                MiniJson.Parse("{\"v\": \"random 2~3\"}")["v"], RandomRange.Fixed(0f));
            Assert.AreEqual(2f, range.min, 1e-4f);
            Assert.AreEqual(3f, range.max, 1e-4f);

            RandomRange terse = SpawnPlanParser.ReadRange(
                MiniJson.Parse("{\"v\": \"3~4\"}")["v"], RandomRange.Fixed(0f));
            Assert.AreEqual(3f, terse.min, 1e-4f);
            Assert.AreEqual(4f, terse.max, 1e-4f);
        }

        // ------------------------------------------------------------------ Property 9

        [Test]
        public void Property09_ScheduleIsDeterministic()
        {
            Property.ForAll("Property 9: schedule determinism", (seed, trace) =>
            {
                SpawnPlan plan = RandomPlan(trace);
                var mods = new MissionModifiers
                {
                    mixColors = trace.Rng.Next(2) == 0,
                    blindRatio = (float)trace.Rng.NextDouble()
                };

                int planSeed = trace.Rng.Next();
                trace.Log("seed " + planSeed + " mixColors=" + mods.mixColors +
                          " blindRatio=" + mods.blindRatio.ToString("F3"));

                List<SpawnRequest> first = Replay(plan, mods, planSeed, trace, out int plannedA);
                List<SpawnRequest> second = Replay(plan, mods, planSeed, trace, out int plannedB);

                trace.Require(plannedA == plannedB,
                    "Planned differed between replays: " + plannedA + " vs " + plannedB);
                trace.Require(first.Count == second.Count,
                    "emitted count differed: " + first.Count + " vs " + second.Count);

                for (int i = 0; i < first.Count; i++)
                {
                    trace.Require(
                        first[i].inletId == second[i].inletId &&
                        first[i].color == second[i].color &&
                        first[i].blind == second[i].blind,
                        "request " + i + " differed: " + first[i] + " vs " + second[i]);
                }
            });
        }

        static List<SpawnRequest> Replay(
            SpawnPlan plan, MissionModifiers mods, int seed, Property.Trace trace, out int planned)
        {
            SpawnSchedule schedule = Build(plan, mods, seed);
            planned = schedule.Planned;

            var emitted = new List<SpawnRequest>();
            int last = 0;
            for (int tick = 0; tick < 200000 && !schedule.Finished; tick++)
            {
                float step = 1f / 60f;
                while (schedule.TryDequeue(step, out SpawnRequest request))
                {
                    step = 0f;
                    emitted.Add(request);
                    schedule.ConfirmEmit();
                }

                trace.Require(schedule.Emitted >= last, "Emitted went backwards");
                trace.Require(schedule.Emitted <= schedule.Planned, "Emitted exceeded Planned");
                last = schedule.Emitted;
            }

            return emitted;
        }

        static SpawnPlan RandomPlan(Property.Trace trace)
        {
            var plan = new SpawnPlan
            {
                repeat = trace.Rng.Next(1, 4),
                startDelay = RandomRange.Between(0f, (float)trace.Rng.NextDouble())
            };

            int waves = trace.Rng.Next(1, 4);
            for (int i = 0; i < waves; i++)
            {
                var wave = new SpawnWaveDef
                {
                    count = RandomRange.Fixed(trace.Rng.Next(1, 12)),
                    spacing = RandomRange.Between(0f, 1.5f),
                    delayAfter = RandomRange.Between(0f, 3f),
                    parallel = i > 0 && trace.Rng.Next(2) == 0
                };

                if (trace.Rng.Next(2) == 0)
                {
                    wave.inlets.Add(TwoInlets[trace.Rng.Next(TwoInlets.Count)]);
                }

                int colors = trace.Rng.Next(0, 4);
                for (int c = 0; c < colors; c++)
                {
                    wave.colors.Add(ThreeColors[trace.Rng.Next(ThreeColors.Count)]);
                }

                plan.waves.Add(wave);
            }

            return plan;
        }

        [Test]
        public void BackPressureDefersParcelsInsteadOfLosingThem()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            MissionDef mission = LevelFixtures.Mission("m2_dual_flow");

            List<SpawnRequest> reference =
                LevelFixtures.DrainAll(LevelFixtures.BuildSchedule(config, mission));

            SpawnSchedule choppy = LevelFixtures.BuildSchedule(config, mission);
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

            Assert.AreEqual(reference.Count, got.Count,
                "a host that refuses half the time still receives every parcel");
        }

        [Test]
        public void RewindReplaysTheIdenticalSequenceForARetry()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            MissionDef mission = LevelFixtures.Mission("m1_first_shift");

            SpawnSchedule schedule = LevelFixtures.BuildSchedule(config, mission);
            List<SpawnRequest> first = LevelFixtures.DrainAll(schedule);

            schedule.Rewind();
            Assert.AreEqual(0, schedule.Emitted, "Rewind resets the emitted count");

            List<SpawnRequest> second = LevelFixtures.DrainAll(schedule);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].ToString(), second[i].ToString(),
                    "request " + i + " changed after a rewind");
            }
        }

        // ------------------------------------------------------------------ Property 10

        [Test]
        public void ABlindRatioOfZeroOrOneIsExact()
        {
            SpawnSchedule none = Build(Plan(4, Wave("in_a", 25, 0f)),
                new MissionModifiers { blindRatio = 0f }, 11);
            Assert.AreEqual(0, none.PlannedBlind);

            SpawnSchedule all = Build(Plan(4, Wave("in_a", 25, 0f)),
                new MissionModifiers { blindRatio = 1f }, 11);
            Assert.AreEqual(all.Planned, all.PlannedBlind);
        }

        [Test]
        public void Property10_BlindCountSitsInsideTheBinomialBound()
        {
            Property.ForAll("Property 10: blind ratio within binomial bound", (seed, trace) =>
            {
                float p = (float)trace.Rng.NextDouble();
                int count = trace.Rng.Next(5, 40);
                int repeat = trace.Rng.Next(1, 5);

                SpawnSchedule schedule = Build(Plan(repeat, Wave("in_a", count, 0f)),
                    new MissionModifiers { blindRatio = p }, trace.Rng.Next());

                int n = schedule.Planned;
                int k = schedule.PlannedBlind;
                double mean = n * p;
                double bound = 4.0 * Math.Sqrt(n * p * (1.0 - p)) + 1.0;

                trace.Log("n=" + n + " p=" + p.ToString("F3") + " k=" + k +
                          " bound=" + bound.ToString("F2"));
                trace.Require(Math.Abs(k - mean) <= bound,
                    "|k - n*p| = " + Math.Abs(k - mean).ToString("F2") + " exceeded " +
                    bound.ToString("F2"));

                // The emitted stream must agree with the planned figure.
                int emittedBlind = 0;
                foreach (SpawnRequest request in LevelFixtures.DrainAll(schedule))
                {
                    if (request.blind)
                    {
                        emittedBlind++;
                    }
                }

                trace.Require(emittedBlind == k,
                    "emitted blind " + emittedBlind + " != planned blind " + k);
            });
        }

        // ------------------------------------------------------------------ shipped missions

        [Test]
        public void ShippedMissionsPlanExactlyTheDocumentedParcelCounts()
        {
            // These three numbers underpin every other balance claim, including the argument that
            // M3 needs two Scanners, so they are asserted directly against the shipped JSON.
            AssertPlanned("m1_first_shift", 18, 15);
            AssertPlanned("m2_dual_flow", 56, 46);
            AssertPlanned("m3_night_blind", 108, 84);
        }

        static void AssertPlanned(string missionId, int expectedPlanned, int expectedTarget)
        {
            LevelConfig config = LevelFixtures.Level01Config();
            MissionDef mission = LevelFixtures.Mission(missionId);

            Assert.AreEqual(expectedTarget, mission.objective.targetDelivered,
                missionId + " targetDelivered");

            SpawnSchedule schedule = LevelFixtures.BuildSchedule(config, mission);
            Assert.AreEqual(expectedPlanned, schedule.Planned, missionId + " Planned");
            Assert.AreEqual(expectedPlanned, mission.PlannedFromPlan(),
                missionId + " PlannedFromPlan should agree with the built schedule");
            Assert.Greater(schedule.Planned, mission.objective.targetDelivered,
                missionId + " must plan more parcels than it demands");
            Assert.AreEqual(expectedPlanned, LevelFixtures.DrainAll(schedule).Count,
                missionId + " should release every planned parcel");
        }

        [Test]
        public void EveryShippedWaveCountIsAConstant()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            for (int m = 0; m < config.missions.Count; m++)
            {
                MissionDef mission = config.missions[m];
                for (int w = 0; w < mission.spawn.waves.Count; w++)
                {
                    RandomRange count = mission.spawn.waves[w].count;
                    Assert.IsTrue(Mathf.Approximately(count.min, count.max),
                        mission.id + " wave " + w + " uses a random count (" + count.min + "~" +
                        count.max + "); Planned must be deterministic");
                }

                Assert.IsFalse(mission.ConfigError,
                    mission.id + " reported a config error: " + mission.ConfigErrorText);
            }
        }

        [Test]
        public void MissionThreeBlindLoadFarExceedsItsErrorBudgetWithoutScanners()
        {
            // With three bays, an unrevealed parcel is a 1-in-3 guess, so the expected misroutes
            // are 2/3 of the blind count. Against maxWrong 9 that is not a close call.
            LevelConfig config = LevelFixtures.Level01Config();
            MissionDef m3 = LevelFixtures.Mission("m3_night_blind");
            SpawnSchedule schedule = LevelFixtures.BuildSchedule(config, m3);

            Assert.AreEqual(0.35f, m3.modifiers.blindRatio, 1e-4f);

            double expectedMisroutes = schedule.PlannedBlind * 2.0 / 3.0;
            Assert.Greater(expectedMisroutes, m3.objective.maxWrong * 2,
                "expected misroutes (" + expectedMisroutes.ToString("F1") +
                ") should dwarf maxWrong " + m3.objective.maxWrong + ", making Scanners mandatory");

            double bound = 4.0 * Math.Sqrt(108 * 0.35 * 0.65) + 1.0;
            Assert.LessOrEqual(Math.Abs(schedule.PlannedBlind - 37.8), bound,
                "blind count should sit near the documented 37.8");
        }
    }
}
