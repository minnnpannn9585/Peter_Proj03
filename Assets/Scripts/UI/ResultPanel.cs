using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// The settle screen. Layered over the running layout rather than replacing it, so the player
    /// can still see the yard they just worked. States the reason for a loss explicitly: "you ran
    /// out of time" and "you misrouted too many" call for completely different next attempts.
    /// </summary>
    public class ResultPanel : MonoBehaviour, IHudPanel
    {
        RectTransform root;
        Text headlineLabel;
        Text reasonLabel;
        Text statsLabel;
        Text coinsLabel;
        Button retryButton;
        Button backButton;
        YardDirector director;

        public GameObject Panel => gameObject;

        public void Build(Transform parent)
        {
            root = HudFactory.Configure(
                gameObject, parent, HudLayout.ResultPanel, new Color(0.05f, 0.06f, 0.08f, 0.96f));

            headlineLabel = HudFactory.CreateText("Headline", root, HudFactory.Row(28f, 52f, 32f),
                string.Empty, 42, TextAnchor.UpperCenter, HudFactory.TextColor);
            reasonLabel = HudFactory.CreateText("Reason", root, HudFactory.Row(92f, 32f, 32f),
                string.Empty, 22, TextAnchor.UpperCenter, HudFactory.MutedTextColor);
            statsLabel = HudFactory.CreateText("Stats", root, HudFactory.Row(150f, 120f, 40f),
                string.Empty, 24, TextAnchor.UpperLeft, HudFactory.TextColor);
            coinsLabel = HudFactory.CreateText("Coins", root, HudFactory.Row(280f, 40f, 40f),
                string.Empty, 30, TextAnchor.UpperCenter, HudFactory.FullColor);

            retryButton = HudFactory.CreateButton("Retry", root,
                new RectSpec(new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f),
                    new Vector2(40f, 32f), new Vector2(-60f, 64f)),
                "重试", 26, out _);
            retryButton.onClick.AddListener(OnRetry);

            backButton = HudFactory.CreateButton("Back", root,
                new RectSpec(new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    new Vector2(20f, 32f), new Vector2(-60f, 64f)),
                "返回准备", 26, out _);
            backButton.onClick.AddListener(OnBack);
        }

        public void Bind(YardDirector yardDirector)
        {
            director = yardDirector;
            Refresh();
        }

        void OnRetry()
        {
            director?.RetrySelectedMission();
        }

        void OnBack()
        {
            director?.ReturnToPrep();
        }

        public void Refresh()
        {
            if (director == null)
            {
                return;
            }

            MissionOutcome outcome = director.LastOutcome;

            if (headlineLabel != null)
            {
                headlineLabel.text = outcome.won ? "任务完成" : "任务失败";
                headlineLabel.color = outcome.won ? HudFactory.GoodColor : HudFactory.WarnColor;
            }

            if (reasonLabel != null)
            {
                reasonLabel.text = outcome.won
                    ? (director.SelectedMission != null ? director.SelectedMission.displayName : string.Empty)
                    : ReasonText(outcome.reason);
            }

            if (statsLabel != null)
            {
                statsLabel.text = "送达正确    " + outcome.correct + "\n" +
                                  "错分        " + outcome.wrong + "\n" +
                                  "堵塞        " + outcome.jams + "\n" +
                                  "用时        " + outcome.seconds.ToString("F1") + " 秒";
            }

            if (coinsLabel != null)
            {
                if (outcome.coinsAwarded <= 0)
                {
                    coinsLabel.text = "获得金币 0";
                }
                else if (outcome.firstClear)
                {
                    coinsLabel.text = "获得金币 " + outcome.coinsAwarded + "（含首通奖励）";
                }
                else
                {
                    coinsLabel.text = "获得金币 " + outcome.coinsAwarded;
                }
            }

            if (retryButton != null)
            {
                retryButton.interactable = director.SelectedMission != null;
            }
        }

        static string ReasonText(MissionFailReason reason)
        {
            switch (reason)
            {
                case MissionFailReason.TooManyWrong:
                    return "错分包裹超出上限";
                case MissionFailReason.TooManyJams:
                    return "堵塞次数超出上限";
                case MissionFailReason.Timeout:
                    return "时间耗尽";
                case MissionFailReason.None:
                    return string.Empty;
            }

            return string.Empty;
        }
    }
}
