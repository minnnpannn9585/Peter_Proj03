using UnityEngine;

namespace ParcelSort
{
    public class Parcel : MonoBehaviour
    {
        public ParcelData Data { get; private set; }

        public void Initialize(ParcelData data)
        {
            Data = data ?? new ParcelData();
            ApplyVisual();
        }

        public void ApplyVisual()
        {
            if (Data == null)
            {
                return;
            }

            Renderer renderer = GetComponentInChildren<Renderer>();
            DestinationPalette.Apply(renderer, DestinationPalette.For(Data.destination));
        }
    }
}
