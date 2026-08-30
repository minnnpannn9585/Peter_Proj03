using NUnit.Framework;
using UnityEngine;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Feature: stage1-hud-device-mission-system,
    /// Property 13: the two speed channels are independent - no sequence of SpeedMultiplier writes
    /// can disturb DeviceSpeedBonus, and EffectiveSpeed is always
    /// BaseSpeed x SpeedMultiplier x DeviceSpeedBonus.
    ///
    /// Regression guard for a real defect. BeltPauseSwitch and BeltSpeedToggle overwrite
    /// SpeedMultiplier outright. Had the Booster written to that same field, tapping pause would
    /// have silently erased a 45 coin purchase, with the outcome depending on click order.
    /// </summary>
    [TestFixture]
    public class SpeedChannelRegressionTests
    {
        MiniYard yard;

        [TearDown]
        public void TearDown()
        {
            yard?.Dispose();
            yard = null;
        }

        [Test]
        public void BoosterBonusSurvivesPauseAndFastToggling()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            float baseSpeed = belt.BaseSpeed;

            yard.InstallBooster(belt, 0, 1.6f);
            Assert.AreEqual(1.6f, belt.DeviceSpeedBonus, 1e-4f, "installing a Booster sets the bonus");

            float fast = yard.Rules.fastMultiplier;
            float[] playerWrites = { 0f, 1f, fast, 0f, fast, 1f };

            for (int i = 0; i < playerWrites.Length; i++)
            {
                belt.SpeedMultiplier = playerWrites[i];

                Assert.AreEqual(1.6f, belt.DeviceSpeedBonus, 1e-4f,
                    "SpeedMultiplier = " + playerWrites[i] + " must not touch DeviceSpeedBonus");
                Assert.AreEqual(baseSpeed * playerWrites[i] * 1.6f, belt.EffectiveSpeed, 1e-4f,
                    "EffectiveSpeed must stay base x player x device");
            }
        }

        [Test]
        public void PauseSwitchDoesNotEraseTheBoosterBonus()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            yard.InstallBooster(belt, 0, 1.6f);

            // The real player-facing component, not a bare field write.
            var pause = belt.gameObject.AddComponent<BeltPauseSwitch>();
            pause.Configure();

            pause.OnYardClick();
            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0f, belt.EffectiveSpeed, 1e-4f, "a paused belt does not move");
            Assert.AreEqual(1.6f, belt.DeviceSpeedBonus, 1e-4f, "but the bonus is still there");

            pause.OnYardClick();
            Assert.IsFalse(pause.IsPaused);
            Assert.AreEqual(1.6f, belt.DeviceSpeedBonus, 1e-4f,
                "resuming restores movement with the bonus intact");
            Assert.AreEqual(belt.BaseSpeed * 1.6f, belt.EffectiveSpeed, 1e-4f);
        }

        [Test]
        public void SpeedToggleDoesNotEraseTheBoosterBonus()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            yard.InstallBooster(belt, 0, 1.6f);

            var toggle = belt.gameObject.AddComponent<BeltSpeedToggle>();
            toggle.Configure(yard.Rules.fastMultiplier, null);

            toggle.OnYardClick();
            Assert.IsTrue(toggle.IsFast);
            Assert.AreEqual(1.6f, belt.DeviceSpeedBonus, 1e-4f);
            Assert.AreEqual(belt.BaseSpeed * yard.Rules.fastMultiplier * 1.6f, belt.EffectiveSpeed, 1e-4f,
                "the player's boost and the device's boost compose");

            toggle.OnYardClick();
            Assert.AreEqual(1.6f, belt.DeviceSpeedBonus, 1e-4f);
        }

        [Test]
        public void RemovingABoosterReturnsTheBeltToItsBaseSpeed()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            BoosterDevice booster = yard.InstallBooster(belt, 0, 1.6f);

            Assert.AreEqual(1.6f, belt.DeviceSpeedBonus, 1e-4f);
            Assert.IsTrue(belt.IsSlotOccupied(0), "the booster occupies its slot");

            Object.DestroyImmediate(booster.gameObject);

            Assert.AreEqual(1f, belt.DeviceSpeedBonus, 1e-4f,
                "a removed Booster must not keep boosting");
            Assert.IsFalse(belt.IsSlotOccupied(0), "and it frees its slot");
        }

        /// <summary>Property 13 over random write sequences.</summary>
        [Test]
        public void SpeedChannelsStayIndependentUnderRandomWrites()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            yard.InstallBooster(belt, 0, 1.6f);
            float baseSpeed = belt.BaseSpeed;
            float fast = yard.Rules.fastMultiplier;

            Property.ForAll("Property 13: speed channels are independent", (seed, trace) =>
            {
                float[] options = { 0f, 1f, fast };
                int writes = trace.Rng.Next(1, 30);
                for (int i = 0; i < writes; i++)
                {
                    float value = options[trace.Rng.Next(options.Length)];
                    belt.SpeedMultiplier = value;
                    trace.Log("SpeedMultiplier = " + value);

                    trace.Require(Mathf.Approximately(belt.DeviceSpeedBonus, 1.6f),
                        "DeviceSpeedBonus drifted to " + belt.DeviceSpeedBonus);

                    float expected = Mathf.Max(0f, baseSpeed * value * 1.6f);
                    trace.Require(Mathf.Abs(belt.EffectiveSpeed - expected) < 1e-3f,
                        "EffectiveSpeed was " + belt.EffectiveSpeed + ", expected " + expected);
                }
            });
        }

        [Test]
        public void MissionSpeedScaleFoldsIntoBaseSpeedNotIntoTheDeviceChannel()
        {
            // speedScale is a per-mission tempo. It must not become a third multiplier, or the
            // "EffectiveSpeed == base x player x device" contract would no longer hold.
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            float authored = belt.BaseSpeed;

            yard.InstallBooster(belt, 0, 1.6f);
            belt.SetMissionSpeedScale(1.15f);

            Assert.AreEqual(authored * 1.15f, belt.BaseSpeed, 1e-4f, "the scale lands on BaseSpeed");
            Assert.AreEqual(1.6f, belt.DeviceSpeedBonus, 1e-4f, "and leaves the device channel alone");
            Assert.AreEqual(belt.BaseSpeed * belt.SpeedMultiplier * belt.DeviceSpeedBonus,
                belt.EffectiveSpeed, 1e-4f);

            belt.SetMissionSpeedScale(1f);
            Assert.AreEqual(authored, belt.BaseSpeed, 1e-4f, "and it is reversible");
        }
    }
}
