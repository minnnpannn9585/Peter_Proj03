using System;
using System.Collections.Generic;
using System.Globalization;

namespace ParcelSort
{
    /// <summary>Reads the optional "spawn" block of a level file into a <see cref="SpawnPlan"/>.</summary>
    public static class SpawnPlanParser
    {
        static readonly char[] RangeSeparators = { '~', '-', ':' };

        public static void Read(JsonValue value, SpawnPlan plan)
        {
            if (plan == null || !value.Exists)
            {
                return;
            }

            if (value.Has("seed"))
            {
                plan.seed = value["seed"].AsInt();
                plan.hasSeed = true;
            }

            plan.startDelay = ReadRange(value["startDelay"], plan.startDelay);
            plan.repeat = Math.Max(1, value["repeat"].AsInt(1));
            ReadWaves(value["waves"], plan.waves);
        }

        /// <summary>Accepts 4, [4, 8], "4~8" or "random 4~8".</summary>
        public static RandomRange ReadRange(JsonValue value, RandomRange fallback)
        {
            if (!value.Exists)
            {
                return fallback;
            }

            if (value.Kind == JsonKind.Number)
            {
                return RandomRange.Fixed(value.AsFloat());
            }

            if (value.Kind == JsonKind.Array && value.Count >= 2)
            {
                return RandomRange.Between(value[0].AsFloat(), value[1].AsFloat());
            }

            if (value.Kind == JsonKind.String)
            {
                return ParseRangeText(value.StringValue, fallback);
            }

            return fallback;
        }

        static RandomRange ParseRangeText(string text, RandomRange fallback)
        {
            if (string.IsNullOrEmpty(text))
            {
                return fallback;
            }

            string body = text.Trim();
            if (body.StartsWith("random", StringComparison.OrdinalIgnoreCase))
            {
                body = body.Substring(6);
            }

            body = body.Trim();
            int split = body.IndexOfAny(RangeSeparators, 1);
            if (split > 0)
            {
                string low = body.Substring(0, split).Trim();
                string high = body.Substring(split + 1).Trim();
                if (TryFloat(low, out float a) && TryFloat(high, out float b))
                {
                    return a <= b ? RandomRange.Between(a, b) : RandomRange.Between(b, a);
                }

                return fallback;
            }

            return TryFloat(body, out float single) ? RandomRange.Fixed(single) : fallback;
        }

        static void ReadWaves(JsonValue value, List<SpawnWaveDef> waves)
        {
            if (value.Kind != JsonKind.Array)
            {
                return;
            }

            for (int i = 0; i < value.Count; i++)
            {
                JsonValue item = value[i];
                var wave = new SpawnWaveDef
                {
                    parallel = item["parallel"].AsBool(item["withPrevious"].AsBool())
                };

                ReadIdList(item.Has("inlets") ? item["inlets"] : item["inlet"], wave.inlets);
                ReadColorList(item.Has("colors") ? item["colors"] : item["color"], wave.colors, i);

                wave.count = ReadRange(item["count"], wave.count);
                wave.spacing = ReadRange(item.Has("spacing") ? item["spacing"] : item["gap"], wave.spacing);
                wave.delayAfter = ReadRange(
                    item.Has("delayAfter") ? item["delayAfter"] : item["rest"], wave.delayAfter);

                waves.Add(wave);
            }
        }

        static bool TryFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>"random" or "any" leaves the list empty, which the director reads as "pick one".</summary>
        static void ReadIdList(JsonValue value, List<string> target)
        {
            if (value.Kind == JsonKind.Array)
            {
                for (int i = 0; i < value.Count; i++)
                {
                    AddId(value[i].AsString(), target);
                }

                return;
            }

            if (value.Kind == JsonKind.String)
            {
                AddId(value.StringValue, target);
            }
        }

        static void AddId(string id, List<string> target)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            string trimmed = id.Trim();
            if (trimmed.Length == 0 ||
                string.Equals(trimmed, "random", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "any", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            target.Add(trimmed);
        }

        static void ReadColorList(JsonValue value, List<DestinationColor> target, int waveIndex)
        {
            if (value.Kind == JsonKind.Array)
            {
                for (int i = 0; i < value.Count; i++)
                {
                    AddColor(value[i].AsString(), target, waveIndex);
                }

                return;
            }

            if (value.Kind == JsonKind.String)
            {
                AddColor(value.StringValue, target, waveIndex);
            }
        }

        static void AddColor(string text, List<DestinationColor> target, int waveIndex)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string trimmed = text.Trim();
            if (trimmed.Length == 0 ||
                string.Equals(trimmed, "random", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "any", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!DestinationPalette.TryParse(trimmed, out DestinationColor color))
            {
                throw new InvalidOperationException(
                    "Spawn wave " + waveIndex + " has unknown color '" + trimmed + "'.");
            }

            target.Add(color);
        }
    }
}
