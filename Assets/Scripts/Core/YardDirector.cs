using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    public class YardDirector : MonoBehaviour
    {
        [SerializeField] LevelLoader loader;
        [SerializeField] YardCameraRig cameraRig;
        [SerializeField] DebugHud hud;
        [SerializeField] Text hudLabel;
        [SerializeField] TrafficSystem traffic;
        [SerializeField] InstallManager installer;
        [SerializeField] PrepBarUI prepBar;
        [SerializeField] SpawnDirector spawner;

        static readonly string[] LeftoverNames =
        {
            "StartPoint", "end1", "end2", "package (1)", "package (2)", "Cube Instance"
        };

        public int Spawned { get; private set; }
        public int Correct { get; private set; }
        public int Wrong { get; private set; }
        public int Jams { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.Prep;
        public TrafficSystem Traffic => traffic;
        public InstallManager Installer => installer;
        public LevelLoader Loader => loader;
        public SpawnDirector Spawner => spawner;

        /// <summary>Parcels this level will release in total, from its spawn plan.</summary>
        public int PackagesTotal => spawner != null ? spawner.Planned : 0;

        /// <summary>Parcels not yet delivered to a bay. Stands in for the level progress bar.</summary>
        public int PackagesLeft => Mathf.Max(0, PackagesTotal - Correct - Wrong);
        public string LevelDisplayName { get; private set; } = string.Empty;

        void Start()
        {
            HideLegacyMarkers();
            ResolveDependencies();

            if (hud != null)
            {
                hud.Bind(this, hudLabel);
            }

            LoadMap(loader != null ? loader.LevelFileName : null);
        }

        void ResolveDependencies()
        {
            if (loader == null)
            {
                loader = FindFirstObjectByType<LevelLoader>();
            }

            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<YardCameraRig>();
            }

            if (hud == null)
            {
                hud = FindFirstObjectByType<DebugHud>();
            }

            if (traffic == null)
            {
                traffic = FindFirstObjectByType<TrafficSystem>();
                if (traffic == null)
                {
                    traffic = gameObject.AddComponent<TrafficSystem>();
                }
            }

            if (installer == null)
            {
                installer = FindFirstObjectByType<InstallManager>();
                if (installer == null)
                {
                    installer = gameObject.AddComponent<InstallManager>();
                }
            }

            if (prepBar == null)
            {
                prepBar = FindFirstObjectByType<PrepBarUI>();
            }

            if (spawner == null)
            {
                spawner = FindFirstObjectByType<SpawnDirector>();
                if (spawner == null)
                {
                    spawner = gameObject.AddComponent<SpawnDirector>();
                }
            }
        }

        public void NotifySpawned()
        {
            Spawned++;
        }

        public void NotifyCorrect()
        {
            Correct++;
        }

        public void NotifyWrong()
        {
            Wrong++;
        }

        public void NotifyJam()
        {
            Jams++;
        }

        /// <summary>Loads a level file and drops straight into the prep phase.</summary>
        public void LoadMap(string fileName)
        {
            ResolveDependencies();
            if (loader == null)
            {
                Debug.LogError("YardDirector has no LevelLoader.");
                return;
            }

            Phase = GamePhase.Prep;
            if (traffic != null)
            {
                traffic.SetRunning(false);
                traffic.ClearAll();
            }

            if (spawner != null)
            {
                spawner.Clear();
            }

            if (installer != null)
            {
                installer.ClearMarkers();
            }

            loader.SetLevelFileName(fileName);
            if (!loader.TryLoad(this))
            {
                Debug.LogError("YardDirector failed to load '" + fileName + "'.");
                return;
            }

            LevelDisplayName = string.IsNullOrEmpty(loader.LoadedConfig.displayName)
                ? loader.LoadedConfig.id
                : loader.LoadedConfig.displayName;

            if (traffic != null)
            {
                traffic.Bind(loader.Graph, loader.LoadedConfig.rules, this);
            }

            if (cameraRig != null)
            {
                cameraRig.Frame(loader.YardBounds);
            }

            EnterPrep();
        }

        void EnterPrep()
        {
            Phase = GamePhase.Prep;
            ResetCounters();
            SetInletsActive(false);

            // Built during prep so the HUD can show the parcel total before the round starts.
            if (spawner != null)
            {
                spawner.SetRunning(false);
                spawner.Build(loader.Graph, loader.LoadedConfig.spawn);
            }

            if (installer != null)
            {
                installer.SetCatalog(loader.Catalog);
                installer.BeginPrep(loader.Graph, loader.LoadedConfig);
            }

            if (prepBar != null)
            {
                prepBar.Bind(this);
                prepBar.Show(true);
            }
        }

        /// <summary>Locks installs, hides the tray and starts production.</summary>
        public void StartRound()
        {
            if (Phase == GamePhase.Running || loader == null || loader.Graph == null)
            {
                return;
            }

            Phase = GamePhase.Running;
            if (installer != null)
            {
                installer.EndPrep();
            }

            if (prepBar != null)
            {
                prepBar.Show(false);
            }

            SetInletsActive(true);
            if (traffic != null)
            {
                traffic.SetRunning(true);
            }

            if (spawner != null)
            {
                spawner.SetRunning(true);
            }
        }

        /// <summary>Reloads the current level, which clears installs and counters.</summary>
        public void ResetToPrep()
        {
            LoadMap(loader != null ? loader.LevelFileName : null);
        }

        void SetInletsActive(bool active)
        {
            if (loader == null || loader.Graph == null)
            {
                return;
            }

            System.Collections.Generic.IReadOnlyList<YardNode> inlets = loader.Graph.Inlets;
            for (int i = 0; i < inlets.Count; i++)
            {
                var inlet = inlets[i].GetComponent<Inlet>();
                if (inlet != null)
                {
                    inlet.SetActive(active);
                }
            }
        }

        void ResetCounters()
        {
            Spawned = 0;
            Correct = 0;
            Wrong = 0;
            Jams = 0;
        }

        void HideLegacyMarkers()
        {
            for (int i = 0; i < LeftoverNames.Length; i++)
            {
                GameObject leftover = GameObject.Find(LeftoverNames[i]);
                if (leftover != null)
                {
                    leftover.SetActive(false);
                }
            }
        }
    }
}
