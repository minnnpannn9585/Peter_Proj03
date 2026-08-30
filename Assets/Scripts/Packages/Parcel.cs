using UnityEngine;

namespace ParcelSort
{
    public class Parcel : MonoBehaviour
    {
        /// <summary>Child object shown while the parcel's destination is unknown, if authored.</summary>
        [SerializeField] GameObject blindMark;

        public ParcelData Data { get; private set; }

        /// <summary>False while the destination colour is hidden from the player.</summary>
        public bool IsRevealed => Data != null && Data.isRevealed;

        public void Initialize(ParcelData data)
        {
            Data = data ?? new ParcelData();
            ApplyVisual();
        }

        /// <summary>
        /// Turns a blind parcel into a readable one. One-way and idempotent: there is no code
        /// path anywhere that sets <see cref="ParcelData.isRevealed"/> back to false, which is
        /// what lets the player trust a scanner's output.
        /// </summary>
        public void Reveal()
        {
            if (Data == null || Data.isRevealed)
            {
                return;
            }

            Data.isRevealed = true;
            ApplyVisual();
        }

        public void ApplyVisual()
        {
            if (Data == null)
            {
                return;
            }

            Renderer renderer = GetComponentInChildren<Renderer>();

            // Blind parcels read as neutral grey so the player cannot guess a destination from
            // them; revealed parcels wear their bay colour.
            Color color = Data.isRevealed
                ? DestinationPalette.For(Data.destination)
                : DestinationPalette.Blind;
            DestinationPalette.Apply(renderer, color);

            ResolveBlindMark();
            if (blindMark != null)
            {
                blindMark.SetActive(!Data.isRevealed);
            }
        }

        void ResolveBlindMark()
        {
            if (blindMark != null)
            {
                return;
            }

            Transform found = transform.Find("BlindMark");
            if (found != null)
            {
                blindMark = found.gameObject;
            }
        }
    }
}
