using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Scene level conductor. Owns the phase machine, wires the meta systems to the yard, and is
    /// the one place that pays out coins and writes the save.
    ///
    /// The meta systems themselves are plain classes; this component only assembles and forwards,
    /// which is what keeps the money and mission rules testable without an engine.
    /// </summary>
    public class YardDirector : MonoBehaviour
    {
        [SerializeField] LevelLoader loader;
        [SerializeField] YardCameraRig cameraRig;
        [SerializeField] DebugHud hud;
        [SerializeField] Text hudLabel;
        [SerializeField] TrafficSystem traffic;
        [SerializeField] InstallManager installer;
        [SerializeField] HudRoot hudRoot;
        [SerializeField] SpawnDirector spawner;

        static readonly string[] LeftoverNames =
        {
            "StartPoint", "end1", "end2", "package (1)", "package (2)", "Cube Instance"
        };

        readonly List<MissionDef> missions = new List<MissionDef>();
        readonly Dictionary<string, int> plannedByMission = new Dictionary<string, int>();

        ProfileStore store;

        public int Spawned { get; private set; }
        public int Correct { get; private set; }
        public int Wrong { get; private set; }
        public int Jams { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.Prep;
        public TrafficSystem Traffic => traffic;
        public InstallManager Installer => installer;
        public LevelLoader Loader => loader;
        public SpawnDirector Spawner => spawner;
        public HudRoot Hud => hudRoot;

        public PlayerProfile Profile { get; private set; }
        public Wallet Wallet { get; private set; }
        public DeviceInventory Inventory { get; private set; }
        public ShopService Shop { get; private set; }

        public IReadOnlyList<MissionDef> Missions => missions;
        public MissionDef SelectedMission { get; private set; }
        public MissionRuntime Mission { get; private set; }

        /// <summary>The settled result of the round on screen, valid during Result.</summary>
        public MissionOutcome LastOutcome { get; private set; }

        /// <summary>Raised whenever the phase changes, so the HUD can re-apply its layout.</summary>
        public event Action<GamePhase> PhaseChanged;

        /// <summary>Raised when coins, stock or mission records change.</summary>
        public event Action ProgressChanged;

        public LevelConfig Config => loader != null ? loader.LoadedConfig : null;

        /// <summary>Parcels this round will release in total, from the selected mission's plan.</summary>
        public int PackagesTotal => Mission != null
            ? Mission.Planned
            : (spawner != null ? spawner.Planned : 0);

        /// <summary>Parcels not yet delivered to a bay. Drives the run progress bar.</summary>
        public int PackagesLeft => Mission != null
            ? Mission.LeftToDeliver
            : Mathf.Max(0, PackagesTotal - Correct - Wrong);

        public string LevelDisplayName { get; private set; } = string.Empty;

        /// <summary>Parcels the selected mission will release, known before the round starts.</summary>
        public int PlannedFor(MissionDef mission)
        {
            if (mission == null)
            {
                return 0;
            }

            return plannedByMission.TryGetValue(mission.id, out int planned) ? planned : 0;
        }

        public MissionRecord RecordFor(MissionDef mission)
        {
            if (mission == null || Profile == null)
            {
                return null;
            }

            return Profile.RecordFor(mission.id);
        }

        /// <summary>A mission is selectable unless its authored numbers make it unwinnable.</summary>
        public bool IsSelectable(MissionDef mission)
        {
            return mission != null && !mission.ConfigError;
        }

        void Start()
        {
            HideLegacyMarkers();
            ResolveDependencies();
            LoadProfile();

            if (hud != null)
            {
                hud.Bind(this, hudLabel);
            }

            LoadMap(loader != null ? loader.LevelFileName : null);
        }

        void LoadProfile()
        {
            store = ProfileStore.Default();
            Profile = store.Load();

            Wallet = new Wallet(Profile.coins);
            Inventory = new DeviceInventory();
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                DeviceType device = DeviceTypes.All[i];
                Inventory.SetOwned(device, Profile.Owned(device));
            }

            Wallet.BalanceChanged += _ => ProgressChanged?.Invoke();
            Inventory.Changed += () => ProgressChanged?.Invoke();
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

            if (hudRoot == null)
            {
                hudRoot = FindFirstObjectByType<HudRoot>();
            }

            if (spawner == null)
            {
                spawner = FindFirstObjectByType<SpawnDirector>();
                if (spawner == null)
                {
                    spawner = gameObject.AddComponent<SpawnDirector>();
                }
            }

            installer.SetPhaseSource(() => Phase);
        }

        // ---------------------------------------------------------------- notifications

        public void NotifySpawned()
        {
            Spawned++;
        }

        public void NotifyCorrect()
        {
            Correct++;
            Mission?.ReportCorrect();
            CheckVerdict();
        }

        public void NotifyWrong()
        {
            Wrong++;
            Mission?.ReportWrong();
            CheckVerdict();
        }

        public void NotifyJam()
        {
            Jams++;
            Mission?.ReportJam();
            CheckVerdict();
        }

        void Update()
        {
            if (Phase != GamePhase.Running || Mission == null)
            {
                return;
            }

            Mission.Tick(Time.deltaTime);
            CheckVerdict();
        }

        /// <summary>Moves to the result screen the moment the mission has an answer.</summary>
        void CheckVerdict()
        {
            if (Phase != GamePhase.Running || Mission == null || !Mission.IsOver)
            {
                return;
            }

            EnterResult(Mission.Resolve(RecordFor(SelectedMission)));
        }

        // ---------------------------------------------------------------- level lifecycle

        /// <summary>Loads a level file and drops straight into the prep phase.</summary>
        public void LoadMap(string fileName)
        {
            ResolveDependencies();
            if (loader == null)
            {
                Debug.LogError("YardDirector has no LevelLoader.");
                return;
            }

            SetPhase(GamePhase.Prep);
            if (traffic != null)
            {
                traffic.SetRunning(false);
                traffic.ClearAll();
            }

            spawner?.Clear();

            if (installer != null)
            {
                // Devices belong to the yard being torn down; hand the stock back before it goes.
                installer.ClearInstalls();
                installer.ClearMarkers();
            }

            SelectedMission = null;
            Mission = null;
            missions.Clear();
            plannedByMission.Clear();

            loader.SetLevelFileName(fileName);
            if (!loader.TryLoad(this))
            {
                Debug.LogError("YardDirector failed to load '" + fileName + "'.");
                return;
            }

            LevelConfig config = loader.LoadedConfig;
            LevelDisplayName = string.IsNullOrEmpty(config.displayName) ? config.id : config.displayName;

            Shop = new ShopService(Wallet, Inventory, config.devices, () => Phase);
            if (config.devices.Count == 0)
            {
                Debug.LogWarning("Level '" + config.id + "' authors no devices block; the shop " +
                                 "will be empty. Bare handed play is still possible.");
            }

            traffic?.Bind(loader.Graph, config.rules, this);
            cameraRig?.Frame(loader.YardBounds);

            CollectMissions(config);
            EnterPrep(restoreInstalls: true);
        }

        /// <summary>
        /// Reads the level's missions and works out each one's parcel total up front, which is
        /// also where an unwinnable objective is caught.
        /// </summary>
        void CollectMissions(LevelConfig config)
        {
            missions.AddRange(config.missions);
            if (missions.Count == 0)
            {
                Debug.LogWarning("Level '" + config.id + "' authors no missions; prep will show an " +
                                 "empty mission list and START stays disabled.");
                return;
            }

            List<string> inletIds = loader.Graph.InletIds();
            var bayColors = new List<DestinationColor>(loader.Graph.BayColors());

            for (int i = 0; i < missions.Count; i++)
            {
                MissionDef mission = missions[i];
                var probe = new SpawnSchedule();
                probe.Build(inletIds, bayColors, mission.spawn, mission.modifiers,
                    mission.spawn.hasSeed ? mission.spawn.seed : 0);

                plannedByMission[mission.id] = probe.Planned;

                if (probe.Planned < mission.objective.targetDelivered)
                {
                    mission.MarkConfigError("plans " + probe.Planned + " parcels but demands " +
                                            mission.objective.targetDelivered);
                    Debug.LogError("Mission '" + mission.id + "' is unwinnable as authored: Planned=" +
                                   probe.Planned + " < targetDelivered=" +
                                   mission.objective.targetDelivered +
                                   ". The mission has been marked unselectable.");
                }
            }
        }

        void EnterPrep(bool restoreInstalls)
        {
            SetPhase(GamePhase.Prep);
            ResetCounters();
            SetInletsActive(false);

            traffic?.SetRunning(false);
            spawner?.SetRunning(false);

            ApplyMissionSpeedScale(1f);

            if (installer != null)
            {
                installer.SetCatalog(loader.Catalog);
                installer.BeginPrep(loader.Graph, loader.LoadedConfig, Inventory);
                if (restoreInstalls)
                {
                    installer.Restore(Profile.installs);
                }
            }

            hudRoot?.Bind(this);
            hudRoot?.Apply(Phase);
            ProgressChanged?.Invoke();
        }

        /// <summary>Returns to prep from the result screen, keeping every placed device.</summary>
        public void ReturnToPrep()
        {
            if (Phase != GamePhase.Result)
            {
                return;
            }

            traffic?.SetRunning(false);
            traffic?.ClearAll();
            spawner?.SetRunning(false);
            SetInletsActive(false);

            Mission = null;
            SetPhase(GamePhase.Prep);
            ResetCounters();
            ApplyMissionSpeedScale(1f);

            // Devices stay exactly where they are; only the drop targets come back.
            installer?.RefreshMarkers();

            hudRoot?.Apply(Phase);
            ProgressChanged?.Invoke();
        }

        // ---------------------------------------------------------------- mission selection

        /// <summary>Picks the mission to play. Only legal during prep.</summary>
        public bool TrySelectMission(string missionId)
        {
            if (Phase != GamePhase.Prep)
            {
                return false;
            }

            for (int i = 0; i < missions.Count; i++)
            {
                if (!string.Equals(missions[i].id, missionId))
                {
                    continue;
                }

                if (!IsSelectable(missions[i]))
                {
                    Debug.LogWarning("Mission '" + missionId + "' cannot be selected: " +
                                     missions[i].ConfigErrorText);
                    return false;
                }

                SelectedMission = missions[i];
                hudRoot?.Apply(Phase);
                ProgressChanged?.Invoke();
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- round lifecycle

        /// <summary>Locks installs, hides the prep panels and starts production.</summary>
        public void StartRound()
        {
            if (Phase != GamePhase.Prep)
            {
                return;
            }

            if (SelectedMission == null)
            {
                Debug.LogWarning("StartRound was called with no mission selected; staying in prep.");
                return;
            }

            if (loader == null || loader.Graph == null)
            {
                return;
            }

            BeginRound(SelectedMission);
        }

        /// <summary>Replays the same mission, keeping every placed device where it is.</summary>
        public void RetrySelectedMission()
        {
            if (Phase != GamePhase.Result || SelectedMission == null)
            {
                return;
            }

            BeginRound(SelectedMission);
        }

        void BeginRound(MissionDef mission)
        {
            traffic?.ClearAll();
            ResetCounters();

            ApplyMissionSpeedScale(mission.modifiers.speedScale);

            spawner?.Build(loader.Graph, mission.spawn, mission.modifiers);
            int planned = spawner != null ? spawner.Planned : 0;
            plannedByMission[mission.id] = planned;

            Mission = new MissionRuntime(mission, planned);
            Mission.Begin();

            SetPhase(GamePhase.Running);
            installer?.EndPrep();

            SetInletsActive(true);
            traffic?.SetRunning(true);
            spawner?.SetRunning(true);

            hudRoot?.Apply(Phase);
            ProgressChanged?.Invoke();
        }

        /// <summary>
        /// Settles the round: pays out, updates progress, and writes the save. Called once per
        /// round because <see cref="MissionRuntime.Resolve"/> latches after its first call.
        /// </summary>
        public void EnterResult(MissionOutcome outcome)
        {
            if (Phase == GamePhase.Result)
            {
                return;
            }

            SetPhase(GamePhase.Result);
            traffic?.SetRunning(false);
            spawner?.SetRunning(false);
            SetInletsActive(false);

            LastOutcome = outcome;

            if (outcome.coinsAwarded > 0)
            {
                Wallet.Earn(outcome.coinsAwarded);
            }

            SyncProfile();
            UpdateUnlocks();
            store?.Save(Profile);

            hudRoot?.Apply(Phase);
            ProgressChanged?.Invoke();
        }

        /// <summary>Copies the live meta state back into the profile before it is written.</summary>
        void SyncProfile()
        {
            if (Profile == null)
            {
                return;
            }

            Profile.coins = Wallet.Balance;
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                DeviceType device = DeviceTypes.All[i];
                Profile.SetOwned(device, Inventory.Owned(device));
            }

            if (installer != null && !string.IsNullOrEmpty(installer.LevelId))
            {
                Profile.ClearInstallsFor(installer.LevelId);
                Profile.installs.AddRange(installer.Snapshot());
            }
        }

        /// <summary>
        /// The next level opens only when every mission here is cleared, so the unlock marker
        /// genuinely means "this yard is mastered" rather than "this yard was visited".
        /// </summary>
        void UpdateUnlocks()
        {
            LevelConfig config = Config;
            if (config == null || string.IsNullOrEmpty(config.nextLevel) || missions.Count == 0)
            {
                return;
            }

            for (int i = 0; i < missions.Count; i++)
            {
                if (!Profile.IsCleared(missions[i].id))
                {
                    return;
                }
            }

            Profile.Unlock(config.nextLevel);
        }

        /// <summary>
        /// Reloads the level, which clears installs and counters. Devices return to the
        /// inventory rather than being lost.
        /// </summary>
        public void ResetToPrep()
        {
            LoadMap(loader != null ? loader.LevelFileName : null);
        }

        /// <summary>Persists progress on the way out so a quit during prep is not lost.</summary>
        void OnApplicationQuit()
        {
            SyncProfile();
            store?.Save(Profile);
        }

        // ---------------------------------------------------------------- helpers

        void SetPhase(GamePhase phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        void ApplyMissionSpeedScale(float scale)
        {
            if (loader == null || loader.Graph == null)
            {
                return;
            }

            IReadOnlyList<BeltPath> belts = loader.Graph.AllBelts;
            for (int i = 0; i < belts.Count; i++)
            {
                belts[i].SetMissionSpeedScale(scale);
            }
        }

        void SetInletsActive(bool active)
        {
            if (loader == null || loader.Graph == null)
            {
                return;
            }

            IReadOnlyList<YardNode> inlets = loader.Graph.Inlets;
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
