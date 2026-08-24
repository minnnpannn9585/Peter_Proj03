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
        public float StallTime { get; set; }
        public bool JamCounted { get; set; }

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
