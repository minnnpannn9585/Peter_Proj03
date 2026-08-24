using System.Collections.Generic;

namespace ParcelSort
{
    /// <summary>
    /// A number that a level file may author as a constant or as a random range.
    /// JSON accepts 4, "4~8", "random 4~8" or "random4-8".
    /// </summary>
    public struct RandomRange
    {
        public float min;
        public float max;

        public static RandomRange Fixed(float value)
        {
            return new RandomRange { min = value, max = value };
        }

        public static RandomRange Between(float low, float high)
        {
            return new RandomRange { min = low, max = high };
        }

        public float NextFloat(System.Random rng)
        {
            if (max <= min || rng == null)
            {
                return min;
            }

            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>Inclusive on both ends so "4~8" can yield 8.</summary>
        public int NextInt(System.Random rng)
        {
            int low = UnityEngine.Mathf.RoundToInt(min);
            int high = UnityEngine.Mathf.RoundToInt(max);
            if (high <= low || rng == null)
            {
                return low;
            }

            return rng.Next(low, high + 1);
        }
    }

    /// <summary>One authored burst: a run of same-colour parcels out of one inlet.</summary>
    public class SpawnWaveDef
    {
        /// <summary>Candidate inlet ids. Empty means "pick any inlet at random".</summary>
        public List<string> inlets = new List<string>();

        /// <summary>Candidate colours. Empty means "pick any bay colour at random".</summary>
        public List<DestinationColor> colors = new List<DestinationColor>();

        /// <summary>How many parcels this burst emits.</summary>
        public RandomRange count = RandomRange.Between(4f, 8f);

        /// <summary>Seconds between parcels inside the burst. 0 means back to back.</summary>
        public RandomRange spacing = RandomRange.Fixed(0f);

        /// <summary>Idle seconds after the whole group finishes.</summary>
        public RandomRange delayAfter = RandomRange.Between(3f, 5f);

        /// <summary>True runs this burst at the same time as the previous one.</summary>
        public bool parallel;
    }

    /// <summary>The whole level's parcel schedule, authored up front in the level JSON.</summary>
    public class SpawnPlan
    {
        public int seed;
        public bool hasSeed;

        /// <summary>Grace period before the first burst starts.</summary>
        public RandomRange startDelay = RandomRange.Fixed(0.5f);

        /// <summary>How many times the wave list is played through.</summary>
        public int repeat = 1;

        public List<SpawnWaveDef> waves = new List<SpawnWaveDef>();

        public bool HasWaves => waves.Count > 0;
    }
}
