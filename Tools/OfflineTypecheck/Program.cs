using System;
using System.Collections.Generic;

namespace ParcelSort.Offline
{
    /// <summary>
    /// Entry point for the offline harness. Runs every check and reports a summary.
    /// See README.md for what this does and does not prove.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            bool verbose = Array.IndexOf(args, "--verbose") >= 0;
            var runner = new CheckRunner(verbose);

            WalletChecks.Run(runner);
            InventoryChecks.Run(runner);
            ShopChecks.Run(runner);
            JsonWriterChecks.Run(runner);
            ProfileChecks.Run(runner);
            MissionRuntimeChecks.Run(runner);
            SpawnScheduleChecks.Run(runner);
            LevelChecks.Run(runner);
            BalanceChecks.Run(runner);
            FeasibilityChecks.Run(runner);

            return runner.Report();
        }
    }

    /// <summary>Very small assertion collector so a failure names the check that broke.</summary>
    public class CheckRunner
    {
        readonly List<string> failures = new List<string>();
        readonly bool verbose;
        string section = "(none)";
        int passed;

        public CheckRunner(bool verbose)
        {
            this.verbose = verbose;
        }

        public void Section(string name)
        {
            section = name;
            Console.WriteLine();
            Console.WriteLine("== " + name);
        }

        public void Check(bool condition, string description)
        {
            if (condition)
            {
                passed++;
                if (verbose)
                {
                    Console.WriteLine("   ok   " + description);
                }

                return;
            }

            failures.Add(section + ": " + description);
            Console.WriteLine("   FAIL " + description);
        }

        public void AreEqual(int expected, int actual, string description)
        {
            Check(expected == actual, description + " (expected " + expected + ", got " + actual + ")");
        }

        public void AreEqual(string expected, string actual, string description)
        {
            Check(string.Equals(expected, actual),
                description + " (expected '" + expected + "', got '" + actual + "')");
        }

        public void AreClose(float expected, float actual, float tolerance, string description)
        {
            Check(Math.Abs(expected - actual) <= tolerance,
                description + " (expected " + expected + " +/- " + tolerance + ", got " + actual + ")");
        }

        public void Throws<T>(Action action, string description) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                Check(true, description);
                return;
            }
            catch (Exception ex)
            {
                Check(false, description + " (threw " + ex.GetType().Name + " instead)");
                return;
            }

            Check(false, description + " (did not throw)");
        }

        public void Info(string message)
        {
            Console.WriteLine("   ..   " + message);
        }

        public int Report()
        {
            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------");
            if (failures.Count == 0)
            {
                Console.WriteLine("offline checks: " + passed + " passed, 0 failed");
                Console.WriteLine();
                Console.WriteLine("NOTE: this harness covers non-MonoBehaviour logic only.");
                Console.WriteLine("      It is NOT a Unity compile and NOT the Unity Test Runner.");
                return 0;
            }

            Console.WriteLine("offline checks: " + passed + " passed, " + failures.Count + " FAILED");
            for (int i = 0; i < failures.Count; i++)
            {
                Console.WriteLine("  - " + failures[i]);
            }

            return 1;
        }
    }
}
