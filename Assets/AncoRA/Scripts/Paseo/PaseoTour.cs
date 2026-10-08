using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Immersal;
using Immersal.XR;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// Runs the tour. Every map has its own XR Space, so a localization moves only the space of its map and the boxes
    /// of other buildings stay where their last localization left them. A box is shown only after its space received
    /// the pose: the result event fires before the SDK moves the space, and showing earlier would flash the box at the
    /// world origin. None of this proves the box is physically on the building.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class PaseoTour : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const float MinChangeMeters = 0.002f;
        const float MinChangeDegrees = 0.05f;
        // A re-localization that lands on (almost) the same pose does not move the space measurably.
        const float AcceptWithoutMoveSeconds = 0.5f;
        const float SdkStartTimeoutSeconds = 15f;

        [SerializeField] ImmersalSDK sdk;
        [SerializeField] Localizer localizer;
        [SerializeField] PaseoMapContent[] contents = Array.Empty<PaseoMapContent>();

        sealed class MapRuntime
        {
            public PaseoMapContent Content;
            public XRSpace Space;
            public int Attempts;
            public int Successes;
            public bool Waiting;
            public float WaitingSince;
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

        readonly Dictionary<int, MapRuntime> maps = new();
        readonly PaseoVisibility visibility = new();
        readonly PaseoMapLoad mapLoad = new();
        PaseoBoxStore store;
        float sceneStart;
        string error = "";
        bool reportedTimeout;
        bool pendingLoadsResolved;
        bool wasTracking;
        int lastLocatedMapId = -1;

        public ImmersalSDK Sdk => sdk;
        public Localizer Localizer => localizer;
        public IReadOnlyList<PaseoMapContent> Contents => contents;
        public string Error => error;
        public bool SdkReady => sdk != null && sdk.IsReady;
        public float SecondsSinceStart => Time.realtimeSinceStartup - sceneStart;

        public string FirstMapStillLoading
        {
            get
            {
                int pending = mapLoad.FirstPending;
                return pending < 0 ? null : $"{pending} ({maps[pending].Content.BuildingName})";
            }
        }

        /// <summary>The box the team can edit: the one of the map that located last, while it is shown.</summary>
        public PaseoMapContent EditableContent =>
            lastLocatedMapId >= 0 && maps.TryGetValue(lastLocatedMapId, out var runtime) && runtime.Shown ? runtime.Content : null;

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
            sceneStart = Time.realtimeSinceStartup;
            XRMapVisualization.pointCloudVisible = false;
            store = new PaseoBoxStore();

            foreach (var content in contents)
            {
                var space = content.GetComponentInParent<XRSpace>();
                var runtime = new MapRuntime
                {
                    Content = content,
                    Space = space,
                    LastPosition = space.transform.position,
                    LastRotation = space.transform.rotation
                };
                maps[content.MapId] = runtime;
                visibility.AddMap(content.MapId, content.BuildingId);
                mapLoad.Add(content.MapId);
                ApplyStored(runtime);
                content.SetVisible(false);
            }

            if (MapManager.MapRegisteredAndLoaded == null)
                MapManager.MapRegisteredAndLoaded = new UnityEvent<int>();
            MapManager.MapRegisteredAndLoaded.AddListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.AddListener(OnLocalizationResult);

            Debug.Log($"{Tag} Inicio del paseo; versión {Application.version}, build {Application.buildGUID}, SDK {ImmersalSDK.sdkVersion}; " +
                      $"mapas [{string.Join(", ", contents.Select(c => $"{c.MapId} {c.BuildingId}"))}]; sin token embebido.");
        }

        void OnDestroy()
        {
            MapManager.MapRegisteredAndLoaded?.RemoveListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.RemoveListener(OnLocalizationResult);
        }

        // Phone values win over the scene they were edited from. If the scene was baked again since then (new
        // measurements, another phone's placements), the phone values are stale and are dropped.
        void ApplyStored(MapRuntime runtime)
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

            bool hadPose = store.HasPose(content.MapId);
            runtime.PhonePose = store.TryLoadPose(content.MapId, runtime.BakedPose, out var position, out float yaw);
            if (runtime.PhonePose)
                content.SetPose(position, yaw, placed: true);
            else
            {
                if (hadPose)
                    Debug.Log($"{Tag} La caja guardada en el teléfono para el mapa {content.MapId} venía de otra versión de la escena; se descarta.");
                runtime.InitialPlacementPending = !content.Placed;
            }
            if (runtime.PhonePose || runtime.PhoneSize)
                Debug.Log($"{Tag} El mapa {content.MapId} ({content.BuildingName}) usa valores ajustados en este teléfono.");
        }

        void OnMapLoaded(int id)
        {
            if (!maps.TryGetValue(id, out var runtime))
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
                if (!maps.TryGetValue(result.MapId, out var runtime))
                    continue;
                runtime.Attempts++;
                if (!result.Success)
                    continue;
                runtime.Successes++;
                if (!mapLoad.IsLoaded(result.MapId) || !tracking || runtime.Waiting)
                    continue;
                runtime.Waiting = true;
                runtime.WaitingSince = Time.realtimeSinceStartup;
            }
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            if (wasTracking && !tracking)
            {
                visibility.LoseTracking();
                Debug.Log($"{Tag} El teléfono perdió el tracking: se ocultan las cajas hasta que cada mapa vuelva a localizar.");
            }
            wasTracking = tracking;

            foreach (var runtime in maps.Values)
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
                bool settled = moved || (runtime.SpaceEverMoved && now - runtime.WaitingSince >= AcceptWithoutMoveSeconds);
                if (!settled)
                    continue;
                runtime.Waiting = false;
                if (!tracking)
                    continue;

                int id = runtime.Content.MapId;
                bool wasLocated = visibility.IsLocated(id);
                visibility.MarkLocated(id, now);
                lastLocatedMapId = id;
                if (runtime.InitialPlacementPending)
                    PlaceInitially(runtime);
                if (!wasLocated)
                    Debug.Log($"{Tag} {runtime.Content.BuildingName} ubicado con el mapa {id} (t={now - sceneStart:F1}s). Alineación física NO verificada.");
            }

            foreach (var runtime in maps.Values)
            {
                bool show = tracking && visibility.VisibleMap(runtime.Content.BuildingId) == runtime.Content.MapId;
                if (show == runtime.Shown)
                    continue;
                runtime.Shown = show;
                runtime.Content.SetVisible(show);
                Debug.Log(show
                    ? $"{Tag} Se muestra {runtime.Content.BuildingName} con el mapa {runtime.Content.MapId}."
                    : $"{Tag} Se oculta la caja del mapa {runtime.Content.MapId} ({runtime.Content.BuildingName}).");
            }

            CheckSdkStart();
            ResolvePendingLoads();
        }

        // A map whose native load failed never fires its event, so once the SDK is ready it is reported as failed
        // instead of "loading" forever; the other buildings keep working.
        void ResolvePendingLoads()
        {
            if (pendingLoadsResolved || !SdkReady)
                return;
            pendingLoadsResolved = true;
            foreach (int id in mapLoad.FailPending())
                Debug.LogError($"{Tag} ERROR: el SDK quedó listo sin cargar el mapa {id} ({maps[id].Content.BuildingName}); ese edificio no podrá ubicarse.");
        }

        void PlaceInitially(MapRuntime runtime)
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
            Debug.Log($"{Tag} La caja del mapa {content.MapId} ({content.BuildingName}) no estaba colocada: se puso a " +
                      $"{PaseoPlacement.DistanceMeters} m delante de la cámara. Ajustarla con el panel del equipo.");
        }

        void CheckSdkStart()
        {
            if (reportedTimeout || SdkReady || SecondsSinceStart < SdkStartTimeoutSeconds)
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
                var own = maps.Values.Where(m => m.Content.BuildingId == building).ToList();
                int visible = visibility.VisibleMap(building);
                var state = own.All(m => mapLoad.IsFailed(m.Content.MapId)) ? PaseoBuildingState.MapFailed
                    : visible < 0 ? PaseoBuildingState.Searching
                    : visibility.IsHeld(visible, now) ? PaseoBuildingState.Held
                    : PaseoBuildingState.Located;
                list.Add(new PaseoBuildingStatus(own[0].Content.BuildingName, state));
            }
            return list;
        }

        public string DescribeMaps()
        {
            var text = new StringBuilder();
            foreach (var runtime in maps.Values)
            {
                var c = runtime.Content;
                string load = mapLoad.IsLoaded(c.MapId) ? "cargado" : mapLoad.IsFailed(c.MapId) ? "NO CARGÓ" : "cargando";
                text.AppendLine($"Mapa {c.MapId} {c.BuildingName}: {load} | intentos/éxitos {runtime.Attempts}/{runtime.Successes} | " +
                                $"{(visibility.IsLocated(c.MapId) ? "ubicado" : "sin ubicar")} | {(runtime.Shown ? "visible" : "oculto")} | " +
                                $"{(c.Placed ? "caja colocada" : "CAJA SIN COLOCAR")}" +
                                (runtime.PhonePose || runtime.PhoneSize ? " | valores del teléfono" : ""));
            }
            return text.ToString().TrimEnd();
        }

        public void SaveEdit(PaseoMapContent content, Vector3 position, float yaw, Vector3 size, bool solid)
        {
            var edited = maps[content.MapId];
            content.SetPose(position, yaw, placed: true);
            store.SavePose(content.MapId, position, yaw, edited.BakedPose);
            edited.PhonePose = true;
            edited.InitialPlacementPending = false;
            foreach (var runtime in maps.Values)
                if (runtime.Content.BuildingId == content.BuildingId)
                {
                    runtime.Content.SetSize(size, solid);
                    runtime.PhoneSize = true;
                }
            store.SaveSize(content.BuildingId, size, solid, edited.BakedSize);
        }

        /// <summary>Forgets every value saved on this phone and reloads, so the scene values apply again.</summary>
        public void ForgetSavedAndReload()
        {
            store.Clear(contents.Select(c => c.MapId), contents.Select(c => c.BuildingId).Distinct());
            Debug.Log($"{Tag} Ajustes del teléfono borrados; se recarga la escena con los valores horneados.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public PaseoAdjustment BuildAdjustment()
        {
            var adjustment = new PaseoAdjustment { generado = DateTime.Now.ToString("s"), build = Application.buildGUID };
            foreach (var group in contents.GroupBy(c => c.BuildingId))
            {
                var size = group.First().SizeMeters;
                var building = new PaseoAdjustmentBuilding
                {
                    id = group.Key,
                    tamano = new[] { size.x, size.y, size.z },
                    solido = group.First().Box.Solid
                };
                foreach (var content in group)
                {
                    var p = content.LocalPosition;
                    building.mapas.Add(new PaseoAdjustmentMap
                    {
                        id = content.MapId, posicion = new[] { p.x, p.y, p.z }, giro = content.LocalYaw, colocada = content.Placed
                    });
                }
                adjustment.edificios.Add(building);
            }
            return adjustment;
        }
    }
}
