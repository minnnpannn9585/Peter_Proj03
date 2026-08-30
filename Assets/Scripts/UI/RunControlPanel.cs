using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Bottom left run controls: freeze the round, or walk away from it.
    ///
    /// These are the only two HUD actions that exist purely for the player rather than for the
    /// fiction, so they are kept together, off in the corner the device shop vacates when the
    /// round starts. PAUSE relabels itself to RESUME instead of adding a third button, so the
    /// current state is readable without a separate indicator.
    /// </summary>
    public class RunControlPanel : MonoBehaviour, IHudPanel
    {
        Button pauseButton;
        Text pauseLabel;
        Button exitButton;
        YardDirector director;

        public GameObject Panel => gameObject;

        public void Build(Transform parent)
        {
            RectTransform root = HudFactory.Configure(
                gameObject, parent, HudLayout.RunControlPanel, HudFactory.PanelColor);

            pauseButton = HudFactory.CreateButton("Pause", root, HudLayout.PauseButton,
                UiStrings.Pause, 22, out pauseLabel);
            pauseButton.onClick.AddListener(OnPauseClicked);

            exitButton = HudFactory.CreateButton("Exit", root, HudLayout.ExitButton,
                UiStrings.ExitLevel, 22, out _);
            exitButton.onClick.AddListener(OnExitClicked);
        }

        public void Bind(YardDirector yardDirector)
        {
            if (director != null)
            {
                director.PauseChanged -= OnPauseChanged;
            }

            director = yardDirector;
            if (director != null)
            {
                director.PauseChanged += OnPauseChanged;
            }

            Refresh();
        }

        void OnDestroy()
        {
            if (director != null)
            {
                director.PauseChanged -= OnPauseChanged;
            }
        }

        void OnPauseChanged(bool paused)
        {
            Refresh();
        }

        void OnPauseClicked()
        {
            director?.TogglePause();
        }

        void OnExitClicked()
        {
            director?.ExitLevel();
        }

        public void Refresh()
        {
            if (director == null)
            {
                return;
            }

            bool running = director.Phase == GamePhase.Running;
            bool paused = director.IsPaused;

            if (pauseLabel != null)
            {
                pauseLabel.text = paused ? UiStrings.Resume : UiStrings.Pause;
                pauseLabel.color = paused ? HudFactory.FullColor : HudFactory.TextColor;
            }

            if (pauseButton != null)
            {
                // Only a live round can be frozen. On the result screen the clock has already
                // stopped, so the button goes dead rather than lying about what it would do.
                pauseButton.interactable = running;
                if (pauseButton.targetGraphic != null)
                {
                    pauseButton.targetGraphic.color = running
                        ? (paused ? HudFactory.SelectedColor : HudFactory.CardColor)
                        : HudFactory.CardDimColor;
                }
            }

            if (exitButton != null)
            {
                // Leaving is always allowed while this panel is up, paused or not: a player who
                // wants out of a round must never have to finish it first.
                exitButton.interactable = true;
            }
        }
    }
}
