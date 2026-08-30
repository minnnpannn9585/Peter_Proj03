using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ParcelSort.Offline
{
    /// <summary>Property 8: profile round-trip, plus every documented save/load failure path.</summary>
    public static class ProfileChecks
    {
        public static void Run(CheckRunner r)
        {
            r.Section("ProfileStore (Property 8)");

            string root = Path.Combine(Path.GetTempPath(),
                "parcelsort-offline-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                PlayerProfile fresh = ProfileStore.CreateDefault();
                r.AreEqual(20, fresh.coins, "a default profile starts with 20 coins");
                r.AreEqual(0, fresh.Owned(DeviceType.Gate), "a default profile owns no devices");
                r.AreEqual(0, fresh.missions.Count, "a default profile has no mission records");
                r.Check(fresh.IsUnlocked("level_01"), "a default profile unlocks level_01");
                r.AreEqual(1, fresh.unlockedLevels.Count, "a default profile unlocks nothing else");

                // Missing file
                var missing = new ProfileStore(Path.Combine(root, "nope.json"));
                UnityEngine.Debug.Clear();
                PlayerProfile loaded = missing.Load();
                r.AreEqual(20, loaded.coins, "a missing file loads the default profile");

                // Truncated / illegal JSON
                string brokenPath = Path.Combine(root, "broken.json");
                File.WriteAllText(brokenPath, "{ \"coins\": 40, \"owned\": { ");
                var broken = new ProfileStore(brokenPath);
                UnityEngine.Debug.Clear();
                PlayerProfile brokenLoaded = broken.Load();
                r.AreEqual(20, brokenLoaded.coins, "unparseable JSON loads the default profile");
                r.Check(UnityEngine.Debug.Warnings.Count > 0, "unparseable JSON logs a warning");

                // Schema mismatch keeps a .bak
                string oldPath = Path.Combine(root, "old.json");
                File.WriteAllText(oldPath, "{ \"schemaVersion\": 99, \"coins\": 500 }");
                var old = new ProfileStore(oldPath);
                UnityEngine.Debug.Clear();
                PlayerProfile oldLoaded = old.Load();
                r.AreEqual(20, oldLoaded.coins, "a future schema loads the default profile");
                r.Check(File.Exists(oldPath + ".bak"), "a future schema is preserved as .bak");

                // Wrong field type degrades that field only
                string mixedPath = Path.Combine(root, "mixed.json");
                File.WriteAllText(mixedPath,
                    "{ \"schemaVersion\": 1, \"coins\": \"lots\", \"owned\": { \"gate\": 3 }, " +
                    "\"unlockedLevels\": [\"level_01\", \"level_02\"] }");
                PlayerProfile mixed = new ProfileStore(mixedPath).Load();
                r.AreEqual(20, mixed.coins, "a string coins field falls back to the default");
                r.AreEqual(3, mixed.Owned(DeviceType.Gate), "the other fields still load");
                r.Check(mixed.IsUnlocked("level_02"), "unlocked levels still load");

                // Atomic save
                string savePath = Path.Combine(root, "profile.json");
                var store = new ProfileStore(savePath);
                PlayerProfile rich = BuildRichProfile();
                store.Save(rich);
                r.Check(File.Exists(savePath), "Save writes the profile");
                r.Check(!File.Exists(savePath + ".tmp"), "Save leaves no .tmp behind");
                store.Save(rich);
                r.Check(File.Exists(savePath) && !File.Exists(savePath + ".tmp"),
                    "a second Save also replaces cleanly");

                PlayerProfile reloaded = store.Load();
                r.Check(Same(rich, reloaded, out string why), "a saved profile reloads field for field: " + why);

                // Property 8 over random profiles.
                const int iterations = 200;
                bool roundTripped = true;
                int badSeed = -1;
                for (int seed = 0; seed < iterations; seed++)
                {
                    PlayerProfile p = RandomProfile(new Random(seed + 31337));
                    PlayerProfile back = store.Deserialize(store.Serialize(p));
                    if (!Same(p, back, out string reason))
                    {
                        roundTripped = false;
                        badSeed = seed;
                        r.Info("seed " + seed + ": " + reason);
                    }
                }

                r.Check(roundTripped, "Deserialize(Serialize(p)) == p over " + iterations +
                                      " random profiles (seed " + badSeed + ")");

                // Corrupt byte strings never throw and never return null.
                bool survivedCorruption = true;
                for (int seed = 0; seed < 200; seed++)
                {
                    var rng = new Random(seed + 5150);
                    string good = store.Serialize(BuildRichProfile());
                    var sb = new StringBuilder(good);
                    int cuts = rng.Next(1, 6);
                    for (int c = 0; c < cuts && sb.Length > 1; c++)
                    {
                        sb[rng.Next(sb.Length)] = (char)rng.Next(32, 127);
                    }

                    if (rng.Next(2) == 0 && sb.Length > 8)
                    {
                        sb.Length = rng.Next(1, sb.Length);
                    }

                    string corruptPath = Path.Combine(root, "corrupt.json");
                    File.WriteAllText(corruptPath, sb.ToString());
                    try
                    {
                        PlayerProfile result = new ProfileStore(corruptPath).Load();
                        if (result == null)
                        {
                            survivedCorruption = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        survivedCorruption = false;
                        r.Info("corrupt seed " + seed + " threw " + ex.GetType().Name);
                    }
                }

                r.Check(survivedCorruption,
                    "200 corrupted files all load a usable profile without throwing");
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (Exception)
                {
                    // Temp cleanup is best effort.
                }
            }
        }

        static PlayerProfile BuildRichProfile()
        {
            var profile = ProfileStore.CreateDefault();
            profile.coins = 240;
            profile.SetOwned(DeviceType.Gate, 2);
            profile.SetOwned(DeviceType.Booster, 1);
            profile.SetOwned(DeviceType.Scanner, 2);
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

        static PlayerProfile RandomProfile(Random rng)
        {
            var profile = new PlayerProfile
            {
                schemaVersion = ProfileStore.SchemaVersion,
                coins = rng.Next(0, 100000)
            };

            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                profile.SetOwned(DeviceTypes.All[i], rng.Next(0, 6));
            }

            int installCount = rng.Next(0, 6);
            for (int i = 0; i < installCount; i++)
            {
                bool node = rng.Next(2) == 0;
                profile.installs.Add(new InstallRecord
                {
                    levelId = "level_0" + rng.Next(1, 3),
                    device = DeviceTypes.All[rng.Next(DeviceTypes.All.Length)],
                    target = node ? InstallTargetKind.Node : InstallTargetKind.BeltSlot,
                    beltId = node ? string.Empty : "belt_" + rng.Next(0, 30),
                    nodeId = node ? "node_" + rng.Next(0, 30) : string.Empty,
                    slotIndex = node ? 0 : rng.Next(0, 5)
                });
            }

            string[] names = { "m1_first_shift", "夜班盲件", "m\"quoted\"", "tab\there", "" };
            int missionCount = rng.Next(0, 5);
            for (int i = 0; i < missionCount; i++)
            {
                string id = names[rng.Next(names.Length)] + "_" + i;
                profile.missions[id] = MissionRecord.Restore(
                    rng.Next(2) == 0,
                    rng.Next(2) == 0,
                    rng.Next(0, 200),
                    rng.Next(0, 50),
                    (float)Math.Round(rng.NextDouble() * 300.0, 3));
            }

            profile.Unlock("level_01");
            if (rng.Next(2) == 0)
            {
                profile.Unlock("level_02");
            }

            return profile;
        }

        /// <summary>Field-for-field comparison with collections normalised to a stable order.</summary>
        static bool Same(PlayerProfile a, PlayerProfile b, out string reason)
        {
            reason = "ok";
            if (a.schemaVersion != b.schemaVersion)
            {
                reason = "schemaVersion";
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
                    reason = "owned " + device;
                    return false;
                }
            }

            List<string> left = Normalise(a.installs);
            List<string> right = Normalise(b.installs);
            if (left.Count != right.Count)
            {
                reason = "install count " + left.Count + " vs " + right.Count;
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    reason = "install " + left[i] + " vs " + right[i];
                    return false;
                }
            }

            if (a.missions.Count != b.missions.Count)
            {
                reason = "mission count";
                return false;
            }

            foreach (KeyValuePair<string, MissionRecord> pair in a.missions)
            {
                if (!b.missions.TryGetValue(pair.Key, out MissionRecord other))
                {
                    reason = "missing mission " + pair.Key;
                    return false;
                }

                MissionRecord mine = pair.Value;
                if (mine.Cleared != other.Cleared ||
                    mine.FirstClearPaid != other.FirstClearPaid ||
                    mine.BestDelivered != other.BestDelivered ||
                    mine.Attempts != other.Attempts ||
                    mine.BestSeconds != other.BestSeconds)
                {
                    reason = "mission fields " + pair.Key;
                    return false;
                }
            }

            if (!a.unlockedLevels.SetEquals(b.unlockedLevels))
            {
                reason = "unlockedLevels";
                return false;
            }

            return true;
        }

        static List<string> Normalise(List<InstallRecord> records)
        {
            var keys = new List<string>();
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
