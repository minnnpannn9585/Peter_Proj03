using System.Collections.Generic;

namespace ParcelSort.Offline
{
    /// <summary>level_01.json topology, slot budget and device catalog.</summary>
    public static class LevelChecks
    {
        public static void Run(CheckRunner r)
        {
            r.Section("level_01.json");

            LevelConfig config = LevelFixture.Level01();

            r.AreEqual("level_01", config.id, "level id");
            r.AreEqual(36, config.grid.cellsX, "grid width");
            r.AreEqual(24, config.grid.cellsZ, "grid height");
            r.AreClose(2.0f, config.grid.size, 0.0001f, "cell size");
            r.AreClose(1.5f, config.grid.levelHeight, 0.0001f, "level height");
            r.AreClose(2.1f, config.grid.visualScale, 0.0001f, "visual scale");
            r.AreClose(1.25f, config.grid.viewPadCells, 0.0001f, "view pad cells");
            r.AreEqual(2, config.rules.minBeltCells, "min belt cells");
            r.AreEqual(8, config.rules.maxBeltCells, "max belt cells");

            LevelTopologyCheck.Report report = LevelTopologyCheck.Validate(config);
            if (!report.Ok)
            {
                for (int i = 0; i < report.errors.Count; i++)
                {
                    r.Info("topology error: " + report.errors[i]);
                }
            }

            r.Check(report.Ok, "the level passes every topology rule");

            // Node census
            int inlets = 0;
            int bays = 0;
            for (int i = 0; i < config.nodes.Count; i++)
            {
                if (config.nodes[i].type == NodeType.Inlet)
                {
                    inlets++;
                }
                else if (config.nodes[i].type == NodeType.Bay)
                {
                    bays++;
                }
            }

            r.AreEqual(2, inlets, "two inlets");
            r.AreEqual(3, bays, "three truck bays");
            r.AreEqual(22, config.belts.Count, "22 belts");

            // Diverters: hub_a, hub_b, hub_m
            r.AreEqual(3, report.deviceNodes.Count, "three diverters carry a node device slot");
            r.Check(report.deviceNodes.Contains("hub_a") &&
                    report.deviceNodes.Contains("hub_b") &&
                    report.deviceNodes.Contains("hub_m"),
                "the diverters are hub_a, hub_b and hub_m");

            // Slot budget
            int installable = 0;
            int noInstall = 0;
            for (int i = 0; i < config.belts.Count; i++)
            {
                if (config.belts[i].allowInstall)
                {
                    installable++;
                }
                else
                {
                    noInstall++;
                }
            }

            r.AreEqual(19, installable, "19 belts allow installs");
            r.AreEqual(3, noInstall, "3 bay approaches forbid installs");

            string[] approaches = { "b_mb1_bay_blue", "b_r2_bay_red", "b_g2_bay_green" };
            for (int i = 0; i < approaches.Length; i++)
            {
                r.Check(report.beltSlots.TryGetValue(approaches[i], out int approachSlots) &&
                        approachSlots == 0,
                    approaches[i] + " generates 0 slots");
            }

            r.AreEqual(63, report.TotalBeltSlots, "63 belt install slots");
            r.AreEqual(3, report.TotalNodeSlots, "3 node install slots");
            r.AreEqual(66, report.TotalInstallSlots, "66 install slots in total");

            // Slot maths matches the documented sequence {1.2, 4.064, 6.929, 9.793, ...}
            var slots = new List<float>();
            float deckWidth = BeltSlotMath.DeckWidth(config.grid.visualScale);
            BeltSlotMath.Fill(slots, 12f, config.grid.size, deckWidth, true);
            r.AreEqual(4, slots.Count, "a 12.0 long belt yields 4 slots");
            r.AreClose(1.2f, slots[0], 0.001f, "first slot sits at margin 1.2");
            r.AreClose(2.8644f, BeltSlotMath.Spacing(config.grid.size, deckWidth), 0.001f,
                "slot spacing is about 2.864");
            r.AreClose(4.0644f, slots[1], 0.001f, "second slot");
            r.AreClose(6.9288f, slots[2], 0.001f, "third slot");
            r.AreClose(9.7932f, slots[3], 0.001f, "fourth slot");

            // Both inlets must be able to feed all three bays.
            foreach (KeyValuePair<string, HashSet<string>> pair in report.reachableBays)
            {
                r.AreEqual(3, pair.Value.Count, pair.Key + " reaches all three bays");
                r.Check(pair.Value.Contains("bay_red") && pair.Value.Contains("bay_green") &&
                        pair.Value.Contains("bay_blue"),
                    pair.Key + " reaches red, green and blue");
            }

            // Device catalog
            r.AreEqual(4, config.devices.Count, "the shop offers four devices");
            CheckSpec(r, config, DeviceType.Gate, 60, 4, InstallTargetKind.BeltSlot);
            CheckSpec(r, config, DeviceType.Booster, 45, 3, InstallTargetKind.BeltSlot);
            CheckSpec(r, config, DeviceType.Scanner, 110, 2, InstallTargetKind.BeltSlot);
            CheckSpec(r, config, DeviceType.AutoArm, 180, 1, InstallTargetKind.Node);

            int capTotal = 0;
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                config.devices.TryGet(DeviceTypes.All[i], out DeviceSpec spec);
                capTotal += spec != null ? spec.cap : 0;
            }

            r.AreEqual(10, capTotal, "device caps total 10 units");
            r.Check(66 > capTotal, "there are far more legal positions than devices (66 vs 10)");

            config.devices.TryGet(DeviceType.Gate, out DeviceSpec gate);
            r.AreClose(8.0f, gate.holdSeconds, 0.001f, "gate holds for 8 seconds");
            config.devices.TryGet(DeviceType.Booster, out DeviceSpec booster);
            r.AreClose(1.6f, booster.speedBonus, 0.001f, "booster bonus is 1.6");
            config.devices.TryGet(DeviceType.AutoArm, out DeviceSpec arm);
            r.AreClose(0.6f, arm.switchCooldown, 0.001f, "autoArm cooldown is 0.6s");
            r.Check(arm.skipBlind, "autoArm skips blind parcels");

            // Missions
            r.AreEqual(3, config.missions.Count, "three missions");
            CheckMission(r, config, "m1_first_shift", "首日轮班", 15, 3, -1, 150f, 0.8f, 0f, false, 30, 30);
            CheckMission(r, config, "m2_dual_flow", "双线并流", 46, 6, 4, 150f, 1.0f, 0f, true, 55, 65);
            CheckMission(r, config, "m3_night_blind", "夜班盲件", 84, 9, 6, 200f, 1.15f, 0.35f, true, 90, 110);

            for (int i = 0; i < config.missions.Count; i++)
            {
                MissionDef mission = config.missions[i];
                r.Check(!mission.ConfigError,
                    mission.id + " has no config error: " + mission.ConfigErrorText);

                bool allConstant = true;
                for (int w = 0; w < mission.spawn.waves.Count; w++)
                {
                    RandomRange count = mission.spawn.waves[w].count;
                    if (count.min != count.max)
                    {
                        allConstant = false;
                    }
                }

                r.Check(allConstant, mission.id + " writes every wave count as a constant");
            }

            config.TryGetMission("m3_night_blind", out MissionDef m3);
            r.AreEqual("盲件 35%", m3.SpecialTag(), "m3 advertises its blind mechanic");
            config.TryGetMission("m1_first_shift", out MissionDef m1);
            r.AreEqual(string.Empty, m1.SpecialTag(), "m1 advertises no special mechanic");

            r.AreEqual("level_02", config.nextLevel, "level_01 points at level_02");
        }

        static void CheckSpec(
            CheckRunner r, LevelConfig config, DeviceType device,
            int price, int cap, InstallTargetKind target)
        {
            if (!config.devices.TryGet(device, out DeviceSpec spec))
            {
                r.Check(false, "the shop offers " + device);
                return;
            }

            r.AreEqual(price, spec.price, device + " price");
            r.AreEqual(cap, spec.cap, device + " cap");
            r.Check(spec.target == target, device + " installs on " + target);
        }

        static void CheckMission(
            CheckRunner r, LevelConfig config, string id, string name,
            int target, int maxWrong, int maxJams, float limit,
            float speedScale, float blindRatio, bool mixColors, int baseCoins, int firstClear)
        {
            if (!config.TryGetMission(id, out MissionDef mission))
            {
                r.Check(false, "level_01 defines " + id);
                return;
            }

            r.AreEqual(name, mission.displayName, id + " display name");
            r.AreEqual(target, mission.objective.targetDelivered, id + " targetDelivered");
            r.AreEqual(maxWrong, mission.objective.maxWrong, id + " maxWrong");
            r.AreEqual(maxJams, mission.objective.maxJams, id + " maxJams");
            r.AreClose(limit, mission.objective.timeLimitSeconds, 0.001f, id + " timeLimit");
            r.AreClose(speedScale, mission.modifiers.speedScale, 0.001f, id + " speedScale");
            r.AreClose(blindRatio, mission.modifiers.blindRatio, 0.001f, id + " blindRatio");
            r.Check(mission.modifiers.mixColors == mixColors, id + " mixColors is " + mixColors);
            r.AreEqual(baseCoins, mission.rewards.baseCoins, id + " base reward");
            r.AreEqual(firstClear, mission.rewards.firstClearCoins, id + " first-clear reward");
        }
    }
}
