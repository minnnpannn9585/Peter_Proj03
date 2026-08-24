using UnityEngine;

namespace ParcelSort
{
    /// <summary>Whole-belt stop switch. Lives on the BeltPath, not on a single cell.</summary>
    public class BeltPauseSwitch : MonoBehaviour, IYardClickable
    {
        static readonly Color PausedColor = new Color(0.24f, 0.24f, 0.28f, 1f);
        static readonly Color NormalColor = new Color(0.45f, 0.45f, 0.48f, 1f);

        BeltPath belt;
        bool paused;

        public bool IsPaused => paused;

        public void Configure()
        {
            belt = GetComponent<BeltPath>();
            paused = false;
            Apply();
        }

        public void OnYardClick()
        {
            paused = !paused;
            Apply();
        }

        void Apply()
        {
            if (belt == null)
            {
                belt = GetComponent<BeltPath>();
            }

            if (belt == null)
            {
                return;
            }

            belt.SpeedMultiplier = paused ? 0f : 1f;
            DestinationPalette.ApplyAll(belt.DeckRenderers, paused ? PausedColor : NormalColor);
        }
    }
}
