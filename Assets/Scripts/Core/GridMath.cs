using UnityEngine;

namespace ParcelSort
{
    public static class GridMath
    {
        public static Vector3 CellToWorld(int cellX, int cellZ, float gridSize)
        {
            return new Vector3(cellX * gridSize, 0f, cellZ * gridSize);
        }

        /// <summary>Cell center in world space including the vertical level offset.</summary>
        public static Vector3 CellToWorld(int cellX, int cellZ, int level, float gridSize, float levelHeight)
        {
            return new Vector3(cellX * gridSize, level * levelHeight, cellZ * gridSize);
        }

        public static bool IsStepAdjacent(int ax, int az, int bx, int bz)
        {
            int dx = Mathf.Abs(ax - bx);
            int dz = Mathf.Abs(az - bz);
            return (dx <= 1 && dz <= 1 && dx + dz > 0);
        }

        public static Vector2Int Step(int fromX, int fromZ, int toX, int toZ)
        {
            return new Vector2Int(toX - fromX, toZ - fromZ);
        }

        /// <summary>True when the delta runs along an axis or an exact 45 degree diagonal.</summary>
        public static bool IsAxisOrDiagonal(Vector2Int delta)
        {
            if (delta.x == 0 && delta.y == 0)
            {
                return false;
            }

            if (delta.x == 0 || delta.y == 0)
            {
                return true;
            }

            return Mathf.Abs(delta.x) == Mathf.Abs(delta.y);
        }

        /// <summary>Cell count spanned by a valid axis or diagonal delta.</summary>
        public static int CellSpan(Vector2Int delta)
        {
            return Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        }

        public static bool IsDiagonal(Vector2Int delta)
        {
            return delta.x != 0 && delta.y != 0;
        }

        public static Vector3 ToVector(Vector2Int delta)
        {
            return new Vector3(delta.x, 0f, delta.y);
        }

        public static float PadYawDegrees(Vector2Int delta)
        {
            if (delta.x == 0 && delta.y == 0)
            {
                return 0f;
            }

            return Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        }

        /// <summary>Yaw so that local +Z points along the supplied world direction.</summary>
        public static float YawDegrees(Vector3 worldDirection)
        {
            Vector3 flat = new Vector3(worldDirection.x, 0f, worldDirection.z);
            if (flat.sqrMagnitude < 0.000001f)
            {
                return 0f;
            }

            return Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        /// <summary>Pitch so that local +Z follows a sloped direction. Negative means climbing.</summary>
        public static float PitchDegrees(Vector3 worldDirection)
        {
            Vector3 flat = new Vector3(worldDirection.x, 0f, worldDirection.z);
            float horizontal = flat.magnitude;
            if (horizontal < 0.000001f)
            {
                return 0f;
            }

            return -Mathf.Atan2(worldDirection.y, horizontal) * Mathf.Rad2Deg;
        }

        public static bool TryParseFacing(string text, out Vector2Int delta)
        {
            delta = new Vector2Int(1, 0);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            switch (text.Trim().ToUpperInvariant())
            {
                case "E":
                case "EAST":
                    delta = new Vector2Int(1, 0);
                    return true;
                case "W":
                case "WEST":
                    delta = new Vector2Int(-1, 0);
                    return true;
                case "N":
                case "NORTH":
                    delta = new Vector2Int(0, 1);
                    return true;
                case "S":
                case "SOUTH":
                    delta = new Vector2Int(0, -1);
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryDirection(int fromX, int fromZ, int toX, int toZ, out GridDir dir)
        {
            int dx = toX - fromX;
            int dz = toZ - fromZ;
            if (dx == 1 && dz == 0)
            {
                dir = GridDir.East;
                return true;
            }

            if (dx == -1 && dz == 0)
            {
                dir = GridDir.West;
                return true;
            }

            if (dx == 0 && dz == 1)
            {
                dir = GridDir.North;
                return true;
            }

            if (dx == 0 && dz == -1)
            {
                dir = GridDir.South;
                return true;
            }

            dir = GridDir.East;
            return false;
        }

        public static GridDir Opposite(GridDir dir)
        {
            switch (dir)
            {
                case GridDir.East:
                    return GridDir.West;
                case GridDir.West:
                    return GridDir.East;
                case GridDir.North:
                    return GridDir.South;
                default:
                    return GridDir.North;
            }
        }

        public static Vector3 ToVector(GridDir dir)
        {
            switch (dir)
            {
                case GridDir.East:
                    return Vector3.right;
                case GridDir.West:
                    return Vector3.left;
                case GridDir.North:
                    return Vector3.forward;
                default:
                    return Vector3.back;
            }
        }

        public static float YawDegrees(GridDir dir)
        {
            switch (dir)
            {
                case GridDir.East:
                    return 90f;
                case GridDir.West:
                    return -90f;
                case GridDir.South:
                    return 180f;
                default:
                    return 0f;
            }
        }

        public static float PadYawDegrees(GridDir dir)
        {
            return dir == GridDir.North || dir == GridDir.South ? 90f : 0f;
        }
    }
}
