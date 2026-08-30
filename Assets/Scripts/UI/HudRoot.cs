using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Single owner of HUD visibility. Every panel's show/hide decision lives in
    /// <see cref="Apply"/>, so "what is on screen right now" is one readable switch instead of a
    /// rule scattered across seven components.
    ///
    /// The panels are constructed here from <see cref="HudLayout"/> rather than authored into the
    /// scene. That keeps every anchor value in reviewable source, lets an Edit Mode test assert
    /// the layout, and avoids a large hand-written block of scene YAML that nothing can validate.
    /// Existing scene objects (the debug label and the GM panel) are found by name and reused.
    /// </summary>
    public class HudRoot : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] GameObject debugLabel;
        [SerializeField] GameObject gmPanel;

        MapNameAndCoinPanel namePanel;
        RunProgressBar progressBar;
        MissionListPanel missionList;
        DeviceShopBar shopBar;
        StartButtonUI startButton;
        ResultPanel resultPanel;
        RunControlPanel runControls;

        YardDirector director;
        bool built;

        public MapNameAndCoinPanel NamePanel => namePanel;
        public RunProgressBar ProgressBar => progressBar;
        public MissionListPanel MissionList => missionList;
        public DeviceShopBar ShopBar => shopBar;
        public StartButtonUI StartButton => startButton;
        public ResultPanel Result => resultPanel;
        public RunControlPanel RunControls => runControls;

        void Awake()
        {
            EnsureBuilt();
        }

        void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                Debug.LogError("HudRoot needs to live under a Canvas.");
                return;
            }

            ConfigureCanvas();

            Transform parent = canvas.transform;
            RemoveLegacyChildren();

            namePanel = Create<MapNameAndCoinPanel>("MapNameAndCoin", parent);
            progressBar = Create<RunProgressBar>("RunProgress", parent);
            missionList = Create<MissionListPanel>("MissionList", parent);
            shopBar = Create<DeviceShopBar>("DeviceShop", parent);
            startButton = Create<StartButtonUI>("Start", parent);
            runControls = Create<RunControlPanel>("RunControls", parent);
            resultPanel = Create<ResultPanel>("Result", parent);

            ResolveLegacyPanels(parent);
        }

        /// <summary>
        /// Creates one panel. Every panel is its own root object, positioned by its own
        /// <see cref="HudLayout"/> entry, so the hierarchy stays flat and one panel can never
        /// drag another around.
        /// </summary>
        static T Create<T>(string name, Transform parent) where T : Component, IHudPanel
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var panel = go.AddComponent<T>();
            panel.Build(parent);
            return panel;
        }

        /// <summary>
        /// Screen Space Overlay with Scale With Screen Size against 1920x1080 and a 0.5 match, so
        /// the layout keeps the same proportions at 1280x720.
        /// </summary>
        void ConfigureCanvas()
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudLayout.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = HudLayout.ScaleMatch;
        }

        /// <summary>
        /// Drops the old prep tray's children. The tray has been replaced by the mission list,
        /// the shop bar and the start button, and leaving its cards behind would double up.
        /// </summary>
        void RemoveLegacyChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            var image = GetComponent<Image>();
            if (image != null)
            {
                // The tray's own backing panel is no longer wanted; each new panel draws its own.
                image.enabled = false;
            }
        }

        void ResolveLegacyPanels(Transform parent)
        {
            if (debugLabel == null)
            {
                Transform found = parent.Find("DebugLabel");
                if (found != null)
                {
                    debugLabel = found.gameObject;
                }
            }

            if (gmPanel == null)
            {
                Transform found = parent.Find("GmPanel");
                if (found != null)
                {
                    gmPanel = found.gameObject;
                }
            }
        }

        public void Bind(YardDirector yardDirector)
        {
            EnsureBuilt();
            if (director != null)
            {
                director.ProgressChanged -= RefreshAll;
                director.PhaseChanged -= OnPhaseChanged;
            }

            director = yardDirector;
            if (director != null)
            {
                director.ProgressChanged += RefreshAll;
                director.PhaseChanged += OnPhaseChanged;
            }

            namePanel?.Bind(director);
            progressBar?.Bind(director);
            missionList?.Bind(director);
            shopBar?.Bind(director);
            startButton?.Bind(director);
            runControls?.Bind(director);
            resultPanel?.Bind(director);
        }

        void OnDestroy()
        {
            if (director == null)
            {
                return;
            }

            director.ProgressChanged -= RefreshAll;
            director.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            Apply(phase);
        }

        void RefreshAll()
        {
            namePanel?.Refresh();
            missionList?.Refresh();
            shopBar?.Refresh();
            startButton?.Refresh();
            progressBar?.Refresh();
            runControls?.Refresh();
            resultPanel?.Refresh();
        }

        /// <summary>
        /// The whole visibility contract, in one place.
        ///
        /// Prep     mission list + shop + START, no progress bar, no run controls, no result.
        /// Running  progress bar and the pause/exit controls; every prep panel is switched off so
        ///          the yard is unobscured.
        /// Result   the running layout with the result panel layered on top.
        ///
        /// The name and coin panel is visible throughout. Nothing here disables yard interaction,
        /// so gates, belt switches and diverters stay clickable during the round.
        /// </summary>
        public void Apply(GamePhase phase)
        {
            EnsureBuilt();

            bool prep = phase == GamePhase.Prep;
            bool result = phase == GamePhase.Result;
            bool roundVisible = phase == GamePhase.Running || result;

            HudFactory.SetActive(namePanel?.Panel, true);
            HudFactory.SetActive(missionList?.Panel, prep);
            HudFactory.SetActive(shopBar?.Panel, prep);
            HudFactory.SetActive(startButton?.Panel, prep);
            HudFactory.SetActive(progressBar?.Panel, roundVisible);
            HudFactory.SetActive(runControls?.Panel, roundVisible);
            HudFactory.SetActive(resultPanel?.Panel, result);

            // The GM shortcuts swap levels, which only makes sense before a round begins.
            HudFactory.SetActive(gmPanel, prep);
            HudFactory.SetActive(debugLabel, true);

            RefreshAll();
        }
    }
}
