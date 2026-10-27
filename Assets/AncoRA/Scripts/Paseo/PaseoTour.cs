using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Immersal;
using Immersal.XR;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// Runs the tour. Every building has its own XR Space with all its maps inside, so a localization with any of them
    /// moves the space of its building (and its one box), and the boxes of other buildings stay where their last
    /// localization left them. A box is shown only after its space received the pose: the result event fires before the
    /// SDK moves the space, and showing earlier would flash the box at the world origin. None of this proves the box is
    /// physically on the building.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class PaseoTour : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const float MinChangeMeters = 0.002f;
        const float MinChangeDegrees = 0.05f;
        const float SdkStartTimeoutSeconds = 15f;
        // Every fix, even standing still: with a minimum distance Android stops sending them and the fix goes stale
        // (PaseoGpsFix.MaxAgeSeconds) while the phone points at a building.
        const float GpsAccuracyWantedMeters = 5f;
        const float GpsUpdateMeters = 0f;
        const float GpsRetrySeconds = 30f;

        [SerializeField] ImmersalSDK sdk;
        [SerializeField] Localizer localizer;
        [SerializeField] PaseoMapContent[] contents = Array.Empty<PaseoMapContent>();

        sealed class BuildingRuntime
        {
            public PaseoMapContent Content;
            public XRSpace Space;
            public PaseoPoseProcessor Filter;
            public bool Waiting;
            public float WaitingSince;
            // Filter counters when the result arrived: the filter sees it afterwards and decides whether it counts.
            public int FilterResultsBefore;
            public int FilterUsedBefore;
            public bool SpaceEverMoved;
            public bool InitialPlacementPending;
            public bool Shown;
            // Baked scene values this box started from; phone edits are only valid on top of these.
            public string BakedPose;
            public string BakedSize;
            public bool PhonePose;
            public bool PhoneSize;
            public Vector3 LastPosition;
            public Quaternion LastRotation;
        }

        // One runtime per building (scene order), reachable by building id and by the id of each of its maps.
        readonly List<BuildingRuntime> runtimes = new();
        readonly Dictionary<string, BuildingRuntime> byBuilding = new();
        readonly Dictionary<int, BuildingRuntime> byMap = new();
        readonly PaseoVisibility visibility = new();
        readonly PaseoMapLoad mapLoad = new();
        PaseoBoxStore store;
        float sceneStart;
        // When the scene loaded, which is when the SDK ran its own start; Begin resets sceneStart but not this.
        float loadedAt;
        string error = "";
        bool reportedTimeout;
        bool pendingLoadsResolved;
        bool wasTracking;
        string lastLocatedBuildingId;
        int localizationAttempts;
        int localizationFailures;
        Task sdkRetry;
        float lastSdkTry = float.NegativeInfinity;
        int sdkRetries;
        bool begun = true;
        readonly PaseoPace pace = new();
        readonly List<Vector3> shownViewportPoints = new();
        readonly List<string> shownBuildingIds = new();
        ImmersalSession session;
        // The search by building (PaseoLocalization + PaseoSearchPlan).
        PaseoLocalization localization;
        PaseoSearchPlan searchPlan;
        string inViewBuildingId;
        PaseoSearch currentSearch;
        PaseoAttempt? lastAttempt;
        int failuresInARow;
        PaseoGpsFix gpsFix;
        bool gpsStarted;
        bool gpsFailureReported;
        bool gpsFixReported;
        float nextGpsRetry;
        // Raw GPS of the last frame, for the debug lines (gpsFix keeps only a usable one).
        bool gpsRunning;
        LocationInfo gpsData;
        double gpsAge;
        // Detection debug (PaseoDebug): what was last logged.
        string loggedTracking;
        LocationServiceStatus loggedGpsStatus = LocationServiceStatus.Stopped;
        bool cameraLogged;
        // Debug photos (PaseoDebugImages) and the previous pose sample, for speeds.
        float lastPhoto = float.NegativeInfinity;
        int photos;
        Vector3 lastPoseCamera;
        float lastPoseYaw;
        float lastPoseTime = -1f;
        const float GroundHintSeconds = 1.5f;
        float groundSince = float.NaN;

        // Record mode (PaseoRecorder): saves the full image of every attempt while the team turns it on.
        PaseoRecorder recorder;

        public ImmersalSDK Sdk => sdk;
        public Localizer Localizer => localizer;
        /// <summary>Record mode of the team sheet; its recordings go next to the session log.</summary>
        public PaseoRecorder Recorder => recorder;
        public IReadOnlyList<PaseoMapContent> Contents => contents;
        public string Error => error;
        public bool SdkReady => sdk != null && sdk.IsReady;
        public float SecondsSinceStart => Time.realtimeSinceStartup - sceneStart;
        public bool Begun => begun;
        public int ShownCount => runtimes.Count(r => r.Shown);
        /// <summary>The camera has pointed at the ground for a moment: the card asks to raise the phone.</summary>
        public bool LookingAtGround { get; private set; }
        /// <summary>Seconds between localization tries right now (PaseoPace).</summary>
        public float LocalizationInterval { get; private set; } = PaseoPace.FastSeconds;
        public string LocalizationSummary => $"{localizationAttempts} intentos, {localizationFailures} sin coincidencia";
        public int LocalizationAttempts => localizationAttempts;
        public int LocalizationFailures => localizationFailures;
        /// <summary>Horizontal accuracy of the last GPS position while the location service runs; NaN otherwise.</summary>
        public float GpsAccuracyMeters { get; private set; } = float.NaN;

        public string GpsDescription =>
            !gpsStarted ? "GPS apagado"
            : gpsFix.Valid ? $"GPS ±{gpsFix.AccuracyMeters:F0} m"
            : float.IsNaN(GpsAccuracyMeters) ? "GPS sin posición"
            : $"GPS ±{GpsAccuracyMeters:F0} m, no sirve";

        /// <summary>Immersal reduces each image to 960 × 720 at most (the SDK's default) or hands the plugin the full one.</summary>
        public bool ReducedImage => sdk == null || sdk.downsample;

        /// <summary>The interface holds the tour until the welcome is closed and the permissions answered (PaseoUi).</summary>
        public void WaitForBegin() => begun = false;

        /// <summary>
        /// Starts the clocks of the SDK timeout and retry, so time spent on the welcome does not count, and the GPS when
        /// the location permission was given.
        /// </summary>
        public void Begin(bool useLocation = false)
        {
            if (useLocation)
                StartGps();
            if (begun)
                return;
            begun = true;
            sceneStart = Time.realtimeSinceStartup;
            Debug.Log($"{Tag} Comienza el paseo: bienvenida cerrada y permisos respondidos.");
        }

        public IReadOnlyList<PaseoMapRow> MapRows()
        {
            var rows = new List<PaseoMapRow>();
            // One row per map; the state of the building (located, shown, box) repeats on each of its maps, and the
            // last verdict and the counts are those of that map alone.
            foreach (var runtime in runtimes)
            {
                var c = runtime.Content;
                var filter = runtime.Filter;
                bool hasFilter = filter != null;
                foreach (int mapId in c.MapIds)
                {
                    PaseoMapStats stats = default;
                    bool hasStats = hasFilter && filter.TryGetMapStats(mapId, out stats);
                    rows.Add(PaseoMapRow.Build(new PaseoMapRowInputs
                    {
                        MapId = mapId,
                        BuildingName = c.BuildingName,
                        Loaded = mapLoad.IsLoaded(mapId),
                        Failed = mapLoad.IsFailed(mapId),
                        Located = visibility.IsLocated(c.BuildingId),
                        Shown = runtime.Shown,
                        HasFilter = hasFilter,
                        Placed = c.Placed,
                        PhoneValues = runtime.PhonePose || runtime.PhoneSize,
                        Last = hasStats ? stats.Last : null,
                        Used = hasStats ? stats.Used : 0,
                        Seen = hasStats ? stats.Seen : 0
                    }));
                }
            }
            return rows;
        }

        public string FirstMapStillLoading
        {
            get
            {
                int pending = mapLoad.FirstPending;
                return pending < 0 ? null : $"{pending} ({byMap[pending].Content.BuildingName})";
            }
        }

        /// <summary>The box the team can edit: the one of the building that located last, while it is shown.</summary>
        public PaseoMapContent EditableContent =>
            lastLocatedBuildingId != null && byBuilding.TryGetValue(lastLocatedBuildingId, out var runtime) && runtime.Shown
                ? runtime.Content
                : null;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(ImmersalSDK immersal, Localizer immersalLocalizer, PaseoMapContent[] mapContents)
        {
            sdk = immersal;
            localizer = immersalLocalizer;
            contents = mapContents;
        }

        void Awake()
        {
            // The development console pops up on every error and covers the adjust bar; errors still reach logcat.
            Debug.developerConsoleVisible = false;
            sceneStart = loadedAt = Time.realtimeSinceStartup;
            session = sdk != null ? sdk.Session as ImmersalSession : null;
            XRMapVisualization.pointCloudVisible = false;
            store = new PaseoBoxStore();
            recorder = new PaseoRecorder(PaseoSessionLog.RecordingFolder,
                () => PaseoRecorder.FreeBytes(Application.persistentDataPath));

            foreach (var content in contents)
            {
                var space = content.GetComponentInParent<XRSpace>();
                var runtime = new BuildingRuntime
                {
                    Content = content,
                    Space = space,
                    Filter = space.GetComponent<PaseoPoseProcessor>(),
                    LastPosition = space.transform.position,
                    LastRotation = space.transform.rotation
                };
                runtimes.Add(runtime);
                byBuilding[content.BuildingId] = runtime;
                visibility.AddBuilding(content.BuildingId);
                foreach (int mapId in content.MapIds)
                {
                    byMap[mapId] = runtime;
                    mapLoad.Add(mapId);
                }
                ApplyStored(runtime);
                content.SetVisible(false);
            }

            if (MapManager.MapRegisteredAndLoaded == null)
                MapManager.MapRegisteredAndLoaded = new UnityEvent<int>();
            MapManager.MapRegisteredAndLoaded.AddListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.AddListener(OnLocalizationResult);

            // One entry per building, in scene order (edificio.json folders): the search's fallback order without GPS.
            var buildings = contents.Select(c =>
            {
                int[] ids = c.MapIds.ToArray();
                return c.HasGps
                    ? new PaseoSearchBuilding(c.BuildingId, c.BuildingName, ids, c.Latitude, c.Longitude)
                    : new PaseoSearchBuilding(c.BuildingId, c.BuildingName, ids);
            }).ToList();
            searchPlan = new PaseoSearchPlan(buildings);
            localization = localizer != null ? localizer.AvailableLocalizationMethods.OfType<PaseoLocalization>().FirstOrDefault() : null;
            if (localization != null)
            {
                localization.NextSearch = NextSearch;
                localization.AttemptStarted += OnAttemptStarted;
                localization.AttemptFinished += OnAttemptFinished;
                localization.CaptureImage = WantsPhoto;
                localization.ImageCaptured += OnImageCaptured;
                localization.RecordFrame = WantsFrame;
                localization.FrameRecorded += OnFrameRecorded;
            }
            else if (contents.Length > 0)
                Debug.LogWarning($"{Tag} El Localizer no tiene PaseoLocalization: cada intento busca en todos los mapas.");

            Debug.Log($"{Tag} Inicio del paseo; versión {Application.version}, build {Application.buildGUID}, SDK {ImmersalSDK.sdkVersion}; " +
                      $"edificios [{string.Join(", ", contents.Select(c => $"{c.BuildingId}: {string.Join("+", c.MapIds)}"))}]; " +
                      $"{buildings.Count(b => b.HasGps)} de {buildings.Count} edificios con GPS; sin token embebido.");
        }

        void OnDestroy()
        {
            MapManager.MapRegisteredAndLoaded?.RemoveListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.RemoveListener(OnLocalizationResult);
            if (localization != null)
            {
                localization.NextSearch = null;
                localization.AttemptStarted -= OnAttemptStarted;
                localization.AttemptFinished -= OnAttemptFinished;
                localization.CaptureImage = null;
                localization.ImageCaptured -= OnImageCaptured;
                localization.RecordFrame = null;
                localization.FrameRecorded -= OnFrameRecorded;
            }
            recorder?.Disable();
            if (gpsStarted)
                Input.location.Stop();
        }

        // PaseoLocalization asks as each attempt starts: the building in view first, then the nearest by GPS (PaseoSearchPlan).
        PaseoSearch NextSearch() => searchPlan.Next(gpsFix, inViewBuildingId);

        void OnAttemptStarted(PaseoSearch search) => currentSearch = search;

        void OnAttemptFinished(PaseoAttempt attempt)
        {
            lastAttempt = attempt;
            failuresInARow = attempt.Matched ? 0 : failuresInARow + 1;
            Debug.Log(PaseoLogFormat.Attempt(attempt));
            if (PaseoDebug.Enabled)
                Debug.Log(PaseoLogFormat.AttemptDebug(attempt, new PaseoDebugContext
                {
                    Tracking = ARSession.state.ToString(),
                    InView = inViewBuildingId,
                    Interval = LocalizationInterval,
                    GpsRunning = gpsRunning,
                    GpsUsable = gpsFix.Valid,
                    Latitude = gpsData.latitude,
                    Longitude = gpsData.longitude,
                    GpsAccuracy = gpsData.horizontalAccuracy,
                    GpsAge = gpsAge
                }));
        }

        // A debug photo every few seconds (PaseoDebugImages), next to the session log.
        bool WantsPhoto()
        {
            float now = Time.realtimeSinceStartup;
            if (!PaseoDebug.Enabled || string.IsNullOrEmpty(PaseoSessionLog.CurrentImageFolder) ||
                !PaseoDebugImages.ShouldCapture(now, lastPhoto, photos))
                return false;
            lastPhoto = now;
            photos++;
            return true;
        }

        void OnImageCaptured(PaseoAttempt attempt, byte[] pixels, int width, int height, int channels)
        {
            // Held upright, the landscape sensor image is turned a quarter to look like the screen.
            _ = PaseoDebugImages.Save(PaseoSessionLog.CurrentImageFolder, attempt, pixels, width, height, channels, Screen.height > Screen.width);
            Debug.Log($"{Tag} DEPURACION_FOTO n={attempt.Number} archivo={PaseoDebugImages.FileName(attempt.Number, attempt.MatchedMapId)}");
        }

        // Record mode: the full image of this attempt, if the team turned it on and no limit was reached.
        bool WantsFrame() => recorder != null && recorder.WantsFrame(Time.realtimeSinceStartup);

        void OnFrameRecorded(PaseoAttempt attempt, PaseoFrame frame)
        {
            var sel = recorder.Selection;
            string label = sel.Label;
            string file = PaseoRecorder.FileName(attempt.Number, label);
            var search = attempt.Search;
            var meta = new PaseoRecordMeta
            {
                Attempt = attempt.Number,
                File = file,
                Time = DateTime.Now,
                Realtime = Time.realtimeSinceStartup,
                Session = PaseoSessionLog.Stamp,
                Point = label,
                Building = sel.Building,
                Light = sel.Light,
                Width = frame.Width,
                Height = frame.Height,
                Channels = frame.Channels,
                ReducedByPlugin = ReducedImage,
                Intrinsics = frame.Intrinsics,
                CameraPosition = frame.CameraPosition,
                CameraRotation = frame.CameraRotation,
                ScreenOrientation = frame.ScreenOrientation,
                ImageOrientation = frame.ImageOrientation,
                Yaw = frame.CameraRotation.eulerAngles.y,
                Pitch = PaseoLocalization.PitchDegrees(frame.CameraRotation),
                Tracking = ARSession.state.ToString(),
                InView = inViewBuildingId,
                GpsRunning = gpsRunning,
                GpsUsable = gpsFix.Valid,
                Latitude = gpsData.latitude,
                Longitude = gpsData.longitude,
                GpsAccuracy = gpsData.horizontalAccuracy,
                GpsAge = gpsAge,
                SearchedBuilding = search.BuildingId,
                SearchedMaps = search.MapIds,
                SearchReason = PaseoLogFormat.ReasonName(search.Reason),
                GpsDistance = search.GpsDistanceMeters,
                MatchedMap = attempt.MatchedMapId,
                Confidence = attempt.Confidence,
                Rmse = attempt.Rmse,
                Seconds = attempt.Seconds,
                Phone = SystemInfo.deviceModel,
                Android = SystemInfo.operatingSystem,
                Version = Application.version,
                Build = Application.buildGUID
            };
            _ = recorder.Save(frame, meta);
            Debug.Log($"{Tag} GRABACION n={attempt.Number} punto={label} luz={sel.Light} archivo={file}");
        }

        /// <summary>
        /// The camera now for the DEPURACION_POSE line (PaseoSessionLog, once a second): position, heading and pitch in the AR
        /// world, speeds since the previous sample, and each shown box with its distance and bearing.
        /// </summary>
        public bool TryPoseSample(out PaseoPoseSample sample)
        {
            sample = default;
            var cam = Camera.main;
            if (cam == null)
                return false;
            float now = Time.realtimeSinceStartup;
            var t = cam.transform;
            float yaw = t.eulerAngles.y;
            float dt = lastPoseTime < 0f ? 0f : now - lastPoseTime;
            sample = new PaseoPoseSample
            {
                Seconds = SecondsSinceStart,
                Camera = t.position,
                Yaw = yaw,
                Pitch = -Mathf.DeltaAngle(0f, t.eulerAngles.x),
                Speed = dt > 0f ? Vector3.Distance(t.position, lastPoseCamera) / dt : 0f,
                TurnSpeed = dt > 0f ? Mathf.Abs(Mathf.DeltaAngle(lastPoseYaw, yaw)) / dt : 0f,
                Tracking = ARSession.state.ToString(),
                Boxes = new List<PaseoBoxSighting>()
            };
            foreach (var runtime in runtimes)
                if (runtime.Shown)
                {
                    var centre = runtime.Content.transform.position;
                    sample.Boxes.Add(new PaseoBoxSighting(runtime.Content.BuildingId, Vector3.Distance(t.position, centre),
                        PaseoDebugGeometry.Bearing(t.position, t.forward, centre)));
                }
            lastPoseCamera = t.position;
            lastPoseYaw = yaw;
            lastPoseTime = now;
            return true;
        }

        // Debug lines (PaseoDebug) of what changes outside the attempts: tracking and its reason, the camera
        // configuration once AR tracks, and the GPS state.
        void LogDebugChanges(bool tracking)
        {
            if (!PaseoDebug.Enabled)
                return;
            string state = $"{ARSession.state}/{ARSession.notTrackingReason}";
            if (state != loggedTracking)
            {
                loggedTracking = state;
                Debug.Log(PaseoLogFormat.TrackingChange(ARSession.state.ToString(), ARSession.notTrackingReason.ToString(), SecondsSinceStart));
            }
            if (tracking && !cameraLogged)
            {
                cameraLogged = true;
                var cameraManager = Camera.main != null ? Camera.main.GetComponent<ARCameraManager>() : null;
                var config = cameraManager != null ? cameraManager.currentConfiguration : null;
                Debug.Log(config is { } c
                    ? PaseoLogFormat.CameraConfig(c.width, c.height, c.framerate ?? 0, ReducedImage)
                    : PaseoLogFormat.CameraConfig(0, 0, 0, ReducedImage));
            }
            if (gpsStarted && Input.location.status != loggedGpsStatus)
            {
                loggedGpsStatus = Input.location.status;
                Debug.Log($"{Tag} DEPURACION_GPS estado={loggedGpsStatus} activado_por_usuario={(Input.location.isEnabledByUser ? 1 : 0)} t={SecondsSinceStart:F1}");
            }
        }

        void StartGps()
        {
            if (gpsStarted)
                return;
            gpsStarted = true;
            if (!Input.location.isEnabledByUser)
                Debug.LogWarning($"{Tag} La ubicación del teléfono está apagada: la búsqueda no usa el GPS mientras no se encienda.");
            Input.location.Start(GpsAccuracyWantedMeters, GpsUpdateMeters);
            nextGpsRetry = Time.realtimeSinceStartup + GpsRetrySeconds;
            Debug.Log($"{Tag} GPS encendido para elegir qué edificio buscar primero.");
        }

        void ReadGps(float now)
        {
            if (!gpsStarted)
                return;
            var status = Input.location.status;
            if (status == LocationServiceStatus.Failed)
            {
                if (!gpsFailureReported)
                {
                    gpsFailureReported = true;
                    Debug.LogWarning($"{Tag} El GPS no entrega posición; se reintenta cada {GpsRetrySeconds:F0} s y mientras tanto se busca sin él.");
                }
                if (now >= nextGpsRetry)
                {
                    nextGpsRetry = now + GpsRetrySeconds;
                    Input.location.Start(GpsAccuracyWantedMeters, GpsUpdateMeters);
                }
            }
            bool running = status == LocationServiceStatus.Running;
            var data = running ? Input.location.lastData : default;
            double age = running ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - data.timestamp : double.PositiveInfinity;
            gpsRunning = running;
            gpsData = data;
            gpsAge = age;
            gpsFix = PaseoGpsFix.From(running, data.latitude, data.longitude, data.horizontalAccuracy, age);
            GpsAccuracyMeters = running && data.horizontalAccuracy > 0f ? data.horizontalAccuracy : float.NaN;
            if (gpsFix.Valid && !gpsFixReported)
            {
                gpsFixReported = true;
                Debug.Log($"{Tag} Primera posición GPS utilizable: ±{gpsFix.AccuracyMeters:F0} m (t={now - sceneStart:F1}s).");
            }
        }

        /// <summary>The line under the status pill (PaseoActivity): what the search looks for and what it found.</summary>
        public PaseoActivityLine Activity()
        {
            var inputs = new PaseoActivityInputs
            {
                Active = localization != null && SdkReady && ARSession.state == ARSessionState.SessionTracking,
                SearchingFor = currentSearch.BuildingName,
                FailuresInARow = failuresInARow
            };
            if (lastAttempt is { } attempt)
            {
                inputs.SecondsSinceLast = Time.realtimeSinceStartup - attempt.FinishedAt;
                if (attempt.Matched && byMap.TryGetValue(attempt.MatchedMapId, out var runtime))
                {
                    inputs.LastMatched = true;
                    inputs.MatchedBuilding = runtime.Content.BuildingName;
                    inputs.MatchReport = runtime.Filter == null ? null
                        : PaseoActivity.ReportFor(attempt, runtime.Filter.LastReport, runtime.Filter.LastReportAt);
                }
            }
            return PaseoActivity.Describe(inputs);
        }

        /// <summary>Whether the box of this building rests on two localizations that agree, not only on the first (PaseoPoseFilter).</summary>
        public bool IsConfirmed(PaseoMapContent content) =>
            !byBuilding.TryGetValue(content.BuildingId, out var runtime) || runtime.Filter == null || runtime.Filter.Confirmed;

        // Phone values win over the scene they were edited from. If the scene was baked again since then (new
        // measurements, another phone's placements), the phone values are stale and are dropped.
        void ApplyStored(BuildingRuntime runtime)
        {
            var content = runtime.Content;
            runtime.BakedPose = PaseoBoxStore.Fingerprint(content.LocalPosition, content.LocalYaw, content.Placed);
            runtime.BakedSize = PaseoBoxStore.Fingerprint(content.SizeMeters, content.Box.Solid);

            bool hadSize = store.HasSize(content.BuildingId);
            runtime.PhoneSize = store.TryLoadSize(content.BuildingId, runtime.BakedSize, out var size, out bool solid);
            if (runtime.PhoneSize)
                content.SetSize(size, solid);
            else if (hadSize)
                Debug.Log($"{Tag} El tamaño guardado en el teléfono para {content.BuildingName} venía de otra versión de la escena; se descarta.");

            bool hadPose = store.HasPose(content.BuildingId);
            runtime.PhonePose = store.TryLoadPose(content.BuildingId, runtime.BakedPose, out var position, out float yaw);
            if (runtime.PhonePose)
                content.SetPose(position, yaw, placed: true);
            else
            {
                if (hadPose)
                    Debug.Log($"{Tag} La caja guardada en el teléfono para {content.BuildingName} venía de otra versión de la escena; se descarta.");
                runtime.InitialPlacementPending = !content.Placed;
            }
            if (runtime.PhonePose || runtime.PhoneSize)
                Debug.Log($"{Tag} {content.BuildingName} usa valores ajustados en este teléfono.");
        }

        void OnMapLoaded(int id)
        {
            if (!byMap.TryGetValue(id, out var runtime))
                return;
            // This event follows Core.LoadMap; an imported TextAsset alone is not proof of loading.
            int points = Core.GetPointCloudSize(id);
            mapLoad.Report(id, points);
            if (mapLoad.IsLoaded(id))
                Debug.Log($"{Tag} Mapa {id} ({runtime.Content.BuildingName}) cargado en el plugin: {points} puntos.");
            else
                Debug.LogError($"{Tag} ERROR: el mapa {id} ({runtime.Content.BuildingName}) se cargó sin puntos ({points}); ese edificio no podrá ubicarse.");
        }

        void OnLocalizationResult(ILocalizationResults results)
        {
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            foreach (var result in results.Results)
            {
                // Failed device localizations have MapId 0: count them globally, not against a guessed building.
                localizationAttempts++;
                if (!result.Success)
                {
                    localizationFailures++;
                    continue;
                }
                if (!byMap.TryGetValue(result.MapId, out var runtime))
                    continue;
                if (!mapLoad.IsLoaded(result.MapId) || !tracking || runtime.Waiting)
                    continue;
                runtime.Waiting = true;
                runtime.WaitingSince = Time.realtimeSinceStartup;
                runtime.FilterResultsBefore = runtime.Filter != null ? runtime.Filter.Results : 0;
                runtime.FilterUsedBefore = runtime.Filter != null ? runtime.Filter.UsedResults : 0;
            }
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            if (wasTracking && !tracking)
            {
                visibility.LoseTracking();
                // The AR world may shift while tracking is lost: each filter must agree on fresh localizations again.
                foreach (var runtime in runtimes)
                    runtime.Space.TriggerResetScene();
                searchPlan.Reset();
                Debug.Log($"{Tag} El teléfono perdió el tracking: se ocultan las cajas y se reinician los filtros hasta que cada edificio vuelva a localizar.");
            }
            wasTracking = tracking;

            foreach (var runtime in runtimes)
            {
                var t = runtime.Space.transform;
                bool moved = Vector3.Distance(t.position, runtime.LastPosition) >= MinChangeMeters ||
                             Quaternion.Angle(t.rotation, runtime.LastRotation) >= MinChangeDegrees;
                if (moved)
                {
                    runtime.SpaceEverMoved = true;
                    runtime.LastPosition = t.position;
                    runtime.LastRotation = t.rotation;
                }
                if (!runtime.Waiting)
                    continue;
                var filter = runtime.Filter;
                var decision = PaseoLocalizationSettle.Decide(
                    filter == null || filter.Results > runtime.FilterResultsBefore,
                    filter == null || filter.UsedResults > runtime.FilterUsedBefore,
                    moved, runtime.SpaceEverMoved, now - runtime.WaitingSince, filter == null || filter.HasPose);
                if (decision == PaseoSettle.Wait)
                    continue;
                runtime.Waiting = false;
                if (decision == PaseoSettle.Dropped || !tracking)
                    continue;

                string id = runtime.Content.BuildingId;
                bool wasLocated = visibility.IsLocated(id);
                visibility.MarkLocated(id, now);
                lastLocatedBuildingId = id;
                if (runtime.InitialPlacementPending)
                    PlaceInitially(runtime);
                if (!wasLocated)
                {
                    // The map that located it is the one the filter judged last.
                    string via = filter != null && filter.LastReport != null ? $" con el mapa {filter.LastMapId}" : "";
                    Debug.Log($"{Tag} {runtime.Content.BuildingName} ubicado{via} (t={now - sceneStart:F1}s). Alineación física NO verificada.");
                }
            }

            foreach (var runtime in runtimes)
            {
                bool show = tracking && visibility.IsLocated(runtime.Content.BuildingId);
                if (show == runtime.Shown)
                    continue;
                runtime.Shown = show;
                runtime.Content.SetVisible(show);
                Debug.Log(show
                    ? $"{Tag} Se muestra {runtime.Content.BuildingName}."
                    : $"{Tag} Se oculta la caja de {runtime.Content.BuildingName}.");
            }

            ReadGps(now);
            LogDebugChanges(tracking);
            UpdatePace(now, tracking);
            UpdateGroundHint(now, tracking);
            CheckSdkStart();
            RetrySdkStart(now, tracking);
            ResolvePendingLoads();
        }

        // Fast while searching, slow while the camera rests on a building already located (PaseoPace). The building in
        // view is also the one the search tries first.
        void UpdateGroundHint(float now, bool tracking)
        {
            var cam = Camera.main;
            bool down = tracking && cam != null && PaseoLocalization.PitchDegrees(cam.transform.rotation) < PaseoLocalization.MinPitchDegrees;
            if (!down)
            {
                groundSince = float.NaN;
                LookingAtGround = false;
                return;
            }
            if (float.IsNaN(groundSince))
                groundSince = now;
            LookingAtGround = now - groundSince >= GroundHintSeconds;
        }

        void UpdatePace(float now, bool tracking)
        {
            shownViewportPoints.Clear();
            shownBuildingIds.Clear();
            var cam = Camera.main;
            if (tracking && cam != null)
                foreach (var runtime in runtimes)
                    if (runtime.Shown)
                    {
                        shownViewportPoints.Add(cam.WorldToViewportPoint(runtime.Content.transform.position));
                        shownBuildingIds.Add(runtime.Content.BuildingId);
                    }
            int inView = PaseoCardChoice.Pick(shownViewportPoints);
            inViewBuildingId = inView >= 0 ? shownBuildingIds[inView] : null;
            bool locatedInView = inView >= 0;
            float interval = pace.Interval(now, locatedInView);
            if (Mathf.Approximately(interval, LocalizationInterval))
                return;
            LocalizationInterval = interval;
            if (session != null)
                session.SessionUpdateInterval = interval;
            Debug.Log($"{Tag} Ritmo de ubicación: un intento cada {interval:F1} s ({(locatedInView ? "edificio ubicado a la vista" : "buscando")}).");
        }

        // The SDK configures the camera only once; if the camera was not running yet (the permission dialog of the first
        // launch) it stays not ready and nothing localizes. Start it again once the camera runs (PaseoSdkStart).
        void RetrySdkStart(float now, bool tracking)
        {
            if (!begun)
                return;
            if (sdkRetry is { IsFaulted: true })
            {
                Debug.LogError($"{Tag} El reintento de inicio del SDK falló: {sdkRetry.Exception?.GetBaseException().Message}");
                sdkRetry = null;
            }
            if (sdk == null || !PaseoSdkStart.ShouldRetry(SdkReady, tracking, mapLoad.FirstPending < 0,
                    sdkRetry is { IsCompleted: false }, now - loadedAt, now - lastSdkTry, localization != null && localization.Configured))
                return;
            lastSdkTry = now;
            sdkRetries++;
            Debug.LogWarning($"{Tag} El SDK no quedó listo (la cámara no corría cuando se configuró); se inicia otra vez (intento {sdkRetries}).");
            sdkRetry = sdk.Initialize();
        }

        // A map whose native load failed never fires its event, so once the SDK is ready it is reported as failed
        // instead of "loading" forever; the other buildings keep working.
        void ResolvePendingLoads()
        {
            if (pendingLoadsResolved || !SdkReady)
                return;
            pendingLoadsResolved = true;
            foreach (int id in mapLoad.FailPending())
                Debug.LogError($"{Tag} ERROR: el SDK quedó listo sin cargar el mapa {id} ({byMap[id].Content.BuildingName}); ese edificio no podrá ubicarse.");
        }

        void PlaceInitially(BuildingRuntime runtime)
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            var content = runtime.Content;
            var space = runtime.Space.transform;
            PaseoPlacement.InitialPose(cam.transform.position, cam.transform.forward, content.SizeMeters,
                space.position, space.rotation, out var position, out float yaw);
            content.SetPose(position, yaw, placed: false);
            runtime.InitialPlacementPending = false;
            Debug.Log($"{Tag} La caja de {content.BuildingName} no estaba colocada: se puso a " +
                      $"{PaseoPlacement.DistanceMeters} m delante de la cámara. Ajustarla con el panel del equipo.");
        }

        void CheckSdkStart()
        {
            if (SdkReady)
            {
                error = "";
                return;
            }
            if (!begun || reportedTimeout || SecondsSinceStart < SdkStartTimeoutSeconds)
                return;
            reportedTimeout = true;
            error = "El SDK no quedó listo en 15 s; revisar el log de inicio.";
            Debug.LogError($"{Tag} {error}");
        }

        public IReadOnlyList<PaseoBuildingStatus> BuildingStatuses()
        {
            float now = Time.realtimeSinceStartup;
            var list = new List<PaseoBuildingStatus>();
            foreach (string building in visibility.Buildings)
            {
                var content = byBuilding[building].Content;
                var state = content.MapIds.All(mapLoad.IsFailed) ? PaseoBuildingState.MapFailed
                    : !visibility.IsLocated(building) ? PaseoBuildingState.Searching
                    : visibility.IsHeld(building, now) ? PaseoBuildingState.Held
                    : PaseoBuildingState.Located;
                list.Add(new PaseoBuildingStatus(content.BuildingName, state));
            }
            return list;
        }

        public void SaveEdit(PaseoMapContent content, Vector3 position, float yaw, Vector3 size, bool solid)
        {
            var edited = byBuilding[content.BuildingId];
            content.SetPose(position, yaw, placed: true);
            content.SetSize(size, solid);
            store.SavePose(content.BuildingId, position, yaw, edited.BakedPose);
            store.SaveSize(content.BuildingId, size, solid, edited.BakedSize);
            edited.PhonePose = true;
            edited.PhoneSize = true;
            edited.InitialPlacementPending = false;
        }

        /// <summary>Forgets every value saved on this phone and reloads, so the scene values apply again.</summary>
        public void ForgetSavedAndReload()
        {
            store.Clear(contents.Select(c => c.BuildingId));
            Debug.Log($"{Tag} Ajustes del teléfono borrados; se recarga la escena con los valores horneados.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// What "Copiar valores" writes: per building, its size and its one box pose in the building frame (the frame of
        /// its reference map). PaseoSetup.ApplyFieldAdjustment carries the pose back to the box of every map.
        /// </summary>
        public PaseoAdjustment BuildAdjustment()
        {
            var adjustment = new PaseoAdjustment { generado = DateTime.Now.ToString("s"), build = Application.buildGUID };
            foreach (var content in contents)
            {
                var size = content.SizeMeters;
                var p = content.LocalPosition;
                adjustment.edificios.Add(new PaseoAdjustmentBuilding
                {
                    id = content.BuildingId,
                    referencia = content.ReferenceMapId,
                    tamano = new[] { size.x, size.y, size.z },
                    solido = content.Box.Solid,
                    posicion = new[] { p.x, p.y, p.z },
                    giro = content.LocalYaw,
                    colocada = content.Placed
                });
            }
            return adjustment;
        }
    }
}
