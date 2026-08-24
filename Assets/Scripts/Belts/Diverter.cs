using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Player operated switch on a junction node. Cycles through the node's outgoing belts,
    /// so it supports any number of branches even though Map 1 and Map 2 only use two.
    /// </summary>
    public class Diverter : MonoBehaviour, IYardClickable
    {
        static readonly Color IndicatorColor = new Color(1f, 0.78f, 0.15f, 1f);

        [SerializeField] Transform indicator;

        YardNode node;

        public int SelectedIndex => node != null ? node.SelectedOutput : 0;

        public void Configure(YardNode owner)
        {
            node = owner;
            ResolveIndicator();
            Refresh();
        }

        public void OnYardClick()
        {
            if (node == null || node.OutBelts.Count <= 1)
            {
                return;
            }

            node.SelectedOutput = (node.SelectedOutput + 1) % node.OutBelts.Count;
            Refresh();
        }

        void ResolveIndicator()
        {
            if (indicator != null)
            {
                return;
            }

            Transform found = transform.Find("Indicator");
            if (found != null)
            {
                indicator = found;
            }
        }

        public void Refresh()
        {
            ResolveIndicator();
            if (indicator == null || node == null)
            {
                return;
            }

            BeltPath belt = node.SelectedBelt;
            if (belt == null)
            {
                return;
            }

            belt.Evaluate(0f, out _, out Vector3 tangent);
            Vector3 flat = new Vector3(tangent.x, 0f, tangent.z);
            if (flat.sqrMagnitude < 0.000001f)
            {
                return;
            }

            flat.Normalize();
            float reach = indicator.localScale.z * 0.5f + 0.1f;
            indicator.rotation = Quaternion.LookRotation(flat, Vector3.up);
            indicator.position = node.PathPos + Vector3.up * 0.22f + flat * reach;
            DestinationPalette.Apply(indicator.GetComponent<Renderer>(), IndicatorColor);
        }
    }
}
