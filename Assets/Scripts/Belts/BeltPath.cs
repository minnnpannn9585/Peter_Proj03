using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// One belt run: a polyline edge of the yard graph. A belt is the unit the player clicks,
    /// the unit that carries a speed setting, and the host for installable devices.
    /// Parcels live on it as an ordered list of arc-length positions.
    /// </summary>
    public class BeltPath : MonoBehaviour
    {
        /// <summary>Authored deck top surface height, before <see cref="VisualScale"/>.</summary>
        public const float BaseDeckTop = 0.30f;

        public const float BaseDeckThickness = 0.22f;
        public const float BaseDeckWidth = 0.62f;

        /// <summary>Authored half height of a parcel cube (Parcel.prefab is a 0.42 cube).</summary>
        public const float BaseParcelHalf = 0.21f;

        /// <summary>
        /// Uniform multiplier applied to every yard visual (decks, legs, nodes, parcels, devices).
        /// The grid keeps its spacing, so raising this fills the empty space between belts and
        /// makes the whole yard readable from a top-down overview. Set by LevelLoader per level.
        /// </summary>
        public static float VisualScale { get; set; } = 1f;

        /// <summary>Deck top surface height above the belt's level plane.</summary>
        public static float DeckTop => BaseDeckTop * VisualScale;

        public static float DeckThickness => BaseDeckThickness * VisualScale;

        public static float DeckWidth => BaseDeckWidth * VisualScale;

        public static float ParcelHalf => BaseParcelHalf * VisualScale;

        /// <summary>Height of the parcel centre line above the belt's level plane.</summary>
        public static float RideHeight => DeckTop + ParcelHalf;

        public string BeltId { get; private set; } = string.Empty;
        public YardNode From { get; private set; }
        public YardNode To { get; private set; }
        public int OutputIndex { get; private set; }
        public bool AllowInstall { get; private set; } = true;
        public bool CanPause { get; private set; }
        public bool CanSpeed { get; private set; }
        public float GridSize { get; private set; } = 2f;
        public float LevelHeight { get; private set; } = 1.5f;

        /// <summary>Belt speed with no player modifier applied, in world units per second.</summary>
        public float BaseSpeed { get; private set; } = 5f;

        public float SpeedMultiplier { get; set; } = 1f;

        public float EffectiveSpeed => Mathf.Max(0f, BaseSpeed * SpeedMultiplier);

        /// <summary>Polyline at parcel centre height, first point on From, last point on To.</summary>
        public Vector3[] Points { get; private set; } = System.Array.Empty<Vector3>();

        /// <summary>Cell space polyline as (cellX, cellZ, level), same count as Points.</summary>
        public Vector3Int[] CellPoints { get; private set; } = System.Array.Empty<Vector3Int>();

        public float Length { get; private set; }
        public int CellLength { get; private set; }

        /// <summary>Parcels on this belt sorted by descending distance (front of queue first).</summary>
        public readonly List<ParcelRuntime> Parcels = new List<ParcelRuntime>();

        /// <summary>Installed gates sorted by ascending distance.</summary>
        public readonly List<GateDevice> Gates = new List<GateDevice>();

        public Renderer[] DeckRenderers { get; set; } = System.Array.Empty<Renderer>();

        float[] cumulative = System.Array.Empty<float>();
        float[] slotDistances = System.Array.Empty<float>();
        bool[] slotOccupied = System.Array.Empty<bool>();

        public int SlotCount => slotDistances.Length;

        public void Configure(BeltDef def, YardNode from, YardNode to, GridSettings grid, RuleSettings rules)
        {
            BeltId = def.id;
            From = from;
            To = to;
            OutputIndex = def.output;
            AllowInstall = def.allowInstall;
            CanPause = def.canPause;
            CanSpeed = def.canSpeed;
            GridSize = grid.size;
            LevelHeight = grid.levelHeight;
            BaseSpeed = Mathf.Max(0.1f, rules.baseSpeed * def.speed);
            SpeedMultiplier = 1f;
            gameObject.name = def.id;

            BuildPolyline(def, from, to, grid);
            BuildSlots();
        }

        void BuildPolyline(BeltDef def, YardNode from, YardNode to, GridSettings grid)
        {
            var cells = new List<Vector3Int>(def.path.Count + 2);
            cells.Add(new Vector3Int(from.CellX, from.CellZ, from.Level));

            int lastLevel = from.Level;
            for (int i = 0; i < def.path.Count; i++)
            {
                PathPoint point = def.path[i];
                int level = point.hasLevel ? point.level : lastLevel;
                lastLevel = level;
                cells.Add(new Vector3Int(point.cellX, point.cellZ, level));
            }

            cells.Add(new Vector3Int(to.CellX, to.CellZ, to.Level));

            // Drop duplicated consecutive points so authoring can repeat an endpoint harmlessly.
            for (int i = cells.Count - 1; i > 0; i--)
            {
                if (cells[i] == cells[i - 1])
                {
                    cells.RemoveAt(i);
                }
            }

            CellPoints = cells.ToArray();
            Points = new Vector3[CellPoints.Length];
            CellLength = 0;
            for (int i = 0; i < CellPoints.Length; i++)
            {
                Vector3Int cell = CellPoints[i];
                Vector3 world = GridMath.CellToWorld(cell.x, cell.y, cell.z, grid.size, grid.levelHeight);
                world.y += RideHeight;
                Points[i] = world;
                if (i > 0)
                {
                    Vector3Int prev = CellPoints[i - 1];
                    CellLength += GridMath.CellSpan(new Vector2Int(cell.x - prev.x, cell.y - prev.y));
                }
            }

            cumulative = new float[Points.Length];
            cumulative[0] = 0f;
            for (int i = 1; i < Points.Length; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(Points[i - 1], Points[i]);
            }

            Length = cumulative.Length > 0 ? cumulative[cumulative.Length - 1] : 0f;
        }

        void BuildSlots()
        {
            var distances = new List<float>();
            float margin = GridSize * 0.6f;

            // Slots are spaced wider than a cell so the enlarged markers read as separate
            // drop targets instead of one continuous ribbon over the deck.
            float spacing = Mathf.Max(GridSize, DeckWidth * 2.2f);
            if (AllowInstall && Length > margin * 2f)
            {
                for (float d = margin; d <= Length - margin + 0.001f; d += spacing)
                {
                    distances.Add(d);
                }
            }

            slotDistances = distances.ToArray();
            slotOccupied = new bool[slotDistances.Length];
        }

        public float GetSlotDistance(int index)
        {
            if (index < 0 || index >= slotDistances.Length)
            {
                return 0f;
            }

            return slotDistances[index];
        }

        public bool IsSlotOccupied(int index)
        {
            return index >= 0 && index < slotOccupied.Length && slotOccupied[index];
        }

        public void SetSlotOccupied(int index, bool occupied)
        {
            if (index >= 0 && index < slotOccupied.Length)
            {
                slotOccupied[index] = occupied;
            }
        }

        public void ClearSlots()
        {
            for (int i = 0; i < slotOccupied.Length; i++)
            {
                slotOccupied[i] = false;
            }
        }

        /// <summary>Position and unit tangent at an arc-length distance along the belt.</summary>
        public void Evaluate(float distance, out Vector3 position, out Vector3 tangent)
        {
            if (Points.Length == 0)
            {
                position = transform.position;
                tangent = Vector3.forward;
                return;
            }

            if (Points.Length == 1)
            {
                position = Points[0];
                tangent = Vector3.forward;
                return;
            }

            float d = Mathf.Clamp(distance, 0f, Length);
            int segment = Points.Length - 2;
            for (int i = 1; i < cumulative.Length; i++)
            {
                if (d <= cumulative[i])
                {
                    segment = i - 1;
                    break;
                }
            }

            float segStart = cumulative[segment];
            float segLength = cumulative[segment + 1] - segStart;
            float t = segLength > 0.0001f ? (d - segStart) / segLength : 0f;
            Vector3 a = Points[segment];
            Vector3 b = Points[segment + 1];
            position = Vector3.Lerp(a, b, t);
            Vector3 dir = b - a;
            tangent = dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector3.forward;
        }

        public Vector3 PositionAt(float distance)
        {
            Evaluate(distance, out Vector3 position, out _);
            return position;
        }

        /// <summary>Distance of the closest closed gate strictly ahead of a parcel, or -1.</summary>
        public float NextClosedGateDistance(float fromDistance)
        {
            for (int i = 0; i < Gates.Count; i++)
            {
                GateDevice gate = Gates[i];
                if (gate == null || !gate.IsClosed)
                {
                    continue;
                }

                if (gate.Distance >= fromDistance - 0.001f)
                {
                    return gate.Distance;
                }
            }

            return -1f;
        }

        public void SortGates()
        {
            Gates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        /// <summary>Distance of the last (rear-most) parcel on the belt, or -1 when empty.</summary>
        public float RearParcelDistance()
        {
            if (Parcels.Count == 0)
            {
                return -1f;
            }

            return Parcels[Parcels.Count - 1].Distance;
        }

        /// <summary>Appends a parcel at the rear of the queue. Caller guarantees clearance.</summary>
        public void AppendParcel(ParcelRuntime parcel, float distance)
        {
            parcel.Belt = this;
            parcel.Distance = distance;
            Parcels.Add(parcel);
        }

        public void RemoveParcel(ParcelRuntime parcel)
        {
            Parcels.Remove(parcel);
        }
    }
}
