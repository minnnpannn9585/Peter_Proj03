using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Installable speed-up. Writes only <see cref="BeltPath.DeviceSpeedBonus"/> and never touches
    /// <see cref="BeltPath.SpeedMultiplier"/>, so the player's pause and fast switches cannot
    /// erase the bonus and the two effects compose predictably.
    ///
    /// One per belt: <see cref="InstallManager"/> refuses a second one, which is why the belt
    /// stores a single bonus value instead of a product.
    /// </summary>
    public class BoosterDevice : MonoBehaviour, IYardClickable
    {
        static readonly Color ActiveColor = new Color(0.95f, 0.62f, 0.16f, 1f);

        [SerializeField] Renderer bodyRenderer;

        public BeltPath Belt { get; private set; }
        public int SlotIndex { get; private set; } = -1;
        public float Distance { get; private set; }
        public float SpeedBonus { get; private set; } = 1f;

        public void Install(BeltPath belt, int slotIndex, float speedBonus)
        {
            if (belt == null)
            {
                return;
            }

            Belt = belt;
            SlotIndex = slotIndex;
            SpeedBonus = Mathf.Max(1f, speedBonus);
            Distance = belt.GetSlotDistance(slotIndex);

            belt.Evaluate(Distance, out Vector3 position, out Vector3 tangent);
            transform.SetParent(belt.transform, true);
            transform.localScale = Vector3.one * BeltPath.VisualScale;
            transform.position = position - Vector3.up * BeltPath.ParcelHalf;
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            gameObject.name = "Booster_" + belt.BeltId + "_" + slotIndex;

            belt.DeviceSpeedBonus = SpeedBonus;
            belt.SetSlotOccupied(slotIndex, true);
            Refresh();
        }

        public void OnYardClick()
        {
            // Passive device: it has no state the player can toggle.
        }

        void Refresh()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<Renderer>();
            }

            DestinationPalette.Apply(bodyRenderer, ActiveColor);
        }

        /// <summary>
        /// Detaches from the belt without destroying the object, so the same Booster can be moved
        /// to another slot. Idempotent, and shared with <see cref="OnDestroy"/>.
        /// </summary>
        public void Uninstall()
        {
            if (Belt == null)
            {
                return;
            }

            // Hand the belt back its default speed; leaving 1.6 behind would make a removed
            // Booster keep working forever.
            Belt.DeviceSpeedBonus = 1f;
            Belt.SetSlotOccupied(SlotIndex, false);
            Belt = null;
            SlotIndex = -1;
        }

        void OnDestroy()
        {
            Uninstall();
        }
    }
}
