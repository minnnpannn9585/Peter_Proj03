using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Movement state for one parcel. Stepping is driven centrally by TrafficSystem so that
    /// queueing, gate back pressure and merge arbitration stay deterministic.
    /// </summary>
    public class ParcelRuntime : MonoBehaviour
    {
        public BeltPath Belt { get; set; }
        public float Distance { get; set; }

        /// <summary>Seconds spent stalled for a reason that counts as congestion.</summary>
        public float StallTime { get; set; }

        public bool JamCounted { get; set; }

        /// <summary>
        /// Seconds spent held by a closed gate. Tracked separately from <see cref="StallTime"/>
        /// because a gate is the player deliberately queueing parcels, not the yard seizing up,
        /// and a gate's hold (8s) is longer than the jam threshold (3s).
        /// </summary>
        public float GateWaitTime { get; set; }

        /// <summary>
        /// True when this parcel is only stopped because the parcel ahead is gate-held. The flag
        /// propagates back along the queue so the whole tailback behind a gate is exempt from the
        /// jam counter, not just the parcel touching the barrier.
        /// </summary>
        public bool UpstreamGateHeld { get; set; }

        /// <summary>Clears every congestion signal, for a fresh belt entry.</summary>
        public void ResetFlow()
        {
            StallTime = 0f;
            JamCounted = false;
            GateWaitTime = 0f;
            UpstreamGateHeld = false;
        }

        Parcel parcel;

        public Parcel Parcel
        {
            get
            {
                if (parcel == null)
                {
                    parcel = GetComponent<Parcel>();
                }

                return parcel;
            }
        }

        public void ApplyPose(Vector3 position, Vector3 tangent)
        {
            transform.position = position;
            if (tangent.sqrMagnitude > 0.000001f)
            {
                transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            }
        }

        public void RefreshPose()
        {
            if (Belt == null)
            {
                return;
            }

            Belt.Evaluate(Distance, out Vector3 pos, out Vector3 tangent);
            ApplyPose(pos, tangent);
        }
    }
}
