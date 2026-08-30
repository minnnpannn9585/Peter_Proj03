using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// A hand-off point in the yard graph. A junction with more than one outgoing belt is a
    /// player controlled diverter; a junction with more than one incoming belt merges traffic.
    /// </summary>
    public class YardNode : MonoBehaviour
    {
        public string NodeId { get; private set; } = string.Empty;
        public NodeType Type { get; private set; }
        public int CellX { get; private set; }
        public int CellZ { get; private set; }
        public int Level { get; private set; }
        public float GridSize { get; private set; } = 2f;
        public float LevelHeight { get; private set; } = 1.5f;
        public Vector2Int Facing { get; private set; } = new Vector2Int(1, 0);

        public readonly List<BeltPath> InBelts = new List<BeltPath>();
        public readonly List<BeltPath> OutBelts = new List<BeltPath>();

        /// <summary>Index into OutBelts chosen by the diverter lever.</summary>
        public int SelectedOutput { get; set; }

        /// <summary>Installed arm driving this node's lever, or null while it is manual.</summary>
        public AutoArmDevice AutoArm { get; set; }

        /// <summary>Prep phase drop target for node-mounted devices. Only diverters have one.</summary>
        public NodeDeviceSlot DeviceSlot { get; set; }

        /// <summary>Round robin cursor so merging inputs cannot starve each other.</summary>
        public int LastServedInput { get; set; }

        public Vector3 GroundPos => GridMath.CellToWorld(CellX, CellZ, Level, GridSize, LevelHeight);

        public Vector3 PathPos
        {
            get
            {
                Vector3 pos = GroundPos;
                pos.y += BeltPath.RideHeight;
                return pos;
            }
        }

        public bool IsDiverter => Type == NodeType.Junction && OutBelts.Count > 1;

        public void Configure(NodeDef def, GridSettings grid)
        {
            NodeId = def.id;
            Type = def.type;
            CellX = def.cellX;
            CellZ = def.cellZ;
            Level = def.level;
            GridSize = grid.size;
            LevelHeight = grid.levelHeight;
            if (def.hasFacing)
            {
                Facing = def.facing;
            }

            SelectedOutput = 0;
            LastServedInput = 0;
            gameObject.name = def.id;
            transform.position = GroundPos;
        }

        public BeltPath SelectedBelt
        {
            get
            {
                if (OutBelts.Count == 0)
                {
                    return null;
                }

                int index = Mathf.Clamp(SelectedOutput, 0, OutBelts.Count - 1);
                return OutBelts[index];
            }
        }
    }
}
