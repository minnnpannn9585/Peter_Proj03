using System;
using NUnit.Framework;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Feature: stage1-hud-device-mission-system.
    /// Property 1: coin conservation and non-negativity.
    /// Property 2: owned count never exceeds the cap.
    /// Property 3: installed count never exceeds owned count.
    /// </summary>
    [TestFixture]
    public class MetaSystemTests
    {
        static DeviceCatalogConfig Catalog()
        {
            // Prices and caps come from the shipped level file, so a JSON edit is felt here too.
            return LevelFixtures.Level01Devices();
        }

        // ------------------------------------------------------------------ Wallet

        [Test]
        public void EarnRejectsNegativeAmountsAndLeavesTheBalanceAlone()
        {
            var wallet = new Wallet(20);
            Assert.Throws<ArgumentException>(() => wallet.Earn(-1));
            Assert.AreEqual(20, wallet.Balance);
        }

        [Test]
        public void SpendingExactlyTheBalanceSucceedsAndLandsOnZero()
        {
            var wallet = new Wallet(60);
            Assert.IsTrue(wallet.TrySpend(60));
            Assert.AreEqual(0, wallet.Balance);
        }

        [Test]
        public void SpendingMoreThanTheBalanceFailsWithoutChangingIt()
        {
            var wallet = new Wallet(59);
            Assert.IsFalse(wallet.TrySpend(60));
            Assert.AreEqual(59, wallet.Balance);
        }

        [Test]
        public void BalanceChangeIsRaisedSoTheHudCanUpdateInTheSameFrame()
        {
            var wallet = new Wallet(0);
            int seen = -1;
            wallet.BalanceChanged += value => seen = value;

            wallet.Earn(30);
            Assert.AreEqual(30, seen, "Earn notifies with the new balance");

            wallet.TrySpend(10);
            Assert.AreEqual(20, seen, "TrySpend notifies with the new balance");
        }

        [Test]
        public void Property01_CoinsAreConservedAndNeverNegative()
        {
            Property.ForAll("Property 1: coin conservation and non-negativity", (seed, trace) =>
            {
                int initial = trace.Rng.Next(0, 500);
                var wallet = new Wallet(initial);
                int earned = 0;
                int spent = 0;

                int ops = trace.Rng.Next(1, 60);
                for (int i = 0; i < ops; i++)
                {
                    if (trace.Rng.Next(2) == 0)
                    {
                        int amount = trace.Rng.Next(0, 200);
                        wallet.Earn(amount);
                        earned += amount;
                        trace.Log("Earn(" + amount + ") -> " + wallet.Balance);
                    }
                    else
                    {
                        int amount = trace.Rng.Next(0, 200);
                        int before = wallet.Balance;
                        bool ok = wallet.TrySpend(amount);
                        if (ok)
                        {
                            spent += amount;
                        }
                        else
                        {
                            trace.Require(wallet.Balance == before,
                                "a refused TrySpend changed the balance");
                        }

                        trace.Log("TrySpend(" + amount + ") = " + ok + " -> " + wallet.Balance);
                    }

                    trace.Require(wallet.Balance >= 0, "balance went negative");
                }

                trace.Require(wallet.Balance == initial + earned - spent,
                    "expected " + (initial + earned - spent) + ", got " + wallet.Balance);
            });
        }

        // ------------------------------------------------------------------ DeviceInventory

        [Test]
        public void FreeIsOwnedMinusInstalled()
        {
            var inventory = new DeviceInventory();
            inventory.Grant(DeviceType.Gate, 3);
            Assert.IsTrue(inventory.TryTakeForInstall(DeviceType.Gate));

            Assert.AreEqual(3, inventory.Owned(DeviceType.Gate));
            Assert.AreEqual(1, inventory.Installed(DeviceType.Gate));
            Assert.AreEqual(2, inventory.Free(DeviceType.Gate));
        }

        [Test]
        public void InstallingWithNothingFreeFails()
        {
            var inventory = new DeviceInventory();
            inventory.Grant(DeviceType.Scanner, 1);
            Assert.IsTrue(inventory.TryTakeForInstall(DeviceType.Scanner));
            Assert.IsFalse(inventory.TryTakeForInstall(DeviceType.Scanner),
                "there is nothing spare to place");
        }

        [Test]
        public void ReturningMoreThanWasInstalledClampsAtZero()
        {
            var inventory = new DeviceInventory();
            inventory.Grant(DeviceType.Booster, 1);
            inventory.TryTakeForInstall(DeviceType.Booster);

            inventory.ReturnFromInstall(DeviceType.Booster);
            inventory.ReturnFromInstall(DeviceType.Booster);
            inventory.ReturnFromInstall(DeviceType.Booster);

            Assert.AreEqual(0, inventory.Installed(DeviceType.Booster));
            Assert.AreEqual(1, inventory.Free(DeviceType.Booster));
        }

        [Test]
        public void Property03_InstalledNeverExceedsOwned()
        {
            Property.ForAll("Property 3: installed <= owned", (seed, trace) =>
            {
                var inventory = new DeviceInventory();
                int ops = trace.Rng.Next(1, 80);

                for (int i = 0; i < ops; i++)
                {
                    DeviceType device = DeviceTypes.All[trace.Rng.Next(DeviceTypes.All.Length)];
                    switch (trace.Rng.Next(3))
                    {
                        case 0:
                            int count = trace.Rng.Next(0, 4);
                            inventory.Grant(device, count);
                            trace.Log("Grant(" + device + ", " + count + ")");
                            break;
                        case 1:
                            trace.Log("TryTakeForInstall(" + device + ") = " +
                                      inventory.TryTakeForInstall(device));
                            break;
                        default:
                            inventory.ReturnFromInstall(device);
                            trace.Log("ReturnFromInstall(" + device + ")");
                            break;
                    }

                    for (int d = 0; d < DeviceTypes.All.Length; d++)
                    {
                        DeviceType check = DeviceTypes.All[d];
                        trace.Require(inventory.Installed(check) >= 0,
                            check + " installed went negative");
                        trace.Require(inventory.Installed(check) <= inventory.Owned(check),
                            check + " installed " + inventory.Installed(check) +
                            " exceeds owned " + inventory.Owned(check));
                        trace.Require(
                            inventory.Free(check) == inventory.Owned(check) - inventory.Installed(check),
                            check + " Free is out of step with Owned - Installed");
                    }
                }
            });
        }

        // ------------------------------------------------------------------ ShopService

        [Test]
        public void PricesAndCapsComeFromTheLevelFile()
        {
            var shop = new ShopService(new Wallet(0), new DeviceInventory(), Catalog());

            Assert.AreEqual(60, shop.Price(DeviceType.Gate));
            Assert.AreEqual(45, shop.Price(DeviceType.Booster));
            Assert.AreEqual(110, shop.Price(DeviceType.Scanner));
            Assert.AreEqual(180, shop.Price(DeviceType.AutoArm));

            Assert.AreEqual(4, shop.Cap(DeviceType.Gate));
            Assert.AreEqual(3, shop.Cap(DeviceType.Booster));
            Assert.AreEqual(2, shop.Cap(DeviceType.Scanner));
            Assert.AreEqual(1, shop.Cap(DeviceType.AutoArm));

            int total = 0;
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                total += shop.Cap(DeviceTypes.All[i]);
            }

            Assert.AreEqual(10, total, "the whole shop tops out at 10 devices");
        }

        [Test]
        public void ABuyDeductsAndGrantsTogether()
        {
            var wallet = new Wallet(100);
            var inventory = new DeviceInventory();
            var shop = new ShopService(wallet, inventory, Catalog());

            Assert.IsTrue(shop.CanBuy(DeviceType.Gate, out ShopError error));
            Assert.AreEqual(ShopError.None, error);
            Assert.IsTrue(shop.TryBuy(DeviceType.Gate));
            Assert.AreEqual(40, wallet.Balance);
            Assert.AreEqual(1, inventory.Owned(DeviceType.Gate));
        }

        [Test]
        public void CapReachedGreysOutTheDeviceWithoutCharging()
        {
            var wallet = new Wallet(100000);
            var inventory = new DeviceInventory();
            var shop = new ShopService(wallet, inventory, Catalog());

            for (int i = 0; i < shop.Cap(DeviceType.Scanner); i++)
            {
                Assert.IsTrue(shop.TryBuy(DeviceType.Scanner));
            }

            int balance = wallet.Balance;
            Assert.IsFalse(shop.CanBuy(DeviceType.Scanner, out ShopError error));
            Assert.AreEqual(ShopError.CapReached, error);
            Assert.IsFalse(shop.TryBuy(DeviceType.Scanner));
            Assert.AreEqual(balance, wallet.Balance, "a capped buy is free");
            Assert.AreEqual(2, inventory.Owned(DeviceType.Scanner));
        }

        [Test]
        public void NotEnoughCoinsFailsWithoutChangingAnything()
        {
            var wallet = new Wallet(59);
            var inventory = new DeviceInventory();
            var shop = new ShopService(wallet, inventory, Catalog());

            Assert.IsFalse(shop.CanBuy(DeviceType.Gate, out ShopError error));
            Assert.AreEqual(ShopError.NotEnoughCoins, error);
            Assert.IsFalse(shop.TryBuy(DeviceType.Gate));
            Assert.AreEqual(59, wallet.Balance);
            Assert.AreEqual(0, inventory.Owned(DeviceType.Gate));
        }

        [Test]
        public void BuyingOutsidePrepIsRefused()
        {
            GamePhase phase = GamePhase.Running;
            var wallet = new Wallet(1000);
            var inventory = new DeviceInventory();
            var shop = new ShopService(wallet, inventory, Catalog(), () => phase);

            Assert.IsFalse(shop.CanBuy(DeviceType.Gate, out ShopError error));
            Assert.AreEqual(ShopError.NotInPrep, error);
            Assert.IsFalse(shop.TryBuy(DeviceType.Gate));
            Assert.AreEqual(1000, wallet.Balance);

            phase = GamePhase.Result;
            Assert.IsFalse(shop.TryBuy(DeviceType.Gate), "the result screen is not a shop either");

            phase = GamePhase.Prep;
            Assert.IsTrue(shop.TryBuy(DeviceType.Gate), "and prep is");
        }

        [Test]
        public void ADeviceTheLevelDoesNotSellReportsUnknownDevice()
        {
            var shop = new ShopService(new Wallet(1000), new DeviceInventory(), new DeviceCatalogConfig());

            Assert.IsFalse(shop.CanBuy(DeviceType.Scanner, out ShopError error));
            Assert.AreEqual(ShopError.UnknownDevice, error);
            Assert.IsFalse(shop.IsSold(DeviceType.Scanner));
        }

        [Test]
        public void Property02_OwnedNeverExceedsTheCap()
        {
            Property.ForAll("Property 2: owned <= cap", (seed, trace) =>
            {
                var wallet = new Wallet(trace.Rng.Next(0, 900));
                var inventory = new DeviceInventory();
                var shop = new ShopService(wallet, inventory, Catalog());

                int ops = trace.Rng.Next(1, 40);
                for (int i = 0; i < ops; i++)
                {
                    DeviceType device = DeviceTypes.All[trace.Rng.Next(DeviceTypes.All.Length)];
                    int ownedBefore = inventory.Owned(device);
                    int balanceBefore = wallet.Balance;
                    bool bought = shop.TryBuy(device);
                    trace.Log("TryBuy(" + device + ") = " + bought + " -> " + wallet.Balance);

                    if (bought)
                    {
                        trace.Require(inventory.Owned(device) == ownedBefore + 1,
                            "a successful buy did not grant exactly one");
                        trace.Require(wallet.Balance == balanceBefore - shop.Price(device),
                            "a successful buy charged the wrong amount");
                    }
                    else
                    {
                        trace.Require(inventory.Owned(device) == ownedBefore,
                            "a failed buy still granted a device");
                        trace.Require(wallet.Balance == balanceBefore,
                            "a failed buy still charged the player");
                    }

                    for (int d = 0; d < DeviceTypes.All.Length; d++)
                    {
                        DeviceType check = DeviceTypes.All[d];
                        trace.Require(inventory.Owned(check) >= 0, check + " owned went negative");
                        trace.Require(inventory.Owned(check) <= shop.Cap(check),
                            check + " owned " + inventory.Owned(check) +
                            " exceeds cap " + shop.Cap(check));
                    }
                }
            });
        }
    }
}
