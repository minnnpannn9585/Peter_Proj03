using NUnit.Framework;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Feature: stage1-hud-device-mission-system,
    /// Property 12: gates never produce jams - stalling caused only by a closed gate contributes
    /// zero to the jam counter, however long it lasts, and the gate always reopens so the queue
    /// drains.
    ///
    /// This is a regression guard for a real defect. Before the fix, TrafficSystem accumulated
    /// StallTime for any parcel that barely moved, so a gate's 8 second hold blew straight through
    /// the 3 second jam threshold: closing a gate reliably manufactured the very jams the gate
    /// exists to prevent, and M2/M3 both lose on too many jams.
    /// </summary>
    [TestFixture]
    public class GateJamRegressionTests
    {
        MiniYard yard;

        [TearDown]
        public void TearDown()
        {
            yard?.Dispose();
            yard = null;
        }

        [Test]
        public void ClosedGateHoldingLongerThanTheJamThresholdProducesNoJams()
        {
            yard = new MiniYard();
            Assert.AreEqual(3f, yard.Rules.jamStallSeconds, "fixture expects the default jam threshold");

            BeltPath belt = yard.BuildStraightLine(6);
            GateDevice gate = yard.InstallGate(belt, 1, 8f);
            yard.SetRunning(true);

            // A queue behind the barrier, not just the one parcel touching it.
            for (int i = 0; i < 4; i++)
            {
                yard.Emit(belt, DestinationColor.Red);
                yard.Run(0.5f);
            }

            gate.Close();
            Assert.IsTrue(gate.IsClosed, "the gate should be closed");

            int jamsBefore = yard.Director.Jams;

            // Hold for the full 8 seconds, well past the 3 second jam threshold. The gate is
            // deliberately not aged here, so it stays shut for the whole window.
            for (int i = 0; i < 480; i++)
            {
                yard.Traffic.Tick(1f / 60f);
            }

            Assert.AreEqual(jamsBefore, yard.Director.Jams,
                "a deliberately closed gate must not register as congestion");
        }

        [Test]
        public void ParcelsQueuedBehindAGateAreAlsoExemptFromTheJamCounter()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            GateDevice gate = yard.InstallGate(belt, 2, 8f);
            yard.SetRunning(true);

            // Fill the run so several parcels stack up behind the leader.
            for (int i = 0; i < 5; i++)
            {
                yard.Emit(belt, DestinationColor.Red);
                yard.Run(0.4f);
            }

            gate.Close();
            for (int i = 0; i < 480; i++)
            {
                yard.Traffic.Tick(1f / 60f);
            }

            Assert.AreEqual(0, yard.Director.Jams,
                "the whole tailback behind a gate is the player's queue, not a jam");

            // The exemption must not hide a real stall: every held parcel records gate wait time.
            int held = 0;
            for (int i = 0; i < belt.Parcels.Count; i++)
            {
                if (belt.Parcels[i].GateWaitTime > 0f)
                {
                    held++;
                }
            }

            Assert.Greater(held, 0, "gate-held parcels should accumulate GateWaitTime");
        }

        [Test]
        public void GateReopensAfterItsHoldAndTheQueueDrains()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            GateDevice gate = yard.InstallGate(belt, 1, 8f);
            yard.SetRunning(true);

            for (int i = 0; i < 3; i++)
            {
                yard.Emit(belt, DestinationColor.Red);
                yard.Run(0.4f);
            }

            gate.Close();

            // Age the gate this time, so it opens itself. That self-opening is what makes the jam
            // exemption safe: the queue is guaranteed to clear.
            yard.Run(9f);

            Assert.IsFalse(gate.IsClosed, "the gate must open itself after holdSeconds");

            yard.Run(12f);
            Assert.AreEqual(0, belt.Parcels.Count, "the queue drains once the gate reopens");
            Assert.AreEqual(0, yard.Director.Jams, "and still no jams were counted");
        }

        [Test]
        public void GenuineBlockageStillCountsAsAJam()
        {
            // The exemption must be narrow. With the belt stopped by the player's own pause
            // switch - not by a gate - a stalled parcel is still congestion.
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            yard.SetRunning(true);

            yard.Emit(belt, DestinationColor.Red);
            belt.SpeedMultiplier = 0f;

            for (int i = 0; i < 300; i++)
            {
                yard.Traffic.Tick(1f / 60f);
            }

            Assert.AreEqual(1, yard.Director.Jams,
                "a stalled parcel with no gate involved is a jam, counted once");
        }

        [Test]
        public void OneParcelIsCountedAsAJamOnlyOnce()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            yard.SetRunning(true);

            yard.Emit(belt, DestinationColor.Red);
            belt.SpeedMultiplier = 0f;

            // Ten times the threshold: the JamCounted latch must keep this at exactly one.
            for (int i = 0; i < 1800; i++)
            {
                yard.Traffic.Tick(1f / 60f);
            }

            Assert.AreEqual(1, yard.Director.Jams, "JamCounted keeps one parcel to one jam");
        }
    }
}
