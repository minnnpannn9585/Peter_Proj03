using System;
using UnityEngine;

namespace ParcelSort
{
    [CreateAssetMenu(fileName = "ModuleCatalog", menuName = "Parcel Sort/Module Catalog")]
    public class ModuleCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string type;
            public GameObject prefab;
        }

        [Header("Node prefabs (keyed by node type name)")]
        public Entry[] entries;

        [Header("Belt pieces")]
        public GameObject beltDeckPrefab;
        public GameObject beltCornerPrefab;
        public GameObject beltLegPrefab;
        public GameObject chevronPrefab;

        [Header("Equipment")]
        public GameObject slotMarkerPrefab;
        public GameObject nodeSlotMarkerPrefab;
        public GameObject gatePrefab;
        public GameObject boosterPrefab;
        public GameObject scannerPrefab;
        public GameObject autoArmPrefab;

        [Header("Packages")]
        public GameObject parcelPrefab;

        /// <summary>
        /// Prefab for one device kind. Every <see cref="DeviceType"/> is listed explicitly and
        /// there is no default branch, so adding a device to the enum breaks the build here
        /// instead of silently returning null at install time.
        /// </summary>
        public GameObject GetDevicePrefab(DeviceType device)
        {
            switch (device)
            {
                case DeviceType.Gate:
                    return gatePrefab;
                case DeviceType.Booster:
                    return boosterPrefab;
                case DeviceType.Scanner:
                    return scannerPrefab;
                case DeviceType.AutoArm:
                    return autoArmPrefab;
            }

            Debug.LogError("ModuleCatalog has no prefab mapping for device '" + device + "'.");
            return null;
        }

        public GameObject GetPrefab(string type)
        {
            if (entries == null || string.IsNullOrEmpty(type))
            {
                return null;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry != null && string.Equals(entry.type, type, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.prefab;
                }
            }

            return null;
        }

        public GameObject GetNodePrefab(NodeType type, bool plain)
        {
            if (plain && type == NodeType.Junction)
            {
                GameObject seam = GetPrefab("junction_plain");
                if (seam != null)
                {
                    return seam;
                }
            }

            switch (type)
            {
                case NodeType.Inlet:
                    return GetPrefab("inlet");
                case NodeType.Bay:
                    return GetPrefab("bay");
                default:
                    return GetPrefab("junction");
            }
        }
    }
}
