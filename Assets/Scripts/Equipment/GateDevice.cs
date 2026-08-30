using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Installable stop bar. Closing it holds parcels back so they queue up on the belt;
    /// it never touches production. Auto opens after holdSeconds, shown by a small bar.
    /// </summary>
    public class GateDevice : MonoBehaviour, IYardClickable
    {
        static readonly Color OpenColor = new Color(0.32f, 0.66f, 0.34f, 1f);
        static readonly Color ClosedColor = new Color(0.86f, 0.24f, 0.18f, 1f);

        [SerializeField] Transform bar;
        [SerializeField] Renderer barRenderer;
        [SerializeField] Transform countdownRoot;
        [SerializeField] Image countdownFill;

        public BeltPath Belt { get; private set; }
        public int SlotIndex { get; private set; } = -1;
        public float Distance { get; private set; }
        public bool IsClosed { get; private set; }

        float holdSeconds = 8f;
        float closedTime;
        Camera billboardCamera;

        public float RemainingSeconds => IsClosed ? Mathf.Max(0f, holdSeconds - closedTime) : 0f;

        public void Install(BeltPath belt, int slotIndex, float hold)
        {
            Belt = belt;
            SlotIndex = slotIndex;
            holdSeconds = Mathf.Max(0.5f, hold);
            Distance = belt.GetSlotDistance(slotIndex);
            IsClosed = false;
            closedTime = 0f;

            belt.Evaluate(Distance, out Vector3 position, out Vector3 tangent);
            transform.SetParent(belt.transform, true);
            transform.localScale = Vector3.one * BeltPath.VisualScale;
            transform.position = position - Vector3.up * BeltPath.ParcelHalf;
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            gameObject.name = "Gate_" + belt.BeltId + "_" + slotIndex;

            belt.Gates.Add(this);
            belt.SortGates();
            belt.SetSlotOccupied(slotIndex, true);
            ResolveParts();
            Refresh();
        }

        public void OnYardClick()
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            closedTime = 0f;
            Refresh();
        }

        void ResolveParts()
        {
            if (bar == null)
            {
                bar = transform.Find("Bar");
            }

            if (barRenderer == null && bar != null)
            {
                barRenderer = bar.GetComponent<Renderer>();
            }

            if (countdownRoot == null)
            {
                countdownRoot = transform.Find("Countdown");
            }

            if (countdownFill == null && countdownRoot != null)
            {
                Transform fill = countdownRoot.Find("Fill");
                if (fill != null)
                {
                    countdownFill = fill.GetComponent<Image>();
                }
            }
        }

        void Update()
        {
            Advance(Time.deltaTime);
            Billboard();
        }

        /// <summary>
        /// Advances the hold timer and opens the gate when it expires. Public and separate from
        /// Update so a test can drive the 8 second hold at a fixed timestep, which is what the
        /// "a closed gate must not count as a jam" regression needs.
        /// </summary>
        public void Advance(float dt)
        {
            if (!IsClosed || dt <= 0f)
            {
                return;
            }

            closedTime += dt;
            if (closedTime >= holdSeconds)
            {
                IsClosed = false;
                closedTime = 0f;
                Refresh();
                return;
            }

            if (countdownFill != null)
            {
                countdownFill.fillAmount = Mathf.Clamp01(1f - closedTime / holdSeconds);
            }
        }

        /// <summary>Closes the gate. Same effect as a player click, without needing a raycast.</summary>
        public void Close()
        {
            OnYardClick();
        }

        void Billboard()
        {
            if (countdownRoot == null || !countdownRoot.gameObject.activeSelf)
            {
                return;
            }

            if (billboardCamera == null)
            {
                billboardCamera = Camera.main;
            }

            if (billboardCamera == null)
            {
                return;
            }

            Vector3 toCamera = billboardCamera.transform.position - countdownRoot.position;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                return;
            }

            countdownRoot.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }

        void Refresh()
        {
            ResolveParts();
            if (bar != null)
            {
                // The bar drops into the belt when open so it reads as "clear".
                bar.localPosition = new Vector3(0f, IsClosed ? 0.42f : 0.06f, 0f);
            }

            DestinationPalette.Apply(barRenderer, IsClosed ? ClosedColor : OpenColor);

            if (countdownRoot != null)
            {
                countdownRoot.gameObject.SetActive(IsClosed);
            }

            if (countdownFill != null)
            {
                countdownFill.fillAmount = 1f;
            }
        }

        void OnDestroy()
        {
            if (Belt != null)
            {
                Belt.Gates.Remove(this);
                Belt.SetSlotOccupied(SlotIndex, false);
            }
        }
    }
}
