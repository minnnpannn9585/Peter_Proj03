using System;

namespace ParcelSort.Offline
{
    /// <summary>Property 3: installed count never exceeds owned count.</summary>
    public static class InventoryChecks
    {
        public static void Run(CheckRunner r)
        {
            r.Section("DeviceInventory (Property 3)");

            var inv = new DeviceInventory();
            r.AreEqual(0, inv.Owned(DeviceType.Gate), "nothing owned to start with");
            r.Check(!inv.TryTakeForInstall(DeviceType.Gate), "cannot install what is not owned");

            inv.Grant(DeviceType.Gate, 2);
            r.AreEqual(2, inv.Free(DeviceType.Gate), "Free equals Owned before any install");
            r.Check(inv.TryTakeForInstall(DeviceType.Gate), "first install takes a free unit");
            r.Check(inv.TryTakeForInstall(DeviceType.Gate), "second install takes the last unit");
            r.Check(!inv.TryTakeForInstall(DeviceType.Gate), "third install has nothing left");
            r.AreEqual(0, inv.Free(DeviceType.Gate), "Free is zero when everything is placed");

            inv.ReturnFromInstall(DeviceType.Gate);
            r.AreEqual(1, inv.Free(DeviceType.Gate), "a removal frees one unit");
            inv.ReturnFromInstall(DeviceType.Gate);
            inv.ReturnFromInstall(DeviceType.Gate);
            r.AreEqual(0, inv.Installed(DeviceType.Gate), "ReturnFromInstall clamps at zero");

            const int iterations = 300;
            bool held = true;
            int badSeed = -1;
            for (int seed = 0; seed < iterations; seed++)
            {
                var rng = new Random(seed + 7919);
                var bag = new DeviceInventory();
                int ops = rng.Next(1, 80);
                for (int i = 0; i < ops; i++)
                {
                    DeviceType device = DeviceTypes.All[rng.Next(DeviceTypes.All.Length)];
                    switch (rng.Next(3))
                    {
                        case 0:
                            bag.Grant(device, rng.Next(0, 4));
                            break;
                        case 1:
                            bag.TryTakeForInstall(device);
                            break;
                        default:
                            bag.ReturnFromInstall(device);
                            break;
                    }

                    for (int d = 0; d < DeviceTypes.All.Length; d++)
                    {
                        DeviceType check = DeviceTypes.All[d];
                        if (bag.Installed(check) < 0 ||
                            bag.Installed(check) > bag.Owned(check) ||
                            bag.Free(check) != bag.Owned(check) - bag.Installed(check))
                        {
                            held = false;
                            badSeed = seed;
                        }
                    }
                }
            }

            r.Check(held, "0 <= Installed <= Owned and Free == Owned - Installed over " +
                          iterations + " random sequences (seed " + badSeed + ")");
        }
    }
}
