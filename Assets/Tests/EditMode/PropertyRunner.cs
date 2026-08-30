using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Minimal property-based test driver. Runs a body over many seeds and, on the first failure,
    /// reports the seed and the recorded operation log so the case can be replayed by hand.
    ///
    /// Hand written rather than pulled from a package: the project has no external test
    /// dependencies, and a seed plus an operation log is all the shrinking these properties need.
    /// </summary>
    public static class Property
    {
        /// <summary>Minimum iterations for every property test in this suite.</summary>
        public const int Iterations = 200;

        /// <summary>
        /// Runs <paramref name="body"/> once per seed. The body throws (via NUnit asserts or
        /// <see cref="Trace.Fail"/>) to signal a counterexample.
        /// </summary>
        public static void ForAll(string propertyName, Action<int, Trace> body, int iterations = Iterations)
        {
            for (int seed = 0; seed < iterations; seed++)
            {
                var trace = new Trace(seed);
                try
                {
                    body(seed, trace);
                }
                catch (Exception ex)
                {
                    Assert.Fail(
                        propertyName + " failed.\n" +
                        "  seed: " + seed + "\n" +
                        "  reason: " + ex.Message + "\n" +
                        "  operations:\n" + trace.Format());
                }
            }
        }

        /// <summary>Records the operations a single iteration performed, for failure output.</summary>
        public class Trace
        {
            readonly List<string> steps = new List<string>(64);

            public Trace(int seed)
            {
                Seed = seed;
                Rng = new Random(seed);
            }

            public int Seed { get; }

            public Random Rng { get; }

            public void Log(string step)
            {
                steps.Add(step);
            }

            /// <summary>Fails the iteration, which <see cref="ForAll"/> turns into a report.</summary>
            public void Fail(string reason)
            {
                throw new PropertyViolation(reason);
            }

            public void Require(bool condition, string reason)
            {
                if (!condition)
                {
                    Fail(reason);
                }
            }

            public string Format()
            {
                var builder = new StringBuilder();
                int start = Math.Max(0, steps.Count - 40);
                if (start > 0)
                {
                    builder.Append("    ... ").Append(start).Append(" earlier step(s) elided\n");
                }

                for (int i = start; i < steps.Count; i++)
                {
                    builder.Append("    ").Append(i).Append(": ").Append(steps[i]).Append('\n');
                }

                return builder.ToString();
            }
        }

        public class PropertyViolation : Exception
        {
            public PropertyViolation(string message)
                : base(message)
            {
            }
        }
    }
}
