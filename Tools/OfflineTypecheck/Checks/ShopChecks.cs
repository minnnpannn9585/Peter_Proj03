using System;

namespace ParcelSort.Offline
{
    /// <summary>Property 2: owned count never exceeds the cap; every ShopError branch.</summary>
    public static class ShopChecks
    {
        static DeviceCatalogConfig Catalog()
        {
            var catalog = new DeviceCatalogConfig();
            catalog.Add(new DeviceSpec { device = DeviceType.Gate, price = 60, cap = 4 });
            catalog.Add(new DeviceSpec { device = DeviceType.Booster, price = 45, cap = 3 });
            catalog.Add(new DeviceSpec { device = DeviceType.Scanner, price = 110, cap = 2 });
            catalog.Add(new DeviceSpec
            {
                device = DeviceType.AutoArm, price = 180, cap = 1, target = InstallTargetKind.Node
            });
            return catalog;
        }

        public static void Run(CheckRunner r)
        {
            r.Section("ShopService (Property 2)");

            GamePhase phase = GamePhase.Prep;
            var wallet = new Wallet(200);
            var inventory = new DeviceInventory();
            var shop = new ShopService(wallet, inventory, Catalog(), () => phase);

            // ShopError.None
            r.Check(shop.CanBuy(DeviceType.Gate, out ShopError err) && err == ShopError.None,
                "an affordable device under cap is buyable");
            r.Check(shop.TryBuy(DeviceType.Gate), "buying succeeds");
            r.AreEqual(140, wallet.Balance, "the price was deducted");
            r.AreEqual(1, inventory.Owned(DeviceType.Gate), "the device was granted");

            // ShopError.NotEnoughCoins
            var poor = new Wallet(10);
            var poorInv = new DeviceInventory();
            var poorShop = new ShopService(poor, poorInv, Catalog(), () => GamePhase.Prep);
            poorShop.CanBuy(DeviceType.Gate, out ShopError poorErr);
            r.Check(poorErr == ShopError.NotEnoughCoins, "an unaffordable device reports NotEnoughCoins");
            r.Check(!poorShop.TryBuy(DeviceType.Gate), "an unaffordable buy fails");
            r.AreEqual(10, poor.Balance, "a failed buy leaves the balance alone");
            r.AreEqual(0, poorInv.Owned(DeviceType.Gate), "a failed buy grants nothing");

            // ShopError.CapReached
            var rich = new Wallet(100000);
            var richInv = new DeviceInventory();
            var richShop = new ShopService(rich, richInv, Catalog(), () => GamePhase.Prep);
            for (int i = 0; i < 4; i++)
            {
                richShop.TryBuy(DeviceType.Gate);
            }

            richShop.CanBuy(DeviceType.Gate, out ShopError capErr);
            r.Check(capErr == ShopError.CapReached, "a maxed device reports CapReached");
            int balanceAtCap = rich.Balance;
            r.Check(!richShop.TryBuy(DeviceType.Gate), "buying past the cap fails");
            r.AreEqual(4, richInv.Owned(DeviceType.Gate), "the cap holds at 4");
            r.AreEqual(balanceAtCap, rich.Balance, "a capped buy costs nothing");

            // ShopError.NotInPrep
            phase = GamePhase.Running;
            shop.CanBuy(DeviceType.Booster, out ShopError phaseErr);
            r.Check(phaseErr == ShopError.NotInPrep, "buying outside prep reports NotInPrep");
            r.Check(!shop.TryBuy(DeviceType.Booster), "buying outside prep fails");
            phase = GamePhase.Prep;

            // ShopError.UnknownDevice
            var emptyShop = new ShopService(
                new Wallet(1000), new DeviceInventory(), new DeviceCatalogConfig(), () => GamePhase.Prep);
            emptyShop.CanBuy(DeviceType.Scanner, out ShopError unknownErr);
            r.Check(unknownErr == ShopError.UnknownDevice,
                "a device the level does not sell reports UnknownDevice");

            // Property 2 over random buy sequences.
            const int iterations = 200;
            bool capHeld = true;
            bool atomic = true;
            int badSeed = -1;
            for (int seed = 0; seed < iterations; seed++)
            {
                var rng = new Random(seed + 4242);
                var w = new Wallet(rng.Next(0, 900));
                var inv = new DeviceInventory();
                DeviceCatalogConfig cat = Catalog();
                var s = new ShopService(w, inv, cat, () => GamePhase.Prep);

                int ops = rng.Next(1, 40);
                for (int i = 0; i < ops; i++)
                {
                    DeviceType device = DeviceTypes.All[rng.Next(DeviceTypes.All.Length)];
                    int ownedBefore = inv.Owned(device);
                    int balanceBefore = w.Balance;
                    bool bought = s.TryBuy(device);

                    if (bought)
                    {
                        if (inv.Owned(device) != ownedBefore + 1 ||
                            w.Balance != balanceBefore - s.Price(device))
                        {
                            atomic = false;
                            badSeed = seed;
                        }
                    }
                    else if (inv.Owned(device) != ownedBefore || w.Balance != balanceBefore)
                    {
                        atomic = false;
                        badSeed = seed;
                    }

                    for (int d = 0; d < DeviceTypes.All.Length; d++)
                    {
                        DeviceType check = DeviceTypes.All[d];
                        if (inv.Owned(check) < 0 || inv.Owned(check) > s.Cap(check))
                        {
                            capHeld = false;
                            badSeed = seed;
                        }
                    }

                }
            }

            r.Check(capHeld, "0 <= Owned <= Cap over " + iterations +
                             " random buy sequences (seed " + badSeed + ")");
            r.Check(atomic, "every buy is all-or-nothing (seed " + badSeed + ")");
        }
    }
}
