using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// One row of the mission list. Shows the name, both reward figures, whether it has been
    /// cleared, and any special mechanic - the last of these matters because a player should
    /// know a mission has blind parcels *before* spending money to attempt it.
    /// </summary>
    public class MissionEntryUI : MonoBehaviour
    {
        Image background;
        Text titleLabel;
        Text rewardLabel;
        Text tagLabel;
        Text statusLabel;
        Button button;

        MissionListPanel owner;
        MissionDef mission;

        public MissionDef Mission => mission;

        public void Build(Transform parent)
        {
            var rect = gameObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = gameObject.AddComponent<RectTransform>();
            }

            rect.SetParent(parent, false);
            rect.sizeDelta = HudLayout.MissionEntrySize;

            background = gameObject.AddComponent<Image>();
            background.color = HudFactory.CardColor;
            button = gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(OnClicked);

            var element = gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = HudLayout.MissionEntrySize.x;
            element.preferredHeight = HudLayout.MissionEntrySize.y;
            element.minHeight = HudLayout.MissionEntrySize.y;

            titleLabel = HudFactory.CreateText("Title", rect, HudFactory.Row(8f, 30f),
                string.Empty, 22, TextAnchor.UpperLeft, HudFactory.TextColor);
            rewardLabel = HudFactory.CreateText("Reward", rect, HudFactory.Row(42f, 24f),
                string.Empty, 17, TextAnchor.UpperLeft, HudFactory.FullColor);
            tagLabel = HudFactory.CreateText("Tag", rect, HudFactory.Row(70f, 24f),
                string.Empty, 17, TextAnchor.UpperLeft, HudFactory.WarnColor);
            statusLabel = HudFactory.CreateText("Status", rect, HudFactory.Row(98f, 24f),
                string.Empty, 17, TextAnchor.UpperLeft, HudFactory.MutedTextColor);
        }

        public void Bind(MissionListPanel panel, MissionDef def)
        {
            owner = panel;
            mission = def;
            Refresh();
        }

        void OnClicked()
        {
            owner?.OnEntryClicked(this);
        }

        public void Refresh()
        {
            if (mission == null || owner == null)
            {
                return;
            }

            YardDirector director = owner.Director;
            bool selectable = director != null && director.IsSelectable(mission);
            bool selected = director != null && director.SelectedMission == mission;
            MissionRecord record = director?.RecordFor(mission);
            bool cleared = record != null && record.Cleared;

            if (titleLabel != null)
            {
                // The icon key is carried through as a text prefix: this build ships no icon
                // atlas, so the key is shown rather than silently dropped.
                titleLabel.text = (cleared ? "✔ " : string.Empty) + mission.displayName;
                titleLabel.color = selectable ? HudFactory.TextColor : HudFactory.WarnColor;
            }

            if (rewardLabel != null)
            {
                rewardLabel.text = "奖励 " + mission.rewards.baseCoins +
                                   "  首通 +" + mission.rewards.firstClearCoins;
            }

            if (tagLabel != null)
            {
                string tag = mission.SpecialTag();
                tagLabel.text = tag;
                HudFactory.SetActive(tagLabel.gameObject, !string.IsNullOrEmpty(tag));
            }

            if (statusLabel != null)
            {
                if (!selectable)
                {
                    statusLabel.text = "配置错误";
                    statusLabel.color = HudFactory.WarnColor;
                }
                else
                {
                    int planned = director != null ? director.PlannedFor(mission) : 0;
                    statusLabel.text = "目标 " + mission.objective.targetDelivered + " / 共 " + planned +
                                       (cleared ? "  已通关" : string.Empty);
                    statusLabel.color = cleared ? HudFactory.GoodColor : HudFactory.MutedTextColor;
                }
            }

            if (background != null)
            {
                background.color = !selectable
                    ? HudFactory.CardDimColor
                    : (selected ? HudFactory.SelectedColor : HudFactory.CardColor);
            }

            if (button != null)
            {
                button.interactable = selectable;
            }
        }
    }
}
