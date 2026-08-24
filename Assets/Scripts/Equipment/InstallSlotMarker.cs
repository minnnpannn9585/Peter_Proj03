using UnityEngine;

namespace ParcelSort
{
    /// <summary>Prep phase target ring that shows where a device can be dropped.</summary>
    public class InstallSlotMarker : MonoBehaviour, IYardClickable
    {
        static readonly Color IdleColor = new Color(0.55f, 0.85f, 1f, 1f);
        static readonly Color HoverColor = new Color(0.35f, 1f, 0.55f, 1f);
        static readonly Color BlockedColor = new Color(0.9f, 0.35f, 0.3f, 1f);

        public BeltPath Belt { get; private set; }
        public int SlotIndex { get; private set; }

        Renderer[] renderers;
        bool hovered;

        public bool IsFree => Belt != null && !Belt.IsSlotOccupied(SlotIndex);

        public void Configure(BeltPath belt, int slotIndex)
        {
            Belt = belt;
            SlotIndex = slotIndex;
            renderers = GetComponentsInChildren<Renderer>(true);

            belt.Evaluate(belt.GetSlotDistance(slotIndex), out Vector3 position, out Vector3 tangent);
            transform.SetParent(belt.transform, true);
            // Slightly under the full visual scale so the ring sits inside the deck width.
            float s = BeltPath.VisualScale * 0.85f;
            transform.localScale = Vector3.one * s;
            transform.position = position - Vector3.up * (BeltPath.ParcelHalf - 0.02f * s);
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            gameObject.name = "Slot_" + belt.BeltId + "_" + slotIndex;
            SetHovered(false);
        }

        public void SetHovered(bool value)
        {
            hovered = value;
            Color color = !IsFree ? BlockedColor : (hovered ? HoverColor : IdleColor);
            DestinationPalette.ApplyAll(renderers, color);
        }

        public void OnYardClick()
        {
            // Slots are drop targets, not buttons. Kept clickable so raycasts resolve to them.
        }
    }
}
