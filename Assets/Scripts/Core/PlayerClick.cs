using UnityEngine;

namespace ParcelSort
{
    public class PlayerClick : MonoBehaviour
    {
        [SerializeField] Camera rayCamera;
        [SerializeField] float maxDistance = 500f;
        [SerializeField] YardDirector director;
        [SerializeField] YardCameraRig cameraRig;

        readonly RaycastHit[] hits = new RaycastHit[32];

        void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (IsBlocked())
            {
                return;
            }

            Camera cam = rayCamera != null ? rayCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            int count = Physics.RaycastNonAlloc(ray, hits, maxDistance);
            if (count <= 0)
            {
                return;
            }

            float closestDistance = float.MaxValue;
            IYardClickable closest = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider.GetComponentInParent<Parcel>() != null)
                {
                    continue;
                }

                IYardClickable clickable = hit.collider.GetComponentInParent<IYardClickable>();
                if (clickable == null)
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closest = clickable;
                }
            }

            if (closest != null)
            {
                closest.OnYardClick();
            }
        }

        /// <summary>
        /// Yard clicks are ignored over UI, while orbiting the camera, and outside the round.
        /// During prep the yard is arranged by dragging shop cards, not by clicking; on the result
        /// screen the round is already settled, so a click on a gate would do nothing useful.
        /// </summary>
        bool IsBlocked()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<YardCameraRig>();
            }

            if (cameraRig != null && cameraRig.IsOrbiting)
            {
                return true;
            }

            if (director == null)
            {
                director = FindFirstObjectByType<YardDirector>();
            }

            return director != null && director.Phase != GamePhase.Running;
        }
    }
}
