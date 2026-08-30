using System;

namespace ParcelSort.Offline
{
    /// <summary>Property 1: coin conservation and non-negativity.</summary>
    public static class WalletChecks
    {
        public static void Run(CheckRunner r)
        {
            r.Section("Wallet (Property 1)");

            var wallet = new Wallet(20);
            r.AreEqual(20, wallet.Balance, "starts at the seeded balance");

            r.Throws<ArgumentException>(() => wallet.Earn(-1), "Earn rejects a negative amount");
            r.AreEqual(20, wallet.Balance, "a rejected Earn leaves the balance alone");

            r.Check(!wallet.TrySpend(21), "TrySpend over balance returns false");
            r.AreEqual(20, wallet.Balance, "a refused TrySpend leaves the balance alone");

            r.Check(wallet.TrySpend(20), "TrySpend of the exact balance succeeds");
            r.AreEqual(0, wallet.Balance, "spending everything lands on zero");

            // Property 1 over random operation sequences.
            const int iterations = 300;
            bool conserved = true;
            bool nonNegative = true;
            int badSeed = -1;
            for (int seed = 0; seed < iterations; seed++)
            {
                var rng = new Random(seed);
                int initial = rng.Next(0, 500);
                var w = new Wallet(initial);
                int earned = 0;
                int spent = 0;

                int ops = rng.Next(1, 60);
                for (int i = 0; i < ops; i++)
                {
                    if (rng.Next(2) == 0)
                    {
                        int amount = rng.Next(0, 200);
                        w.Earn(amount);
                        earned += amount;
                    }
                    else
                    {
                        int amount = rng.Next(0, 200);
                        int before = w.Balance;
                        if (w.TrySpend(amount))
                        {
                            spent += amount;
                        }
                        else if (w.Balance != before)
                        {
                            conserved = false;
                            badSeed = seed;
                        }
                    }

                    if (w.Balance < 0)
                    {
                        nonNegative = false;
                        badSeed = seed;
                    }
                }

                if (w.Balance != initial + earned - spent)
                {
                    conserved = false;
                    badSeed = seed;
                }
            }

            r.Check(conserved, "final == initial + earned - spent over " + iterations +
                               " random sequences (seed " + badSeed + ")");
            r.Check(nonNegative, "balance never goes negative (seed " + badSeed + ")");
        }
    }
}
