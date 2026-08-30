using System;

namespace ParcelSort
{
    /// <summary>Why a purchase was refused. Also drives the shop card's feedback.</summary>
    public enum ShopError
    {
        None = 0,
        UnknownDevice = 1,
        CapReached = 2,
        NotEnoughCoins = 3,
        NotInPrep = 4
    }

    /// <summary>
    /// Validates and settles device purchases. <see cref="CanBuy"/> is the single decision
    /// point; <see cref="TryBuy"/> only ever acts on a <see cref="ShopError.None"/> verdict,
    /// and the coin deduction and the grant happen together or not at all.
    ///
    /// The phase is injected as a delegate so this class stays free of MonoBehaviour.
    /// </summary>
    public class ShopService
    {
        readonly Wallet wallet;
        readonly DeviceInventory inventory;
        readonly DeviceCatalogConfig catalog;
        readonly Func<GamePhase> phaseSource;

        public ShopService(
            Wallet wallet,
            DeviceInventory inventory,
            DeviceCatalogConfig catalog,
            Func<GamePhase> phaseSource = null)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            // No source means "always in prep", which is what the offline tests want.
            this.phaseSource = phaseSource ?? (() => GamePhase.Prep);
        }

        public Wallet Wallet => wallet;
        public DeviceInventory Inventory => inventory;
        public DeviceCatalogConfig Catalog => catalog;

        /// <summary>Price from the level config, or 0 when the level does not sell the device.</summary>
        public int Price(DeviceType device)
        {
            return catalog.TryGet(device, out DeviceSpec spec) ? spec.price : 0;
        }

        /// <summary>Ownership cap from the level config, or 0 when the device is not sold.</summary>
        public int Cap(DeviceType device)
        {
            return catalog.TryGet(device, out DeviceSpec spec) ? spec.cap : 0;
        }

        public bool IsSold(DeviceType device)
        {
            return catalog.TryGet(device, out _);
        }

        /// <summary>
        /// The only place a purchase is judged. Order matters for the UI: an unknown device
        /// suppresses the card entirely, a reached cap greys the button out, and only then is
        /// the balance considered.
        /// </summary>
        public bool CanBuy(DeviceType device, out ShopError error)
        {
            if (!catalog.TryGet(device, out DeviceSpec spec))
            {
                error = ShopError.UnknownDevice;
                return false;
            }

            if (phaseSource() != GamePhase.Prep)
            {
                error = ShopError.NotInPrep;
                return false;
            }

            if (inventory.Owned(device) >= spec.cap)
            {
                error = ShopError.CapReached;
                return false;
            }

            if (wallet.Balance < spec.price)
            {
                error = ShopError.NotEnoughCoins;
                return false;
            }

            error = ShopError.None;
            return true;
        }

        /// <summary>
        /// Buys one unit. Atomic: the spend is attempted only after every other check has
        /// passed, and the grant follows immediately, so no state changes on any failure path.
        /// </summary>
        public bool TryBuy(DeviceType device)
        {
            if (!CanBuy(device, out ShopError error) || error != ShopError.None)
            {
                return false;
            }

            // CanBuy already proved the balance covers this, so TrySpend cannot half-fail.
            if (!wallet.TrySpend(Price(device)))
            {
                return false;
            }

            inventory.Grant(device, 1);
            return true;
        }
    }
}
