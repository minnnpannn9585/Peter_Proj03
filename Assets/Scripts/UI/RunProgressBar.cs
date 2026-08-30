using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// The only prep-era panel that survives into the round: how many parcels are left and how
    /// long there is to move them. Everything else is hidden so the player can watch the yard.
    /// </summary>
    public class RunProgressBar : MonoBehaviour, IHudPanel
    {
        RectTransform root;
        Image fill;
        Text progressLabel;
        Text timerLabel;
        YardDirector director;

        public GameObject Panel => gameObject;

        public void Build(Transform parent)
        {
            root = HudFactory.Configure(
                gameObject, parent, HudLayout.RunProgressPanel, HudFactory.PanelColor);

            RectTransform fillRect = HudFactory.CreateRect("ProgressFill", root, HudLayout.ProgressFill);
            fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = HudFactory.FillColor;
            fill.raycastTarget = false;

            // A sprite-less Image cannot use fill modes, so the built-in UI sprite is required.
            fill.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            progressLabel = HudFactory.CreateText("ProgressLabel", root, HudLayout.ProgressLabel,
                string.Empty, 22, TextAnchor.MiddleCenter, HudFactory.TextColor);

            timerLabel = HudFactory.CreateText("TimerLabel", root, HudLayout.TimerLabel,
                string.Empty, 20, TextAnchor.UpperRight, HudFactory.TextColor);
        }

        public void Bind(YardDirector yardDirector)
        {
            director = yardDirector;
            Refresh();
        }

        void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (director == null)
            {
                return;
            }

            MissionRuntime mission = director.Mission;
            int planned = mission != null ? mission.Planned : 0;
            int left = mission != null ? mission.LeftToDeliver : 0;

            if (progressLabel != null)
            {
                progressLabel.text = "剩余 " + left + " / " + planned;
            }

            if (fill != null)
            {
                // Guard the divide: a mission whose plan released nothing shows an empty bar
                // rather than producing a NaN width.
                fill.fillAmount = planned <= 0
                    ? 0f
                    : Mathf.Clamp01(1f - (float)left / planned);
            }

            if (timerLabel != null)
            {
                float remaining = mission != null ? mission.Remaining : 0f;
                int total = Mathf.CeilToInt(remaining);
                int minutes = total / 60;
                int seconds = total % 60;
                timerLabel.text = minutes + ":" + seconds.ToString("00");
                timerLabel.color = remaining <= 15f ? HudFactory.WarnColor : HudFactory.TextColor;
            }
        }
    }
}
