using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// The flagship device: reveals blind parcels as they pass it.
    ///
    /// Its reach is deliberately tiny - only parcels on its own belt, and only once they have
    /// travelled past its exact arc-length position. That is the whole point: buying one is
    /// useless unless it sits downstream of an inlet and upstream of the first diverter, so
    /// "where" matters as much as "how many".
    ///
    /// <see cref="TrafficSystem"/> drives <see cref="ScanPass"/> rather than this component
    /// using Update, so a reveal happens on the same tick as the movement that earned it and a
    /// whole round stays replayable at a fixed timestep.
    /// </summary>
    public class ScannerDevice : MonoBehaviour, IYardClickable
    {
        static readonly Color IdleColor = new Color(0.30f, 0.78f, 0.92f, 1f);

        [SerializeField] Renderer bodyRenderer;

        public BeltPath Belt { get; private set; }
        public int SlotIndex { get; private set; } = -1;
        public float Distance { get; private set; }

        /// <summary>How many parcels this scanner has revealed. Monotonically non-decreasing.</summary>
        public int RevealedCount { get; private set; }

        public void Install(BeltPath belt, int slotIndex)
        {
            if (belt == null)
            {
                return;
            }

            Belt = belt;
            SlotIndex = slotIndex;
            Distance = belt.GetSlotDistance(slotIndex);
            RevealedCount = 0;

            belt.Evaluate(Distance, out Vector3 position, out Vector3 tangent);
            transform.SetParent(belt.transform, true);
            transform.localScale = Vector3.one * BeltPath.VisualScale;
            transform.position = position - Vector3.up * BeltPath.ParcelHalf;
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            gameObject.name = "Scanner_" + belt.BeltId + "_" + slotIndex;

            belt.Scanners.Add(this);
            belt.SortScanners();
            belt.SetSlotOccupied(slotIndex, true);
            Refresh();
        }

        /// <summary>
        /// Reveals every parcel on this belt that has already passed the scanner head. Safe to
        /// call every tick: <see cref="Parcel.Reveal"/> is idempotent.
        /// </summary>
        public void ScanPass()
        {
            if (Belt == null)
            {
                return;
            }

            List<ParcelRuntime> parcels = Belt.Parcels;
            for (int i = 0; i < parcels.Count; i++)
            {
                ParcelRuntime runtime = parcels[i];
                if (runtime == null || runtime.Distance < Distance)
                {
                    continue;
                }

                Parcel parcel = runtime.Parcel;
                if (parcel == null || parcel.IsRevealed)
                {
                    continue;
                }

                parcel.Reveal();
                RevealedCount++;
            }
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

            DestinationPalette.Apply(bodyRenderer, IdleColor);
        }

        /// <summary>
        /// Detaches from the belt without destroying the object, so the same scanner can be moved
        /// further upstream. Idempotent, and shared with <see cref="OnDestroy"/>.
        /// </summary>
        public void Uninstall()
        {
            if (Belt == null)
            {
                return;
            }

            Belt.Scanners.Remove(this);
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
