using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Feature: stage1-hud-device-mission-system,
    /// Property 11: revealing is one-way and irreversible - Reveal() is idempotent, isRevealed only
    /// ever goes false to true, and ScannerDevice.RevealedCount never decreases.
    ///
    /// This matters because the whole Scanner purchase rests on the player being able to trust a
    /// revealed parcel. A single path that hid a parcel again would make the device worthless.
    /// </summary>
    [TestFixture]
    public class RevealTests
    {
        MiniYard yard;

        [TearDown]
        public void TearDown()
        {
            yard?.Dispose();
            yard = null;
        }

        static Parcel MakeParcel(bool blind)
        {
            var go = new GameObject("Parcel");
            Parcel parcel = go.AddComponent<Parcel>();
            parcel.Initialize(new ParcelData
            {
                destination = DestinationColor.Blue,
                isRevealed = !blind,
                size = ParcelSize.Small
            });

            return parcel;
        }

        [Test]
        public void ABlindParcelStartsUnrevealed()
        {
            Parcel parcel = MakeParcel(true);
            try
            {
                Assert.IsFalse(parcel.IsRevealed);
            }
            finally
            {
                Object.DestroyImmediate(parcel.gameObject);
            }
        }

        [Test]
        public void ANormalParcelStartsRevealed()
        {
            Parcel parcel = MakeParcel(false);
            try
            {
                Assert.IsTrue(parcel.IsRevealed);
            }
            finally
            {
                Object.DestroyImmediate(parcel.gameObject);
            }
        }

        [Test]
        public void RevealIsIdempotentAndOneWay()
        {
            Parcel parcel = MakeParcel(true);
            try
            {
                parcel.Reveal();
                Assert.IsTrue(parcel.IsRevealed);

                for (int i = 0; i < 20; i++)
                {
                    parcel.Reveal();
                    Assert.IsTrue(parcel.IsRevealed, "Reveal must never un-reveal");
                }
            }
            finally
            {
                Object.DestroyImmediate(parcel.gameObject);
            }
        }

        [Test]
        public void Property11_RandomRevealSequencesNeverGoBackwards()
        {
            Property.ForAll("Property 11: reveal is one-way", (seed, trace) =>
            {
                Parcel parcel = MakeParcel(trace.Rng.Next(2) == 0);
                try
                {
                    bool everRevealed = parcel.IsRevealed;
                    int calls = trace.Rng.Next(1, 40);

                    for (int i = 0; i < calls; i++)
                    {
                        if (trace.Rng.Next(2) == 0)
                        {
                            parcel.Reveal();
                            everRevealed = true;
                            trace.Log("Reveal()");
                        }
                        else
                        {
                            parcel.ApplyVisual();
                            trace.Log("ApplyVisual()");
                        }

                        trace.Require(parcel.IsRevealed == everRevealed || parcel.IsRevealed,
                            "isRevealed regressed to false");

                        if (everRevealed)
                        {
                            trace.Require(parcel.IsRevealed,
                                "a revealed parcel became hidden again");
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(parcel.gameObject);
                }
            });
        }

        [Test]
        public void NoProductionCodePathWritesIsRevealedBackToFalse()
        {
            // A static guard, because this invariant is easier to break by editing an unrelated
            // file than by breaking any single test. Initialisation from a spawn flag is allowed;
            // a bare assignment of false is not.
            string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            Assert.IsTrue(Directory.Exists(scriptsRoot), "expected " + scriptsRoot);

            var offenders = new System.Collections.Generic.List<string>();
            var bareFalse = new Regex(@"isRevealed\s*=\s*false");

            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string[] lines = File.ReadAllLines(files[i]);
                for (int line = 0; line < lines.Length; line++)
                {
                    if (bareFalse.IsMatch(lines[line]))
                    {
                        offenders.Add(Path.GetFileName(files[i]) + ":" + (line + 1) + "  " +
                                      lines[line].Trim());
                    }
                }
            }

            Assert.IsEmpty(offenders,
                "isRevealed must only ever move false -> true. Offending write(s):\n  " +
                string.Join("\n  ", offenders) +
                "\nUse `isRevealed = !blind` at construction instead of assigning false later.");
        }

        // ------------------------------------------------------------------ ScannerDevice

        [Test]
        public void AScannerRevealsParcelsThatPassItAndCountsThem()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            ScannerDevice scanner = yard.InstallScanner(belt, 1);
            yard.SetRunning(true);

            ParcelRuntime parcel = yard.Emit(belt, DestinationColor.Blue, true);
            Assert.IsNotNull(parcel);
            Assert.IsFalse(parcel.Parcel.IsRevealed, "it starts blind");
            Assert.Less(parcel.Distance, scanner.Distance, "and it starts upstream of the scanner");

            // Not yet past the head.
            yard.Tick(1f / 60f);
            Assert.IsFalse(parcel.Parcel.IsRevealed);
            Assert.AreEqual(0, scanner.RevealedCount);

            // Carry it past.
            yard.Run(2f);
            Assert.Greater(parcel.Distance, scanner.Distance);
            Assert.IsTrue(parcel.Parcel.IsRevealed, "passing the scanner reveals it");
            Assert.AreEqual(1, scanner.RevealedCount);
        }

        [Test]
        public void AScannerIgnoresParcelsOnOtherBelts()
        {
            yard = new MiniYard();
            YardNode inlet = yard.AddNode("in_a", NodeType.Inlet, 2, 10);
            YardNode mid = yard.AddNode("mid", NodeType.Junction, 8, 10);
            YardNode bay = yard.AddNode("bay", NodeType.Bay, 14, 10, DestinationColor.Blue);

            BeltPath first = yard.AddBelt("b_first", inlet, mid);
            BeltPath second = yard.AddBelt("b_second", mid, bay);

            ScannerDevice scanner = yard.InstallScanner(second, 0);
            yard.SetRunning(true);

            ParcelRuntime parcel = yard.Emit(first, DestinationColor.Blue, true);
            yard.Run(1f);

            Assert.IsFalse(parcel.Parcel.IsRevealed,
                "a scanner downstream on another belt has not touched it yet");
            Assert.AreEqual(0, scanner.RevealedCount);

            // Once it hands off and travels past the head, it is revealed. This is exactly why
            // placement matters more than quantity.
            yard.Run(6f);
            Assert.IsTrue(parcel.Parcel.IsRevealed);
            Assert.AreEqual(1, scanner.RevealedCount);
        }

        [Test]
        public void RevealedCountIsMonotonic()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            ScannerDevice scanner = yard.InstallScanner(belt, 0);
            yard.SetRunning(true);

            int last = 0;
            for (int i = 0; i < 12; i++)
            {
                yard.Emit(belt, DestinationColor.Blue, true);
                yard.Run(0.6f);
                Assert.GreaterOrEqual(scanner.RevealedCount, last,
                    "RevealedCount must never decrease");
                last = scanner.RevealedCount;
            }

            Assert.Greater(scanner.RevealedCount, 0, "the scanner did some work");
        }

        [Test]
        public void RemovingAScannerUnregistersItFromTheBelt()
        {
            yard = new MiniYard();
            BeltPath belt = yard.BuildStraightLine(6);
            ScannerDevice scanner = yard.InstallScanner(belt, 0);

            Assert.AreEqual(1, belt.Scanners.Count);
            Assert.IsTrue(belt.IsSlotOccupied(0));

            Object.DestroyImmediate(scanner.gameObject);

            Assert.AreEqual(0, belt.Scanners.Count);
            Assert.IsFalse(belt.IsSlotOccupied(0));
        }

        [Test]
        public void TwoScannersOnTheInletFeedsRevealEverythingBeforeTheFirstDiverter()
        {
            // The M3 argument in miniature: a scanner on each inlet feed means nothing reaches a
            // decision point still blind.
            yard = new MiniYard();
            YardNode inletA = yard.AddNode("in_a", NodeType.Inlet, 2, 20);
            YardNode inletB = yard.AddNode("in_b", NodeType.Inlet, 2, 4);
            YardNode hubA = yard.AddNode("hub_a", NodeType.Junction, 8, 20);
            YardNode hubB = yard.AddNode("hub_b", NodeType.Junction, 8, 4);
            YardNode bayA = yard.AddNode("bay_a", NodeType.Bay, 14, 20, DestinationColor.Red);
            YardNode bayB = yard.AddNode("bay_b", NodeType.Bay, 14, 4, DestinationColor.Blue);

            BeltPath feedA = yard.AddBelt("b_feed_a", inletA, hubA);
            BeltPath feedB = yard.AddBelt("b_feed_b", inletB, hubB);
            yard.AddBelt("b_out_a", hubA, bayA);
            yard.AddBelt("b_out_b", hubB, bayB);

            yard.InstallScanner(feedA, 0);
            yard.InstallScanner(feedB, 0);
            yard.SetRunning(true);

            var blind = new System.Collections.Generic.List<ParcelRuntime>();
            for (int i = 0; i < 4; i++)
            {
                ParcelRuntime a = yard.Emit(feedA, DestinationColor.Red, true);
                ParcelRuntime b = yard.Emit(feedB, DestinationColor.Blue, true);
                if (a != null)
                {
                    blind.Add(a);
                }

                if (b != null)
                {
                    blind.Add(b);
                }

                yard.Run(0.8f);
            }

            yard.Run(4f);

            int stillBlind = 0;
            for (int i = 0; i < blind.Count; i++)
            {
                if (blind[i] != null && blind[i].Parcel != null && !blind[i].Parcel.IsRevealed)
                {
                    stillBlind++;
                }
            }

            Assert.AreEqual(0, stillBlind,
                "with a scanner on each inlet feed, nothing should still be blind downstream");
        }
    }
}
