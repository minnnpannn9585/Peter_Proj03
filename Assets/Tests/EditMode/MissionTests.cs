using NUnit.Framework;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Feature: stage1-hud-device-mission-system.
    /// Property 6: mission progress is monotonic - cleared never regresses, bestDelivered never shrinks.
    /// Property 7: the first-clear bonus is paid exactly once per mission per save.
    /// Property 14: Delivered == Correct + Wrong always, and a win implies every limit was respected.
    /// Property 15: the next level unlocks exactly when every mission in this level is cleared.
    /// </summary>
    [TestFixture]
    public class MissionTests
    {
        static MissionDef Def(
            int target, int maxWrong, int maxJams, float limit,
            int baseCoins = 30, int firstClear = 30, string id = "m_test")
        {
            return new MissionDef
            {
                id = id,
                displayName = id,
                objective = new MissionObjective
                {
                    targetDelivered = target,
                    maxWrong = maxWrong,
                    maxJams = maxJams,
                    timeLimitSeconds = limit
                },
                rewards = new MissionRewards { baseCoins = baseCoins, firstClearCoins = firstClear }
            };
        }

        static MissionRuntime Started(MissionDef def, int planned = 100)
        {
            var runtime = new MissionRuntime(def, planned);
            runtime.Begin();
            return runtime;
        }

        // ------------------------------------------------------------------ verdicts

        [Test]
        public void ReachingTheTargetWins()
        {
            MissionRuntime run = Started(Def(3, 2, -1, 100f));
            run.ReportCorrect();
            run.ReportCorrect();
            Assert.AreEqual(MissionState.Running, run.State);
            run.ReportCorrect();
            Assert.AreEqual(MissionState.Won, run.State);
            Assert.AreEqual(MissionFailReason.None, run.FailReason);
        }

        [Test]
        public void ExceedingMaxWrongLosesWithTooManyWrong()
        {
            MissionRuntime run = Started(Def(10, 2, -1, 100f));
            run.ReportWrong();
            run.ReportWrong();
            Assert.AreEqual(MissionState.Running, run.State, "wrong == maxWrong is still alive");
            run.ReportWrong();
            Assert.AreEqual(MissionState.Lost, run.State);
            Assert.AreEqual(MissionFailReason.TooManyWrong, run.FailReason);
        }

        [Test]
        public void ExceedingMaxJamsLosesWithTooManyJams()
        {
            MissionRuntime run = Started(Def(10, 5, 1, 100f));
            run.ReportJam();
            Assert.AreEqual(MissionState.Running, run.State);
            run.ReportJam();
            Assert.AreEqual(MissionState.Lost, run.State);
            Assert.AreEqual(MissionFailReason.TooManyJams, run.FailReason);
        }

        [Test]
        public void AJamCapOfMinusOneMeansJamsCannotLoseTheRound()
        {
            // M1 sets maxJams -1 on purpose: a beginner should not be punished by a mechanic
            // they have not been taught yet.
            MissionRuntime run = Started(Def(10, 5, -1, 100f));
            for (int i = 0; i < 100; i++)
            {
                run.ReportJam();
            }

            Assert.AreEqual(MissionState.Running, run.State);
            Assert.AreEqual(100, run.Jams, "jams are still counted, they just do not lose");
        }

        [Test]
        public void RunningOutOfTimeLosesWithTimeout()
        {
            MissionRuntime run = Started(Def(10, 5, -1, 1f));
            for (int i = 0; i < 120; i++)
            {
                run.Tick(1f / 60f);
            }

            Assert.AreEqual(MissionState.Lost, run.State);
            Assert.AreEqual(MissionFailReason.Timeout, run.FailReason);
            Assert.AreEqual(0f, run.Remaining);
        }

        [Test]
        public void LossesAreCheckedBeforeTheWin()
        {
            // A run that blows the error budget does not get to claim the win it reached on the
            // same parcel. This is what keeps "won implies within every limit" true.
            MissionRuntime run = Started(Def(1, 0, -1, 100f));
            run.ReportWrong();
            Assert.AreEqual(MissionState.Lost, run.State);

            run.ReportCorrect();
            Assert.AreEqual(MissionState.Lost, run.State, "a finished round ignores later reports");
        }

        [Test]
        public void ProgressCountersAreConsistent()
        {
            MissionRuntime run = Started(Def(50, 50, -1, 100f), 56);
            run.ReportCorrect();
            run.ReportCorrect();
            run.ReportWrong();

            Assert.AreEqual(3, run.Delivered);
            Assert.AreEqual(2, run.Correct);
            Assert.AreEqual(1, run.Wrong);
            Assert.AreEqual(56, run.Planned);
            Assert.AreEqual(53, run.LeftToDeliver);
        }

        // ------------------------------------------------------------------ settlement

        [Test]
        public void Property07_TheFirstClearBonusIsPaidExactlyOnce()
        {
            var record = new MissionRecord();
            int totalPaid = 0;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                MissionRuntime run = Started(Def(1, 5, -1, 100f, 30, 30));
                run.ReportCorrect();
                totalPaid += run.Resolve(record).coinsAwarded;
            }

            Assert.AreEqual(30 * 6 + 30, totalPaid,
                "six clears pay six base rewards plus exactly one first-clear bonus");
            Assert.IsTrue(record.FirstClearPaid);
            Assert.AreEqual(6, record.Attempts);
        }

        [Test]
        public void ResolveIsIdempotentForOneRound()
        {
            var record = new MissionRecord();
            MissionRuntime run = Started(Def(1, 5, -1, 100f, 30, 30));
            run.ReportCorrect();

            MissionOutcome first = run.Resolve(record);
            MissionOutcome second = run.Resolve(record);
            MissionOutcome third = run.Resolve(record);

            Assert.AreEqual(60, first.coinsAwarded, "base 30 plus first clear 30");
            Assert.IsTrue(first.firstClear);
            Assert.AreEqual(0, second.coinsAwarded + third.coinsAwarded,
                "repeat settlements of the same round pay nothing");
            Assert.IsTrue(second.won && third.won, "but they report the same verdict");
            Assert.AreEqual(1, record.Attempts, "and they do not inflate the attempt count");
        }

        [Test]
        public void ALossPaysNothingAndLeavesTheLatchesAlone()
        {
            var record = new MissionRecord();
            MissionRuntime run = Started(Def(10, 0, -1, 100f, 30, 30));
            run.ReportWrong();

            MissionOutcome outcome = run.Resolve(record);

            Assert.IsFalse(outcome.won);
            Assert.AreEqual(0, outcome.coinsAwarded);
            Assert.IsFalse(record.Cleared);
            Assert.IsFalse(record.FirstClearPaid);
            Assert.AreEqual(1, record.Attempts, "a loss is still an attempt");
        }

        [Test]
        public void AFailedRunAfterAClearDoesNotUndoTheClear()
        {
            var record = new MissionRecord();

            MissionRuntime win = Started(Def(1, 5, -1, 100f));
            win.ReportCorrect();
            win.Resolve(record);
            Assert.IsTrue(record.Cleared);

            MissionRuntime loss = Started(Def(1, 0, -1, 100f));
            loss.ReportWrong();
            loss.Resolve(record);

            Assert.IsTrue(record.Cleared, "cleared is a one-way latch");
        }

        // ------------------------------------------------------------------ Property 6

        [Test]
        public void Property06_MissionProgressIsMonotonic()
        {
            Property.ForAll("Property 6: progress is monotonic", (seed, trace) =>
            {
                var record = new MissionRecord();
                bool everCleared = false;
                int bestSeen = 0;

                int rounds = trace.Rng.Next(1, 25);
                for (int i = 0; i < rounds; i++)
                {
                    bool shouldWin = trace.Rng.Next(2) == 0;
                    int target = trace.Rng.Next(1, 10);
                    MissionRuntime run = Started(Def(target, 5, -1, 1000f));

                    int correct = shouldWin ? target : trace.Rng.Next(0, target);
                    for (int c = 0; c < correct; c++)
                    {
                        run.ReportCorrect();
                    }

                    MissionOutcome outcome = run.Resolve(record);
                    everCleared |= outcome.won;
                    trace.Log("round " + i + " won=" + outcome.won + " correct=" + correct +
                              " best=" + record.BestDelivered);

                    trace.Require(record.Cleared == everCleared || record.Cleared,
                        "cleared regressed");
                    trace.Require(record.BestDelivered >= bestSeen,
                        "bestDelivered fell from " + bestSeen + " to " + record.BestDelivered);
                    trace.Require(record.Attempts == i + 1, "attempts did not advance by one");

                    bestSeen = record.BestDelivered;
                }

                trace.Require(record.Cleared == everCleared,
                    "cleared should be true exactly when at least one round was won");
            });
        }

        // ------------------------------------------------------------------ Property 14

        [Test]
        public void Property14_SettlementIsConsistent()
        {
            Property.ForAll("Property 14: settlement consistency", (seed, trace) =>
            {
                MissionDef def = Def(
                    trace.Rng.Next(1, 20),
                    trace.Rng.Next(0, 8),
                    trace.Rng.Next(2) == 0 ? -1 : trace.Rng.Next(0, 8),
                    trace.Rng.Next(1, 60));

                MissionRuntime run = Started(def, trace.Rng.Next(0, 200));

                int steps = trace.Rng.Next(1, 300);
                for (int i = 0; i < steps; i++)
                {
                    switch (trace.Rng.Next(4))
                    {
                        case 0:
                            run.ReportCorrect();
                            trace.Log("correct");
                            break;
                        case 1:
                            run.ReportWrong();
                            trace.Log("wrong");
                            break;
                        case 2:
                            run.ReportJam();
                            trace.Log("jam");
                            break;
                        default:
                            run.Tick(1f / 60f);
                            break;
                    }

                    trace.Require(run.Delivered == run.Correct + run.Wrong,
                        "Delivered " + run.Delivered + " != Correct " + run.Correct +
                        " + Wrong " + run.Wrong);
                    trace.Require(run.LeftToDeliver >= 0, "LeftToDeliver went negative");
                }

                if (run.State == MissionState.Won)
                {
                    trace.Require(run.Correct >= def.objective.targetDelivered,
                        "won without reaching the target");
                    trace.Require(run.Wrong <= def.objective.maxWrong,
                        "won with too many misroutes");
                    trace.Require(def.objective.maxJams < 0 || run.Jams <= def.objective.maxJams,
                        "won with too many jams");
                    trace.Require(run.Elapsed <= def.objective.timeLimitSeconds,
                        "won after the clock expired");
                }
            });
        }

        // ------------------------------------------------------------------ Property 15

        [Test]
        public void Property15_NextLevelUnlocksExactlyWhenEveryMissionIsCleared()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            Assert.AreEqual(3, config.missions.Count);
            Assert.AreEqual("level_02", config.nextLevel);

            Property.ForAll("Property 15: unlock iff all cleared", (seed, trace) =>
            {
                PlayerProfile profile = ProfileStore.CreateDefault();

                bool all = true;
                for (int i = 0; i < config.missions.Count; i++)
                {
                    bool clear = trace.Rng.Next(2) == 0;
                    all &= clear;
                    if (clear)
                    {
                        profile.RecordFor(config.missions[i].id).MarkCleared();
                    }

                    trace.Log(config.missions[i].id + " cleared=" + clear);
                }

                // Mirrors YardDirector.UpdateUnlocks.
                bool everyOne = true;
                for (int i = 0; i < config.missions.Count; i++)
                {
                    everyOne &= profile.IsCleared(config.missions[i].id);
                }

                if (everyOne)
                {
                    profile.Unlock(config.nextLevel);
                }

                trace.Require(profile.IsUnlocked(config.nextLevel) == all,
                    "unlocked=" + profile.IsUnlocked(config.nextLevel) + " but allCleared=" + all);
            });
        }

        [Test]
        public void APartlyClearedLevelDoesNotUnlockTheNextOne()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            PlayerProfile profile = ProfileStore.CreateDefault();

            profile.RecordFor("m1_first_shift").MarkCleared();
            profile.RecordFor("m2_dual_flow").MarkCleared();

            bool all = true;
            for (int i = 0; i < config.missions.Count; i++)
            {
                all &= profile.IsCleared(config.missions[i].id);
            }

            Assert.IsFalse(all, "m3 is still outstanding");
            Assert.IsFalse(profile.IsUnlocked("level_02"));
        }
    }
}
