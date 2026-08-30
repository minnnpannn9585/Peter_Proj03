using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Prep phase drop target sitting on a diverter node. Node slots exist because an AutoArm
    /// operates a lever, not a stretch of belt, so it needs a target the belt slots cannot express.
    /// </summary>
    public class NodeDeviceSlot : MonoBehaviour, IYardClickable
    {
        static readonly Color IdleColor = new Color(1f, 0.82f, 0.35f, 1f);
        static readonly Color HoverColor = new Color(0.35f, 1f, 0.55f, 1f);
        static readonly Color BlockedColor = new Color(0.9f, 0.35f, 0.3f, 1f);

        Renderer[] renderers;
        bool hovered;
        bool occupied;

        public YardNode Node { get; private set; }

        public bool IsFree => Node != null && !occupied;

        public void Configure(YardNode node)
        {
            Node = node;
            occupied = false;
            renderers = GetComponentsInChildren<Renderer>(true);

            transform.SetParent(node.transform, true);
            float scale = BeltPath.VisualScale * 0.9f;
            transform.localScale = Vector3.one * scale;

            // Sits just above the deck plane so it reads as a lever cap rather than a belt slot.
            transform.position = node.PathPos + Vector3.up * (BeltPath.ParcelHalf * 0.6f);
            transform.rotation = Quaternion.identity;
            gameObject.name = "NodeSlot_" + node.NodeId;
            SetHovered(false);
        }

        public void SetHovered(bool value)
        {
            hovered = value;
            Repaint();
        }

        public void SetOccupied(bool value)
        {
            occupied = value;
            Repaint();
        }

        /// <summary>Flashes red once to explain a refused install.</summary>
        public void FlashBlocked()
        {
            DestinationPalette.ApplyAll(renderers, BlockedColor);
        }

        void Repaint()
        {
            Color color = !IsFree ? BlockedColor : (hovered ? HoverColor : IdleColor);
            DestinationPalette.ApplyAll(renderers, color);
        }

        public void OnYardClick()
        {
            // A drop target, not a button. Kept clickable so raycasts resolve to it.
        }
    }
}
