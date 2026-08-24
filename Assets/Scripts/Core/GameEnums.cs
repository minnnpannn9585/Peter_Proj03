namespace ParcelSort
{
    public enum DestinationColor
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3
    }

    public enum BeltSpeed
    {
        Stopped = 0,
        Normal = 1,
        Fast = 2
    }

    public enum ParcelSize
    {
        Small = 0,
        Large = 1,
        Oversize = 2
    }

    public enum GridDir
    {
        East = 0,
        North = 1,
        West = 2,
        South = 3
    }

    /// <summary>Round lifecycle. Prep is the pre-round install window.</summary>
    public enum GamePhase
    {
        Prep = 0,
        Running = 1
    }

    public enum NodeType
    {
        Inlet = 0,
        Junction = 1,
        Bay = 2
    }

    public enum DeviceType
    {
        Gate = 0
    }
}
