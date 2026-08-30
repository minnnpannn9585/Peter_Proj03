using System;
using System.Collections.Generic;
using System.IO;

namespace ParcelSort.Offline
{
    /// <summary>Loads the real level file off disk so checks assert against shipped data.</summary>
    public static class LevelFixture
    {
        const string RelativePath = "Assets/StreamingAssets/Configs/Levels";

        static LevelConfig cached;

        public static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, RelativePath)))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "Could not find " + RelativePath + " above " + AppContext.BaseDirectory);
        }

        public static string LevelPath(string fileName)
        {
            return Path.Combine(RepoRoot(), RelativePath, fileName);
        }

        public static LevelConfig Level01()
        {
            return cached ?? (cached = LevelConfigParser.Parse(
                File.ReadAllText(LevelPath("level_01.json"))));
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

        /// <summary>Builds a schedule for a mission exactly the way the game would.</summary>
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

        /// <summary>
        /// Drains a schedule at a fixed timestep with a host that always accepts, which is the
        /// upper bound on what the schedule can release.
        /// </summary>
        public static List<SpawnRequest> DrainAll(SpawnSchedule schedule, float dt, int maxTicks)
        {
            var emitted = new List<SpawnRequest>();
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
    }
}
