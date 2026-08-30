using System.Collections.Generic;
using NUnit.Framework;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Replays the documented growth curve using the numbers actually in level_01.json.
    ///
    /// The point of this test is to fail loudly if anyone edits a price, a cap or a reward without
    /// re-deriving the curve. Nothing here hard-codes a shop value: prices and rewards are read
    /// from the config, and only the resulting balances are asserted.
    /// </summary>
    [TestFixture]
    public class BalanceCurveTests
    {
        /// <summary>Seconds one attempt occupies: run time plus setup and retry slack.</summary>
        static readonly Dictionary<string, float> AttemptSeconds = new Dictionary<string, float>
        {
            { "m1_first_shift", 160f },
            { "m2_dual_flow", 218f },
            { "m3_night_blind", 289f }
        };

        LevelConfig config;
        Wallet wallet;
        DeviceInventory inventory;
        ShopService shop;
        Dictionary<string, MissionRecord> records;

        [SetUp]
        public void SetUp()
        {
            config = LevelFixtures.Level01Config();
            wallet = new Wallet(ProfileStore.DefaultCoins);
            inventory = new DeviceInventory();
            shop = new ShopService(wallet, inventory, config.devices, () => GamePhase.Prep);
            records = new Dictionary<string, MissionRecord>();
        }

        [Test]
        public void TheDocumentedEightRoundWalkthroughReachesTwoFortyThenSixty()
        {
            Assert.AreEqual(20, wallet.Balance, "step 0: a new save holds 20 coins");

            Play("m1_first_shift", 80, "step 1: M1 first clear");
            Buy(DeviceType.Gate, 20, "step 2: buy Gate #1");
            Play("m1_first_shift", 50, "step 3: M1 replay");
            Play("m1_first_shift", 80, "step 4: M1 replay");
            Buy(DeviceType.Gate, 20, "step 5: buy Gate #2");
            Play("m2_dual_flow", 140, "step 6: M2 first clear");
            Buy(DeviceType.Booster, 95, "step 7: buy Booster #1");
            Play("m2_dual_flow", 150, "step 8: M2 replay");
            Buy(DeviceType.Scanner, 40, "step 9: buy Scanner #1");
            Play("m2_dual_flow", 95, "step 10: M2 replay");
            Play("m2_dual_flow", 150, "step 11: M2 replay");
            Buy(DeviceType.Scanner, 40, "step 12: buy Scanner #2");
            Play("m3_night_blind", 240, "step 13: M3 first clear");
            Buy(DeviceType.AutoArm, 60, "step 14: buy AutoArm");

            Assert.AreEqual(2, inventory.Owned(DeviceType.Gate));
            Assert.AreEqual(1, inventory.Owned(DeviceType.Booster));
            Assert.AreEqual(2, inventory.Owned(DeviceType.Scanner));
            Assert.AreEqual(1, inventory.Owned(DeviceType.AutoArm));

            Assert.AreEqual(3, records["m1_first_shift"].Attempts, "M1 x 3");
            Assert.AreEqual(4, records["m2_dual_flow"].Attempts, "M2 x 4");
            Assert.AreEqual(1, records["m3_night_blind"].Attempts, "M3 x 1");
        }

        [Test]
        public void FirstClearEfficiencyRisesWithEveryMission()
        {
            // If an earlier mission ever paid better per minute, farming it would be optimal and
            // the difficulty curve would stop meaning anything.
            string[] ids = { "m1_first_shift", "m2_dual_flow", "m3_night_blind" };
            float[] documented = { 22.5f, 33.0f, 41.5f };

            float previous = -1f;
            for (int i = 0; i < ids.Length; i++)
            {
                MissionDef mission = LevelFixtures.Mission(ids[i]);
                int firstClearTotal = mission.rewards.baseCoins + mission.rewards.firstClearCoins;
                float perMinute = firstClearTotal / (AttemptSeconds[ids[i]] / 60f);

                Assert.AreEqual(documented[i], perMinute, 1f,
                    ids[i] + " first-clear efficiency should be about " + documented[i] + " coins/min");
                Assert.Greater(perMinute, previous,
                    ids[i] + " must pay better per minute than the mission before it");
                previous = perMinute;
            }
        }

        [Test]
        public void FirstClearTotalsMatchTheDesign()
        {
            AssertRewards("m1_first_shift", 30, 30, 60);
            AssertRewards("m2_dual_flow", 55, 65, 120);
            AssertRewards("m3_night_blind", 90, 110, 200);
        }

        static void AssertRewards(string id, int baseCoins, int firstClear, int total)
        {
            MissionDef mission = LevelFixtures.Mission(id);
            Assert.AreEqual(baseCoins, mission.rewards.baseCoins, id + " base");
            Assert.AreEqual(firstClear, mission.rewards.firstClearCoins, id + " firstClear");
            Assert.AreEqual(total, mission.rewards.baseCoins + mission.rewards.firstClearCoins,
                id + " first-clear total");
        }

        [Test]
        public void MissionOneIsTheAntiDeadlockFloor()
        {
            // M1 exists so progress can never stall: two replays must always cover another Gate.
            MissionDef m1 = LevelFixtures.Mission("m1_first_shift");
            DeviceSpec gate = LevelFixtures.Spec(DeviceType.Gate);

            Assert.GreaterOrEqual(m1.rewards.baseCoins * 2, gate.price,
                "two M1 replays must always pay for the cheapest meaningful upgrade");
        }

        [Test]
        public void NoStepOfTheCurveEverGoesNegative()
        {
            // Same walkthrough, asserted only on the invariant rather than on exact balances, so
            // this keeps working if the curve is re-tuned.
            string[] rounds =
            {
                "m1_first_shift", "m1_first_shift", "m1_first_shift",
                "m2_dual_flow", "m2_dual_flow", "m2_dual_flow", "m2_dual_flow",
                "m3_night_blind"
            };

            DeviceType[] purchases =
            {
                DeviceType.Gate, DeviceType.Gate, DeviceType.Booster,
                DeviceType.Scanner, DeviceType.Scanner, DeviceType.AutoArm
            };

            int purchaseIndex = 0;
            for (int i = 0; i < rounds.Length; i++)
            {
                Win(rounds[i]);
                Assert.GreaterOrEqual(wallet.Balance, 0, "balance went negative after " + rounds[i]);

                // Buy whatever is next as soon as it is affordable.
                while (purchaseIndex < purchases.Length && shop.TryBuy(purchases[purchaseIndex]))
                {
                    purchaseIndex++;
                    Assert.GreaterOrEqual(wallet.Balance, 0, "balance went negative after a purchase");
                }
            }

            Assert.AreEqual(purchases.Length, purchaseIndex,
                "the documented eight rounds must fund every purchase in the curve");
        }

        // ------------------------------------------------------------------ helpers

        void Play(string missionId, int expectedBalance, string label)
        {
            MissionOutcome outcome = Win(missionId);
            Assert.IsTrue(outcome.won, label + " should be a win");
            Assert.AreEqual(expectedBalance, wallet.Balance, label + " balance");
            Assert.GreaterOrEqual(wallet.Balance, 0, label + " must not go negative");
        }

        MissionOutcome Win(string missionId)
        {
            MissionDef mission = LevelFixtures.Mission(missionId);
            if (!records.TryGetValue(missionId, out MissionRecord record))
            {
                record = new MissionRecord();
                records[missionId] = record;
            }

            var run = new MissionRuntime(mission, mission.PlannedFromPlan());
            run.Begin();
            for (int i = 0; i < mission.objective.targetDelivered; i++)
            {
                run.ReportCorrect();
            }

            MissionOutcome outcome = run.Resolve(record);
            wallet.Earn(outcome.coinsAwarded);
            return outcome;
        }

        void Buy(DeviceType device, int expectedBalance, string label)
        {
            Assert.IsTrue(shop.TryBuy(device), label + " should succeed");
            Assert.AreEqual(expectedBalance, wallet.Balance, label + " balance");
            Assert.GreaterOrEqual(wallet.Balance, 0, label + " must not go negative");
        }
    }
}
