using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ParcelSort.Tests
{
    /// <summary>
    /// Feature: stage1-hud-device-mission-system.
    /// Property 4: install slots are mutually exclusive - at most one device per (belt, slot) and
    /// per node slot, and IsSlotOccupied agrees with reality.
    /// Property 5: Boosters do not stack - DeviceSpeedBonus is 1.6 exactly when a belt carries one.
    ///
    /// These drive the real <see cref="InstallManager"/>, with a catalog assembled in the test
    /// rather than loaded from disk, so the actual accept/refuse logic is under test.
    /// </summary>
    [TestFixture]
    public class InstallTests
    {
        MiniYard yard;
        ModuleCatalog catalog;
        InstallManager installer;
        DeviceInventory inventory;
        LevelConfig config;
        BeltPath beltA;
        BeltPath beltB;
        YardNode diverter;
        readonly List<GameObject> prefabs = new List<GameObject>();
        GameObject installerHost;

        [SetUp]
        public void SetUp()
        {
            yard = new MiniYard();

            // A yard with two installable belts and one real diverter (two outputs).
            YardNode inlet = yard.AddNode("in_a", NodeType.Inlet, 2, 10);
            diverter = yard.AddNode("hub", NodeType.Junction, 8, 10);
            YardNode bayRed = yard.AddNode("bay_red", NodeType.Bay, 14, 10, DestinationColor.Red);
            YardNode bayBlue = yard.AddNode("bay_blue", NodeType.Bay, 14, 4, DestinationColor.Blue);

            beltA = yard.AddBelt("b_in_hub", inlet, diverter);
            beltB = yard.AddBelt("b_hub_red", diverter, bayRed, 0);
            yard.AddBelt("b_hub_blue", diverter, bayBlue, 1);

            bayRed.gameObject.AddComponent<TruckBay>().Configure(DestinationColor.Red, null);
            bayBlue.gameObject.AddComponent<TruckBay>().Configure(DestinationColor.Blue, null);

            catalog = BuildCatalog();

            // Device prices, caps and tuning come from the shipped level file.
            config = LevelFixtures.Level01Config();

            installerHost = new GameObject("InstallManager");
            installer = installerHost.AddComponent<InstallManager>();
            installer.SetCatalog(catalog);
            installer.SetPhaseSource(() => GamePhase.Prep);

            inventory = new DeviceInventory();
            inventory.Grant(DeviceType.Gate, 4);
            inventory.Grant(DeviceType.Booster, 3);
            inventory.Grant(DeviceType.Scanner, 2);
            inventory.Grant(DeviceType.AutoArm, 1);

            installer.BeginPrep(yard.Graph, config, inventory);
        }

        [TearDown]
        public void TearDown()
        {
            installer?.ClearInstalls();
            installer?.ClearMarkers();

            if (installerHost != null)
            {
                Object.DestroyImmediate(installerHost);
            }

            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null)
                {
                    Object.DestroyImmediate(prefabs[i]);
                }
            }

            prefabs.Clear();

            if (catalog != null)
            {
                Object.DestroyImmediate(catalog);
            }

            yard?.Dispose();
            yard = null;
        }

        ModuleCatalog BuildCatalog()
        {
            var made = ScriptableObject.CreateInstance<ModuleCatalog>();
            made.slotMarkerPrefab = Prefab<InstallSlotMarker>("SlotMarkerPrefab");
            made.nodeSlotMarkerPrefab = Prefab<NodeDeviceSlot>("NodeSlotPrefab");
            made.gatePrefab = Prefab<GateDevice>("GatePrefab");
            made.boosterPrefab = Prefab<BoosterDevice>("BoosterPrefab");
            made.scannerPrefab = Prefab<ScannerDevice>("ScannerPrefab");
            made.autoArmPrefab = Prefab<AutoArmDevice>("AutoArmPrefab");
            return made;
        }

        GameObject Prefab<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.AddComponent<T>();
            go.SetActive(false);
            prefabs.Add(go);
            return go;
        }

        InstallSlotMarker MarkerFor(BeltPath belt, int slotIndex)
        {
            IReadOnlyList<InstallSlotMarker> markers = installer.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Belt == belt && markers[i].SlotIndex == slotIndex)
                {
                    return markers[i];
                }
            }

            Assert.Fail("no marker for " + belt.BeltId + " slot " + slotIndex);
            return null;
        }

        NodeDeviceSlot NodeSlotFor(YardNode node)
        {
            IReadOnlyList<NodeDeviceSlot> slots = installer.NodeSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Node == node)
                {
                    return slots[i];
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ happy paths

        [Test]
        public void MarkersAreCreatedForInstallableBeltsAndDivertersOnly()
        {
            Assert.Greater(installer.Markers.Count, 0, "installable belts get slot markers");
            Assert.AreEqual(1, installer.NodeSlots.Count,
                "only the diverter gets a node slot; a single-exit junction has no lever");
            Assert.AreEqual(diverter, installer.NodeSlots[0].Node);
        }

        [Test]
        public void InstallingAGateConsumesStockAndOccupiesItsSlot()
        {
            Assert.IsTrue(installer.TryInstall(DeviceType.Gate, MarkerFor(beltA, 0)));

            Assert.AreEqual(1, inventory.Installed(DeviceType.Gate));
            Assert.AreEqual(3, inventory.Free(DeviceType.Gate));
            Assert.IsTrue(beltA.IsSlotOccupied(0));
            Assert.AreEqual(1, beltA.Gates.Count);
            Assert.AreEqual(1, installer.InstalledCount);
        }

        [Test]
        public void RemovingADeviceReturnsItToStockAndFreesTheSlot()
        {
            InstallSlotMarker marker = MarkerFor(beltA, 0);
            Assert.IsTrue(installer.TryInstall(DeviceType.Gate, marker));
            Assert.IsTrue(installer.TryRemoveAt(marker));

            Assert.AreEqual(0, inventory.Installed(DeviceType.Gate));
            Assert.AreEqual(4, inventory.Free(DeviceType.Gate));
            Assert.IsFalse(beltA.IsSlotOccupied(0));
            Assert.AreEqual(0, installer.InstalledCount);
        }

        [Test]
        public void AnAutoArmInstallsOnADiverterNode()
        {
            NodeDeviceSlot slot = NodeSlotFor(diverter);
            Assert.IsNotNull(slot);
            Assert.IsTrue(installer.TryInstall(DeviceType.AutoArm, slot));

            Assert.IsNotNull(diverter.AutoArm, "the node now has an arm");
            Assert.IsFalse(slot.IsFree);
            Assert.AreEqual(1, inventory.Installed(DeviceType.AutoArm));

            // The routing table must resolve both colours reachable through this diverter.
            Assert.IsTrue(diverter.AutoArm.Routing.ContainsKey(DestinationColor.Red));
            Assert.IsTrue(diverter.AutoArm.Routing.ContainsKey(DestinationColor.Blue));
            Assert.AreEqual(0, diverter.AutoArm.Routing[DestinationColor.Red]);
            Assert.AreEqual(1, diverter.AutoArm.Routing[DestinationColor.Blue]);
        }

        // ------------------------------------------------------------------ refusals

        [Test]
        public void InstallingOutsidePrepIsRefusedAndChangesNothing()
        {
            installer.SetPhaseSource(() => GamePhase.Running);
            InstallSlotMarker marker = MarkerFor(beltA, 0);

            Assert.IsFalse(installer.TryInstall(DeviceType.Gate, marker));
            Assert.AreEqual(0, inventory.Installed(DeviceType.Gate));
            Assert.IsFalse(beltA.IsSlotOccupied(0));

            installer.SetPhaseSource(() => GamePhase.Result);
            Assert.IsFalse(installer.TryInstall(DeviceType.Gate, marker));
            Assert.IsFalse(installer.TryRemoveAt(marker));
        }

        [Test]
        public void AnOccupiedSlotIsRefused()
        {
            InstallSlotMarker marker = MarkerFor(beltA, 0);
            Assert.IsTrue(installer.TryInstall(DeviceType.Gate, marker));

            int installedBefore = inventory.Installed(DeviceType.Gate);
            Assert.IsFalse(installer.TryInstall(DeviceType.Gate, marker),
                "a slot holds at most one device");
            Assert.AreEqual(installedBefore, inventory.Installed(DeviceType.Gate),
                "a refused install consumes no stock");
        }

        [Test]
        public void RunningOutOfFreeStockIsRefused()
        {
            var empty = new DeviceInventory();
            empty.Grant(DeviceType.Gate, 1);
            installer.BeginPrep(yard.Graph, config, empty);

            Assert.IsTrue(installer.TryInstall(DeviceType.Gate, MarkerFor(beltA, 0)));
            Assert.IsFalse(installer.TryInstall(DeviceType.Gate, MarkerFor(beltA, 1)),
                "everything owned is already on the yard");
            Assert.AreEqual(1, empty.Installed(DeviceType.Gate));
        }

        [Test]
        public void ABeltDeviceIsRefusedOnANodeSlotAndViceVersa()
        {
            NodeDeviceSlot slot = NodeSlotFor(diverter);

            Assert.IsFalse(installer.TryInstall(DeviceType.Gate, slot),
                "a Gate belongs on a belt slot");
            Assert.AreEqual(0, inventory.Installed(DeviceType.Gate));

            Assert.IsFalse(installer.TryInstall(DeviceType.AutoArm, MarkerFor(beltA, 0)),
                "an AutoArm belongs on a node");
            Assert.AreEqual(0, inventory.Installed(DeviceType.AutoArm));
            Assert.IsFalse(beltA.IsSlotOccupied(0));
        }

        [Test]
        public void ABeltThatForbidsInstallsHasNoMarkersAndRefusesDirectly()
        {
            YardNode from = yard.AddNode("x_from", NodeType.Junction, 2, 20);
            YardNode to = yard.AddNode("x_to", NodeType.Bay, 8, 20, DestinationColor.Green);
            BeltPath locked = yard.AddBelt("b_locked", from, to, 0, false);

            Assert.AreEqual(0, locked.SlotCount, "a no-install belt generates no slots");

            installer.RefreshMarkers();
            IReadOnlyList<InstallSlotMarker> markers = installer.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                Assert.AreNotEqual(locked, markers[i].Belt,
                    "no marker should exist on a no-install belt");
            }
        }

        [Test]
        public void ASecondBoosterOnTheSameBeltIsRefused()
        {
            Assert.IsTrue(installer.TryInstall(DeviceType.Booster, MarkerFor(beltA, 0)));
            Assert.AreEqual(1.6f, beltA.DeviceSpeedBonus, 1e-4f);

            int installedBefore = inventory.Installed(DeviceType.Booster);
            Assert.IsFalse(installer.TryInstall(DeviceType.Booster, MarkerFor(beltA, 1)),
                "boosters must not stack on one belt");
            Assert.AreEqual(installedBefore, inventory.Installed(DeviceType.Booster));
            Assert.IsFalse(beltA.IsSlotOccupied(1));
            Assert.AreEqual(1.6f, beltA.DeviceSpeedBonus, 1e-4f, "the bonus is unchanged");

            // A different belt is fine: the cap is per belt, not global.
            Assert.IsTrue(installer.TryInstall(DeviceType.Booster, MarkerFor(beltB, 0)));
            Assert.AreEqual(1.6f, beltB.DeviceSpeedBonus, 1e-4f);
        }

        [Test]
        public void AnAutoArmIsRefusedOnANodeWithOneExit()
        {
            YardNode from = yard.AddNode("y_from", NodeType.Junction, 2, 22);
            YardNode to = yard.AddNode("y_to", NodeType.Bay, 8, 22, DestinationColor.Green);
            yard.AddBelt("b_single", from, to, 0);

            installer.RefreshMarkers();
            Assert.IsNull(NodeSlotFor(from),
                "a single-exit junction never even offers a node slot");
        }

        // ------------------------------------------------------------------ Property 4 and 5

        [Test]
        public void Property04And05_SlotExclusivityAndBoosterNonStacking()
        {
            var belts = new List<BeltPath> { beltA, beltB };

            Property.ForAll("Properties 4 and 5: slot exclusivity, booster non-stacking",
                (seed, trace) =>
                {
                    installer.ClearInstalls();
                    installer.BeginPrep(yard.Graph, config, inventory);

                    int ops = trace.Rng.Next(1, 40);
                    for (int i = 0; i < ops; i++)
                    {
                        BeltPath belt = belts[trace.Rng.Next(belts.Count)];
                        if (belt.SlotCount == 0)
                        {
                            continue;
                        }

                        int slotIndex = trace.Rng.Next(belt.SlotCount);
                        InstallSlotMarker marker = MarkerFor(belt, slotIndex);

                        if (trace.Rng.Next(3) == 0)
                        {
                            bool removed = installer.TryRemoveAt(marker);
                            trace.Log("remove " + belt.BeltId + "#" + slotIndex + " = " + removed);
                        }
                        else
                        {
                            DeviceType device = trace.Rng.Next(2) == 0
                                ? DeviceType.Booster
                                : (trace.Rng.Next(2) == 0 ? DeviceType.Gate : DeviceType.Scanner);
                            bool ok = installer.TryInstall(device, marker);
                            trace.Log("install " + device + " at " + belt.BeltId + "#" + slotIndex +
                                      " = " + ok);
                        }

                        CheckInvariants(trace, belts);
                    }
                }, Property.Iterations);
        }

        void CheckInvariants(Property.Trace trace, List<BeltPath> belts)
        {
            List<InstallRecord> snapshot = installer.Snapshot();

            // Property 4: no two devices share a slot, and the belt's own occupancy flags agree.
            var seen = new HashSet<string>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                InstallRecord record = snapshot[i];
                string key = record.target == InstallTargetKind.Node
                    ? "node:" + record.nodeId
                    : "belt:" + record.beltId + "#" + record.slotIndex;

                trace.Require(seen.Add(key), "two devices occupy " + key);
            }

            for (int b = 0; b < belts.Count; b++)
            {
                BeltPath belt = belts[b];
                for (int s = 0; s < belt.SlotCount; s++)
                {
                    bool claimed = seen.Contains("belt:" + belt.BeltId + "#" + s);
                    trace.Require(belt.IsSlotOccupied(s) == claimed,
                        belt.BeltId + "#" + s + " occupancy flag says " + belt.IsSlotOccupied(s) +
                        " but the snapshot says " + claimed);
                }

                // Property 5: the bonus is 1.6 exactly when one Booster sits on the belt.
                int boosters = 0;
                for (int i = 0; i < snapshot.Count; i++)
                {
                    if (snapshot[i].device == DeviceType.Booster &&
                        snapshot[i].target == InstallTargetKind.BeltSlot &&
                        snapshot[i].beltId == belt.BeltId)
                    {
                        boosters++;
                    }
                }

                trace.Require(boosters <= 1, belt.BeltId + " carries " + boosters + " boosters");
                trace.Require(
                    Mathf.Approximately(belt.DeviceSpeedBonus, boosters == 1 ? 1.6f : 1f),
                    belt.BeltId + " bonus " + belt.DeviceSpeedBonus + " with " + boosters +
                    " booster(s)");
            }

            // Property 3 again, this time through the real install path.
            for (int d = 0; d < DeviceTypes.All.Length; d++)
            {
                DeviceType device = DeviceTypes.All[d];
                trace.Require(inventory.Installed(device) <= inventory.Owned(device),
                    device + " installed exceeds owned");
            }
        }

        // ------------------------------------------------------------------ snapshot / restore

        [Test]
        public void SnapshotAddressesInstallsByLevelBeltAndSlot()
        {
            installer.TryInstall(DeviceType.Gate, MarkerFor(beltA, 0));
            installer.TryInstall(DeviceType.AutoArm, NodeSlotFor(diverter));

            List<InstallRecord> snapshot = installer.Snapshot();
            Assert.AreEqual(2, snapshot.Count);

            InstallRecord gate = snapshot.Find(r => r.device == DeviceType.Gate);
            Assert.AreEqual(config.id, gate.levelId, "installs are bound to their level");
            Assert.AreEqual("b_in_hub", gate.beltId);
            Assert.AreEqual(0, gate.slotIndex);
            Assert.AreEqual(InstallTargetKind.BeltSlot, gate.target);

            InstallRecord arm = snapshot.Find(r => r.device == DeviceType.AutoArm);
            Assert.AreEqual("hub", arm.nodeId);
            Assert.AreEqual(InstallTargetKind.Node, arm.target);
        }

        [Test]
        public void RestoreRebuildsALayoutExactly()
        {
            installer.TryInstall(DeviceType.Gate, MarkerFor(beltA, 0));
            installer.TryInstall(DeviceType.Scanner, MarkerFor(beltA, 1));
            installer.TryInstall(DeviceType.AutoArm, NodeSlotFor(diverter));
            List<InstallRecord> saved = installer.Snapshot();

            // Tear the yard's devices down the way a level reload would.
            installer.ClearInstalls();
            Assert.AreEqual(0, installer.InstalledCount);
            Assert.AreEqual(0, inventory.Installed(DeviceType.Gate));

            installer.BeginPrep(yard.Graph, config, inventory);
            installer.Restore(saved);

            Assert.AreEqual(3, installer.InstalledCount);
            Assert.IsTrue(beltA.IsSlotOccupied(0));
            Assert.IsTrue(beltA.IsSlotOccupied(1));
            Assert.IsNotNull(diverter.AutoArm);
            Assert.AreEqual(1, inventory.Installed(DeviceType.Gate));
            Assert.AreEqual(1, inventory.Installed(DeviceType.Scanner));
            Assert.AreEqual(1, inventory.Installed(DeviceType.AutoArm));
        }

        [Test]
        public void RestoreSkipsStaleRecordsAndReturnsTheirDevicesToStock()
        {
            var stale = new List<InstallRecord>
            {
                // Belt that no longer exists.
                new InstallRecord
                {
                    levelId = config.id, device = DeviceType.Gate,
                    target = InstallTargetKind.BeltSlot, beltId = "b_gone", slotIndex = 0
                },
                // Slot index past the end of a real belt.
                new InstallRecord
                {
                    levelId = config.id, device = DeviceType.Gate,
                    target = InstallTargetKind.BeltSlot, beltId = "b_in_hub", slotIndex = 999
                },
                // Node that no longer exists.
                new InstallRecord
                {
                    levelId = config.id, device = DeviceType.AutoArm,
                    target = InstallTargetKind.Node, nodeId = "node_gone"
                },
                // A different level's install must be ignored, not skipped-with-a-warning.
                new InstallRecord
                {
                    levelId = "level_99", device = DeviceType.Gate,
                    target = InstallTargetKind.BeltSlot, beltId = "b_in_hub", slotIndex = 0
                },
                // One good record, to prove the rest still load.
                new InstallRecord
                {
                    levelId = config.id, device = DeviceType.Scanner,
                    target = InstallTargetKind.BeltSlot, beltId = "b_in_hub", slotIndex = 2
                }
            };

            installer.Restore(stale);

            Assert.AreEqual(1, installer.InstalledCount, "only the resolvable record was rebuilt");
            Assert.AreEqual(0, inventory.Installed(DeviceType.Gate),
                "no ghost gate is holding stock");
            Assert.AreEqual(0, inventory.Installed(DeviceType.AutoArm));
            Assert.AreEqual(1, inventory.Installed(DeviceType.Scanner));
            Assert.AreEqual(4, inventory.Free(DeviceType.Gate), "gate stock is fully available again");
            Assert.IsFalse(beltA.IsSlotOccupied(0), "and no slot was left falsely reserved");
        }

        [Test]
        public void ClearInstallsHandsEverythingBackToStock()
        {
            installer.TryInstall(DeviceType.Gate, MarkerFor(beltA, 0));
            installer.TryInstall(DeviceType.Booster, MarkerFor(beltB, 0));

            installer.ClearInstalls();

            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                DeviceType device = DeviceTypes.All[i];
                Assert.AreEqual(inventory.Owned(device), inventory.Free(device),
                    device + " should be fully available after a teardown");
            }

            Assert.AreEqual(1f, beltB.DeviceSpeedBonus, 1e-4f,
                "and the belt's boost is undone");
        }
    }
}
