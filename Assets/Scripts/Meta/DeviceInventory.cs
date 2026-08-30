using System;
using System.Collections.Generic;

namespace ParcelSort
{
    /// <summary>
    /// How many of each device the player owns, and how many of those are currently bolted
    /// onto the yard. Replaces <see cref="DeviceLoadout"/>'s "per round allowance" meaning:
    /// devices are now permanent property that can be moved between rounds.
    ///
    /// Invariants: <c>0 &lt;= Installed(d) &lt;= Owned(d)</c> and
    /// <c>Free(d) == Owned(d) - Installed(d)</c>, for every device, after every operation.
    /// </summary>
    public class DeviceInventory
    {
        readonly Dictionary<DeviceType, int> owned = new Dictionary<DeviceType, int>();
        readonly Dictionary<DeviceType, int> installed = new Dictionary<DeviceType, int>();

        /// <summary>Raised whenever an owned or installed count changes.</summary>
        public event Action Changed;

        /// <summary>Every device kind, whether or not the player owns one.</summary>
        public IEnumerable<DeviceType> All => DeviceTypes.All;

        public int Owned(DeviceType device)
        {
            return owned.TryGetValue(device, out int value) ? value : 0;
        }

        public int Installed(DeviceType device)
        {
            return installed.TryGetValue(device, out int value) ? value : 0;
        }

        /// <summary>Owned but not currently placed on the yard.</summary>
        public int Free(DeviceType device)
        {
            return Owned(device) - Installed(device);
        }

        public void Grant(DeviceType device, int count)
        {
            if (count <= 0)
            {
                return;
            }

            owned[device] = Owned(device) + count;
            Changed?.Invoke();
        }

        /// <summary>
        /// Replaces the owned count, used when loading a profile. Installed is clamped so the
        /// <c>Installed &lt;= Owned</c> invariant survives a shrinking profile.
        /// </summary>
        public void SetOwned(DeviceType device, int count)
        {
            owned[device] = Math.Max(0, count);
            if (Installed(device) > Owned(device))
            {
                installed[device] = Owned(device);
            }

            Changed?.Invoke();
        }

        /// <summary>Reserves one free unit for an install. False when none are spare.</summary>
        public bool TryTakeForInstall(DeviceType device)
        {
            if (Free(device) <= 0)
            {
                return false;
            }

            installed[device] = Installed(device) + 1;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Hands one unit back after a removal. Clamped at zero.</summary>
        public void ReturnFromInstall(DeviceType device)
        {
            int current = Installed(device);
            if (current <= 0)
            {
                return;
            }

            installed[device] = current - 1;
            Changed?.Invoke();
        }

        /// <summary>Marks everything as unplaced, used when the yard is torn down.</summary>
        public void ClearInstalled()
        {
            if (installed.Count == 0)
            {
                return;
            }

            installed.Clear();
            Changed?.Invoke();
        }

        public void ClearAll()
        {
            owned.Clear();
            installed.Clear();
            Changed?.Invoke();
        }
    }
}
