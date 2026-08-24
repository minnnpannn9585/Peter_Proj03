using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Passive parcel source. It owns nothing about timing: <see cref="SpawnDirector"/>
    /// drives it from the level's authored spawn plan.
    /// </summary>
    public class Inlet : MonoBehaviour
    {
        YardNode node;
        TrafficSystem traffic;
        YardDirector director;
        GameObject parcelPrefab;
        readonly List<DestinationColor> palette = new List<DestinationColor>();
        bool active;

        /// <summary>Colours this inlet falls back to when a wave does not name one.</summary>
        public IReadOnlyList<DestinationColor> Palette => palette;

        public void Configure(
            YardNode owner,
            GameObject prefab,
            TrafficSystem trafficSystem,
            YardDirector yardDirector,
            ICollection<DestinationColor> colors)
        {
            node = owner;
            parcelPrefab = prefab;
            traffic = trafficSystem;
            director = yardDirector;
            palette.Clear();
            if (colors != null)
            {
                palette.AddRange(colors);
            }

            if (palette.Count == 0)
            {
                palette.Add(DestinationColor.Red);
            }

            active = false;
        }

        public void SetActive(bool value)
        {
            active = value;
        }

        /// <summary>Ready when the outgoing belt has room for one more parcel.</summary>
        public bool CanEmit()
        {
            if (!active || parcelPrefab == null || traffic == null || node == null)
            {
                return false;
            }

            BeltPath belt = node.SelectedBelt;
            return belt != null && traffic.CanEnter(belt);
        }

        /// <summary>
        /// Drops one parcel of the requested colour. Returns false when the belt is full,
        /// which lets the scheduler retry next frame instead of losing the parcel.
        /// </summary>
        public bool TryEmit(DestinationColor color)
        {
            if (!active || parcelPrefab == null || traffic == null || node == null)
            {
                return false;
            }

            BeltPath belt = node.SelectedBelt;
            if (belt == null || !traffic.CanEnter(belt))
            {
                return false;
            }

            GameObject instance = Instantiate(parcelPrefab, belt.PositionAt(0f), Quaternion.identity);
            instance.transform.localScale = parcelPrefab.transform.localScale * BeltPath.VisualScale;
            var parcel = instance.GetComponent<Parcel>();
            if (parcel != null)
            {
                parcel.Initialize(new ParcelData
                {
                    destination = color,
                    isRevealed = true,
                    size = ParcelSize.Small
                });
            }

            var runtime = instance.GetComponent<ParcelRuntime>();
            if (runtime == null)
            {
                runtime = instance.AddComponent<ParcelRuntime>();
            }

            if (!traffic.Spawn(runtime, belt))
            {
                Destroy(instance);
                return false;
            }

            if (director != null)
            {
                director.NotifySpawned();
            }

            return true;
        }
    }
}
