using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Shared access to the shipped level file, so numbers asserted by tests come from the same
    /// JSON the game reads rather than from a copy inside the test.
    /// </summary>
    public static class LevelFixtures
    {
        public const string Level01 = "level_01.json";

        static LevelConfig cachedLevel01;

        public static string LevelPath(string fileName)
        {
            return Path.Combine(Application.streamingAssetsPath, "Configs", "Levels", fileName);
        }

        public static LevelConfig Load(string fileName)
        {
            string path = LevelPath(fileName);
            Assert.IsTrue(File.Exists(path), "Level file is missing: " + path);
            return LevelConfigParser.Parse(File.ReadAllText(path));
        }

        public static LevelConfig Level01Config()
        {
            return cachedLevel01 ?? (cachedLevel01 = Load(Level01));
        }

        public static MissionDef Mission(string missionId)
        {
            LevelConfig config = Level01Config();
            Assert.IsTrue(config.TryGetMission(missionId, out MissionDef mission),
                "level_01 does not define mission '" + missionId + "'.");
            return mission;
        }

        public static List<string> InletIds(LevelConfig config)
        {
            var ids = new List<string>();
            for (int i = 0; i < config.nodes.Count; i++)
            {
                if (config.nodes[i].type == NodeType.Inlet)
                {
                    ids.Add(config.nodes[i].id);
                }
            }

            return ids;
        }

        public static List<DestinationColor> BayColors(LevelConfig config)
        {
            var colors = new List<DestinationColor>();
            for (int i = 0; i < config.nodes.Count; i++)
            {
                NodeDef node = config.nodes[i];
                if (node.type == NodeType.Bay && !colors.Contains(node.color))
                {
                    colors.Add(node.color);
                }
            }

            return colors;
        }

        /// <summary>Builds a mission's schedule exactly the way the game does.</summary>
        public static SpawnSchedule BuildSchedule(LevelConfig config, MissionDef mission)
        {
            var schedule = new SpawnSchedule();
            schedule.Build(
                InletIds(config),
                BayColors(config),
                mission.spawn,
                mission.modifiers,
                mission.spawn.hasSeed ? mission.spawn.seed : 0);
            return schedule;
        }

        /// <summary>Drains a schedule with a host that always accepts, at a fixed timestep.</summary>
        public static List<SpawnRequest> DrainAll(SpawnSchedule schedule, float dt = 1f / 60f)
        {
            var emitted = new List<SpawnRequest>();
            const int maxTicks = 2000000;
            for (int tick = 0; tick < maxTicks && !schedule.Finished; tick++)
            {
                float step = dt;
                while (schedule.TryDequeue(step, out SpawnRequest request))
                {
                    step = 0f;
                    emitted.Add(request);
                    schedule.ConfirmEmit();
                }
            }

            return emitted;
        }

        public static DeviceCatalogConfig Level01Devices()
        {
            return Level01Config().devices;
        }

        public static DeviceSpec Spec(DeviceType device)
        {
            Assert.IsTrue(Level01Devices().TryGet(device, out DeviceSpec spec),
                "level_01 does not sell device '" + device + "'.");
            return spec;
        }
    }
}
