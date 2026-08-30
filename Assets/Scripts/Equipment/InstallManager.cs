using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Owns the prep phase drop targets and every device the player has placed on the yard.
    ///
    /// Two rules shape this class. First, a refused install changes nothing at all - no stock is
    /// consumed, no slot is reserved, no object is created - so the player can probe freely.
    /// Second, placements are recorded against the level id, so a layout survives a round, a
    /// retry and a restart, which is what makes "arranging the yard" a lasting investment.
    /// </summary>
    public class InstallManager : MonoBehaviour
    {
        const float ScreenPickRadius = 90f;

        /// <summary>
        /// Pixels the pointer must travel before a press on a placed device counts as a move.
        /// Without this, a plain click would snap the device onto whatever free slot happened to
        /// be nearest, which is a surprising way to lose a careful placement.
        /// </summary>
        const float MoveDragThreshold = 14f;

        /// <summary>A device the player has placed, and where.</summary>
        class Placement
        {
            public DeviceType device;
            public InstallTargetKind target;
            public BeltPath belt;
            public int slotIndex;
            public YardNode node;
            public GameObject instance;

            public InstallRecord ToRecord(string levelId)
            {
                return new InstallRecord
                {
                    levelId = levelId,
                    device = device,
                    target = target,
                    beltId = belt != null ? belt.BeltId : string.Empty,
                    nodeId = node != null ? node.NodeId : string.Empty,
                    slotIndex = slotIndex
                };
            }
        }

        [SerializeField] ModuleCatalog catalog;
        [SerializeField] Camera pickCamera;

        readonly List<InstallSlotMarker> markers = new List<InstallSlotMarker>();
        readonly List<NodeDeviceSlot> nodeSlots = new List<NodeDeviceSlot>();
        readonly List<Placement> placements = new List<Placement>();
        readonly RaycastHit[] hits = new RaycastHit[32];

        Transform markerRoot;
        InstallSlotMarker hoveredMarker;
        NodeDeviceSlot hoveredNode;

        /// <summary>The already-placed device the player is currently dragging, if any.</summary>
        Placement dragged;
        Vector2 dragOrigin;
        bool dragPassedThreshold;

        LevelConfig config;
        YardGraph graph;
        Func<GamePhase> phaseSource = () => GamePhase.Prep;

        /// <summary>Kept for compatibility with older UI; no longer used for install decisions.</summary>
        public DeviceLoadout Loadout { get; } = new DeviceLoadout();

        public DeviceInventory Inventory { get; private set; } = new DeviceInventory();

        public int InstalledCount => placements.Count;

        public IReadOnlyList<InstallSlotMarker> Markers => markers;

        public IReadOnlyList<NodeDeviceSlot> NodeSlots => nodeSlots;

        public string LevelId => config != null ? config.id : string.Empty;

        Camera PickCamera => pickCamera != null ? pickCamera : Camera.main;

        bool InPrep => phaseSource() == GamePhase.Prep;

        public void SetCatalog(ModuleCatalog moduleCatalog)
        {
            if (moduleCatalog != null)
            {
                catalog = moduleCatalog;
            }
        }

        /// <summary>Injects the phase so install gating does not need a hard director reference.</summary>
        public void SetPhaseSource(Func<GamePhase> source)
        {
            if (source != null)
            {
                phaseSource = source;
            }
        }

        public void BeginPrep(YardGraph yardGraph, LevelConfig levelConfig, DeviceInventory inventory)
        {
            CancelMove();
            config = levelConfig;
            graph = yardGraph;
            if (inventory != null)
            {
                Inventory = inventory;
            }

            Loadout.Reset(levelConfig);
            ClearMarkers();
            BuildMarkers();
        }

        /// <summary>Rebuilds the drop targets, e.g. when returning to prep after a round.</summary>
        public void RefreshMarkers()
        {
            ClearMarkers();
            BuildMarkers();
        }

        void BuildMarkers()
        {
            if (graph == null || catalog == null)
            {
                return;
            }

            if (markerRoot == null)
            {
                markerRoot = new GameObject("InstallSlots").transform;
            }

            if (catalog.slotMarkerPrefab != null)
            {
                IReadOnlyList<BeltPath> belts = graph.AllBelts;
                for (int b = 0; b < belts.Count; b++)
                {
                    BeltPath belt = belts[b];
                    if (!belt.AllowInstall)
                    {
                        continue;
                    }

                    for (int s = 0; s < belt.SlotCount; s++)
                    {
                        GameObject go = Instantiate(catalog.slotMarkerPrefab, markerRoot);
                        var marker = go.GetComponent<InstallSlotMarker>();
                        if (marker == null)
                        {
                            marker = go.AddComponent<InstallSlotMarker>();
                        }

                        marker.Configure(belt, s);
                        markers.Add(marker);
                    }
                }
            }

            // Node slots only exist on diverters: anywhere else there is no lever to automate.
            GameObject nodePrefab = catalog.nodeSlotMarkerPrefab != null
                ? catalog.nodeSlotMarkerPrefab
                : catalog.slotMarkerPrefab;
            if (nodePrefab == null)
            {
                return;
            }

            foreach (KeyValuePair<string, YardNode> pair in graph.Nodes)
            {
                YardNode node = pair.Value;
                if (!node.IsDiverter)
                {
                    continue;
                }

                GameObject go = Instantiate(nodePrefab, markerRoot);
                var slot = go.GetComponent<NodeDeviceSlot>();
                if (slot == null)
                {
                    slot = go.AddComponent<NodeDeviceSlot>();
                }

                slot.Configure(node);
                slot.SetOccupied(node.AutoArm != null);
                node.DeviceSlot = slot;
                nodeSlots.Add(slot);
            }
        }

        public void EndPrep()
        {
            CancelMove();
            ClearMarkers();
        }

        public void ClearMarkers()
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null)
                {
                    Destroy(markers[i].gameObject);
                }
            }

            markers.Clear();

            for (int i = 0; i < nodeSlots.Count; i++)
            {
                if (nodeSlots[i] == null)
                {
                    continue;
                }

                if (nodeSlots[i].Node != null)
                {
                    nodeSlots[i].Node.DeviceSlot = null;
                }

                Destroy(nodeSlots[i].gameObject);
            }

            nodeSlots.Clear();
            hoveredMarker = null;
            hoveredNode = null;
        }

        /// <summary>Destroys every placed device and hands the stock back to the inventory.</summary>
        public void ClearInstalls()
        {
            CancelMove();
            for (int i = placements.Count - 1; i >= 0; i--)
            {
                Placement placement = placements[i];
                Inventory.ReturnFromInstall(placement.device);
                if (placement.instance != null)
                {
                    Destroy(placement.instance);
                }
            }

            placements.Clear();
        }

        // ---------------------------------------------------------------- hover

        /// <summary>
        /// Highlights the drop target under the pointer that could actually accept
        /// <paramref name="device"/>, so dragging an AutoArm never lights up a belt slot.
        /// </summary>
        public void UpdateHover(Vector2 screenPosition, DeviceType device)
        {
            InstallTargetKind kind = TargetKindOf(device);
            InstallSlotMarker marker = null;
            NodeDeviceSlot node = null;

            if (kind == InstallTargetKind.BeltSlot)
            {
                marker = FindSlot(screenPosition);
            }
            else
            {
                node = FindNodeSlot(screenPosition);
            }

            if (marker != hoveredMarker)
            {
                hoveredMarker?.SetHovered(false);
                hoveredMarker = marker;
                hoveredMarker?.SetHovered(true);
            }

            if (node != hoveredNode)
            {
                hoveredNode?.SetHovered(false);
                hoveredNode = node;
                hoveredNode?.SetHovered(true);
            }
        }

        /// <summary>Legacy overload: assumes a belt-slot device.</summary>
        public InstallSlotMarker UpdateHover(Vector2 screenPosition)
        {
            UpdateHover(screenPosition, DeviceType.Gate);
            return hoveredMarker;
        }

        public InstallSlotMarker HoveredMarker => hoveredMarker;

        public NodeDeviceSlot HoveredNodeSlot => hoveredNode;

        public void ClearHover()
        {
            hoveredMarker?.SetHovered(false);
            hoveredMarker = null;
            hoveredNode?.SetHovered(false);
            hoveredNode = null;
        }

        InstallTargetKind TargetKindOf(DeviceType device)
        {
            if (config != null && config.devices.TryGet(device, out DeviceSpec spec))
            {
                return spec.target;
            }

            return InstallTargetKind.BeltSlot;
        }

        InstallSlotMarker FindSlot(Vector2 screenPosition)
        {
            Camera cam = PickCamera;
            if (cam == null || markers.Count == 0)
            {
                return null;
            }

            Ray ray = cam.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, hits, 1000f);
            float bestDistance = float.MaxValue;
            InstallSlotMarker best = null;
            for (int i = 0; i < count; i++)
            {
                var marker = hits[i].collider.GetComponentInParent<InstallSlotMarker>();
                if (marker == null || !marker.IsFree)
                {
                    continue;
                }

                if (hits[i].distance < bestDistance)
                {
                    bestDistance = hits[i].distance;
                    best = marker;
                }
            }

            if (best != null)
            {
                return best;
            }

            // Fall back to the nearest marker in screen space so dropping feels forgiving.
            float bestPixels = ScreenPickRadius;
            for (int i = 0; i < markers.Count; i++)
            {
                InstallSlotMarker marker = markers[i];
                if (marker == null || !marker.IsFree)
                {
                    continue;
                }

                Vector3 projected = cam.WorldToScreenPoint(marker.transform.position);
                if (projected.z <= 0f)
                {
                    continue;
                }

                float pixels = Vector2.Distance(screenPosition, new Vector2(projected.x, projected.y));
                if (pixels < bestPixels)
                {
                    bestPixels = pixels;
                    best = marker;
                }
            }

            return best;
        }

        NodeDeviceSlot FindNodeSlot(Vector2 screenPosition)
        {
            Camera cam = PickCamera;
            if (cam == null || nodeSlots.Count == 0)
            {
                return null;
            }

            float bestPixels = ScreenPickRadius;
            NodeDeviceSlot best = null;
            for (int i = 0; i < nodeSlots.Count; i++)
            {
                NodeDeviceSlot slot = nodeSlots[i];
                if (slot == null || !slot.IsFree)
                {
                    continue;
                }

                Vector3 projected = cam.WorldToScreenPoint(slot.transform.position);
                if (projected.z <= 0f)
                {
                    continue;
                }

                float pixels = Vector2.Distance(screenPosition, new Vector2(projected.x, projected.y));
                if (pixels < bestPixels)
                {
                    bestPixels = pixels;
                    best = slot;
                }
            }

            return best;
        }

        // ---------------------------------------------------------------- install

        /// <summary>Installs onto a belt slot. Returns false and changes nothing when refused.</summary>
        public bool TryInstall(DeviceType device, InstallSlotMarker marker)
        {
            if (!InPrep || marker == null || marker.Belt == null || catalog == null)
            {
                return false;
            }

            if (!config.devices.TryGet(device, out DeviceSpec spec))
            {
                return false;
            }

            // A belt-slot drop cannot host a node device, and vice versa.
            if (spec.target != InstallTargetKind.BeltSlot)
            {
                marker.SetHovered(false);
                return false;
            }

            BeltPath belt = marker.Belt;

            if (!belt.AllowInstall ||
                marker.SlotIndex < 0 ||
                marker.SlotIndex >= belt.SlotCount ||
                belt.IsSlotOccupied(marker.SlotIndex))
            {
                marker.SetHovered(false);
                return false;
            }

            // One Booster per belt. Stacking would turn a 1.6x into 2.56x for 45 coins and make
            // the "three trunk lines" cap meaningless.
            if (device == DeviceType.Booster && !Mathf.Approximately(belt.DeviceSpeedBonus, 1f))
            {
                marker.SetHovered(false);
                return false;
            }

            GameObject prefab = catalog.GetDevicePrefab(device);
            if (prefab == null)
            {
                return false;
            }

            // Stock is taken only once every check has passed, so a refusal costs nothing.
            if (!Inventory.TryTakeForInstall(device))
            {
                marker.SetHovered(false);
                return false;
            }

            GameObject go = Instantiate(prefab);
            if (!AttachBeltDevice(device, go, belt, marker.SlotIndex, spec))
            {
                Destroy(go);
                Inventory.ReturnFromInstall(device);
                return false;
            }

            placements.Add(new Placement
            {
                device = device,
                target = InstallTargetKind.BeltSlot,
                belt = belt,
                slotIndex = marker.SlotIndex,
                instance = go
            });

            marker.SetHovered(false);
            return true;
        }

        /// <summary>Installs onto a diverter node. Returns false and changes nothing when refused.</summary>
        public bool TryInstall(DeviceType device, NodeDeviceSlot slot)
        {
            if (!InPrep || slot == null || slot.Node == null || catalog == null)
            {
                return false;
            }

            if (!config.devices.TryGet(device, out DeviceSpec spec))
            {
                return false;
            }

            if (spec.target != InstallTargetKind.Node)
            {
                slot.FlashBlocked();
                return false;
            }

            if (!slot.IsFree || slot.Node.AutoArm != null)
            {
                slot.FlashBlocked();
                return false;
            }

            // An arm on a node with one exit has no lever to throw, so the purchase would be wasted.
            if (slot.Node.OutBelts.Count < 2)
            {
                slot.FlashBlocked();
                return false;
            }

            GameObject prefab = catalog.GetDevicePrefab(device);
            if (prefab == null)
            {
                return false;
            }

            if (!Inventory.TryTakeForInstall(device))
            {
                slot.FlashBlocked();
                return false;
            }

            GameObject go = Instantiate(prefab);
            if (!AttachNodeDevice(device, go, slot.Node, spec))
            {
                Destroy(go);
                Inventory.ReturnFromInstall(device);
                return false;
            }

            slot.SetOccupied(true);
            placements.Add(new Placement
            {
                device = device,
                target = InstallTargetKind.Node,
                node = slot.Node,
                slotIndex = 0,
                instance = go
            });

            return true;
        }

        /// <summary>
        /// Wires up a belt-mounted device. Every device kind is handled explicitly; a kind that
        /// belongs on a node reaches here only through an authoring mistake and is refused.
        /// </summary>
        bool AttachBeltDevice(
            DeviceType device, GameObject go, BeltPath belt, int slotIndex, DeviceSpec spec)
        {
            switch (device)
            {
                case DeviceType.Gate:
                {
                    var gate = go.GetComponent<GateDevice>() ?? go.AddComponent<GateDevice>();
                    gate.Install(belt, slotIndex, spec.holdSeconds);
                    return true;
                }

                case DeviceType.Booster:
                {
                    var booster = go.GetComponent<BoosterDevice>() ?? go.AddComponent<BoosterDevice>();
                    booster.Install(belt, slotIndex, spec.speedBonus);
                    return true;
                }

                case DeviceType.Scanner:
                {
                    var scanner = go.GetComponent<ScannerDevice>() ?? go.AddComponent<ScannerDevice>();
                    scanner.Install(belt, slotIndex);
                    return true;
                }

                case DeviceType.AutoArm:
                    Debug.LogError("AutoArm targets a node slot, not belt '" + belt.BeltId + "'.");
                    return false;
            }

            Debug.LogError("InstallManager has no belt install path for device '" + device + "'.");
            return false;
        }

        /// <summary>Wires up a node-mounted device. Only the AutoArm belongs here today.</summary>
        bool AttachNodeDevice(DeviceType device, GameObject go, YardNode node, DeviceSpec spec)
        {
            switch (device)
            {
                case DeviceType.AutoArm:
                {
                    var arm = go.GetComponent<AutoArmDevice>() ?? go.AddComponent<AutoArmDevice>();
                    arm.Install(node, graph, spec.switchCooldown, spec.skipBlind);
                    return true;
                }

                case DeviceType.Gate:
                case DeviceType.Booster:
                case DeviceType.Scanner:
                    Debug.LogError("Device '" + device + "' targets a belt slot, not node '" +
                                   node.NodeId + "'.");
                    return false;
            }

            Debug.LogError("InstallManager has no node install path for device '" + device + "'.");
            return false;
        }

        // ---------------------------------------------------------------- move an installed device

        /// <summary>True while the player is dragging a device that is already on the yard.</summary>
        public bool IsMovingDevice => dragged != null;

        /// <summary>The device being dragged off its current slot, for HUD feedback.</summary>
        public DeviceType MovingDevice => dragged != null ? dragged.device : DeviceType.Gate;

        /// <summary>
        /// Picking a placed device up and dropping it somewhere else, driven straight off the
        /// mouse because devices are world objects and never enter the uGUI event system.
        ///
        /// A move is not "sell and re-buy": the same instance is re-seated, so no stock changes
        /// hands and a drop on empty ground simply leaves the device where it was.
        /// </summary>
        void Update()
        {
            if (!InPrep)
            {
                CancelMove();
                return;
            }

            if (dragged == null)
            {
                if (Input.GetMouseButtonDown(0) && !PointerOverUi())
                {
                    BeginMove(PointerPosition());
                }

                return;
            }

            Vector2 pointer = PointerPosition();

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                if (dragPassedThreshold)
                {
                    FinishMove();
                }
                else
                {
                    // A click, not a drag. Leave the device exactly where the player put it.
                    CancelMove();
                }

                return;
            }

            if (!dragPassedThreshold &&
                Vector2.Distance(pointer, dragOrigin) >= MoveDragThreshold)
            {
                dragPassedThreshold = true;
            }

            if (dragPassedThreshold)
            {
                UpdateHover(pointer, dragged.device);
            }
        }

        /// <summary>Mouse position as a 2D screen point, which is what the hover API takes.</summary>
        static Vector2 PointerPosition()
        {
            Vector3 position = Input.mousePosition;
            return new Vector2(position.x, position.y);
        }

        static bool PointerOverUi()
        {
            UnityEngine.EventSystems.EventSystem system = UnityEngine.EventSystems.EventSystem.current;
            return system != null && system.IsPointerOverGameObject();
        }

        void BeginMove(Vector2 screenPosition)
        {
            Placement placement = FindPlacementAt(screenPosition);
            if (placement == null)
            {
                return;
            }

            dragged = placement;
            dragOrigin = screenPosition;
            dragPassedThreshold = false;
        }

        void FinishMove()
        {
            Placement placement = dragged;
            InstallSlotMarker marker = hoveredMarker;
            NodeDeviceSlot node = hoveredNode;
            dragged = null;
            dragPassedThreshold = false;
            ClearHover();

            if (placement == null)
            {
                return;
            }

            if (marker != null)
            {
                TryMove(placement, marker);
            }
            else if (node != null)
            {
                TryMove(placement, node);
            }
        }

        void CancelMove()
        {
            if (dragged == null)
            {
                return;
            }

            dragged = null;
            dragPassedThreshold = false;
            ClearHover();
        }

        /// <summary>Moves an installed device to another belt slot. Returns false when refused.</summary>
        public bool TryMoveTo(GameObject instance, InstallSlotMarker marker)
        {
            return TryMove(FindPlacement(instance), marker);
        }

        /// <summary>Moves an installed device to another node slot. Returns false when refused.</summary>
        public bool TryMoveTo(GameObject instance, NodeDeviceSlot slot)
        {
            return TryMove(FindPlacement(instance), slot);
        }

        /// <summary>
        /// Moves whatever sits on one belt slot onto another, addressing the device by the slot it
        /// occupies. This is the shape the drag gesture ends up in, and the shape a test can drive.
        /// </summary>
        public bool TryMove(InstallSlotMarker fromMarker, InstallSlotMarker toMarker)
        {
            if (fromMarker == null || fromMarker.Belt == null)
            {
                return false;
            }

            return TryMove(FindBeltPlacement(fromMarker.Belt, fromMarker.SlotIndex), toMarker);
        }

        /// <summary>Moves the arm on one diverter onto another diverter's free slot.</summary>
        public bool TryMove(NodeDeviceSlot fromSlot, NodeDeviceSlot toSlot)
        {
            if (fromSlot == null || fromSlot.Node == null)
            {
                return false;
            }

            return TryMove(FindNodePlacement(fromSlot.Node), toSlot);
        }

        bool TryMove(Placement placement, InstallSlotMarker marker)
        {
            if (!InPrep || placement == null || marker == null || marker.Belt == null)
            {
                return false;
            }

            if (placement.target != InstallTargetKind.BeltSlot || placement.instance == null)
            {
                marker.SetHovered(false);
                return false;
            }

            BeltPath belt = marker.Belt;
            int slotIndex = marker.SlotIndex;

            if (belt == placement.belt && slotIndex == placement.slotIndex)
            {
                return false;
            }

            if (!belt.AllowInstall ||
                slotIndex < 0 ||
                slotIndex >= belt.SlotCount ||
                belt.IsSlotOccupied(slotIndex))
            {
                marker.SetHovered(false);
                return false;
            }

            if (config == null || !config.devices.TryGet(placement.device, out DeviceSpec spec))
            {
                return false;
            }

            BeltPath fromBelt = placement.belt;
            int fromSlot = placement.slotIndex;

            // Detach before validating the destination. A Booster moving along its own belt has to
            // release that belt's bonus first, or it would be refused for colliding with itself.
            if (!DetachBeltDevice(placement))
            {
                return false;
            }

            bool boosterClash = placement.device == DeviceType.Booster &&
                                !Mathf.Approximately(belt.DeviceSpeedBonus, 1f);

            if (boosterClash ||
                !AttachBeltDevice(placement.device, placement.instance, belt, slotIndex, spec))
            {
                // A refused move must change nothing, so put it back exactly where it came from.
                AttachBeltDevice(placement.device, placement.instance, fromBelt, fromSlot, spec);
                marker.SetHovered(false);
                RepaintMarkers();
                return false;
            }

            placement.belt = belt;
            placement.slotIndex = slotIndex;
            RepaintMarkers();
            return true;
        }

        bool TryMove(Placement placement, NodeDeviceSlot slot)
        {
            if (!InPrep || placement == null || slot == null || slot.Node == null)
            {
                return false;
            }

            if (placement.target != InstallTargetKind.Node || placement.instance == null)
            {
                slot.FlashBlocked();
                return false;
            }

            YardNode node = slot.Node;
            if (node == placement.node)
            {
                return false;
            }

            if (!slot.IsFree || node.AutoArm != null || node.OutBelts.Count < 2)
            {
                slot.FlashBlocked();
                return false;
            }

            if (config == null || !config.devices.TryGet(placement.device, out DeviceSpec spec))
            {
                return false;
            }

            var arm = placement.instance.GetComponent<AutoArmDevice>();
            if (arm == null)
            {
                return false;
            }

            YardNode fromNode = placement.node;
            arm.Uninstall();

            if (!AttachNodeDevice(placement.device, placement.instance, node, spec))
            {
                AttachNodeDevice(placement.device, placement.instance, fromNode, spec);
                if (fromNode != null && fromNode.DeviceSlot != null)
                {
                    fromNode.DeviceSlot.SetOccupied(true);
                }

                slot.FlashBlocked();
                return false;
            }

            slot.SetOccupied(true);
            placement.node = node;
            return true;
        }

        /// <summary>Releases a belt device from its slot, keeping the object alive for a re-seat.</summary>
        static bool DetachBeltDevice(Placement placement)
        {
            GameObject go = placement.instance;
            if (go == null)
            {
                return false;
            }

            switch (placement.device)
            {
                case DeviceType.Gate:
                {
                    var gate = go.GetComponent<GateDevice>();
                    if (gate == null)
                    {
                        return false;
                    }

                    gate.Uninstall();
                    return true;
                }

                case DeviceType.Booster:
                {
                    var booster = go.GetComponent<BoosterDevice>();
                    if (booster == null)
                    {
                        return false;
                    }

                    booster.Uninstall();
                    return true;
                }

                case DeviceType.Scanner:
                {
                    var scanner = go.GetComponent<ScannerDevice>();
                    if (scanner == null)
                    {
                        return false;
                    }

                    scanner.Uninstall();
                    return true;
                }

                case DeviceType.AutoArm:
                    Debug.LogError("An AutoArm sits on a node, so it has no belt slot to leave.");
                    return false;
            }

            Debug.LogError("InstallManager cannot detach device '" + placement.device + "'.");
            return false;
        }

        Placement FindBeltPlacement(BeltPath belt, int slotIndex)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                Placement placement = placements[i];
                if (placement.target == InstallTargetKind.BeltSlot &&
                    placement.belt == belt &&
                    placement.slotIndex == slotIndex)
                {
                    return placement;
                }
            }

            return null;
        }

        Placement FindNodePlacement(YardNode node)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                Placement placement = placements[i];
                if (placement.target == InstallTargetKind.Node && placement.node == node)
                {
                    return placement;
                }
            }

            return null;
        }

        Placement FindPlacement(GameObject instance)
        {
            if (instance == null)
            {
                return null;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].instance == instance)
                {
                    return placements[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The placed device under the pointer. Tries real geometry first, then falls back to
        /// screen distance, so picking a device up is as forgiving as dropping one and works even
        /// for a placeholder prefab that carries no collider.
        /// </summary>
        Placement FindPlacementAt(Vector2 screenPosition)
        {
            Camera cam = PickCamera;
            if (cam == null || placements.Count == 0)
            {
                return null;
            }

            Ray ray = cam.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, hits, 1000f);
            float bestDistance = float.MaxValue;
            Placement best = null;
            for (int i = 0; i < count; i++)
            {
                Placement owner = PlacementOwning(hits[i].collider != null
                    ? hits[i].collider.transform
                    : null);
                if (owner == null || hits[i].distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hits[i].distance;
                best = owner;
            }

            if (best != null)
            {
                return best;
            }

            float bestPixels = ScreenPickRadius;
            for (int i = 0; i < placements.Count; i++)
            {
                GameObject instance = placements[i].instance;
                if (instance == null)
                {
                    continue;
                }

                Vector3 projected = cam.WorldToScreenPoint(instance.transform.position);
                if (projected.z <= 0f)
                {
                    continue;
                }

                float pixels = Vector2.Distance(screenPosition, new Vector2(projected.x, projected.y));
                if (pixels < bestPixels)
                {
                    bestPixels = pixels;
                    best = placements[i];
                }
            }

            return best;
        }

        /// <summary>
        /// Walks up from a hit collider looking for a placed device. The walk is explicit rather
        /// than a GetComponentInParent so any device kind is matched by identity, which keeps this
        /// working for a future device that brings its own components.
        /// </summary>
        Placement PlacementOwning(Transform hit)
        {
            for (Transform t = hit; t != null; t = t.parent)
            {
                for (int i = 0; i < placements.Count; i++)
                {
                    GameObject instance = placements[i].instance;
                    if (instance != null && instance.transform == t)
                    {
                        return placements[i];
                    }
                }
            }

            return null;
        }

        /// <summary>Re-reads occupancy on every belt marker, so a vacated slot stops reading blocked.</summary>
        void RepaintMarkers()
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null)
                {
                    markers[i].SetHovered(false);
                }
            }
        }

        // ---------------------------------------------------------------- remove

        public bool TryRemoveAt(InstallSlotMarker marker)
        {
            if (!InPrep || marker == null || marker.Belt == null)
            {
                return false;
            }

            return RemoveBeltPlacement(marker.Belt, marker.SlotIndex);
        }

        public bool TryRemoveAt(NodeDeviceSlot slot)
        {
            if (!InPrep || slot == null || slot.Node == null)
            {
                return false;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                Placement placement = placements[i];
                if (placement.target != InstallTargetKind.Node || placement.node != slot.Node)
                {
                    continue;
                }

                Remove(i);
                slot.SetOccupied(false);
                return true;
            }

            return false;
        }

        bool RemoveBeltPlacement(BeltPath belt, int slotIndex)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                Placement placement = placements[i];
                if (placement.target != InstallTargetKind.BeltSlot ||
                    placement.belt != belt ||
                    placement.slotIndex != slotIndex)
                {
                    continue;
                }

                Remove(i);
                return true;
            }

            return false;
        }

        void Remove(int index)
        {
            Placement placement = placements[index];
            placements.RemoveAt(index);
            Inventory.ReturnFromInstall(placement.device);

            // OnDestroy on each device frees its own slot and undoes its own belt effect.
            if (placement.instance != null)
            {
                Destroy(placement.instance);
            }
        }

        // ---------------------------------------------------------------- persistence

        /// <summary>Everything currently placed, addressed so it can be rebuilt after a restart.</summary>
        public List<InstallRecord> Snapshot()
        {
            string levelId = LevelId;
            var records = new List<InstallRecord>(placements.Count);
            for (int i = 0; i < placements.Count; i++)
            {
                records.Add(placements[i].ToRecord(levelId));
            }

            return records;
        }

        /// <summary>
        /// Rebuilds placements for the current level from a saved list.
        ///
        /// A record that no longer resolves - a renamed belt, a removed node, a slot index past
        /// the end of a shortened belt - is dropped and its device handed back to the inventory,
        /// so a stale save leaves free stock rather than a ghost that occupies a slot forever.
        /// </summary>
        public void Restore(IReadOnlyList<InstallRecord> records)
        {
            if (records == null || graph == null || config == null)
            {
                return;
            }

            string levelId = LevelId;
            int skipped = 0;

            for (int i = 0; i < records.Count; i++)
            {
                InstallRecord record = records[i];
                if (!string.Equals(record.levelId, levelId))
                {
                    continue;
                }

                if (!config.devices.TryGet(record.device, out DeviceSpec spec))
                {
                    skipped++;
                    continue;
                }

                if (record.target == InstallTargetKind.Node)
                {
                    if (!graph.TryGetNode(record.nodeId, out YardNode node) ||
                        node.OutBelts.Count < 2 ||
                        node.AutoArm != null)
                    {
                        skipped++;
                        continue;
                    }

                    if (!Inventory.TryTakeForInstall(record.device))
                    {
                        skipped++;
                        continue;
                    }

                    GameObject prefab = catalog != null ? catalog.GetDevicePrefab(record.device) : null;
                    GameObject go = prefab != null ? Instantiate(prefab) : null;
                    if (go == null || !AttachNodeDevice(record.device, go, node, spec))
                    {
                        if (go != null)
                        {
                            Destroy(go);
                        }

                        Inventory.ReturnFromInstall(record.device);
                        skipped++;
                        continue;
                    }

                    node.DeviceSlot?.SetOccupied(true);
                    placements.Add(new Placement
                    {
                        device = record.device,
                        target = InstallTargetKind.Node,
                        node = node,
                        instance = go
                    });
                    continue;
                }

                if (!graph.TryGetBelt(record.beltId, out BeltPath belt) ||
                    !belt.AllowInstall ||
                    record.slotIndex < 0 ||
                    record.slotIndex >= belt.SlotCount ||
                    belt.IsSlotOccupied(record.slotIndex))
                {
                    skipped++;
                    continue;
                }

                if (record.device == DeviceType.Booster && !Mathf.Approximately(belt.DeviceSpeedBonus, 1f))
                {
                    skipped++;
                    continue;
                }

                if (!Inventory.TryTakeForInstall(record.device))
                {
                    skipped++;
                    continue;
                }

                GameObject beltPrefab = catalog != null ? catalog.GetDevicePrefab(record.device) : null;
                GameObject beltGo = beltPrefab != null ? Instantiate(beltPrefab) : null;
                if (beltGo == null || !AttachBeltDevice(record.device, beltGo, belt, record.slotIndex, spec))
                {
                    if (beltGo != null)
                    {
                        Destroy(beltGo);
                    }

                    Inventory.ReturnFromInstall(record.device);
                    skipped++;
                    continue;
                }

                placements.Add(new Placement
                {
                    device = record.device,
                    target = InstallTargetKind.BeltSlot,
                    belt = belt,
                    slotIndex = record.slotIndex,
                    instance = beltGo
                });
            }

            if (skipped > 0)
            {
                Debug.LogWarning("InstallManager skipped " + skipped +
                                 " saved install(s) that no longer fit this level; their devices " +
                                 "were returned to the inventory.");
            }

            // Occupied markers must read as blocked straight away.
            for (int i = 0; i < markers.Count; i++)
            {
                markers[i]?.SetHovered(false);
            }
        }
    }
}
