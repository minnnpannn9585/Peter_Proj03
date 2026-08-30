// A larger UnityEngine / UnityEngine.UI / UnityEngine.EventSystems shim whose only purpose is to
// let `dotnet build` TYPE-CHECK every script in Assets/Scripts, including the MonoBehaviour half.
//
// WHY
// ---
// This sandbox has no Unity Editor and no UnityEngine.dll, so `Assets/Scripts` cannot be compiled
// against the real engine here. Without something like this, MonoBehaviour code could only be
// "verified" by reading it. These declarations mirror the shape of the API the project uses -
// names, signatures, generic constraints - so a genuine compile error (a typo, a wrong overload,
// a missing member, an inexhaustive switch) fails the build.
//
// WHAT THIS IS NOT
// ----------------
// Nothing here implements engine behaviour, and this project is never executed. A green build
// proves the code is type-correct against this surface; it does NOT prove the Unity compile is
// clean (Unity may differ in details) and it says nothing about runtime behaviour.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float Epsilon = 1.401298E-45f;
        public const float Rad2Deg = 57.29578f;
        public const float Deg2Rad = 0.0174532924f;
        public const float PI = 3.14159274f;
        public const float Infinity = float.PositiveInfinity;

        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sign(float v) => v < 0f ? -1f : 1f;
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float MoveTowards(float a, float b, float d) => b;
        public static float SmoothStep(float a, float b, float t) => Lerp(a, b, t);
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < 1E-05f;
    }

    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);
        public float magnitude => Mathf.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
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
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
        public Vector3 normalized => this * (1f / Mathf.Max(1e-5f, magnitude));

        public void Normalize()
        {
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * Mathf.Clamp01(t);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    public struct Quaternion
    {
        public static Quaternion identity => new Quaternion();

        public static Quaternion Euler(float x, float y, float z) => identity;

        public static Quaternion LookRotation(Vector3 forward) => identity;

        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => identity;

        public static Vector3 operator *(Quaternion q, Vector3 v) => v;

        public static Quaternion operator *(Quaternion a, Quaternion b) => a;

        public static Quaternion Inverse(Quaternion q) => q;

        public static Quaternion AngleAxis(float angle, Vector3 axis) => identity;

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a;
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
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);
    }

    public struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            this.size = size;
        }

        public Vector3 center { get; set; }
        public Vector3 size { get; set; }
        public Vector3 min => center - size * 0.5f;
        public Vector3 max => center + size * 0.5f;

        public void Encapsulate(Bounds other)
        {
        }

        public void Encapsulate(Vector3 point)
        {
        }

        public void Expand(Vector3 amount)
        {
        }

        public void Expand(float amount)
        {
        }
    }

    public struct Ray
    {
        public Vector3 origin { get; set; }
        public Vector3 direction { get; set; }
    }

    public struct RaycastHit
    {
        public Collider collider { get; set; }
        public float distance { get; set; }
        public Vector3 point { get; set; }
    }

    public static class Physics
    {
        public static int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance) => 0;

        public static bool Raycast(Ray ray, out RaycastHit hit, float maxDistance)
        {
            hit = default;
            return false;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
        public int order;
    }

    /// <summary>
    /// Mirrors UnityEngine.Object, including the null-comparison operators the project relies on
    /// (`component != null` after a Destroy, and `?.` on component references).
    /// </summary>
    public class Object
    {
        public string name { get; set; } = string.Empty;

        public static implicit operator bool(Object value) => !ReferenceEquals(value, null);

        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);

        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);

        public override bool Equals(object other) => ReferenceEquals(this, other);

        public override int GetHashCode() => base.GetHashCode();

        public static void Destroy(Object target)
        {
        }

        public static void Destroy(Object target, float delay)
        {
        }

        public static void DestroyImmediate(Object target)
        {
        }

        public static T Instantiate<T>(T original) where T : Object => original;

        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation)
            where T : Object => original;

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent)
            where T : Object => original;

        public static T FindFirstObjectByType<T>() where T : Object => null;

        public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object =>
            Array.Empty<T>();
    }

    public enum PrimitiveType
    {
        Sphere = 0,
        Capsule = 1,
        Cylinder = 2,
        Cube = 3,
        Plane = 4,
        Quad = 5
    }

    public enum FindObjectsSortMode
    {
        None = 0,
        InstanceID = 1
    }

    public class Component : Object
    {
        public GameObject gameObject { get; } = null;
        public Transform transform { get; } = null;
        public string tag { get; set; } = string.Empty;

        public T GetComponent<T>() => default;

        public Component GetComponent(Type type) => null;

        public T GetComponentInChildren<T>() => default;

        public T GetComponentInChildren<T>(bool includeInactive) => default;

        public T[] GetComponentsInChildren<T>() => Array.Empty<T>();

        public T[] GetComponentsInChildren<T>(bool includeInactive) => Array.Empty<T>();

        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results)
        {
        }

        public T GetComponentInParent<T>() => default;

        public T[] GetComponents<T>() => Array.Empty<T>();
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; } = true;
        public bool isActiveAndEnabled => enabled;
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;

        public void StopCoroutine(IEnumerator routine)
        {
        }

        public void StopAllCoroutines()
        {
        }

        public void Invoke(string methodName, float time)
        {
        }
    }

    public class Coroutine
    {
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => null;
    }

    public sealed class GameObject : Object
    {
        public GameObject()
        {
        }

        public GameObject(string goName)
        {
            name = goName;
        }

        public GameObject(string goName, params Type[] components)
        {
            name = goName;
        }

        public Transform transform { get; } = null;
        public int layer { get; set; }
        public bool activeSelf => true;
        public bool activeInHierarchy => true;

        public void SetActive(bool value)
        {
        }

        public T AddComponent<T>() where T : Component => null;

        public Component AddComponent(Type type) => null;

        public T GetComponent<T>() => default;

        public T GetComponentInChildren<T>() => default;

        public T GetComponentInChildren<T>(bool includeInactive) => default;

        public T[] GetComponentsInChildren<T>() => Array.Empty<T>();

        public T[] GetComponentsInChildren<T>(bool includeInactive) => Array.Empty<T>();

        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results)
        {
        }

        public T GetComponentInParent<T>() => default;

        public static GameObject Find(string searchName) => null;

        public static GameObject CreatePrimitive(PrimitiveType type) => null;
    }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 lossyScale => localScale;
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Transform parent { get; set; }
        public int childCount => 0;

        public Transform GetChild(int index) => null;

        public Transform Find(string path) => null;

        public void SetParent(Transform newParent)
        {
        }

        public void SetParent(Transform newParent, bool worldPositionStays)
        {
        }

        public void SetAsLastSibling()
        {
        }

        public void SetAsFirstSibling()
        {
        }

        public void SetSiblingIndex(int index)
        {
        }

        public void LookAt(Vector3 worldPosition)
        {
        }

        public void LookAt(Transform target)
        {
        }

        public void Rotate(Vector3 eulers)
        {
        }

        public void Translate(Vector3 translation)
        {
        }

        public IEnumerator GetEnumerator() => Array.Empty<Transform>().GetEnumerator();
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect => default;
    }

    public struct Rect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    public class Collider : Component
    {
        public bool enabled { get; set; } = true;
    }

    public class BoxCollider : Collider
    {
        public Vector3 size { get; set; }
        public Vector3 center { get; set; }
    }

    public class Renderer : Component
    {
        public Material material { get; set; } = new Material();
        public Material sharedMaterial { get; set; } = new Material();
        public bool enabled { get; set; } = true;

        public void GetPropertyBlock(MaterialPropertyBlock target)
        {
        }

        public void SetPropertyBlock(MaterialPropertyBlock block)
        {
        }
    }

    public class MeshRenderer : Renderer
    {
    }

    public class Mesh : Object
    {
    }

    public class MeshFilter : Component
    {
        public Mesh mesh { get; set; }
        public Mesh sharedMesh { get; set; }
    }

    public class Material : Object
    {
        public Color color { get; set; }

        public bool HasProperty(string propertyName) => true;

        public void SetColor(string propertyName, Color value)
        {
        }

        public Color GetColor(string propertyName) => Color.white;

        public void SetFloat(string propertyName, float value)
        {
        }
    }

    public class MaterialPropertyBlock
    {
        public void SetColor(string propertyName, Color value)
        {
        }

        public void SetFloat(string propertyName, float value)
        {
        }
    }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public float orthographicSize { get; set; }
        public bool orthographic { get; set; }
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public float aspect => 1.777f;

        public Ray ScreenPointToRay(Vector2 position) => default;

        public Ray ScreenPointToRay(Vector3 position) => default;

        public Vector3 WorldToScreenPoint(Vector3 position) => Vector3.zero;

        public Vector3 ScreenToWorldPoint(Vector3 position) => Vector3.zero;
    }

    public class Font : Object
    {
    }

    public class Sprite : Object
    {
    }

    public class Texture : Object
    {
    }

    public class Texture2D : Texture
    {
    }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object => null;

        public static T Load<T>(string path) where T : Object => null;
    }

    public static class Time
    {
        public static float deltaTime => 0.0166f;
        public static float unscaledDeltaTime => 0.0166f;
        public static float time => 0f;
        public static float timeScale { get; set; } = 1f;
        public static float fixedDeltaTime => 0.02f;
    }

    public static class Input
    {
        public static Vector3 mousePosition => Vector3.zero;
        public static float mouseScrollDelta => 0f;

        public static bool GetMouseButton(int button) => false;

        public static bool GetMouseButtonDown(int button) => false;

        public static bool GetMouseButtonUp(int button) => false;

        public static bool GetKey(KeyCode key) => false;

        public static bool GetKeyDown(KeyCode key) => false;

        public static float GetAxis(string axisName) => 0f;

        public static float GetAxisRaw(string axisName) => 0f;
    }

    public enum KeyCode
    {
        None = 0,
        Space = 32,
        Escape = 27,
        R = 114,
        LeftAlt = 308,
        LeftControl = 306
    }

    public static class Random
    {
        public static float value => 0.5f;

        public static int Range(int min, int max) => min;

        public static float Range(float min, float max) => min;
    }

    public static class Debug
    {
        public static void Log(object message)
        {
        }

        public static void LogWarning(object message)
        {
        }

        public static void LogError(object message)
        {
        }

        public static void DrawLine(Vector3 a, Vector3 b, Color color)
        {
        }
    }

    public static class Application
    {
        public static string persistentDataPath => string.Empty;
        public static string streamingAssetsPath => string.Empty;
        public static string dataPath => string.Empty;
        public static bool isPlaying => true;
    }

    public static class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
    }

    public enum RenderMode
    {
        ScreenSpaceOverlay = 0,
        ScreenSpaceCamera = 1,
        WorldSpace = 2
    }

    public enum TextAnchor
    {
        UpperLeft = 0,
        UpperCenter = 1,
        UpperRight = 2,
        MiddleLeft = 3,
        MiddleCenter = 4,
        MiddleRight = 5,
        LowerLeft = 6,
        LowerCenter = 7,
        LowerRight = 8
    }

    public sealed class RectOffset
    {
        public RectOffset()
        {
        }

        public RectOffset(int left, int right, int top, int bottom)
        {
        }

        public int left { get; set; }
        public int right { get; set; }
        public int top { get; set; }
        public int bottom { get; set; }
    }
}

namespace UnityEngine.UI
{
    public class Graphic : Behaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; } = true;
        public RectTransform rectTransform => null;
        public bool maskable { get; set; } = true;
    }

    public class Image : Graphic
    {
        public enum Type
        {
            Simple = 0,
            Sliced = 1,
            Tiled = 2,
            Filled = 3
        }

        public enum FillMethod
        {
            Horizontal = 0,
            Vertical = 1,
            Radial90 = 2,
            Radial180 = 3,
            Radial360 = 4
        }

        public enum OriginHorizontal
        {
            Left = 0,
            Right = 1
        }

        public enum OriginVertical
        {
            Bottom = 0,
            Top = 1
        }

        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public int fillOrigin { get; set; }
        public float fillAmount { get; set; }
        public bool preserveAspect { get; set; }
    }

    public class Text : Graphic
    {
        public string text { get; set; } = string.Empty;
        public Font font { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public float lineSpacing { get; set; }
        public FontStyle fontStyle { get; set; }
    }

    public enum HorizontalWrapMode
    {
        Wrap = 0,
        Overflow = 1
    }

    public enum VerticalWrapMode
    {
        Truncate = 0,
        Overflow = 1
    }

    public enum FontStyle
    {
        Normal = 0,
        Bold = 1,
        Italic = 2,
        BoldAndItalic = 3
    }

    public class Selectable : Behaviour
    {
        public bool interactable { get; set; } = true;
        public Graphic targetGraphic { get; set; }
    }

    public class ButtonClickedEvent
    {
        public void AddListener(Action call)
        {
        }

        public void RemoveListener(Action call)
        {
        }

        public void RemoveAllListeners()
        {
        }

        public void Invoke()
        {
        }
    }

    public class Button : Selectable
    {
        public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();
    }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public Camera worldCamera { get; set; }
        public int sortingOrder { get; set; }
    }

    public class CanvasScaler : Behaviour
    {
        public enum ScaleMode
        {
            ConstantPixelSize = 0,
            ScaleWithScreenSize = 1,
            ConstantPhysicalSize = 2
        }

        public enum ScreenMatchMode
        {
            MatchWidthOrHeight = 0,
            Expand = 1,
            Shrink = 2
        }

        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : Behaviour
    {
    }

    public class LayoutElement : Behaviour
    {
        public bool ignoreLayout { get; set; }
        public float minWidth { get; set; }
        public float minHeight { get; set; }
        public float preferredWidth { get; set; }
        public float preferredHeight { get; set; }
        public float flexibleWidth { get; set; }
        public float flexibleHeight { get; set; }
    }

    public abstract class LayoutGroup : Behaviour
    {
        public RectOffset padding { get; set; } = new RectOffset();
        public TextAnchor childAlignment { get; set; }
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
        public bool childScaleWidth { get; set; }
        public bool childScaleHeight { get; set; }
        public bool reverseArrangement { get; set; }
    }

    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
    }

    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
    }

    public class ContentSizeFitter : Behaviour
    {
    }
}

namespace UnityEngine.EventSystems
{
    public class EventSystem : Behaviour
    {
        public static EventSystem current => null;

        public bool IsPointerOverGameObject() => false;
    }

    public class PointerEventData
    {
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public GameObject pointerEnter { get; set; }
        public int pointerId { get; set; }
    }

    public interface IEventSystemHandler
    {
    }

    public interface IBeginDragHandler : IEventSystemHandler
    {
        void OnBeginDrag(PointerEventData eventData);
    }

    public interface IDragHandler : IEventSystemHandler
    {
        void OnDrag(PointerEventData eventData);
    }

    public interface IEndDragHandler : IEventSystemHandler
    {
        void OnEndDrag(PointerEventData eventData);
    }

    public interface IPointerClickHandler : IEventSystemHandler
    {
        void OnPointerClick(PointerEventData eventData);
    }

    public interface IPointerDownHandler : IEventSystemHandler
    {
        void OnPointerDown(PointerEventData eventData);
    }

    public interface IPointerEnterHandler : IEventSystemHandler
    {
        void OnPointerEnter(PointerEventData eventData);
    }

    public interface IPointerExitHandler : IEventSystemHandler
    {
        void OnPointerExit(PointerEventData eventData);
    }
}
