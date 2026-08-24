using UnityEngine;

namespace ParcelSort
{
    public class TruckBay : MonoBehaviour
    {
        public DestinationColor AcceptsColor { get; private set; }

        YardDirector director;

        public void Configure(DestinationColor color, YardDirector yardDirector)
        {
            AcceptsColor = color;
            director = yardDirector;
            Renderer renderer = GetComponentInChildren<Renderer>();
            DestinationPalette.Apply(renderer, DestinationPalette.For(color));
        }

        public void Accept(Parcel parcel)
        {
            if (parcel == null)
            {
                return;
            }

            bool correct = parcel.Data != null && parcel.Data.destination == AcceptsColor;
            if (director != null)
            {
                if (correct)
                {
                    director.NotifyCorrect();
                }
                else
                {
                    director.NotifyWrong();
                }
            }

            Destroy(parcel.gameObject);
        }
    }
}
