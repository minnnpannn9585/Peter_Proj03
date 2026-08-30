using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// One row of the HUD anchor table: everything needed to place a RectTransform exactly.
    /// </summary>
    public struct RectSpec
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;

        public RectSpec(
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            this.anchorMin = anchorMin;
            this.anchorMax = anchorMax;
            this.pivot = pivot;
            this.anchoredPosition = anchoredPosition;
            this.sizeDelta = sizeDelta;
        }

        /// <summary>Shorthand for the common case where all three anchor vectors match.</summary>
        public static RectSpec Corner(Vector2 corner, Vector2 position, Vector2 size)
        {
            return new RectSpec(corner, corner, corner, position, size);
        }

        public void ApplyTo(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }

    /// <summary>
    /// The HUD anchor table, transcribed from the design doc and kept in one place so the
    /// numbers can be asserted by a test instead of being eyeballed in a scene file.
    ///
    /// The HUD is built from these values at runtime rather than hand-authored into the scene
    /// YAML. That is a deliberate choice: it makes every anchor reviewable in a diff and
    /// verifiable in Edit Mode, and it keeps the layout correct at any resolution.
    /// </summary>
    public static class HudLayout
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>0.5 balances width and height scaling, so 16:9 and 4:3 both stay usable.</summary>
        public const float ScaleMatch = 0.5f;

        static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        static readonly Vector2 TopRight = new Vector2(1f, 1f);
        static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // Top left: level name and coin balance. Visible in every phase.
        public static readonly RectSpec MapNameAndCoinPanel =
            RectSpec.Corner(TopLeft, new Vector2(24f, -24f), new Vector2(380f, 100f));

        public static readonly RectSpec MapNameLabel =
            new RectSpec(TopLeft, TopRight, TopLeft, new Vector2(12f, -8f), new Vector2(0f, 40f));

        public static readonly RectSpec CoinLabel =
            new RectSpec(TopLeft, TopRight, TopLeft, new Vector2(12f, -56f), new Vector2(0f, 36f));

        // Directly under the name panel: this round's progress. Running and Result only.
        public static readonly RectSpec RunProgressPanel =
            RectSpec.Corner(TopLeft, new Vector2(24f, -136f), new Vector2(380f, 56f));

        public static readonly RectSpec ProgressFill =
            new RectSpec(BottomLeft, TopRight, MiddleLeft, Vector2.zero, Vector2.zero);

        public static readonly RectSpec ProgressLabel =
            new RectSpec(BottomLeft, TopRight, Center, Vector2.zero, Vector2.zero);

        public static readonly RectSpec TimerLabel =
            RectSpec.Corner(TopRight, new Vector2(-8f, 26f), new Vector2(120f, 28f));

        // Left middle: the mission list. Prep only.
        public static readonly RectSpec MissionListPanel =
            RectSpec.Corner(MiddleLeft, new Vector2(24f, 20f), new Vector2(248f, 440f));

        public const float MissionListSpacing = 12f;

        public static readonly Vector2 MissionEntrySize = new Vector2(224f, 128f);

        // Bottom left: the device shop. Prep only.
        public static readonly RectSpec DeviceShopBar =
            RectSpec.Corner(BottomLeft, new Vector2(24f, 24f), new Vector2(760f, 172f));

        public const float ShopBarSpacing = 14f;

        public static readonly Vector2 ShopCardSize = new Vector2(172f, 148f);

        // Bottom left: pause / exit. Running and Result only, so it never fights the shop bar,
        // which owns this corner during prep. The two are never on screen at the same time.
        public static readonly RectSpec RunControlPanel =
            RectSpec.Corner(BottomLeft, new Vector2(24f, 24f), new Vector2(368f, 84f));

        public static readonly RectSpec PauseButton =
            new RectSpec(BottomLeft, new Vector2(0.5f, 0f), BottomLeft,
                new Vector2(12f, 12f), new Vector2(-18f, 60f));

        public static readonly RectSpec ExitButton =
            new RectSpec(new Vector2(0.5f, 0f), new Vector2(1f, 0f), BottomLeft,
                new Vector2(6f, 12f), new Vector2(-18f, 60f));

        // Bottom right: START. Prep only.
        public static readonly RectSpec StartButton =
            RectSpec.Corner(BottomRight, new Vector2(-24f, 24f), new Vector2(320f, 116f));

        // Centre: the settle screen. Result only, layered over the Running layout.
        public static readonly RectSpec ResultPanel =
            RectSpec.Corner(Center, Vector2.zero, new Vector2(660f, 440f));

        // Kept from the existing HUD.
        public static readonly RectSpec DebugLabel =
            RectSpec.Corner(TopRight, new Vector2(-24f, -24f), new Vector2(420f, 220f));

        public static readonly RectSpec GmPanel =
            RectSpec.Corner(TopRight, new Vector2(-24f, -260f), new Vector2(260f, 140f));
    }
}
