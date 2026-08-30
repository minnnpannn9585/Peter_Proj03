using System.Collections.Generic;

namespace ParcelSort
{
    /// <summary>
    /// Where one installed device sits. Bound to a level id, so a device placed on level_01
    /// does not reappear in the middle of level_02.
    /// </summary>
    public class InstallRecord
    {
        public string levelId = string.Empty;
        public DeviceType device = DeviceType.Gate;
        public InstallTargetKind target = InstallTargetKind.BeltSlot;

        /// <summary>Set when <see cref="target"/> is <see cref="InstallTargetKind.BeltSlot"/>.</summary>
        public string beltId = string.Empty;

        /// <summary>Set when <see cref="target"/> is <see cref="InstallTargetKind.Node"/>.</summary>
        public string nodeId = string.Empty;

        public int slotIndex;

        public InstallRecord Clone()
        {
            return new InstallRecord
            {
                levelId = levelId,
                device = device,
                target = target,
                beltId = beltId,
                nodeId = nodeId,
                slotIndex = slotIndex
            };
        }

        public override string ToString()
        {
            string where = target == InstallTargetKind.Node ? nodeId : beltId + "#" + slotIndex;
            return levelId + "/" + DeviceTypes.JsonKey(device) + "@" + where;
        }
    }

    /// <summary>
    /// The whole save file in memory: coins, device ownership, where those devices are bolted
    /// down, mission progress and which levels are unlocked.
    /// </summary>
    public class PlayerProfile
    {
        public int schemaVersion = ProfileStore.SchemaVersion;
        public int coins;

        public readonly Dictionary<DeviceType, int> owned = new Dictionary<DeviceType, int>();
        public readonly List<InstallRecord> installs = new List<InstallRecord>();
        public readonly Dictionary<string, MissionRecord> missions =
            new Dictionary<string, MissionRecord>();
        public readonly HashSet<string> unlockedLevels = new HashSet<string>();

        public int Owned(DeviceType device)
        {
            return owned.TryGetValue(device, out int value) ? value : 0;
        }

        public void SetOwned(DeviceType device, int count)
        {
            owned[device] = count < 0 ? 0 : count;
        }

        /// <summary>Returns the record for a mission, creating an empty one on first sight.</summary>
        public MissionRecord RecordFor(string missionId)
        {
            if (string.IsNullOrEmpty(missionId))
            {
                return new MissionRecord();
            }

            if (!missions.TryGetValue(missionId, out MissionRecord record))
            {
                record = new MissionRecord();
                missions.Add(missionId, record);
            }

            return record;
        }

        /// <summary>Read-only peek that does not create a record as a side effect.</summary>
        public MissionRecord PeekRecord(string missionId)
        {
            if (string.IsNullOrEmpty(missionId))
            {
                return null;
            }

            return missions.TryGetValue(missionId, out MissionRecord record) ? record : null;
        }

        public bool IsCleared(string missionId)
        {
            MissionRecord record = PeekRecord(missionId);
            return record != null && record.Cleared;
        }

        public bool IsUnlocked(string levelId)
        {
            return !string.IsNullOrEmpty(levelId) && unlockedLevels.Contains(levelId);
        }

        public void Unlock(string levelId)
        {
            if (!string.IsNullOrEmpty(levelId))
            {
                unlockedLevels.Add(levelId);
            }
        }

        /// <summary>Drops every install belonging to one level, used when the yard is rebuilt.</summary>
        public void ClearInstallsFor(string levelId)
        {
            for (int i = installs.Count - 1; i >= 0; i--)
            {
                if (string.Equals(installs[i].levelId, levelId))
                {
                    installs.RemoveAt(i);
                }
            }
        }

        public PlayerProfile Clone()
        {
            var copy = new PlayerProfile
            {
                schemaVersion = schemaVersion,
                coins = coins
            };

            foreach (KeyValuePair<DeviceType, int> pair in owned)
            {
                copy.owned[pair.Key] = pair.Value;
            }

            for (int i = 0; i < installs.Count; i++)
            {
                copy.installs.Add(installs[i].Clone());
            }

            foreach (KeyValuePair<string, MissionRecord> pair in missions)
            {
                copy.missions[pair.Key] = pair.Value.Clone();
            }

            copy.unlockedLevels.UnionWith(unlockedLevels);
            return copy;
        }
    }
}
