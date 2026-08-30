using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Automates one diverter's lever. Installs on a <see cref="NodeDeviceSlot"/>, works out at
    /// install time which colours each outgoing belt can eventually reach, and then throws the
    /// lever to match whichever parcel is next across the node.
    ///
    /// Two deliberate limits keep it from trivialising the game:
    /// it refuses to act on a parcel that is still blind (so it cannot substitute for a Scanner),
    /// and it honours a switch cooldown (so it cannot alternate faster than a hand could).
    /// With a cap of one, at least two of the map's three diverters stay manual.
    /// </summary>
    public class AutoArmDevice : MonoBehaviour, IYardClickable
    {
        static readonly Color ActiveColor = new Color(0.62f, 0.42f, 0.92f, 1f);

        /// <summary>How close to the belt end a parcel must be to count as "next across".</summary>
        const float HandoffWindow = 0.35f;

        [SerializeField] Renderer bodyRenderer;

        readonly Dictionary<DestinationColor, int> routing = new Dictionary<DestinationColor, int>();

        float cooldown = 0.6f;
        float sinceSwitch;
        bool skipBlind = true;

        public YardNode Node { get; private set; }

        /// <summary>Seconds until another switch is allowed.</summary>
        public float CooldownRemaining => Mathf.Max(0f, cooldown - sinceSwitch);

        /// <summary>How many times this arm has moved the lever. Useful for tests.</summary>
        public int SwitchCount { get; private set; }

        /// <summary>Colour to output-index map, resolved once at install time.</summary>
        public IReadOnlyDictionary<DestinationColor, int> Routing => routing;

        public void Install(YardNode node, YardGraph graph, float switchCooldown, bool skipBlindParcels)
        {
            if (node == null)
            {
                return;
            }

            Node = node;
            cooldown = Mathf.Max(0f, switchCooldown);
            skipBlind = skipBlindParcels;
            sinceSwitch = cooldown;
            SwitchCount = 0;

            BuildRouting(graph);

            transform.SetParent(node.transform, true);
            transform.localScale = Vector3.one * BeltPath.VisualScale;
            transform.position = node.PathPos + Vector3.up * BeltPath.ParcelHalf;
            transform.rotation = Quaternion.identity;
            gameObject.name = "AutoArm_" + node.NodeId;

            node.AutoArm = this;
            Refresh();
        }

        /// <summary>
        /// For each outgoing belt, walks the graph forward and records every bay colour it can
        /// still reach. The first output that can reach a colour wins, so the table is stable.
        /// </summary>
        void BuildRouting(YardGraph graph)
        {
            routing.Clear();
            if (Node == null || graph == null)
            {
                return;
            }

            Dictionary<string, DestinationColor> bayColors = graph.BayColorsById();
            for (int output = 0; output < Node.OutBelts.Count; output++)
            {
                BeltPath belt = Node.OutBelts[output];
                if (belt == null || belt.To == null)
                {
                    continue;
                }

                HashSet<string> bays = graph.ReachableBays(belt.To);
                foreach (string bayId in bays)
                {
                    if (!bayColors.TryGetValue(bayId, out DestinationColor color))
                    {
                        continue;
                    }

                    if (!routing.ContainsKey(color))
                    {
                        routing.Add(color, output);
                    }
                }
            }
        }

        void Update()
        {
            Advance(Time.deltaTime);
        }

        /// <summary>Public so a test can step the arm at a fixed timestep.</summary>
        public void Advance(float dt)
        {
            if (Node == null || Node.OutBelts.Count <= 1)
            {
                return;
            }

            sinceSwitch += dt;
            if (sinceSwitch < cooldown)
            {
                return;
            }

            Parcel next = FindNextParcel();
            if (next == null)
            {
                return;
            }

            // A blind parcel carries no readable destination. Guessing would burn the player's
            // error budget on the arm's behalf, so the arm stands down and leaves the lever be.
            if (skipBlind && !next.IsRevealed)
            {
                return;
            }

            if (next.Data == null || !routing.TryGetValue(next.Data.destination, out int output))
            {
                return;
            }

            if (output == Node.SelectedOutput)
            {
                return;
            }

            Node.SelectedOutput = output;
            sinceSwitch = 0f;
            SwitchCount++;

            var diverter = Node.GetComponent<Diverter>();
            if (diverter != null)
            {
                diverter.Refresh();
            }
        }

        /// <summary>The parcel closest to crossing this node, across every incoming belt.</summary>
        Parcel FindNextParcel()
        {
            Parcel best = null;
            float bestGap = float.MaxValue;

            for (int i = 0; i < Node.InBelts.Count; i++)
            {
                BeltPath belt = Node.InBelts[i];
                if (belt == null || belt.Parcels.Count == 0)
                {
                    continue;
                }

                ParcelRuntime front = belt.Parcels[0];
                float gap = belt.Length - front.Distance;
                if (gap > HandoffWindow || gap >= bestGap)
                {
                    continue;
                }

                Parcel parcel = front.Parcel;
                if (parcel == null)
                {
                    continue;
                }

                bestGap = gap;
                best = parcel;
            }

            return best;
        }

        public void OnYardClick()
        {
            // The arm owns the lever while installed; a manual click here would fight it.
        }

        void Refresh()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<Renderer>();
            }

            DestinationPalette.Apply(bodyRenderer, ActiveColor);
        }

        void OnDestroy()
        {
            if (Node != null && Node.AutoArm == this)
            {
                Node.AutoArm = null;
            }
        }
    }
}
