using System;

namespace ParcelSort
{
    [Serializable]
    public class ParcelData
    {
        public DestinationColor destination;
        public bool isRevealed;
        public bool chilled;
        public bool urgent;
        public ParcelSize size;
    }
}
