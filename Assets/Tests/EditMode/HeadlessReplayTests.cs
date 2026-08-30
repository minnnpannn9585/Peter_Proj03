using NUnit.Framework;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Whole-round replays through <see cref="HeadlessRoundSim"/>.
    ///
    /// These are the tests that turn the design's balance prose into measurements: that a round is
    /// reproducible from its seed, that M1 is winnable bare handed, and that M3's blind load is
    /// only survivable with Scanners.
    /// </summary>
    [TestFixture]
    public class HeadlessReplayTests
    {
        HeadlessRoundSim sim;
        HeadlessRoundSim other;

        [TearDown]
        public void TearDown()
        {
            sim?.Dispose();
            sim = null;
            other?.Dispose();
            other = null;
        }

        HeadlessRoundSim Sim(string missionId)
        {
            return new HeadlessRoundSim(LevelFixtures.Level01Config(), LevelFixtures.Mission(missionId));
        }

        [Test]
        public void TheSameSeedReplaysAnIdenticalOutcome()
        {
            sim = Sim("m1_first_shift");
            MissionOutcome first = sim.Run();

            other = Sim("m1_first_shift");
            MissionOutcome second = other.Run();

            Assert.AreEqual(first.won, second.won, "won");
            Assert.AreEqual(first.reason, second.reason, "reason");
            Assert.AreEqual(first.correct, second.correct, "correct");
            Assert.AreEqual(first.wrong, second.wrong, "wrong");
            Assert.AreEqual(first.jams, second.jams, "jams");
            Assert.AreEqual(first.coinsAwarded, second.coinsAwarded, "coinsAwarded");
            Assert.AreEqual(first.seconds, second.seconds, 1e-3f, "seconds");
        }

        [Test]
        public void EveryMissionReleasesItsWholePlanWithoutDeadlocking()
        {
            string[] ids = { "m1_first_shift", "m2_dual_flow", "m3_night_blind" };
            int[] planned = { 18, 56, 108 };

            for (int i = 0; i < ids.Length; i++)
            {
                using (var run = new Disposable(Sim(ids[i])))
                {
                    HeadlessRoundSim current = run.Sim;
                    Assert.AreEqual(planned[i], current.Planned, ids[i] + " Planned");

                    // Long budget: this is checking for deadlock, not for a time limit.
                    current.Run(null, 900f);

                    Assert.AreEqual(current.Planned, current.Emitted,
                        ids[i] + " should release every planned parcel (" + current.Emitted +
                        " of " + current.Planned + ")");
                    Assert.AreEqual(0, current.Traffic.LiveParcelCount,
                        ids[i] + " should leave no parcel stranded on the yard");
                    Assert.AreEqual(current.Planned, current.Correct + current.Wrong,
                        ids[i] + " every parcel should reach a bay");
                }
            }
        }

        [Test]
        public void MissionOneIsWinnableBareHanded()
        {
            // The anti-deadlock floor: with no devices at all, M1 must be completable.
            sim = Sim("m1_first_shift");
            MissionOutcome outcome = sim.Run(null, 200f);

            Assert.AreEqual(0, sim.Gates.Count, "no gates were installed");
            Assert.AreEqual(0, sim.Scanners.Count, "no scanners were installed");
            Assert.IsTrue(outcome.won,
                "M1 must be winnable with nothing bought. correct=" + outcome.correct +
                " wrong=" + outcome.wrong + " jams=" + outcome.jams +
                " reason=" + outcome.reason + " seconds=" + outcome.seconds.ToString("F1"));
            Assert.GreaterOrEqual(outcome.correct, 15, "target is 15 correct deliveries");
            Assert.LessOrEqual(outcome.seconds, 150f, "and it fits inside the 150s limit");
        }

        [Test]
        public void MissionThreeLeavesEveryBlindParcelUnreadableWithoutScanners()
        {
            sim = Sim("m3_night_blind");
            sim.Run(null, 900f);

            Assert.AreEqual(0, sim.Scanners.Count);
            Assert.AreEqual(sim.PlannedBlind, sim.NeverRevealed,
                "with no scanner, no blind parcel is ever revealed");
            Assert.Greater(sim.PlannedBlind, 25,
                "M3 should carry a substantial blind load (got " + sim.PlannedBlind + ")");

            // 3 bays, so a guess is right 1 in 3: expected misroutes are 2/3 of the blind count.
            double expectedMisroutes = sim.NeverRevealed * 2.0 / 3.0;
            int maxWrong = LevelFixtures.Mission("m3_night_blind").objective.maxWrong;
            Assert.Greater(expectedMisroutes, maxWrong * 2,
                "expected misroutes " + expectedMisroutes.ToString("F1") +
                " must dwarf maxWrong " + maxWrong + ", so Scanners are not optional");
        }

        [Test]
        public void TwoScannersOnTheInletFeedsRevealTheEntireBlindLoad()
        {
            sim = Sim("m3_night_blind");

            // One per inlet, on the feed belt upstream of the first diverter. The cap of 2 exists
            // precisely to make this the only sensible arrangement.
            Assert.IsNotNull(sim.InstallScanner("b_in_a_fa", 0), "scanner on the in_a feed");
            Assert.IsNotNull(sim.InstallScanner("b_in_b_fb", 0), "scanner on the in_b feed");

            sim.Run(null, 900f);

            Assert.AreEqual(2, sim.Scanners.Count);
            Assert.AreEqual(0, sim.NeverRevealed,
                "every blind parcel should be revealed before it reaches a decision point " +
                "(planned blind " + sim.PlannedBlind + ", revealed " + sim.RevealedTotal + ")");
            Assert.AreEqual(sim.PlannedBlind, sim.RevealedTotal,
                "the two scanners between them cover the whole blind load");
        }

        [Test]
        public void OneScannerCoversOnlyOneInletsStream()
        {
            // Halfway is not good enough, which is what makes the second scanner a real purchase
            // rather than a luxury.
            sim = Sim("m3_night_blind");
            sim.InstallScanner("b_in_a_fa", 0);
            sim.Run(null, 900f);

            Assert.Greater(sim.NeverRevealed, 0,
                "one scanner cannot cover both inlets");
            Assert.Less(sim.NeverRevealed, sim.PlannedBlind,
                "but it does cover its own inlet's share");
        }

        [Test]
        public void ClosingAGateForItsFullHoldProducesNoJamsInAWholeRound()
        {
            // Property 12, end to end rather than on a synthetic belt.
            sim = Sim("m1_first_shift");
            GateDevice gate = sim.InstallGate("b_fa_hub_a", 1);
            Assert.IsNotNull(gate);

            sim.Begin();

            // Let traffic build up, then hold the gate shut for longer than the jam threshold.
            for (int i = 0; i < 600; i++)
            {
                sim.Tick(HeadlessRoundSim.FixedStep);
            }

            gate.Close();
            int jamsBefore = sim.Jams;

            // 8 seconds of hold against a 3 second jam threshold, with the gate not aged.
            for (int i = 0; i < 480; i++)
            {
                sim.Traffic.Tick(HeadlessRoundSim.FixedStep);
            }

            Assert.AreEqual(jamsBefore, sim.Jams,
                "a full gate hold must not add a single jam");
        }

        [Test]
        public void ABoosterRaisesThroughputOnTheBeltItSitsOn()
        {
            sim = Sim("m2_dual_flow");
            BeltPath belt = sim.Belt("b_fa_hub_a");
            Assert.IsNotNull(belt);

            float before = belt.EffectiveSpeed;
            sim.InstallBooster("b_fa_hub_a", 0);
            float after = belt.EffectiveSpeed;

            Assert.AreEqual(before * 1.6f, after, 1e-3f,
                "the booster multiplies this belt's speed by 1.6");

            // And only that belt.
            BeltPath neighbour = sim.Belt("b_fb_hub_b");
            Assert.AreEqual(1f, neighbour.DeviceSpeedBonus, 1e-4f,
                "a booster is local to one belt");
        }

        [Test]
        public void MissionSpeedScaleIsAppliedToTheYard()
        {
            using (var slow = new Disposable(Sim("m1_first_shift")))
            using (var fast = new Disposable(Sim("m3_night_blind")))
            {
                BeltPath slowBelt = slow.Sim.Belt("b_in_a_fa");
                BeltPath fastBelt = fast.Sim.Belt("b_in_a_fa");

                Assert.AreEqual(0.8f, slowBelt.MissionSpeedScale, 1e-3f, "M1 runs at 0.8x");
                Assert.AreEqual(1.15f, fastBelt.MissionSpeedScale, 1e-3f, "M3 runs at 1.15x");
                Assert.Less(slowBelt.EffectiveSpeed, fastBelt.EffectiveSpeed,
                    "M1 should genuinely be gentler than M3");
            }
        }

        /// <summary>Small using-scope helper so a test can run several sims without leaking objects.</summary>
        struct Disposable : System.IDisposable
        {
            public Disposable(HeadlessRoundSim sim)
            {
                Sim = sim;
            }

            public HeadlessRoundSim Sim { get; }

            public void Dispose()
            {
                Sim?.Dispose();
            }
        }
    }
}
