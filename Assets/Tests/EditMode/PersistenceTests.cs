using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Feature: stage1-hud-device-mission-system,
    /// Property 8: a profile survives a save/load round trip field for field, and every kind of
    /// damaged input yields a usable profile instead of an exception.
    /// </summary>
    [TestFixture]
    public class PersistenceTests
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "parcelsort-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
            catch (IOException)
            {
                // Temp cleanup is best effort.
            }
        }

        ProfileStore Store(string fileName = "profile.json")
        {
            return new ProfileStore(Path.Combine(root, fileName));
        }

        // ------------------------------------------------------------------ JsonWriter

        [Test]
        public void EscapingCoversQuotesBackslashesControlCharsAndNonAscii()
        {
            Assert.AreEqual("\"a\\\"b\"", JsonWriter.Escape("a\"b"));
            Assert.AreEqual("\"a\\\\b\"", JsonWriter.Escape("a\\b"));
            Assert.AreEqual("\"a\\nb\"", JsonWriter.Escape("a\nb"));
            Assert.AreEqual("\"a\\rb\"", JsonWriter.Escape("a\rb"));
            Assert.AreEqual("\"a\\tb\"", JsonWriter.Escape("a\tb"));
            Assert.AreEqual("\"\\u591c\\u73ed\"", JsonWriter.Escape("夜班"));
        }

        [Test]
        public void NestedStructuresAndFloatsRoundTripThroughMiniJson()
        {
            string json = JsonWriter.Object()
                .Add("name", "夜班盲件 \"M3\"\n\t")
                .Add("ratio", 0.35f)
                .Add("count", 108)
                .Add("on", true)
                .Add("nested", JsonWriter.Object()
                    .Add("deep", JsonWriter.Array().Add(1).Add(2).Add(3)))
                .Add("emptyArray", JsonWriter.Array())
                .Add("emptyObject", JsonWriter.Object())
                .ToJson();

            JsonValue parsed = MiniJson.Parse(json);
            Assert.AreEqual("夜班盲件 \"M3\"\n\t", parsed["name"].AsString());
            Assert.AreEqual(0.35f, parsed["ratio"].AsFloat());
            Assert.AreEqual(108, parsed["count"].AsInt());
            Assert.IsTrue(parsed["on"].AsBool());
            Assert.AreEqual(3, parsed["nested"]["deep"].Count);
            Assert.AreEqual(2, parsed["nested"]["deep"][1].AsInt());
            Assert.AreEqual(JsonKind.Array, parsed["emptyArray"].Kind);
            Assert.AreEqual(JsonKind.Object, parsed["emptyObject"].Kind);
        }

        [Test]
        public void EveryFloatSampleRoundTripsExactly()
        {
            float[] samples = { 0f, 1f, -1f, 0.1f, 0.35f, 0.8f, 1.15f, 1.6f, 2.8644f, 150f, 1e-5f, 81.6f };
            for (int i = 0; i < samples.Length; i++)
            {
                string json = JsonWriter.Object().Add("v", samples[i]).ToJson(false);
                Assert.AreEqual(samples[i], MiniJson.Parse(json)["v"].AsFloat(),
                    "float " + samples[i] + " did not survive the round trip");
            }
        }

        // ------------------------------------------------------------------ ProfileStore

        [Test]
        public void ADefaultProfileHasTwentyCoinsNoDevicesAndOnlyLevelOneUnlocked()
        {
            PlayerProfile profile = ProfileStore.CreateDefault();

            Assert.AreEqual(20, profile.coins);
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                Assert.AreEqual(0, profile.Owned(DeviceTypes.All[i]));
            }

            Assert.AreEqual(0, profile.missions.Count);
            Assert.AreEqual(1, profile.unlockedLevels.Count);
            Assert.IsTrue(profile.IsUnlocked("level_01"));
        }

        [Test]
        public void AMissingFileLoadsTheDefaultProfileWithoutThrowing()
        {
            PlayerProfile profile = Store("does-not-exist.json").Load();
            Assert.AreEqual(20, profile.coins);
        }

        [Test]
        public void UnparseableJsonLoadsTheDefaultProfileWithoutThrowing()
        {
            string path = Path.Combine(root, "broken.json");
            File.WriteAllText(path, "{ \"coins\": 40, \"owned\": { ");

            // ProfileStore logs a warning here, not an error, so no LogAssert scaffolding is needed.
            PlayerProfile profile = new ProfileStore(path).Load();

            Assert.AreEqual(20, profile.coins);
        }

        [Test]
        public void AnUnknownSchemaIsBackedUpAndFallsBackToDefault()
        {
            string path = Path.Combine(root, "old.json");
            File.WriteAllText(path, "{ \"schemaVersion\": 99, \"coins\": 500 }");

            PlayerProfile profile = new ProfileStore(path).Load();

            Assert.AreEqual(20, profile.coins, "an unknown schema is not trusted");
            Assert.IsTrue(File.Exists(path + ".bak"), "but the old file is kept for diagnosis");
        }

        [Test]
        public void AFieldOfTheWrongTypeDegradesOnlyThatField()
        {
            string path = Path.Combine(root, "mixed.json");
            File.WriteAllText(path,
                "{ \"schemaVersion\": 1, \"coins\": \"lots\", \"owned\": { \"gate\": 3 }, " +
                "\"unlockedLevels\": [\"level_01\", \"level_02\"] }");

            PlayerProfile profile = new ProfileStore(path).Load();

            Assert.AreEqual(20, profile.coins, "a string coins field takes the default");
            Assert.AreEqual(3, profile.Owned(DeviceType.Gate), "the rest of the save still loads");
            Assert.IsTrue(profile.IsUnlocked("level_02"));
        }

        [Test]
        public void SaveIsAtomicAndLeavesNoTempFile()
        {
            ProfileStore store = Store();
            PlayerProfile profile = RichProfile();

            store.Save(profile);
            Assert.IsTrue(File.Exists(store.FilePath));
            Assert.IsFalse(File.Exists(store.FilePath + ".tmp"), "no half written profile is left");

            store.Save(profile);
            Assert.IsTrue(File.Exists(store.FilePath), "a second save replaces cleanly");
            Assert.IsFalse(File.Exists(store.FilePath + ".tmp"));

            PlayerProfile reloaded = store.Load();
            Assert.IsTrue(Same(profile, reloaded, out string reason), reason);
        }

        [Test]
        public void Property08_ProfilesRoundTripFieldForField()
        {
            ProfileStore store = Store();

            Property.ForAll("Property 8: Deserialize(Serialize(p)) == p", (seed, trace) =>
            {
                PlayerProfile original = RandomProfile(trace);
                PlayerProfile restored = store.Deserialize(store.Serialize(original));
                trace.Require(Same(original, restored, out string reason), reason);
            });
        }

        [Test]
        public void Property08_CorruptedFilesNeverThrowAndAlwaysYieldAUsableProfile()
        {
            ProfileStore writer = Store();
            string path = Path.Combine(root, "corrupt.json");

            Property.ForAll("Property 8: corrupt input degrades gracefully", (seed, trace) =>
            {
                var text = new StringBuilder(writer.Serialize(RandomProfile(trace)));

                int mutations = trace.Rng.Next(1, 8);
                for (int i = 0; i < mutations && text.Length > 1; i++)
                {
                    int at = trace.Rng.Next(text.Length);
                    text[at] = (char)trace.Rng.Next(32, 127);
                    trace.Log("mutate char " + at);
                }

                if (trace.Rng.Next(2) == 0 && text.Length > 8)
                {
                    int cut = trace.Rng.Next(1, text.Length);
                    text.Length = cut;
                    trace.Log("truncate to " + cut);
                }

                File.WriteAllText(path, text.ToString());

                PlayerProfile loaded;
                try
                {
                    loaded = new ProfileStore(path).Load();
                }
                catch (Exception ex)
                {
                    trace.Fail("Load threw " + ex.GetType().Name + ": " + ex.Message);
                    return;
                }

                trace.Require(loaded != null, "Load returned null");
                trace.Require(loaded.coins >= 0, "Load produced a negative balance");
                trace.Require(loaded.unlockedLevels.Count > 0,
                    "Load produced a profile with nothing unlocked, which would soft-lock");
            });
        }

        [Test]
        public void InstallRecordsSurviveARoundTripIncludingNodeTargets()
        {
            ProfileStore store = Store();
            PlayerProfile profile = RichProfile();

            PlayerProfile restored = store.Deserialize(store.Serialize(profile));

            Assert.AreEqual(profile.installs.Count, restored.installs.Count);

            InstallRecord node = restored.installs.Find(r => r.target == InstallTargetKind.Node);
            Assert.IsNotNull(node, "the node-mounted install survived");
            Assert.AreEqual("hub_m", node.nodeId);
            Assert.AreEqual(DeviceType.AutoArm, node.device);

            InstallRecord belt = restored.installs.Find(r => r.target == InstallTargetKind.BeltSlot);
            Assert.IsNotNull(belt, "the belt-mounted install survived");
            Assert.AreEqual("b_hub_a_r1", belt.beltId);
            Assert.AreEqual(1, belt.slotIndex);
        }

        // ------------------------------------------------------------------ helpers

        static PlayerProfile RichProfile()
        {
            PlayerProfile profile = ProfileStore.CreateDefault();
            profile.coins = 240;
            profile.SetOwned(DeviceType.Gate, 2);
            profile.SetOwned(DeviceType.Booster, 1);
            profile.SetOwned(DeviceType.Scanner, 2);
            profile.SetOwned(DeviceType.AutoArm, 1);

            profile.installs.Add(new InstallRecord
            {
                levelId = "level_01",
                device = DeviceType.Gate,
                target = InstallTargetKind.BeltSlot,
                beltId = "b_hub_a_r1",
                slotIndex = 1
            });
            profile.installs.Add(new InstallRecord
            {
                levelId = "level_01",
                device = DeviceType.AutoArm,
                target = InstallTargetKind.Node,
                nodeId = "hub_m"
            });

            profile.missions["m1_first_shift"] = MissionRecord.Restore(true, true, 18, 3, 81.6f);
            profile.missions["m3_night_blind"] = MissionRecord.Restore(false, false, 61, 1, 0f);
            profile.Unlock("level_02");
            return profile;
        }

        static PlayerProfile RandomProfile(Property.Trace trace)
        {
            var profile = new PlayerProfile
            {
                schemaVersion = ProfileStore.SchemaVersion,
                coins = trace.Rng.Next(0, 100000)
            };

            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                profile.SetOwned(DeviceTypes.All[i], trace.Rng.Next(0, 6));
            }

            int installCount = trace.Rng.Next(0, 6);
            for (int i = 0; i < installCount; i++)
            {
                bool node = trace.Rng.Next(2) == 0;
                profile.installs.Add(new InstallRecord
                {
                    levelId = "level_0" + trace.Rng.Next(1, 3),
                    device = DeviceTypes.All[trace.Rng.Next(DeviceTypes.All.Length)],
                    target = node ? InstallTargetKind.Node : InstallTargetKind.BeltSlot,
                    beltId = node ? string.Empty : "belt_" + trace.Rng.Next(0, 30),
                    nodeId = node ? "node_" + trace.Rng.Next(0, 30) : string.Empty,
                    slotIndex = node ? 0 : trace.Rng.Next(0, 5)
                });
            }

            // Deliberately awkward ids: non-ASCII, quotes, tabs, and an empty stem.
            string[] stems = { "m1_first_shift", "夜班盲件", "m\"quoted\"", "tab\there", string.Empty };
            int missionCount = trace.Rng.Next(0, 5);
            for (int i = 0; i < missionCount; i++)
            {
                profile.missions[stems[trace.Rng.Next(stems.Length)] + "_" + i] =
                    MissionRecord.Restore(
                        trace.Rng.Next(2) == 0,
                        trace.Rng.Next(2) == 0,
                        trace.Rng.Next(0, 200),
                        trace.Rng.Next(0, 50),
                        (float)Math.Round(trace.Rng.NextDouble() * 300.0, 3));
            }

            profile.Unlock("level_01");
            if (trace.Rng.Next(2) == 0)
            {
                profile.Unlock("level_02");
            }

            return profile;
        }

        /// <summary>Field-for-field equality with collections normalised to a stable order.</summary>
        static bool Same(PlayerProfile a, PlayerProfile b, out string reason)
        {
            reason = "profiles match";
            if (a.schemaVersion != b.schemaVersion)
            {
                reason = "schemaVersion " + a.schemaVersion + " vs " + b.schemaVersion;
                return false;
            }

            if (a.coins != b.coins)
            {
                reason = "coins " + a.coins + " vs " + b.coins;
                return false;
            }

            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                DeviceType device = DeviceTypes.All[i];
                if (a.Owned(device) != b.Owned(device))
                {
                    reason = "owned " + device + " " + a.Owned(device) + " vs " + b.Owned(device);
                    return false;
                }
            }

            List<string> left = NormaliseInstalls(a.installs);
            List<string> right = NormaliseInstalls(b.installs);
            if (left.Count != right.Count)
            {
                reason = "install count " + left.Count + " vs " + right.Count;
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    reason = "install '" + left[i] + "' vs '" + right[i] + "'";
                    return false;
                }
            }

            if (a.missions.Count != b.missions.Count)
            {
                reason = "mission count " + a.missions.Count + " vs " + b.missions.Count;
                return false;
            }

            foreach (KeyValuePair<string, MissionRecord> pair in a.missions)
            {
                if (!b.missions.TryGetValue(pair.Key, out MissionRecord other))
                {
                    reason = "mission '" + pair.Key + "' is missing after the round trip";
                    return false;
                }

                MissionRecord mine = pair.Value;
                if (mine.Cleared != other.Cleared ||
                    mine.FirstClearPaid != other.FirstClearPaid ||
                    mine.BestDelivered != other.BestDelivered ||
                    mine.Attempts != other.Attempts ||
                    mine.BestSeconds != other.BestSeconds)
                {
                    reason = "mission '" + pair.Key + "' fields differ";
                    return false;
                }
            }

            if (!a.unlockedLevels.SetEquals(b.unlockedLevels))
            {
                reason = "unlockedLevels differ";
                return false;
            }

            return true;
        }

        static List<string> NormaliseInstalls(List<InstallRecord> records)
        {
            var keys = new List<string>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                InstallRecord record = records[i];
                keys.Add(string.Join("|",
                    record.levelId ?? string.Empty,
                    DeviceTypes.JsonKey(record.device),
                    InstallTargets.JsonKey(record.target),
                    record.target == InstallTargetKind.Node
                        ? record.nodeId ?? string.Empty
                        : record.beltId ?? string.Empty,
                    record.slotIndex.ToString()));
            }

            keys.Sort(StringComparer.Ordinal);
            return keys;
        }
    }
}
