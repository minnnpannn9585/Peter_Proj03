using UnityEngine;

namespace ParcelSort
{
    public class YardCameraRig : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] float pitch = 50f;
        [SerializeField] float padding = 1.04f;
        [SerializeField] float fieldOfView = 35f;
        [SerializeField] float orbitSpeed = 0.25f;
        [SerializeField] float zoomSpeed = 0.12f;
        [SerializeField] float minPitch = 15f;
        [SerializeField] float maxPitch = 85f;

        Vector3 pivot;
        float yaw = 180f;
        float distance = 40f;
        float baseDistance = 40f;
        bool framed;

        public bool IsOrbiting { get; private set; }

        /// <summary>Locks the orbit pivot to the yard centre and picks a default framing.</summary>
        public void Frame(Bounds bounds)
        {
            Camera cam = TargetCamera;
            if (cam == null)
            {
                return;
            }

            pivot = new Vector3(bounds.center.x, 0f, bounds.center.z);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            yaw = 180f;
            cam.fieldOfView = fieldOfView;
            cam.orthographic = false;
            baseDistance = RequiredDistance(cam, bounds);
            distance = baseDistance;
            framed = true;
            ApplyTransform();
        }

        /// <summary>
        /// Closest distance that still keeps every bounds corner on screen. Solving both the
        /// vertical and the horizontal FOV matters: a wide yard framed by vertical FOV alone
        /// pulls the camera roughly twice as far back as it needs to be.
        /// </summary>
        float RequiredDistance(Camera cam, Bounds bounds)
        {
            float tanV = Mathf.Max(0.01f, Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad));
            float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            float tanH = tanV * aspect;
            Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(pitch, yaw, 0f));

            float needed = 1f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);

                // Camera sits at pivot - forward * distance, so depth is v.z + distance.
                Vector3 v = inverse * (corner - pivot);
                needed = Mathf.Max(needed, Mathf.Abs(v.y) / tanV - v.z);
                needed = Mathf.Max(needed, Mathf.Abs(v.x) / tanH - v.z);
            }

            return needed * Mathf.Max(1f, padding);
        }

        Camera TargetCamera => targetCamera != null ? targetCamera : Camera.main;

        void Update()
        {
            if (!framed)
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                IsOrbiting = true;
            }

            if (Input.GetMouseButtonUp(1))
            {
                IsOrbiting = false;
            }

            bool dirty = false;
            if (IsOrbiting)
            {
                float dx = Input.GetAxisRaw("Mouse X");
                float dy = Input.GetAxisRaw("Mouse Y");
                if (Mathf.Abs(dx) > 0.0001f || Mathf.Abs(dy) > 0.0001f)
                {
                    yaw += dx * orbitSpeed * 60f * Time.unscaledDeltaTime * 10f;
                    pitch = Mathf.Clamp(
                        pitch + dy * orbitSpeed * 60f * Time.unscaledDeltaTime * 10f,
                        minPitch,
                        maxPitch);
                    dirty = true;
                }
            }

            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                distance = Mathf.Clamp(
                    distance * (1f - scroll * zoomSpeed * 10f),
                    baseDistance * 0.3f,
                    baseDistance * 2.5f);
                dirty = true;
            }

            if (dirty)
            {
                ApplyTransform();
            }
        }

        void ApplyTransform()
        {
            Camera cam = TargetCamera;
            if (cam == null)
            {
                return;
            }

            // Negative Z so a positive pitch lifts the camera above the yard.
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
            cam.transform.position = pivot + offset;
            cam.transform.LookAt(pivot);
        }
    }
}
