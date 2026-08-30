using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Where install slots land along a belt. Pulled out of <see cref="BeltPath"/> so the slot
    /// layout of a level file can be counted offline, using the exact same arithmetic the game
    /// uses at runtime rather than a copy of it.
    /// </summary>
    public static class BeltSlotMath
    {
        /// <summary>
        /// Authored deck width before the level's visual scale. Lives here rather than on
        /// <see cref="BeltPath"/> so slot maths stays free of MonoBehaviour and can be checked
        /// without an engine; <see cref="BeltPath.BaseDeckWidth"/> forwards to this value.
        /// </summary>
        public const float BaseDeckWidth = 0.62f;

        public static float DeckWidth(float visualScale)
        {
            return BaseDeckWidth * visualScale;
        }

        /// <summary>Clearance kept at each end so a marker never overlaps a node.</summary>
        public static float Margin(float gridSize)
        {
            return gridSize * 0.6f;
        }

        /// <summary>
        /// Gap between slots. Wider than a cell so the enlarged markers read as separate drop
        /// targets instead of one continuous ribbon over the deck.
        /// </summary>
        public static float Spacing(float gridSize, float deckWidth)
        {
            return Mathf.Max(gridSize, deckWidth * 2.2f);
        }

        /// <summary>Appends the arc-length position of every slot on a belt.</summary>
        public static void Fill(
            List<float> into, float length, float gridSize, float deckWidth, bool allowInstall)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();
            float margin = Margin(gridSize);
            if (!allowInstall || length <= margin * 2f)
            {
                return;
            }

            float spacing = Spacing(gridSize, deckWidth);
            for (float d = margin; d <= length - margin + 0.001f; d += spacing)
            {
                into.Add(d);
            }
        }

        public static int Count(float length, float gridSize, float deckWidth, bool allowInstall)
        {
            var scratch = new List<float>();
            Fill(scratch, length, gridSize, deckWidth, allowInstall);
            return scratch.Count;
        }
    }
}
