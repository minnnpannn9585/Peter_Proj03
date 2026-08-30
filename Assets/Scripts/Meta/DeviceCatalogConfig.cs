using System.Collections.Generic;

namespace ParcelSort
{
    /// <summary>
    /// One device's shop and behaviour numbers. Every value here comes from the level file's
    /// "devices" block; nothing is hard coded, so a balance change is a JSON edit.
    /// </summary>
    public class DeviceSpec
    {
        public DeviceType device = DeviceType.Gate;

        /// <summary>Coin cost of one unit.</summary>
        public int price;

        /// <summary>Hard limit on how many units the player may ever own.</summary>
        public int cap;

        public InstallTargetKind target = InstallTargetKind.BeltSlot;

        /// <summary>Gate: seconds the barrier stays shut before it opens itself.</summary>
        public float holdSeconds = 8f;

        /// <summary>Booster: multiplier written to <see cref="BeltPath.DeviceSpeedBonus"/>.</summary>
        public float speedBonus = 1.6f;

        /// <summary>AutoArm: minimum seconds between two output switches.</summary>
        public float switchCooldown = 0.6f;

        /// <summary>AutoArm: true leaves the lever alone for parcels that are still blind.</summary>
        public bool skipBlind = true;
    }

    /// <summary>
    /// The devices a level offers, keyed by kind. A missing "devices" block yields an empty
    /// catalog rather than an exception: the shop simply renders empty and the player can
    /// still attempt the bare-handed mission.
    /// </summary>
    public class DeviceCatalogConfig
    {
        readonly Dictionary<DeviceType, DeviceSpec> specs = new Dictionary<DeviceType, DeviceSpec>();
        readonly List<DeviceSpec> order = new List<DeviceSpec>();

        public IReadOnlyList<DeviceSpec> All => order;

        public int Count => order.Count;

        public bool TryGet(DeviceType device, out DeviceSpec spec)
        {
            return specs.TryGetValue(device, out spec);
        }

        public void Add(DeviceSpec spec)
        {
            if (spec == null)
            {
                return;
            }

            if (specs.ContainsKey(spec.device))
            {
                specs[spec.device] = spec;
                for (int i = 0; i < order.Count; i++)
                {
                    if (order[i].device == spec.device)
                    {
                        order[i] = spec;
                        return;
                    }
                }

                return;
            }

            specs.Add(spec.device, spec);
            order.Add(spec);
        }

        public void Clear()
        {
            specs.Clear();
            order.Clear();
        }
    }
}
