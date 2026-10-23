using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using MultiSetEventData = MultiSet.EventData;
using MultiSetEvents = MultiSet.EventManager<MultiSet.EventData>;
using MultiSetGps = MultiSet.GpsCoordinateHandler;
using MultiSetLocalizer = MultiSet.SingleFrameLocalizationManager;
using MultiSetManager = MultiSet.MultisetSdkManager;
using MultiSetResponse = MultiSet.LocalizationSuccessResponse;
using MultiSetType = MultiSet.LocalizationType;

namespace AncorRA.AR
{
    /// <summary>
    /// The MultiSet side of the side-by-side test. Reads multiset.json from the phone, starts MultiSet's cloud localizer next
    /// to Immersal once the tour begins and AR tracks, and asks it to localize the camera at its own rhythm (PaseoMultiSetPace).
    /// MultiSet moves a map space object to where its map stands in the AR world; each accepted location is logged, its box
    /// relation to Immersal's box saved the first time (PaseoMultiSetGeometry) and compared after that, and a cyan box shows
    /// where MultiSet alone puts the building. Immersal is not touched. The credentials never leave this class: not in the
    /// scene, nor the log (PaseoLogFormat.RegisterSecret), nor PlayerPrefs.
    /// </summary>
    public sealed class PaseoMultiSetProbe : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const float AuthTimeoutSeconds = 25f;
        const float AuthRetrySeconds = 15f;
        const int AuthRetries = 10;

        [SerializeField] PaseoTour tour;
        // Inactive in the scene, so a phone without multiset.json runs exactly as before: no SDK Awake (frame rate and screen
        // sleep), no watermark GUI. The probe turns it on when multiset.json is complete.
        [SerializeField] GameObject sdkObject;
        [SerializeField] MultiSetManager sdkManager;
        [SerializeField] MultiSetLocalizer localizer;
        // The SDK places this object (a root, never scaled) where its map stands in the AR world after each location.
        [SerializeField] GameObject mapSpace;
        [SerializeField] Material boxBorder;
        [SerializeField] Material boxFill;

        readonly PaseoMultiSetStore store = new();
        readonly HashSet<string> firstLocated = new();
        readonly Dictionary<string, EdificioFacadeFrame> boxes = new();
        PaseoMultiSetConfig config;
        PaseoMultiSetPace pace;
        PaseoMultiSetState state = PaseoMultiSetState.NoConfig;
        string detail = "";
        string loggedState;
        bool started;
        bool authenticated;
        bool authFailed;
        float authStartedAt;
        float nextAuthRetry;
        int authRetries;
        bool queriesOn = true;
        bool wasTracking;
        // The query in flight: when it went out, whether it carried the GPS hint, and the map space as it was (a result below
        // the threshold still moves it, see OnResponse).
        bool pending;
        float queryStartedAt;
        bool queryGps;
        Vector3 spacePosition;
        Quaternion spaceRotation = Quaternion.identity;
        bool spaceWasActive;
        PaseoMultiSetAttempt? last;

        public PaseoTour Tour => tour;
        public GameObject SdkObject => sdkObject;
        public MultiSetManager Manager => sdkManager;
        public MultiSetLocalizer Localizer => localizer;
        public GameObject MapSpace => mapSpace;
        public Material BoxBorder => boxBorder;
        public Material BoxFill => boxFill;
        public PaseoMultiSetState State => state;
        /// <summary>The team's switch: queries go out (or not). Independent of the state, which also says why nothing goes out. Off without multiset.json.</summary>
        public bool QueriesOn => config != null && queriesOn;
        public int Queries => pace?.Queries ?? 0;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(PaseoTour paseoTour, GameObject sdk, MultiSetManager manager, MultiSetLocalizer localization, GameObject space,
            Material border, Material fill)
        {
            tour = paseoTour;
            sdkObject = sdk;
            sdkManager = manager;
            localizer = localization;
            mapSpace = space;
            boxBorder = border;
            boxFill = fill;
        }

        void Start() => LoadConfig();

        void LoadConfig()
        {
            var status = PaseoMultiSetConfig.Load(Application.persistentDataPath, out config, out string message);
            if (status != PaseoMultiSetConfigStatus.Ready)
            {
                config = null;
                detail = message;
                Debug.Log($"{Tag} {message}");
                SetState(PaseoMultiSetState.NoConfig);
                return;
            }
            // From here on any line that carries a credential is written with «***» in its place.
            PaseoLogFormat.RegisterSecret(config.ClientId);
            PaseoLogFormat.RegisterSecret(config.ClientSecret);
            pace = new PaseoMultiSetPace(config.MaxQueries, config.SecondsAfterLocated);
            Debug.Log($"{Tag} MultiSet listo: mapa {config.Code} ({(config.Kind == PaseoMultiSetKind.Map ? "map" : "mapset")}), " +
                      $"{config.Buildings.Count} edificio(s) nombrado(s), pista GPS {(config.GpsHint ? "sí" : "no")}, tope de {config.MaxQueries} consultas.");
            SetState(PaseoMultiSetState.Waiting);
        }

        void OnDestroy()
        {
            if (started)
            {
                MultiSetEvents.StopListening("AuthCallBack", OnAuth);
                if (localizer != null)
                {
                    localizer.OnLocalizationWithResponse -= OnResponse;
                    localizer.LocalizationFailure.RemoveListener(OnFailure);
                }
            }
            // The boxes' meshes belong to EdificioFacadeFrame, which releases them; the materials are scene assets.
            foreach (var frame in boxes.Values)
                if (frame != null)
                    Destroy(frame.gameObject);
            boxes.Clear();
        }

        void Update()
        {
            if (config == null)
                return;
            float now = Time.realtimeSinceStartup;
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            if (wasTracking && !tracking)
            {
                pace.LoseTracking();
                HideBoxes();
            }
            wasTracking = tracking;

            if (!started)
            {
                if (tour != null && tour.Begun && tracking)
                    StartSdk(now);
                return;
            }
            UpdateAuthentication(now);
            if (pending && pace.TimedOut(now))
                GiveUp(now);
            if (authenticated && queriesOn && tracking && pace.Due(now))
                Query(now);
            Refresh();
        }

        // Everything the SDK reads at its own Awake/Start is set before the object turns on.
        void StartSdk(float now)
        {
            started = true;
            sdkManager.clientId = config.ClientId;
            sdkManager.clientSecret = config.ClientSecret;
            localizer.localizationType = config.Kind == PaseoMultiSetKind.Map ? MultiSetType.Map : MultiSetType.Mapset;
            localizer.mapOrMapsetCode = config.Code;
            // We ask, one query at a time: no automatic, background or re-localization, no silent retry of the first failure
            // (it would loop on queries the cap cannot count), and no confidence check, so low results are logged too.
            localizer.autoLocalize = false;
            localizer.relocalization = false;
            localizer.backgroundLocalization = false;
            localizer.firstLocalizationUntilSuccess = false;
            localizer.showAlert = false;
            localizer.confidenceCheck = false;
            // The hint needs the SDK's GPS handler, which its Start creates when this is on.
            localizer.passGeoPose = config.GpsHint;
            localizer.hintRadius = Mathf.RoundToInt(config.GpsRadiusMeters);

            MultiSetEvents.StartListening("AuthCallBack", OnAuth);
            localizer.OnLocalizationWithResponse += OnResponse;
            localizer.LocalizationFailure.AddListener(OnFailure);

            // The SDK's Awake forces 90 fps and a screen that never sleeps; put back what the app had.
            int frameRate = Application.targetFrameRate;
            int sleep = Screen.sleepTimeout;
            sdkObject.SetActive(true);
            Application.targetFrameRate = frameRate;
            Screen.sleepTimeout = sleep;

            // Enabling the manager does not authenticate: it only does when a MultiSetConfig asset sits in a Resources folder
            // (the SDK ships one in its samples), and this project has none. The credentials are the ones set above.
            authStartedAt = now;
            Debug.Log($"{Tag} MultiSet: se inicia el SDK y se autentica.");
            sdkManager.AuthenticateMultiSetSDK();
            Refresh();
        }

        void OnAuth(MultiSetEventData data)
        {
            authenticated = data != null && data.AuthSuccess;
            authFailed = !authenticated;
            if (authenticated)
            {
                authRetries = 0;
                detail = "";
                return;
            }
            detail = "no se pudo autenticar (revisar internet y las credenciales)";
            nextAuthRetry = Time.realtimeSinceStartup + AuthRetrySeconds;
        }

        // No internet leaves the SDK silent (it logs «Network Error!» and sends no event), so a missing answer is an error too.
        void UpdateAuthentication(float now)
        {
            if (authenticated)
                return;
            if (!authFailed && now - authStartedAt >= AuthTimeoutSeconds)
            {
                authFailed = true;
                detail = "MultiSet no respondió (¿hay internet?)";
                nextAuthRetry = now;
            }
            if (!authFailed || authRetries >= AuthRetries || now < nextAuthRetry)
                return;
            authRetries++;
            authFailed = false;
            authStartedAt = now;
            Debug.Log($"{Tag} MultiSet: se reintenta la autenticación ({authRetries}/{AuthRetries}).");
            sdkManager.AuthenticateMultiSetSDK();
        }

        void Query(float now)
        {
            var space = mapSpace.transform;
            spacePosition = space.position;
            spaceRotation = space.rotation;
            spaceWasActive = mapSpace.activeSelf;
            queryGps = config.GpsHint && MultiSetGps.Instance != null && MultiSetGps.Instance.gpsCoordinates.IsValid();
            queryStartedAt = now;
            pending = true;
            pace.Begin(now);
            try
            {
                localizer.LocalizeFrame();
            }
            catch (Exception e)
            {
                pending = false;
                pace.End(now, accepted: false);
                Debug.LogWarning($"{Tag} MultiSet: la consulta {pace.Queries} falló al enviarse: {e.Message}");
            }
        }

        // A query that never answers (no CPU image, no network) is given up so the next one can go.
        void GiveUp(float now)
        {
            pending = false;
            var attempt = new PaseoMultiSetAttempt(pace.Queries, now - queryStartedAt, null, 0f, false, null, queryGps, tour.SecondsSinceStart);
            last = attempt;
            pace.End(now, accepted: false);
            Debug.LogWarning($"{Tag} MultiSet: la consulta {attempt.Number} no respondió en {PaseoMultiSetPace.TimeoutSeconds:F0} s; se da por perdida.");
            Debug.Log(PaseoLogFormat.MultiSetAttempt(attempt));
        }

        // The SDK moves the map space first and raises this event after, so the space already holds this result's pose.
        void OnResponse(MultiSetResponse response)
        {
            if (!pending)
            {
                // An answer after the query was given up: its pose is no longer ours to keep.
                RestoreMapSpace();
                Debug.LogWarning($"{Tag} MultiSet: llegó una respuesta tardía; se ignora.");
                return;
            }
            pending = false;
            float now = Time.realtimeSinceStartup;
            string code = response.mapCodes != null && response.mapCodes.Count > 0 ? response.mapCodes[0]
                : config.Kind == PaseoMultiSetKind.Map ? config.Code : null;
            bool accepted = PaseoMultiSetPace.Accepts(code, response.confidence);
            string building = config.BuildingFor(code);
            var attempt = new PaseoMultiSetAttempt(pace.Queries, now - queryStartedAt, code, response.confidence, accepted, building, queryGps,
                tour.SecondsSinceStart);
            last = attempt;
            Debug.Log(PaseoLogFormat.MultiSetAttempt(attempt));
            pace.End(now, accepted);
            if (!accepted)
            {
                // With the confidence check off the SDK applied this result anyway; a weak one must not move the cyan boxes.
                RestoreMapSpace();
                return;
            }
            OnLocated(building, code, response.confidence);
        }

        void OnFailure()
        {
            if (!pending)
                return;
            pending = false;
            float now = Time.realtimeSinceStartup;
            var attempt = new PaseoMultiSetAttempt(pace.Queries, now - queryStartedAt, null, 0f, false, null, queryGps, tour.SecondsSinceStart);
            last = attempt;
            pace.End(now, accepted: false);
            Debug.Log(PaseoLogFormat.MultiSetAttempt(attempt));
        }

        void RestoreMapSpace()
        {
            mapSpace.transform.SetPositionAndRotation(spacePosition, spaceRotation);
            mapSpace.SetActive(spaceWasActive);
        }

        // An accepted location: tie the building's box to Immersal's the first time, compare it every time after, and show where
        // MultiSet alone puts each building that has a saved relation to this map.
        void OnLocated(string building, string code, float confidence)
        {
            float seconds = tour.SecondsSinceStart;
            var space = mapSpace.transform;
            var evaluation = new PaseoMultiSetEvaluation(PaseoMultiSetRelationState.NoImmersal, default, false, float.NaN);
            if (building != null)
            {
                bool hasSaved = store.TryLoad(config.Code, building, out var saved);
                bool solid = tour.TryGetSolidBox(building, out var content);
                var box = solid ? content.transform : null;
                evaluation = PaseoMultiSetGeometry.Evaluate(hasSaved, saved, solid, solid ? box.position : default,
                    solid ? box.rotation : Quaternion.identity, solid ? content.SizeMeters : default, space.position, space.rotation);
                if (evaluation.State == PaseoMultiSetRelationState.New)
                    store.Save(config.Code, building, evaluation.Relation);
            }
            ShowBoxes();
            float distance = float.NaN;
            var camera = Camera.main;
            if (evaluation.HasRelation && camera != null)
            {
                PaseoMultiSetGeometry.ToWorld(evaluation.Relation, space.position, space.rotation, out var centre, out _);
                distance = Vector3.Distance(camera.transform.position, centre);
            }
            Debug.Log(PaseoLogFormat.MultiSetLocation(building, code, confidence, evaluation.State, evaluation.DifferenceMeters, distance, seconds));
            if (building != null && firstLocated.Add(building))
                Debug.Log(PaseoLogFormat.FirstLocation("multiset", building, seconds));
        }

        // One cyan box per building with a relation saved for this map code; the others hide.
        void ShowBoxes()
        {
            foreach (string building in tour.Contents.Where(c => c != null).Select(c => c.BuildingId).Distinct())
            {
                if (!store.TryLoad(config.Code, building, out var relation))
                {
                    if (boxes.TryGetValue(building, out var stale) && stale != null)
                        stale.SetVisible(false);
                    continue;
                }
                if (!boxes.TryGetValue(building, out var frame) || frame == null)
                {
                    var boxObject = new GameObject($"Caja MultiSet {building}");
                    boxObject.transform.SetParent(mapSpace.transform, false);
                    frame = boxObject.AddComponent<EdificioFacadeFrame>();
                    frame.Configure(relation.Size.x, relation.Size.y, relation.Size.z, boxBorder, boxFill, null, false);
                    boxes[building] = frame;
                }
                else if (frame.WidthMeters != relation.Size.x || frame.HeightMeters != relation.Size.y || frame.DepthMeters != relation.Size.z)
                    frame.SetSize(relation.Size.x, relation.Size.y, relation.Size.z);
                frame.transform.SetLocalPositionAndRotation(relation.Position, relation.Rotation);
                frame.SetVisible(true);
            }
        }

        void HideBoxes()
        {
            foreach (var frame in boxes.Values)
                if (frame != null)
                    frame.SetVisible(false);
        }

        /// <summary>The team's pause: no queries go out and the cyan boxes hide until the next accepted location.</summary>
        public void TogglePause()
        {
            if (config == null)
                return;
            queriesOn = !queriesOn;
            if (!queriesOn)
                HideBoxes();
            Debug.Log($"{Tag} DEPURACION_AJUSTE multiset={(queriesOn ? "si" : "no")} t={(tour != null ? tour.SecondsSinceStart : Time.realtimeSinceStartup):F1}");
            Refresh();
        }

        /// <summary>Forgets the box relations saved on this phone; the next accepted location saves them again from Immersal's box.</summary>
        public void ForgetRelations()
        {
            int forgotten = store.ClearAll();
            HideBoxes();
            Debug.Log($"{Tag} Relaciones de caja de MultiSet olvidadas: {forgotten}.");
        }

        /// <summary>The status row of the team sheet.</summary>
        public string StatusText() =>
            PaseoMultiSetStates.Describe(state, Queries, last, last is { } attempt ? BuildingName(attempt.BuildingId) : null, detail);

        string BuildingName(string buildingId) =>
            string.IsNullOrEmpty(buildingId) ? null : tour.Contents.FirstOrDefault(c => c != null && c.BuildingId == buildingId)?.BuildingName;

        void Refresh()
        {
            var next = config == null ? PaseoMultiSetState.NoConfig
                : !started ? PaseoMultiSetState.Waiting
                : authFailed ? PaseoMultiSetState.Error
                : !authenticated ? PaseoMultiSetState.Authenticating
                : pace.CapReached ? PaseoMultiSetState.CapReached
                : !queriesOn ? PaseoMultiSetState.Paused
                : PaseoMultiSetState.Authenticated;
            SetState(next);
        }

        void SetState(PaseoMultiSetState next)
        {
            state = next;
            string name = PaseoMultiSetStates.LogName(next);
            if (name == loggedState)
                return;
            loggedState = name;
            Debug.Log(PaseoLogFormat.MultiSetState(name, Queries, tour != null ? tour.SecondsSinceStart : Time.realtimeSinceStartup));
            if (next == PaseoMultiSetState.CapReached)
                Debug.Log($"{Tag} MultiSet: tope de {config.MaxQueries} consultas alcanzado; ya no se consulta en esta sesión.");
        }
    }
}
