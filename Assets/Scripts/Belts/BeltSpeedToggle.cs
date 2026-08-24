using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>Whole-belt speed boost. Chevrons are spread along the whole run.</summary>
    public class BeltSpeedToggle : MonoBehaviour, IYardClickable
    {
        static readonly Color FastColor = new Color(0.95f, 0.62f, 0.16f, 1f);
        static readonly Color NormalColor = new Color(0.45f, 0.45f, 0.48f, 1f);

        BeltPath belt;
        float fastMultiplier = 2f;
        bool fast;
        readonly List<GameObject> chevrons = new List<GameObject>();

        public bool IsFast => fast;

        public void Configure(float multiplier, GameObject chevronPrefab)
        {
            belt = GetComponent<BeltPath>();
            fastMultiplier = Mathf.Max(1.05f, multiplier);
            fast = false;
            BuildChevrons(chevronPrefab);
            Apply();
        }

        public void OnYardClick()
        {
            fast = !fast;
            Apply();
        }

        void BuildChevrons(GameObject chevronPrefab)
        {
            if (chevronPrefab == null || belt == null || belt.Length <= 0.01f)
            {
                return;
            }

            float spacing = Mathf.Max(0.8f, belt.GridSize * 0.75f);
            for (float d = spacing * 0.5f; d < belt.Length; d += spacing)
            {
                belt.Evaluate(d, out Vector3 position, out Vector3 tangent);
                GameObject chevron = Instantiate(chevronPrefab, belt.transform);
                chevron.name = "Chevron";
                float s = BeltPath.VisualScale;
                chevron.transform.localScale = Vector3.one * s;
                chevron.transform.position = position - Vector3.up * (BeltPath.ParcelHalf - 0.04f * s);
                chevron.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                chevrons.Add(chevron);
            }
        }

        void Apply()
        {
            if (belt == null)
            {
                belt = GetComponent<BeltPath>();
            }

            if (belt == null)
            {
                return;
            }

            belt.SpeedMultiplier = fast ? fastMultiplier : 1f;
            DestinationPalette.ApplyAll(belt.DeckRenderers, fast ? FastColor : NormalColor);

            for (int i = 0; i < chevrons.Count; i++)
            {
                if (chevrons[i] != null)
                {
                    chevrons[i].SetActive(fast);
                }
            }
        }
    }
}
