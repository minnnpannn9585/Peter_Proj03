// Minimal stand-ins for the UnityEngine members the pure-logic half of the project uses.
//
// WHY THIS EXISTS
// ---------------
// This sandbox has no Unity Editor and no UnityEngine.dll, so the real project cannot be
// compiled here. These shims let `dotnet build` type-check (and actually *run*) every class
// that does not derive from MonoBehaviour: Meta/*, Mission/*, Level/SpawnSchedule and friends.
//
// WHAT THIS IS NOT
// ----------------
// This is NOT a Unity build. MonoBehaviour-derived code is deliberately excluded, and a green
// `dotnet build` here does not prove the Unity compile is clean. See Tools/OfflineTypecheck/README.md.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float Epsilon = 1.401298E-45f;
        public const float Rad2Deg = 57.29578f;
        public const float Deg2Rad = 0.0174532924f;
        public const float PI = 3.14159274f;

        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sign(float v) => v < 0f ? -1f : 1f;

        public static bool Approximately(float a, float b)
        {
            return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f);
        }
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x;
        public int y;

        public Vector2Int(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(Vector2Int other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Vector2Int v && Equals(v);
        public override int GetHashCode() => (x * 397) ^ y;
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.Equals(b);
        public static bool operator !=(Vector2Int a, Vector2Int b) => !a.Equals(b);
        public override string ToString() => "(" + x + ", " + y + ")";
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x;
        public int y;
        public int z;

        public Vector3Int(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(Vector3Int other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is Vector3Int v && Equals(v);
        public override int GetHashCode() => ((x * 397) ^ y) * 397 ^ z;
        public static bool operator ==(Vector3Int a, Vector3Int b) => a.Equals(b);
        public static bool operator !=(Vector3Int a, Vector3Int b) => !a.Equals(b);
        public override string ToString() => "(" + x + ", " + y + ", " + z + ")";
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);

        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1E-05f ? new Vector3(x / m, y / m, z / m) : zero;
            }
        }

        public void Normalize()
        {
            Vector3 n = normalized;
            x = n.x;
            y = n.y;
            z = n.z;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * Mathf.Clamp01(t);

        public override string ToString() =>
            "(" + x.ToString("F2", CultureInfo.InvariantCulture) + ", " +
            y.ToString("F2", CultureInfo.InvariantCulture) + ", " +
            z.ToString("F2", CultureInfo.InvariantCulture) + ")";
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);
    }

    /// <summary>Stub so DestinationPalette can be type-checked offline.</summary>
    public class Material
    {
        readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();

        public bool HasProperty(string name) => true;

        public void SetColor(string name, Color value) => colors[name] = value;

        public Color GetColor(string name) =>
            colors.TryGetValue(name, out Color value) ? value : Color.white;
    }

    public class MaterialPropertyBlock
    {
        readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();

        public void SetColor(string name, Color value) => colors[name] = value;

        public Color GetColor(string name) =>
            colors.TryGetValue(name, out Color value) ? value : Color.white;
    }

    public class Renderer
    {
        public Material material { get; } = new Material();

        MaterialPropertyBlock block = new MaterialPropertyBlock();

        public void GetPropertyBlock(MaterialPropertyBlock target)
        {
        }

        public void SetPropertyBlock(MaterialPropertyBlock value) => block = value;

        public MaterialPropertyBlock CurrentBlock => block;
    }

    /// <summary>Records log calls so offline checks can assert on error and warning output.</summary>
    public static class Debug
    {
        public static readonly List<string> Logs = new List<string>();
        public static readonly List<string> Warnings = new List<string>();
        public static readonly List<string> Errors = new List<string>();

        /// <summary>When true, log calls are also echoed to stdout.</summary>
        public static bool Echo { get; set; }

        public static void Log(object message) => Record(Logs, "LOG", message);
        public static void LogWarning(object message) => Record(Warnings, "WARN", message);
        public static void LogError(object message) => Record(Errors, "ERROR", message);

        public static void Clear()
        {
            Logs.Clear();
            Warnings.Clear();
            Errors.Clear();
        }

        static void Record(List<string> sink, string tag, object message)
        {
            string text = message == null ? "null" : message.ToString();
            sink.Add(text);
            if (Echo)
            {
                Console.WriteLine("[" + tag + "] " + text);
            }
        }
    }

    public static class Application
    {
        public static string persistentDataPath { get; set; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ParcelSortOffline");

        public static string streamingAssetsPath { get; set; } = "Assets/StreamingAssets";
    }
}
