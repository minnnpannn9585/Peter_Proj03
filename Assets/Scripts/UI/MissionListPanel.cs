using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Left-hand mission picker, shown during prep only. One entry per authored mission, plus an
    /// empty-state line so a level with no missions explains itself instead of showing a blank box.
    /// </summary>
    public class MissionListPanel : MonoBehaviour, IHudPanel
    {
        readonly List<MissionEntryUI> entries = new List<MissionEntryUI>();

        RectTransform root;
        Text emptyLabel;
        YardDirector director;

        public GameObject Panel => gameObject;

        public YardDirector Director => director;

        public void Build(Transform parent)
        {
            root = HudFactory.Configure(
                gameObject, parent, HudLayout.MissionListPanel, HudFactory.PanelColor);

            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = HudLayout.MissionListSpacing;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            emptyLabel = HudFactory.CreateText("Empty", root, HudFactory.Stretch(),
                "本关卡没有可选任务", 20, TextAnchor.MiddleCenter, HudFactory.MutedTextColor);

            // Exempt from the layout group so it can sit centred over the whole panel.
            emptyLabel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HudFactory.SetActive(emptyLabel.gameObject, false);
        }

        public void Bind(YardDirector yardDirector)
        {
            director = yardDirector;
            Rebuild();
        }

        void Rebuild()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    Destroy(entries[i].gameObject);
                }
            }

            entries.Clear();

            if (director == null || root == null)
            {
                return;
            }

            IReadOnlyList<MissionDef> missions = director.Missions;
            for (int i = 0; i < missions.Count; i++)
            {
                // Created with a RectTransform up front: adding one to a plain Transform later is
                // legal but relies on Unity swapping the component out from under us.
                var go = new GameObject("MissionEntry_" + missions[i].id, typeof(RectTransform));
                var entry = go.AddComponent<MissionEntryUI>();
                entry.Build(root);
                entry.Bind(this, missions[i]);
                entries.Add(entry);
            }

            if (emptyLabel != null)
            {
                HudFactory.SetActive(emptyLabel.gameObject, missions.Count == 0);
            }

            // The empty-state label must not be pushed around by the layout group.
            if (emptyLabel != null)
            {
                emptyLabel.transform.SetAsLastSibling();
            }
        }

        public void OnEntryClicked(MissionEntryUI entry)
        {
            if (director == null || entry == null || entry.Mission == null)
            {
                return;
            }

            director.TrySelectMission(entry.Mission.id);
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i]?.Refresh();
            }
        }
    }
}
