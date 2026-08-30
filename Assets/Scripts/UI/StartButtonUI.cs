using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Bottom right START button. The subtitle names the mission that is about to be played, so
    /// the player cannot start the wrong round by accident, and the button is dead until a
    /// mission is actually chosen.
    /// </summary>
    public class StartButtonUI : MonoBehaviour, IHudPanel
    {
        Button button;
        Image background;
        Text titleLabel;
        Text subtitleLabel;
        YardDirector director;

        public GameObject Panel => gameObject;

        public void Build(Transform parent)
        {
            RectTransform rect = HudFactory.Configure(
                gameObject, parent, HudLayout.StartButton, HudFactory.SelectedColor);

            background = rect.gameObject.GetComponent<Image>();
            background.raycastTarget = true;
            button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(OnClicked);

            titleLabel = HudFactory.CreateText("Title", rect,
                new RectSpec(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, -16f), new Vector2(0f, 44f)),
                UiStrings.Start, 34, TextAnchor.UpperCenter, HudFactory.TextColor);

            subtitleLabel = HudFactory.CreateText("Subtitle", rect,
                new RectSpec(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    new Vector2(0f, 14f), new Vector2(0f, 34f)),
                string.Empty, 20, TextAnchor.LowerCenter, HudFactory.TextColor);
        }

        public void Bind(YardDirector yardDirector)
        {
            director = yardDirector;
            Refresh();
        }

        void OnClicked()
        {
            director?.StartRound();
        }

        public void Refresh()
        {
            if (director == null)
            {
                return;
            }

            MissionDef selected = director.SelectedMission;
            bool ready = selected != null && director.IsSelectable(selected);

            if (titleLabel != null)
            {
                titleLabel.text = "开始";
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = ready ? selected.displayName : "请先选择任务";
                subtitleLabel.color = ready ? HudFactory.TextColor : HudFactory.MutedTextColor;
            }

            if (button != null)
            {
                button.interactable = ready;
            }

            if (background != null)
            {
                background.color = ready ? HudFactory.SelectedColor : HudFactory.CardDimColor;
            }
        }
    }
}
