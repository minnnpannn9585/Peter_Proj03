using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Owns the prep phase install slots and the devices the player drops onto belts.
    /// </summary>
    public class InstallManager : MonoBehaviour
    {
        const float ScreenPickRadius = 90f;

        [SerializeField] ModuleCatalog catalog;
        [SerializeField] Camera pickCamera;

        readonly List<InstallSlotMarker> markers = new List<InstallSlotMarker>();
        readonly List<GateDevice> installed = new List<GateDevice>();
        readonly RaycastHit[] hits = new RaycastHit[32];

        Transform markerRoot;
        InstallSlotMarker hovered;
        LevelConfig config;

        public DeviceLoadout Loadout { get; } = new DeviceLoadout();

        public int InstalledCount => installed.Count;

        public void SetCatalog(ModuleCatalog moduleCatalog)
        {
            if (moduleCatalog != null)
            {
                catalog = moduleCatalog;
            }
        }

        Camera PickCamera => pickCamera != null ? pickCamera : Camera.main;

        public void BeginPrep(YardGraph graph, LevelConfig levelConfig)
        {
            config = levelConfig;
            Loadout.Reset(levelConfig);
            ClearMarkers();
            installed.Clear();

            if (graph == null || catalog == null || catalog.slotMarkerPrefab == null)
            {
                return;
            }

            if (markerRoot == null)
            {
                markerRoot = new GameObject("InstallSlots").transform;
            }

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

        public void EndPrep()
        {
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
            hovered = null;
        }

        /// <summary>Highlights and returns the slot under the pointer, or null.</summary>
        public InstallSlotMarker UpdateHover(Vector2 screenPosition)
        {
            InstallSlotMarker found = FindSlot(screenPosition);
            if (found != hovered)
            {
                if (hovered != null)
                {
                    hovered.SetHovered(false);
                }

                hovered = found;
                if (hovered != null)
                {
                    hovered.SetHovered(true);
                }
            }

            return hovered;
        }

        public void ClearHover()
        {
            if (hovered != null)
            {
                hovered.SetHovered(false);
                hovered = null;
            }
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

        public bool TryInstall(DeviceType device, InstallSlotMarker marker)
        {
            if (marker == null || !marker.IsFree || catalog == null)
            {
                return false;
            }

            if (device != DeviceType.Gate || catalog.gatePrefab == null)
            {
                return false;
            }

            if (!Loadout.TryConsume(device))
            {
                return false;
            }

            GameObject go = Instantiate(catalog.gatePrefab);
            var gate = go.GetComponent<GateDevice>();
            if (gate == null)
            {
                gate = go.AddComponent<GateDevice>();
            }

            float hold = config != null ? config.gate.holdSeconds : 8f;
            gate.Install(marker.Belt, marker.SlotIndex, hold);
            installed.Add(gate);
            marker.SetHovered(false);
            return true;
        }
    }
}
