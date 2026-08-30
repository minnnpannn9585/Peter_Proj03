using System.Collections.Generic;

namespace ParcelSort.Offline
{
    /// <summary>
    /// Replays the documented growth curve using the numbers actually in level_01.json. If
    /// anyone edits a price or a reward without updating the curve, this check fails.
    /// </summary>
    public static class BalanceChecks
    {
        /// <summary>Estimated seconds one attempt occupies, run time plus setup and retry slack.</summary>
        static readonly Dictionary<string, float> AttemptSeconds = new Dictionary<string, float>
        {
            { "m1_first_shift", 160f },
            { "m2_dual_flow", 218f },
            { "m3_night_blind", 289f }
        };

        public static void Run(CheckRunner r)
        {
            r.Section("Growth curve (Requirement 4.9)");

            LevelConfig config = LevelFixture.Level01();
            var wallet = new Wallet(ProfileStore.DefaultCoins);
            var inventory = new DeviceInventory();
            var shop = new ShopService(wallet, inventory, config.devices, () => GamePhase.Prep);
            var records = new Dictionary<string, MissionRecord>();

            r.AreEqual(20, wallet.Balance, "step 0: a new save holds 20 coins");

            Play(r, config, wallet, records, "m1_first_shift", 80, "step 1: M1 first clear");
            Buy(r, shop, wallet, DeviceType.Gate, 20, "step 2: buy Gate #1");
            Play(r, config, wallet, records, "m1_first_shift", 50, "step 3: M1 replay");
            Play(r, config, wallet, records, "m1_first_shift", 80, "step 4: M1 replay");
            Buy(r, shop, wallet, DeviceType.Gate, 20, "step 5: buy Gate #2");
            Play(r, config, wallet, records, "m2_dual_flow", 140, "step 6: M2 first clear");
            Buy(r, shop, wallet, DeviceType.Booster, 95, "step 7: buy Booster #1");
            Play(r, config, wallet, records, "m2_dual_flow", 150, "step 8: M2 replay");
            Buy(r, shop, wallet, DeviceType.Scanner, 40, "step 9: buy Scanner #1");
            Play(r, config, wallet, records, "m2_dual_flow", 95, "step 10: M2 replay");
            Play(r, config, wallet, records, "m2_dual_flow", 150, "step 11: M2 replay");
            Buy(r, shop, wallet, DeviceType.Scanner, 40, "step 12: buy Scanner #2");
            Play(r, config, wallet, records, "m3_night_blind", 240, "step 13: M3 first clear");
            Buy(r, shop, wallet, DeviceType.AutoArm, 60, "step 14: buy AutoArm");

            r.AreEqual(2, inventory.Owned(DeviceType.Gate), "the run ends with 2 gates");
            r.AreEqual(1, inventory.Owned(DeviceType.Booster), "the run ends with 1 booster");
            r.AreEqual(2, inventory.Owned(DeviceType.Scanner), "the run ends with 2 scanners");
            r.AreEqual(1, inventory.Owned(DeviceType.AutoArm), "the run ends with 1 auto arm");

            r.AreEqual(3, records["m1_first_shift"].Attempts, "M1 was played three times");
            r.AreEqual(4, records["m2_dual_flow"].Attempts, "M2 was played four times");
            r.AreEqual(1, records["m3_night_blind"].Attempts, "M3 was played once");

            // Coin efficiency must rise with mission number, otherwise farming M1 would be optimal.
            float last = -1f;
            string[] ids = { "m1_first_shift", "m2_dual_flow", "m3_night_blind" };
            float[] expected = { 22.5f, 33.0f, 41.5f };
            for (int i = 0; i < ids.Length; i++)
            {
                config.TryGetMission(ids[i], out MissionDef mission);
                int firstClearTotal = mission.rewards.baseCoins + mission.rewards.firstClearCoins;
                float perMinute = firstClearTotal / (AttemptSeconds[ids[i]] / 60f);
                r.AreClose(expected[i], perMinute, 1f,
                    ids[i] + " first-clear efficiency is about " + expected[i] + " coins/min");
                r.Check(perMinute > last, ids[i] + " is more coin efficient than the mission before it");
                last = perMinute;
            }

            // The floor that stops a dead end: M1 always pays for the next Gate within 2 replays.
            config.TryGetMission("m1_first_shift", out MissionDef m1);
            config.devices.TryGet(DeviceType.Gate, out DeviceSpec gate);
            r.Check(m1.rewards.baseCoins * 2 >= gate.price,
                "two M1 replays always cover a Gate, so progress can never stall");
        }

        static void Play(
            CheckRunner r,
            LevelConfig config,
            Wallet wallet,
            Dictionary<string, MissionRecord> records,
            string missionId,
            int expectedBalance,
            string label)
        {
            config.TryGetMission(missionId, out MissionDef mission);
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
            r.Check(outcome.won, label + " is a win");
            wallet.Earn(outcome.coinsAwarded);
            r.AreEqual(expectedBalance, wallet.Balance, label + " leaves the documented balance");
            r.Check(wallet.Balance >= 0, label + " never goes negative");
        }

        static void Buy(
            CheckRunner r, ShopService shop, Wallet wallet,
            DeviceType device, int expectedBalance, string label)
        {
            r.Check(shop.TryBuy(device), label + " succeeds");
            r.AreEqual(expectedBalance, wallet.Balance, label + " leaves the documented balance");
            r.Check(wallet.Balance >= 0, label + " never goes negative");
        }
    }
}
