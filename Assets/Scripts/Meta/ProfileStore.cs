using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Reads and writes the single save file. Every failure path here degrades to "a usable
    /// profile and a log line" rather than an exception, because a corrupt save must never be
    /// able to stop the game from starting.
    /// </summary>
    public class ProfileStore
    {
        public const int SchemaVersion = 1;

        /// <summary>Coins a brand new player starts with.</summary>
        public const int DefaultCoins = 20;

        /// <summary>The level that is playable before anything has been cleared.</summary>
        public const string FirstLevelId = "level_01";

        readonly string filePath;

        public ProfileStore(string filePath)
        {
            this.filePath = filePath;
        }

        /// <summary>Default location: the platform's per-user persistent data folder.</summary>
        public static string DefaultPath =>
            Path.Combine(Application.persistentDataPath, "profile.json");

        public static ProfileStore Default() => new ProfileStore(DefaultPath);

        public string FilePath => filePath;

        string TempPath => filePath + ".tmp";

        string BackupPath => filePath + ".bak";

        /// <summary>A fresh save: 20 coins, no devices, no clears, only level_01 unlocked.</summary>
        public static PlayerProfile CreateDefault()
        {
            var profile = new PlayerProfile
            {
                schemaVersion = SchemaVersion,
                coins = DefaultCoins
            };

            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                profile.SetOwned(DeviceTypes.All[i], 0);
            }

            profile.Unlock(FirstLevelId);
            return profile;
        }

        /// <summary>
        /// Loads the save, falling back to a default profile on every kind of trouble:
        /// missing file, unparseable text, or a schema version this build does not understand.
        /// Never throws.
        /// </summary>
        public PlayerProfile Load()
        {
            string json;
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return CreateDefault();
                }

                json = File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ProfileStore could not read '" + filePath + "': " + ex.Message +
                                 ". Falling back to a default profile.");
                return CreateDefault();
            }

            PlayerProfile parsed;
            try
            {
                parsed = Deserialize(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ProfileStore could not parse '" + filePath + "': " + ex.Message +
                                 ". Falling back to a default profile.");
                return CreateDefault();
            }

            if (parsed == null)
            {
                Debug.LogWarning("ProfileStore read an empty profile from '" + filePath +
                                 "'. Falling back to a default profile.");
                return CreateDefault();
            }

            if (parsed.schemaVersion != SchemaVersion)
            {
                Debug.LogWarning("Profile schema " + parsed.schemaVersion + " does not match " +
                                 SchemaVersion + "; the old file was kept as '" + BackupPath + "'.");
                TryBackup();
                return CreateDefault();
            }

            return parsed;
        }

        void TryBackup()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, BackupPath, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ProfileStore could not back up '" + filePath + "': " + ex.Message);
            }
        }

        /// <summary>
        /// Writes the save atomically: full contents to a .tmp file, flushed, then renamed over
        /// the real file. At no point does a half written profile exist on disk. An IO failure
        /// is logged and swallowed so the round in progress stays playable.
        /// </summary>
        public void Save(PlayerProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            string json = Serialize(profile);
            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(filePath))
                {
                    // Replace is the atomic swap; the destination is only ever the old or new file.
                    File.Replace(TempPath, filePath, null);
                }
                else
                {
                    File.Move(TempPath, filePath);
                }
            }
            catch (IOException ex)
            {
                Debug.LogError("ProfileStore could not write '" + filePath + "': " + ex.Message +
                               ". Progress for this round was not saved.");
                TryDeleteTemp();
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogError("ProfileStore was denied access to '" + filePath + "': " + ex.Message +
                               ". Progress for this round was not saved.");
                TryDeleteTemp();
            }
        }

        void TryDeleteTemp()
        {
            try
            {
                if (File.Exists(TempPath))
                {
                    File.Delete(TempPath);
                }
            }
            catch (Exception)
            {
                // Nothing useful to do; a stale .tmp is harmless because Save always recreates it.
            }
        }

        public string Serialize(PlayerProfile profile)
        {
            if (profile == null)
            {
                profile = CreateDefault();
            }

            JsonObjectBuilder ownedNode = JsonWriter.Object();
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                DeviceType device = DeviceTypes.All[i];
                ownedNode.Add(DeviceTypes.JsonKey(device), profile.Owned(device));
            }

            JsonArrayBuilder installsNode = JsonWriter.Array();
            for (int i = 0; i < profile.installs.Count; i++)
            {
                InstallRecord record = profile.installs[i];
                JsonObjectBuilder item = JsonWriter.Object()
                    .Add("level", record.levelId ?? string.Empty)
                    .Add("device", DeviceTypes.JsonKey(record.device))
                    .Add("target", InstallTargets.JsonKey(record.target));

                if (record.target == InstallTargetKind.Node)
                {
                    item.Add("nodeId", record.nodeId ?? string.Empty);
                }
                else
                {
                    item.Add("beltId", record.beltId ?? string.Empty);
                }

                item.Add("slot", record.slotIndex);
                installsNode.Add(item);
            }

            // Sorted so the file is byte stable regardless of dictionary iteration order.
            var missionIds = new List<string>(profile.missions.Keys);
            missionIds.Sort(StringComparer.Ordinal);
            JsonObjectBuilder missionsNode = JsonWriter.Object();
            for (int i = 0; i < missionIds.Count; i++)
            {
                MissionRecord record = profile.missions[missionIds[i]];
                missionsNode.Add(missionIds[i], JsonWriter.Object()
                    .Add("cleared", record.Cleared)
                    .Add("firstClearPaid", record.FirstClearPaid)
                    .Add("bestDelivered", record.BestDelivered)
                    .Add("attempts", record.Attempts)
                    .Add("bestSeconds", record.BestSeconds));
            }

            var levels = new List<string>(profile.unlockedLevels);
            levels.Sort(StringComparer.Ordinal);
            JsonArrayBuilder levelsNode = JsonWriter.Array();
            for (int i = 0; i < levels.Count; i++)
            {
                levelsNode.Add(levels[i]);
            }

            return JsonWriter.Object()
                .Add("schemaVersion", profile.schemaVersion)
                .Add("coins", profile.coins)
                .Add("owned", ownedNode)
                .Add("installs", installsNode)
                .Add("missions", missionsNode)
                .Add("unlockedLevels", levelsNode)
                .ToJson();
        }

        /// <summary>
        /// Rebuilds a profile from text. Individual fields fall back to their defaults when they
        /// are missing or the wrong type, so one bad value never discards the rest of the save.
        /// </summary>
        public PlayerProfile Deserialize(string json)
        {
            JsonValue root = MiniJson.Parse(json);
            if (root.Kind != JsonKind.Object)
            {
                throw new InvalidOperationException("Profile JSON root must be an object.");
            }

            var profile = new PlayerProfile
            {
                schemaVersion = root["schemaVersion"].AsInt(SchemaVersion),
                coins = Math.Max(0, root["coins"].AsInt(DefaultCoins))
            };

            JsonValue ownedNode = root["owned"];
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                DeviceType device = DeviceTypes.All[i];
                profile.SetOwned(device, ownedNode[DeviceTypes.JsonKey(device)].AsInt());
            }

            JsonValue installsNode = root["installs"];
            if (installsNode.Kind == JsonKind.Array)
            {
                for (int i = 0; i < installsNode.Count; i++)
                {
                    JsonValue item = installsNode[i];
                    if (!DeviceTypes.TryParse(item["device"].AsString(), out DeviceType device))
                    {
                        Debug.LogWarning("Profile install " + i + " names unknown device '" +
                                         item["device"].AsString() + "'; skipped.");
                        continue;
                    }

                    if (!InstallTargets.TryParse(item["target"].AsString(), out InstallTargetKind target))
                    {
                        target = InstallTargetKind.BeltSlot;
                    }

                    profile.installs.Add(new InstallRecord
                    {
                        levelId = item["level"].AsString(),
                        device = device,
                        target = target,
                        beltId = item["beltId"].AsString(),
                        nodeId = item["nodeId"].AsString(),
                        slotIndex = Math.Max(0, item["slot"].AsInt())
                    });
                }
            }

            JsonValue missionsNode = root["missions"];
            if (missionsNode.Kind == JsonKind.Object)
            {
                foreach (KeyValuePair<string, JsonValue> pair in missionsNode.Members)
                {
                    JsonValue item = pair.Value;
                    profile.missions[pair.Key] = MissionRecord.Restore(
                        item["cleared"].AsBool(),
                        item["firstClearPaid"].AsBool(),
                        item["bestDelivered"].AsInt(),
                        item["attempts"].AsInt(),
                        item["bestSeconds"].AsFloat());
                }
            }

            JsonValue levelsNode = root["unlockedLevels"];
            if (levelsNode.Kind == JsonKind.Array)
            {
                for (int i = 0; i < levelsNode.Count; i++)
                {
                    string levelId = levelsNode[i].AsString();
                    if (!string.IsNullOrEmpty(levelId))
                    {
                        profile.Unlock(levelId);
                    }
                }
            }

            // A save that unlocks nothing would soft-lock the level select.
            if (profile.unlockedLevels.Count == 0)
            {
                profile.Unlock(FirstLevelId);
            }

            return profile;
        }
    }
}
