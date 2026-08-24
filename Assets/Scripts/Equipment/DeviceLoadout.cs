using System.Collections.Generic;

namespace ParcelSort
{
    /// <summary>How many of each device the player may install this round.</summary>
    public class DeviceLoadout
    {
        readonly Dictionary<DeviceType, int> granted = new Dictionary<DeviceType, int>();
        readonly Dictionary<DeviceType, int> remaining = new Dictionary<DeviceType, int>();

        public IEnumerable<DeviceType> Devices => granted.Keys;

        public void Reset(LevelConfig config)
        {
            granted.Clear();
            remaining.Clear();
            if (config == null)
            {
                return;
            }

            for (int i = 0; i < config.loadout.Count; i++)
            {
                LoadoutEntry entry = config.loadout[i];
                granted[entry.device] = entry.count;
                remaining[entry.device] = entry.count;
            }
        }

        public int Granted(DeviceType device)
        {
            return granted.TryGetValue(device, out int value) ? value : 0;
        }

        public int Remaining(DeviceType device)
        {
            return remaining.TryGetValue(device, out int value) ? value : 0;
        }

        public bool TryConsume(DeviceType device)
        {
            if (!remaining.TryGetValue(device, out int value) || value <= 0)
            {
                return false;
            }

            remaining[device] = value - 1;
            return true;
        }

        public void Refund(DeviceType device)
        {
            if (!remaining.ContainsKey(device))
            {
                return;
            }

            remaining[device] = System.Math.Min(remaining[device] + 1, Granted(device));
        }
    }
}
