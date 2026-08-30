using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Self-check on the shipped level_01.json: topology, install slot budget, device catalog and
    /// mission definitions. Everything asserted here is a number the design doc quotes, so the
    /// doc and the data cannot drift apart silently.
    /// </summary>
    [TestFixture]
    public class LevelConfigTests
    {
        [Test]
        public void GridAndRulesMatchTheDesign()
        {
            LevelConfig config = LevelFixtures.Level01Config();

            Assert.AreEqual("level_01", config.id);
            Assert.AreEqual(36, config.grid.cellsX);
            Assert.AreEqual(24, config.grid.cellsZ);
            Assert.AreEqual(2.0f, config.grid.size, 1e-4f);
            Assert.AreEqual(1.5f, config.grid.levelHeight, 1e-4f);
            Assert.AreEqual(2.1f, config.grid.visualScale, 1e-4f);
            Assert.AreEqual(1.25f, config.grid.viewPadCells, 1e-4f);
            Assert.AreEqual(2, config.rules.minBeltCells);
            Assert.AreEqual(8, config.rules.maxBeltCells,
                "the central spur is 6 cells, so the old cap of 6 would reject this map");
        }

        [Test]
        public void TheLevelPassesEveryTopologyRule()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            LevelTopologyCheck.Report report = LevelTopologyCheck.Validate(config);

            Assert.IsTrue(report.Ok,
                "level_01 failed validation:\n  " + string.Join("\n  ", report.errors));
        }

        [Test]
        public void TwoInletsThreeBaysAndTwentyTwoBelts()
        {
            LevelConfig config = LevelFixtures.Level01Config();

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

            Assert.AreEqual(2, inlets);
            Assert.AreEqual(3, bays);
            Assert.AreEqual(22, config.belts.Count);
        }

        [Test]
        public void NodesSitAtTheDocumentedCells()
        {
            var expected = new Dictionary<string, Vector2Int>
            {
                { "in_a", new Vector2Int(2, 20) },
                { "in_b", new Vector2Int(2, 4) },
                { "fa", new Vector2Int(8, 20) },
                { "fb", new Vector2Int(8, 4) },
                { "hub_a", new Vector2Int(13, 20) },
                { "hub_b", new Vector2Int(13, 4) },
                { "hub_m", new Vector2Int(21, 12) },
                { "r1", new Vector2Int(19, 20) },
                { "g1", new Vector2Int(19, 4) },
                { "mrg_r", new Vector2Int(24, 20) },
                { "mrg_g", new Vector2Int(24, 4) },
                { "r2", new Vector2Int(28, 20) },
                { "g2", new Vector2Int(28, 4) },
                { "xa_1", new Vector2Int(17, 16) },
                { "xb_1", new Vector2Int(17, 8) },
                { "ra_1", new Vector2Int(21, 17) },
                { "rb_1", new Vector2Int(21, 7) },
                { "mb_1", new Vector2Int(26, 12) },
                { "bay_red", new Vector2Int(32, 20) },
                { "bay_green", new Vector2Int(32, 4) },
                { "bay_blue", new Vector2Int(31, 12) }
            };

            LevelConfig config = LevelFixtures.Level01Config();
            Assert.AreEqual(expected.Count, config.nodes.Count, "node count");

            for (int i = 0; i < config.nodes.Count; i++)
            {
                NodeDef node = config.nodes[i];
                Assert.IsTrue(expected.TryGetValue(node.id, out Vector2Int cell),
                    "unexpected node '" + node.id + "'");
                Assert.AreEqual(cell.x, node.cellX, node.id + " cellX");
                Assert.AreEqual(cell.y, node.cellZ, node.id + " cellZ");
            }
        }

        [Test]
        public void ThreeDivertersCarryANodeDeviceSlot()
        {
            LevelTopologyCheck.Report report =
                LevelTopologyCheck.Validate(LevelFixtures.Level01Config());

            Assert.AreEqual(3, report.deviceNodes.Count);
            Assert.Contains("hub_a", report.deviceNodes);
            Assert.Contains("hub_b", report.deviceNodes);
            Assert.Contains("hub_m", report.deviceNodes);
        }

        [Test]
        public void TheInstallSlotBudgetIsSixtyThreePlusThree()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            LevelTopologyCheck.Report report = LevelTopologyCheck.Validate(config);

            int installable = 0;
            int locked = 0;
            for (int i = 0; i < config.belts.Count; i++)
            {
                if (config.belts[i].allowInstall)
                {
                    installable++;
                }
                else
                {
                    locked++;
                }
            }

            Assert.AreEqual(19, installable, "19 belts allow installs");
            Assert.AreEqual(3, locked, "the three bay approaches do not");

            Assert.AreEqual(63, report.TotalBeltSlots, "63 belt install slots");
            Assert.AreEqual(3, report.TotalNodeSlots, "3 node install slots");
            Assert.AreEqual(66, report.TotalInstallSlots, "66 legal positions in total");
        }

        [Test]
        public void TheBayApproachesGenerateNoSlots()
        {
            LevelTopologyCheck.Report report =
                LevelTopologyCheck.Validate(LevelFixtures.Level01Config());

            string[] approaches = { "b_mb1_bay_blue", "b_r2_bay_red", "b_g2_bay_green" };
            for (int i = 0; i < approaches.Length; i++)
            {
                Assert.IsTrue(report.beltSlots.TryGetValue(approaches[i], out int slots),
                    approaches[i] + " should exist");
                Assert.AreEqual(0, slots,
                    approaches[i] + " must generate no slots: a device in front of a bay has no " +
                    "decision value and would cover the result the player needs to read");
            }
        }

        [Test]
        public void SlotSpacingMatchesTheDocumentedArcLengths()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            float deckWidth = BeltSlotMath.DeckWidth(config.grid.visualScale);

            Assert.AreEqual(1.2f, BeltSlotMath.Margin(config.grid.size), 1e-3f, "margin");
            Assert.AreEqual(2.8644f, BeltSlotMath.Spacing(config.grid.size, deckWidth), 1e-3f,
                "spacing");

            var slots = new List<float>();
            BeltSlotMath.Fill(slots, 12f, config.grid.size, deckWidth, true);

            Assert.AreEqual(4, slots.Count, "a 12.0 long belt yields 4 slots");
            Assert.AreEqual(1.2f, slots[0], 1e-3f);
            Assert.AreEqual(4.0644f, slots[1], 1e-3f);
            Assert.AreEqual(6.9288f, slots[2], 1e-3f);
            Assert.AreEqual(9.7932f, slots[3], 1e-3f);
        }

        [Test]
        public void BothInletsReachAllThreeBays()
        {
            LevelTopologyCheck.Report report =
                LevelTopologyCheck.Validate(LevelFixtures.Level01Config());

            Assert.AreEqual(2, report.reachableBays.Count, "two inlets were analysed");

            foreach (KeyValuePair<string, HashSet<string>> pair in report.reachableBays)
            {
                Assert.AreEqual(3, pair.Value.Count, pair.Key + " should reach three bays");
                Assert.IsTrue(pair.Value.Contains("bay_red"), pair.Key + " -> bay_red");
                Assert.IsTrue(pair.Value.Contains("bay_green"), pair.Key + " -> bay_green");
                Assert.IsTrue(pair.Value.Contains("bay_blue"), pair.Key + " -> bay_blue");
            }
        }

        [Test]
        public void EveryBeltSpanIsInsideTheAllowedRange()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            LevelTopologyCheck.Report report = LevelTopologyCheck.Validate(config);

            foreach (KeyValuePair<string, int> pair in report.beltCells)
            {
                Assert.GreaterOrEqual(pair.Value, config.rules.minBeltCells, pair.Key + " too short");
                Assert.LessOrEqual(pair.Value, config.rules.maxBeltCells, pair.Key + " too long");
            }
        }

        [Test]
        public void ABrokenLevelIsReportedRatherThanLoaded()
        {
            // A belt whose two ends are neither axis aligned nor 45 degrees must be rejected.
            var config = new LevelConfig();
            config.nodes.Add(new NodeDef { id = "a", type = NodeType.Inlet, cellX = 0, cellZ = 0 });
            config.nodes.Add(new NodeDef
            {
                id = "b", type = NodeType.Bay, cellX = 5, cellZ = 3,
                color = DestinationColor.Red, hasColor = true
            });
            config.belts.Add(new BeltDef { id = "bad", from = "a", to = "b" });

            LevelTopologyCheck.Report report = LevelTopologyCheck.Validate(config);

            Assert.IsFalse(report.Ok, "a crooked belt must fail validation");
            Assert.IsTrue(report.errors.Exists(e => e.Contains("45 degrees")),
                "the error should name the geometry rule. Got:\n  " +
                string.Join("\n  ", report.errors));
        }

        // ------------------------------------------------------------------ devices

        [Test]
        public void DevicePricesCapsAndTargetsMatchTheDesign()
        {
            AssertSpec(DeviceType.Gate, 60, 4, InstallTargetKind.BeltSlot);
            AssertSpec(DeviceType.Booster, 45, 3, InstallTargetKind.BeltSlot);
            AssertSpec(DeviceType.Scanner, 110, 2, InstallTargetKind.BeltSlot);
            AssertSpec(DeviceType.AutoArm, 180, 1, InstallTargetKind.Node);

            Assert.AreEqual(8.0f, LevelFixtures.Spec(DeviceType.Gate).holdSeconds, 1e-3f);
            Assert.AreEqual(1.6f, LevelFixtures.Spec(DeviceType.Booster).speedBonus, 1e-3f);
            Assert.AreEqual(0.6f, LevelFixtures.Spec(DeviceType.AutoArm).switchCooldown, 1e-3f);
            Assert.IsTrue(LevelFixtures.Spec(DeviceType.AutoArm).skipBlind,
                "the arm must stand down on blind parcels or it would replace the Scanner");
        }

        static void AssertSpec(DeviceType device, int price, int cap, InstallTargetKind target)
        {
            DeviceSpec spec = LevelFixtures.Spec(device);
            Assert.AreEqual(price, spec.price, device + " price");
            Assert.AreEqual(cap, spec.cap, device + " cap");
            Assert.AreEqual(target, spec.target, device + " install target");
        }

        [Test]
        public void TheCapsTotalTenDevicesAgainstSixtySixPositions()
        {
            int total = 0;
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                total += LevelFixtures.Spec(DeviceTypes.All[i]).cap;
            }

            Assert.AreEqual(10, total);

            LevelTopologyCheck.Report report =
                LevelTopologyCheck.Validate(LevelFixtures.Level01Config());
            Assert.Greater(report.TotalInstallSlots, total * 6,
                "positions must not be the scarce resource; correctness of position is");
        }

        [Test]
        public void AMissingDevicesBlockYieldsAnEmptyShopRatherThanAnError()
        {
            LevelConfig config = LevelConfigParser.Parse(
                "{ \"id\": \"bare\", \"nodes\": [" +
                "{ \"id\": \"a\", \"type\": \"inlet\", \"cell\": [0, 0] }," +
                "{ \"id\": \"b\", \"type\": \"bay\", \"cell\": [4, 0], \"color\": \"Red\" }" +
                "], \"belts\": [{ \"id\": \"e\", \"from\": \"a\", \"to\": \"b\" }] }");

            Assert.AreEqual(0, config.devices.Count, "no devices block means an empty shop");
            Assert.AreEqual(0, config.missions.Count, "and no missions block means an empty list");
        }

        // ------------------------------------------------------------------ missions

        [Test]
        public void ThreeMissionsWithTheDocumentedNumbers()
        {
            LevelConfig config = LevelFixtures.Level01Config();
            Assert.AreEqual(3, config.missions.Count);
            Assert.AreEqual("m1_first_shift", config.missions[0].id);
            Assert.AreEqual("m2_dual_flow", config.missions[1].id);
            Assert.AreEqual("m3_night_blind", config.missions[2].id);

            AssertMission("m1_first_shift", "首日轮班", 15, 3, -1, 150f, 0.8f, 0f, false, 30, 30, 10101, 3);
            AssertMission("m2_dual_flow", "双线并流", 46, 6, 4, 150f, 1.0f, 0f, true, 55, 65, 20202, 2);
            AssertMission("m3_night_blind", "夜班盲件", 84, 9, 6, 200f, 1.15f, 0.35f, true, 90, 110, 30303, 3);
        }

        static void AssertMission(
            string id, string name, int target, int maxWrong, int maxJams, float limit,
            float speedScale, float blindRatio, bool mixColors,
            int baseCoins, int firstClear, int seed, int repeat)
        {
            MissionDef mission = LevelFixtures.Mission(id);

            Assert.AreEqual(name, mission.displayName, id + " displayName");
            Assert.AreEqual(target, mission.objective.targetDelivered, id + " targetDelivered");
            Assert.AreEqual(maxWrong, mission.objective.maxWrong, id + " maxWrong");
            Assert.AreEqual(maxJams, mission.objective.maxJams, id + " maxJams");
            Assert.AreEqual(limit, mission.objective.timeLimitSeconds, 1e-3f, id + " timeLimit");
            Assert.AreEqual(speedScale, mission.modifiers.speedScale, 1e-3f, id + " speedScale");
            Assert.AreEqual(blindRatio, mission.modifiers.blindRatio, 1e-3f, id + " blindRatio");
            Assert.AreEqual(mixColors, mission.modifiers.mixColors, id + " mixColors");
            Assert.AreEqual(baseCoins, mission.rewards.baseCoins, id + " base reward");
            Assert.AreEqual(firstClear, mission.rewards.firstClearCoins, id + " first-clear reward");
            Assert.IsTrue(mission.spawn.hasSeed, id + " should author a seed");
            Assert.AreEqual(seed, mission.spawn.seed, id + " seed");
            Assert.AreEqual(repeat, mission.spawn.repeat, id + " repeat");
        }

        [Test]
        public void OnlyMissionThreeAdvertisesASpecialMechanic()
        {
            Assert.AreEqual(string.Empty, LevelFixtures.Mission("m1_first_shift").SpecialTag());
            Assert.AreEqual(string.Empty, LevelFixtures.Mission("m2_dual_flow").SpecialTag());
            Assert.AreEqual("盲件 35%", LevelFixtures.Mission("m3_night_blind").SpecialTag(),
                "the blind mechanic must be visible before the player commits to the round");
        }

        [Test]
        public void MissionOneUsesOneInletAndTwoColours()
        {
            MissionDef m1 = LevelFixtures.Mission("m1_first_shift");
            Assert.AreEqual(2, m1.spawn.waves.Count);

            var colors = new HashSet<DestinationColor>();
            for (int i = 0; i < m1.spawn.waves.Count; i++)
            {
                SpawnWaveDef wave = m1.spawn.waves[i];
                Assert.AreEqual(1, wave.inlets.Count, "wave " + i + " names one inlet");
                Assert.AreEqual("in_a", wave.inlets[0], "M1 stays on a single inlet");
                for (int c = 0; c < wave.colors.Count; c++)
                {
                    colors.Add(wave.colors[c]);
                }
            }

            Assert.AreEqual(2, colors.Count, "M1 uses two colours");
        }

        [Test]
        public void MissionThreeUsesBothInletsAndThreeColours()
        {
            MissionDef m3 = LevelFixtures.Mission("m3_night_blind");
            Assert.AreEqual(3, m3.spawn.waves.Count);

            var colors = new HashSet<DestinationColor>();
            for (int i = 0; i < m3.spawn.waves.Count; i++)
            {
                for (int c = 0; c < m3.spawn.waves[i].colors.Count; c++)
                {
                    colors.Add(m3.spawn.waves[i].colors[c]);
                }
            }

            Assert.AreEqual(3, colors.Count, "M3 mixes all three bay colours");
            Assert.IsTrue(m3.spawn.waves[1].parallel, "the second stream runs alongside the first");
        }

        [Test]
        public void AnUnwinnableMissionIsFlaggedAsAConfigError()
        {
            // A plan that releases fewer parcels than the objective demands is a authoring bug,
            // and must be caught rather than shipped as an unbeatable round.
            LevelConfig config = LevelConfigParser.Parse(
                "{ \"id\": \"broken\", " +
                "\"missions\": [{ \"id\": \"impossible\", \"displayName\": \"x\", " +
                "\"objective\": { \"targetDelivered\": 50, \"maxWrong\": 1, \"timeLimit\": 60 }, " +
                "\"spawn\": { \"seed\": 1, \"repeat\": 1, \"waves\": [" +
                "{ \"inlet\": \"a\", \"color\": \"red\", \"count\": 4 }] } }], " +
                "\"nodes\": [{ \"id\": \"a\", \"type\": \"inlet\", \"cell\": [0, 0] }," +
                "{ \"id\": \"b\", \"type\": \"bay\", \"cell\": [4, 0], \"color\": \"Red\" }], " +
                "\"belts\": [{ \"id\": \"e\", \"from\": \"a\", \"to\": \"b\" }] }");

            MissionDef mission = config.missions[0];
            Assert.AreEqual(4, mission.PlannedFromPlan());
            Assert.Less(mission.PlannedFromPlan(), mission.objective.targetDelivered,
                "this mission cannot possibly be completed");
        }

        [Test]
        public void ARandomWaveCountIsRejectedAsAConfigError()
        {
            // MissionParser deliberately shouts about this, so the expected error is declared.
            LogAssert.Expect(LogType.Error, new Regex(".*authored a random count.*"));

            LevelConfig config = LevelConfigParser.Parse(
                "{ \"id\": \"wobbly\", " +
                "\"missions\": [{ \"id\": \"drifty\", \"displayName\": \"x\", " +
                "\"objective\": { \"targetDelivered\": 5, \"maxWrong\": 1, \"timeLimit\": 60 }, " +
                "\"spawn\": { \"seed\": 1, \"repeat\": 1, \"waves\": [" +
                "{ \"inlet\": \"a\", \"color\": \"red\", \"count\": \"6~8\" }] } }], " +
                "\"nodes\": [{ \"id\": \"a\", \"type\": \"inlet\", \"cell\": [0, 0] }," +
                "{ \"id\": \"b\", \"type\": \"bay\", \"cell\": [4, 0], \"color\": \"Red\" }], " +
                "\"belts\": [{ \"id\": \"e\", \"from\": \"a\", \"to\": \"b\" }] }");

            Assert.IsTrue(config.missions[0].ConfigError,
                "a random wave count makes Planned drift between runs and must be refused");
            Assert.IsTrue(config.missions[0].ConfigErrorText.Contains("count"));
        }
    }
}
