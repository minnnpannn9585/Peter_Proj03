using System;

namespace ParcelSort
{
    /// <summary>
    /// The player's coin balance. Deliberately a plain class, not a MonoBehaviour, so the
    /// money invariants can be property tested without an engine.
    ///
    /// Invariant: <see cref="Balance"/> is never negative, and the only ways to move money
    /// are <see cref="Earn"/> and <see cref="TrySpend"/>.
    /// </summary>
    public class Wallet
    {
        int balance;

        public Wallet(int initial)
        {
            balance = Math.Max(0, initial);
        }

        /// <summary>Raised after every balance change so the HUD can refresh in the same frame.</summary>
        public event Action<int> BalanceChanged;

        public int Balance => balance;

        /// <summary>Total ever earned. Only used by tests to assert coin conservation.</summary>
        public int TotalEarned { get; private set; }

        /// <summary>Total ever spent. Only used by tests to assert coin conservation.</summary>
        public int TotalSpent { get; private set; }

        /// <summary>Adds coins. Negative amounts are a programming error, not a refund path.</summary>
        public void Earn(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentException("Wallet.Earn cannot take a negative amount: " + amount,
                    nameof(amount));
            }

            if (amount == 0)
            {
                return;
            }

            balance += amount;
            TotalEarned += amount;
            BalanceChanged?.Invoke(balance);
        }

        /// <summary>
        /// Deducts coins when they are available. Returns false and leaves the balance
        /// untouched when they are not, so callers never have to unwind a partial spend.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentException("Wallet.TrySpend cannot take a negative amount: " + amount,
                    nameof(amount));
            }

            if (amount > balance)
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            balance -= amount;
            TotalSpent += amount;
            BalanceChanged?.Invoke(balance);
            return true;
        }

        /// <summary>Replaces the balance wholesale when a profile is loaded from disk.</summary>
        public void ResetTo(int value)
        {
            balance = Math.Max(0, value);
            TotalEarned = 0;
            TotalSpent = 0;
            BalanceChanged?.Invoke(balance);
        }
    }
}
