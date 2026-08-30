using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Reads the level file's "missions" array. Every number the mission plays by lives in
    /// JSON; nothing here carries a default that duplicates an authored value.
    /// </summary>
    public static class MissionParser
    {
        public static void Read(JsonValue value, List<MissionDef> missions)
        {
            if (missions == null)
            {
                return;
            }

            // A level with no missions is legal: prep still opens, with an empty mission list.
            if (value.Kind != JsonKind.Array)
            {
                return;
            }

            for (int i = 0; i < value.Count; i++)
            {
                MissionDef mission = ReadOne(value[i], i);
                if (mission != null)
                {
                    missions.Add(mission);
                }
            }
        }

        static MissionDef ReadOne(JsonValue item, int index)
        {
            if (item.Kind != JsonKind.Object)
            {
                Debug.LogError("Mission " + index + " is not an object; skipped.");
                return null;
            }

            var mission = new MissionDef
            {
                id = item["id"].AsString(),
                displayName = item["displayName"].AsString(),
                iconKey = item.Has("icon") ? item["icon"].AsString() : item["iconKey"].AsString()
            };

            if (string.IsNullOrEmpty(mission.id))
            {
                Debug.LogError("Mission " + index + " has no id; skipped.");
                return null;
            }

            if (string.IsNullOrEmpty(mission.displayName))
            {
                mission.displayName = mission.id;
            }

            ReadObjective(item["objective"], mission.objective);
            ReadModifiers(item["modifiers"], mission.modifiers);
            ReadRewards(item["rewards"], mission.rewards);
            SpawnPlanParser.Read(item["spawn"], mission.spawn);
            ValidateConstantCounts(mission);
            return mission;
        }

        static void ReadObjective(JsonValue value, MissionObjective objective)
        {
            if (!value.Exists)
            {
                return;
            }

            objective.targetDelivered = Mathf.Max(0, value["targetDelivered"].AsInt());
            objective.maxWrong = Mathf.Max(0, value["maxWrong"].AsInt());

            // "maxJams" is intentionally not read. Jams never lose a round any more, so a level
            // that still authors the key is loaded unchanged and the key is simply ignored.

            JsonValue limit = value.Has("timeLimit") ? value["timeLimit"] : value["timeLimitSeconds"];
            objective.timeLimitSeconds = Mathf.Max(0f, limit.AsFloat());
        }

        static void ReadModifiers(JsonValue value, MissionModifiers modifiers)
        {
            if (!value.Exists)
            {
                return;
            }

            modifiers.speedScale = Mathf.Max(0.05f, value["speedScale"].AsFloat(modifiers.speedScale));
            modifiers.blindRatio = Mathf.Clamp01(value["blindRatio"].AsFloat(modifiers.blindRatio));
            modifiers.mixColors = value["mixColors"].AsBool(modifiers.mixColors);
        }

        static void ReadRewards(JsonValue value, MissionRewards rewards)
        {
            if (!value.Exists)
            {
                return;
            }

            JsonValue baseCoins = value.Has("base") ? value["base"] : value["baseCoins"];
            JsonValue firstClear = value.Has("firstClear") ? value["firstClear"] : value["firstClearCoins"];
            rewards.baseCoins = Mathf.Max(0, baseCoins.AsInt());
            rewards.firstClearCoins = Mathf.Max(0, firstClear.AsInt());
        }

        /// <summary>
        /// Wave counts must be constants. If a count were a range, the mission's total parcel
        /// output would drift between runs while the objective stays an absolute number, so an
        /// unlucky roll could make the round mathematically unwinnable. Randomness is confined
        /// to spacing and delayAfter on purpose: the rhythm varies, the yield does not.
        /// </summary>
        static void ValidateConstantCounts(MissionDef mission)
        {
            for (int i = 0; i < mission.spawn.waves.Count; i++)
            {
                SpawnWaveDef wave = mission.spawn.waves[i];
                if (!Mathf.Approximately(wave.count.min, wave.count.max))
                {
                    mission.MarkConfigError("wave " + i + " uses a random count (" +
                                            wave.count.min + "~" + wave.count.max +
                                            "); counts must be constants so Planned is fixed");
                    Debug.LogError("Mission '" + mission.id + "' wave " + i +
                                   " authored a random count. Planned must be deterministic; " +
                                   "use a constant integer and vary spacing/delayAfter instead.");
                }
            }
        }
    }
}
