using System;

namespace ParcelSort.Offline
{
    /// <summary>Properties 6, 7 and 14: settle consistency, monotonic progress, one-off bonus.</summary>
    public static class MissionRuntimeChecks
    {
        static MissionDef Def(int target, int maxWrong, float limit,
            int baseCoins = 30, int firstClear = 30)
        {
            return new MissionDef
            {
                id = "m_test",
                displayName = "test",
                objective = new MissionObjective
                {
                    targetDelivered = target,
                    maxWrong = maxWrong,
                    timeLimitSeconds = limit
                },
                rewards = new MissionRewards { baseCoins = baseCoins, firstClearCoins = firstClear }
            };
        }

        public static void Run(CheckRunner r)
        {
            r.Section("MissionRuntime (Properties 6, 7, 14)");

            // Win
            var win = new MissionRuntime(Def(3, 2, 100f), 10);
            win.Begin();
            win.ReportCorrect();
            win.ReportCorrect();
            win.ReportCorrect();
            r.Check(win.State == MissionState.Won, "hitting the target wins");

            // Too many wrong
            var wrong = new MissionRuntime(Def(10, 2, 100f), 20);
            wrong.Begin();
            wrong.ReportWrong();
            wrong.ReportWrong();
            r.Check(wrong.State == MissionState.Running, "wrong at the cap is still alive");
            wrong.ReportWrong();
            r.Check(wrong.State == MissionState.Lost && wrong.FailReason == MissionFailReason.TooManyWrong,
                "exceeding maxWrong loses with TooManyWrong");

            // Jams: counted, never fatal. There is no jam budget any more.
            var jam = new MissionRuntime(Def(10, 5, 100f), 20);
            jam.Begin();
            for (int i = 0; i < 500; i++)
            {
                jam.ReportJam();
            }

            r.Check(jam.State == MissionState.Running && jam.FailReason == MissionFailReason.None,
                "jams never lose the round, however many there are");
            r.AreEqual(500, jam.Jams, "jams are still counted for the read-out");
            for (int i = 0; i < 10; i++)
            {
                jam.ReportCorrect();
            }

            r.Check(jam.State == MissionState.Won, "a jammed round can still be won");

            // Timeout
            var timeout = new MissionRuntime(Def(10, 5, 1f), 20);
            timeout.Begin();
            for (int i = 0; i < 120; i++)
            {
                timeout.Tick(1f / 60f);
            }

            r.Check(timeout.State == MissionState.Lost && timeout.FailReason == MissionFailReason.Timeout,
                "running out of time loses with Timeout");

            // Property 7: the first-clear bonus is paid exactly once.
            var record = new MissionRecord();
            int totalPaid = 0;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var round = new MissionRuntime(Def(1, 5, 100f, 30, 30), 10);
                round.Begin();
                round.ReportCorrect();
                totalPaid += round.Resolve(record).coinsAwarded;
            }

            r.AreEqual(30 * 5 + 30, totalPaid,
                "five clears pay five base rewards and exactly one first-clear bonus");
            r.Check(record.FirstClearPaid, "the first-clear flag is latched");
            r.Check(record.Cleared, "the clear flag is latched");
            r.AreEqual(5, record.Attempts, "every settled round counts as an attempt");

            // Resolve is idempotent for one round.
            var once = new MissionRuntime(Def(1, 5, 100f, 30, 30), 10);
            var freshRecord = new MissionRecord();
            once.Begin();
            once.ReportCorrect();
            MissionOutcome first = once.Resolve(freshRecord);
            MissionOutcome second = once.Resolve(freshRecord);
            MissionOutcome third = once.Resolve(freshRecord);
            r.AreEqual(60, first.coinsAwarded, "the first settle pays base plus first clear");
            r.AreEqual(0, second.coinsAwarded + third.coinsAwarded,
                "repeat settles of the same round pay nothing");
            r.Check(second.won && third.won, "repeat settles report the same verdict");

            // A loss pays nothing and does not touch the latches.
            var lost = new MissionRuntime(Def(10, 0, 100f, 30, 30), 20);
            var lostRecord = new MissionRecord();
            lost.Begin();
            lost.ReportWrong();
            MissionOutcome lostOutcome = lost.Resolve(lostRecord);
            r.AreEqual(0, lostOutcome.coinsAwarded, "a loss pays nothing");
            r.Check(!lostRecord.Cleared && !lostRecord.FirstClearPaid,
                "a loss leaves cleared and firstClearPaid alone");

            // Property 6: cleared never regresses, bestDelivered never shrinks.
            var progress = MissionRecord.Restore(true, true, 40, 3, 90f);
            progress.RecordDelivered(10);
            r.AreEqual(40, progress.BestDelivered, "a worse run cannot lower bestDelivered");
            progress.RecordDelivered(55);
            r.AreEqual(55, progress.BestDelivered, "a better run raises bestDelivered");
            r.Check(progress.Cleared, "cleared stays true");
            progress.RecordWinSeconds(120f);
            r.AreClose(90f, progress.BestSeconds, 0.001f, "a slower win cannot raise bestSeconds");
            progress.RecordWinSeconds(70f);
            r.AreClose(70f, progress.BestSeconds, 0.001f, "a faster win lowers bestSeconds");

            // Property 14 over random interleavings.
            const int iterations = 300;
            bool deliveredConsistent = true;
            bool winImpliesLimits = true;
            int badSeed = -1;
            for (int seed = 0; seed < iterations; seed++)
            {
                var rng = new Random(seed + 909);
                MissionDef def = Def(rng.Next(1, 20), rng.Next(0, 8), rng.Next(1, 60));
                var run = new MissionRuntime(def, rng.Next(0, 200));
                run.Begin();

                int steps = rng.Next(1, 300);
                for (int i = 0; i < steps; i++)
                {
                    switch (rng.Next(4))
                    {
                        case 0:
                            run.ReportCorrect();
                            break;
                        case 1:
                            run.ReportWrong();
                            break;
                        case 2:
                            run.ReportJam();
                            break;
                        default:
                            run.Tick(1f / 60f);
                            break;
                    }

                    if (run.Delivered != run.Correct + run.Wrong)
                    {
                        deliveredConsistent = false;
                        badSeed = seed;
                    }
                }

                if (run.State == MissionState.Won)
                {
                    bool within = run.Correct >= def.objective.targetDelivered &&
                                  run.Wrong <= def.objective.maxWrong &&
                                  run.Elapsed <= def.objective.timeLimitSeconds;
                    if (!within)
                    {
                        winImpliesLimits = false;
                        badSeed = seed;
                    }
                }
            }

            r.Check(deliveredConsistent, "Delivered == Correct + Wrong over " + iterations +
                                         " random interleavings (seed " + badSeed + ")");
            r.Check(winImpliesLimits, "a win always sits inside every authored limit (seed " +
                                      badSeed + ")");
        }
    }
}
