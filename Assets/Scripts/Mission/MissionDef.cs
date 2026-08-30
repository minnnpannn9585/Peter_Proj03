using UnityEngine;

namespace ParcelSort
{
    /// <summary>What the player has to achieve, and what ends the round early.</summary>
    public class MissionObjective
    {
        /// <summary>Correctly delivered parcels needed to win. An absolute count, not a ratio.</summary>
        public int targetDelivered;

        /// <summary>Misrouted parcels tolerated. Exceeding this loses immediately.</summary>
        public int maxWrong;

        /// <summary>Jams tolerated. -1 means jams never lose the round.</summary>
        public int maxJams = -1;

        public float timeLimitSeconds;
    }

    /// <summary>Per-mission twists applied on top of the level's own rules.</summary>
    public class MissionModifiers
    {
        /// <summary>Multiplied into <see cref="RuleSettings.baseSpeed"/> for the round.</summary>
        public float speedScale = 1f;

        /// <summary>Share of parcels that spawn unrevealed, 0..1.</summary>
        public float blindRatio;

        /// <summary>True picks a colour per parcel from the wave's list instead of per wave.</summary>
        public bool mixColors;
    }

    public class MissionRewards
    {
        public int baseCoins;
        public int firstClearCoins;
    }

    /// <summary>
    /// One playable round: a spawn plan, a win/lose contract, a set of twists and a payout.
    /// Missions never change the yard's topology, only what flows through it.
    /// </summary>
    public class MissionDef
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string iconKey = string.Empty;

        public MissionObjective objective = new MissionObjective();
        public MissionModifiers modifiers = new MissionModifiers();
        public MissionRewards rewards = new MissionRewards();
        public SpawnPlan spawn = new SpawnPlan();

        /// <summary>
        /// Set when the authored numbers are self-contradictory, for example when the plan
        /// releases fewer parcels than the objective demands. Such a mission is not selectable.
        /// </summary>
        public bool ConfigError { get; private set; }

        public string ConfigErrorText { get; private set; } = string.Empty;

        public void MarkConfigError(string reason)
        {
            ConfigError = true;
            ConfigErrorText = reason ?? string.Empty;
        }

        public void ClearConfigError()
        {
            ConfigError = false;
            ConfigErrorText = string.Empty;
        }

        /// <summary>
        /// Parcels this mission will release, summed straight off the authored plan. Wave counts
        /// must be constants for this to be meaningful, which <see cref="MissionParser"/> enforces.
        /// </summary>
        public int PlannedFromPlan()
        {
            if (spawn == null)
            {
                return 0;
            }

            int perPass = 0;
            for (int i = 0; i < spawn.waves.Count; i++)
            {
                perPass += Mathf.Max(0, Mathf.RoundToInt(spawn.waves[i].count.min));
            }

            return perPass * Mathf.Max(1, spawn.repeat);
        }

        /// <summary>"盲件 35%" style label, or empty when the mission has no special mechanic.</summary>
        public string SpecialTag()
        {
            if (modifiers == null || modifiers.blindRatio <= 0f)
            {
                return string.Empty;
            }

            return "盲件 " + Mathf.RoundToInt(modifiers.blindRatio * 100f) + "%";
        }
    }
}
