using System.Collections.Generic;
using System.Text;
using Immersal;
using Immersal.XR;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// Shows the facade frame only after an accepted localization and logs which map produced each pose,
    /// how large every pose change was, and when the localizing map changed. The HUD is for the team only:
    /// it is hidden by default and toggled with five taps on the top-left corner. Nothing here is a
    /// calibration step for the public, and none of it proves physical alignment.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ImmersalEdificioPilotDiagnostics : MonoBehaviour
    {
        const string Tag = "[AncoRA Edificio]";
        const float BigJumpMeters = 0.5f;
        const float BigJumpDegrees = 5f;
        const float MinChangeMeters = 0.002f;
        const float MinChangeDegrees = 0.05f;

        [SerializeField] XRMap[] maps;
        [SerializeField] XRSpace space;
        [SerializeField] ImmersalSDK sdk;
        [SerializeField] Localizer localizer;
        [SerializeField] EdificioFacadeFrame frame;
        [SerializeField] bool hudVisibleAtStart;
        [Tooltip("Draws a large plain-Spanish line with what the app is doing, always visible (Teologia demo).")]
        [SerializeField] bool showStatusBanner;
        [Tooltip("After the first accepted localization the content stays while the phone keeps tracking, even if Immersal " +
                 "has not corrected it recently (Teologia demo). Off = hide it whenever Immersal quality drops to zero.")]
        [SerializeField] bool keepVisibleAfterFirstLocalization;
        [Tooltip("Optional: map mode of the Teologia demo, shown in the banner. Maps that are not active are not tracked.")]
        [SerializeField] TeologiaMapSelector selector;

        sealed class MapState
        {
            public XRMap Map;
            public bool Loaded;
            public int Attempts;
            public int Successes;
            public int LastConfidence;
            public double LastRmse;
            public float LastSuccessTime = -1f;
        }

        readonly Dictionary<int, MapState> states = new();
        float sceneStart;
        float firstAccepted = -1f;
        float firstFrameVisible = -1f;
        bool accepted;
        bool placedOnce;
        bool positionHeld;
        bool waitingForSpacePose;
        bool hadTrackingQuality;
        bool hudVisible;
        bool reportedFailure;
        int teamTaps;
        float lastTapTime;
        int lastPoseMapId = -1;
        int lastPoseConfidence;
        int previousPoseMapId = -1;
        int mapSwitches;
        int spaceUpdates;
        float lastJumpMeters;
        float lastJumpDegrees;
        float maxJumpMeters;
        readonly StringBuilder cycleMaps = new();
        Vector3 lastSpacePosition;
        Quaternion lastSpaceRotation;
        string error = "";

        public bool HudVisible => hudVisible;
        public bool PositionHeld => positionHeld;
        public int LastPoseMapId => lastPoseMapId;
        public int MapSwitches => mapSwitches;
        public float LastJumpMeters => lastJumpMeters;
        public float LastJumpDegrees => lastJumpDegrees;

        // Development builds only: evidence for a remote reader of logcat. Without it a session that never localizes
        // looks the same as one that localizes badly, because failed attempts are not logged anywhere else.
        const float HeartbeatSeconds = 5f;
        const float FailureLogSeconds = 3f;
        float nextHeartbeat;
        float lastFailureLog = -100f;
        int failedAttempts;
        int emptyCycles;

        void Awake()
        {
            // The on-screen development console pops up on every error and covers the bottom adjust bar; errors still
            // reach logcat.
            Debug.developerConsoleVisible = false;
            sceneStart = Time.realtimeSinceStartup;
            hudVisible = hudVisibleAtStart;
            XRMapVisualization.pointCloudVisible = false;
            if (frame != null)
                frame.SetVisible(false);

            // A map switched off by the map selector is not registered by the SDK, so it must not be waited for.
            foreach (var map in maps)
                if (map != null && map.gameObject.activeInHierarchy)
                    states[map.mapId] = new MapState { Map = map };

            if (MapManager.MapRegisteredAndLoaded == null)
                MapManager.MapRegisteredAndLoaded = new UnityEvent<int>();
            MapManager.MapRegisteredAndLoaded.AddListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.AddListener(OnLocalizationResult);

            lastSpacePosition = space.transform.position;
            lastSpaceRotation = space.transform.rotation;
            Debug.Log($"{Tag} Inicio piloto edificio; versión {Application.version}, build {Application.buildGUID}, Unity {Application.unityVersion}, SDK {ImmersalSDK.sdkVersion}; mapas [{string.Join(", ", System.Array.ConvertAll(maps, m => m.mapId + "-" + m.mapName))}]; sin token embebido.");
        }

        void OnDestroy()
        {
            MapManager.MapRegisteredAndLoaded?.RemoveListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.RemoveListener(OnLocalizationResult);
        }

        void OnMapLoaded(int id)
        {
            if (!states.TryGetValue(id, out var state))
                return;
            // This event follows Core.LoadMap; an imported TextAsset or a visible PLY alone is not proof of loading.
            int nativePoints = Core.GetPointCloudSize(id);
            state.Loaded = nativePoints > 0;
            if (state.Loaded)
                Debug.Log($"{Tag} Mapa {id} cargado en plugin local: {nativePoints} puntos.");
            else
            {
                error = $"Carga nativa del mapa {id} sin puntos (resultado {nativePoints}).";
                Debug.LogError($"{Tag} ERROR: {error}");
            }
        }

        void OnLocalizationResult(ILocalizationResults results)
        {
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            cycleMaps.Clear();
            if (results.Results.Length == 0)
            {
                emptyCycles++;
                LogFailureThrottled($"{Tag} Ciclo de localización sin resultados ({emptyCycles} hasta ahora).");
            }
            foreach (var result in results.Results)
            {
                if (!states.TryGetValue(result.MapId, out var state))
                    continue;
                state.Attempts++;
                if (!result.Success)
                {
                    failedAttempts++;
                    LogFailureThrottled($"{Tag} Intento de localización SIN éxito: mapa {result.MapId} ({state.Attempts} intentos, {state.Successes} aciertos, {failedAttempts} fallos en total).");
                    continue;
                }

                state.Successes++;
                state.LastConfidence = result.LocalizeInfo.confidence;
                state.LastRmse = result.LocalizeInfo.rmse;
                state.LastSuccessTime = Time.realtimeSinceStartup - sceneStart;
                if (cycleMaps.Length > 0)
                    cycleMaps.Append('+');
                cycleMaps.Append(result.MapId);

                if (state.Loaded && tracking)
                {
                    // The session loop applies successful results to XR Space in order, so the last one wins.
                    lastPoseMapId = result.MapId;
                    lastPoseConfidence = result.LocalizeInfo.confidence;
                    accepted = true;
                    waitingForSpacePose = true;
                }
                Debug.Log($"{Tag} SDK devolvió localización: mapa {result.MapId}, confianza {result.LocalizeInfo.confidence}, rmse {result.LocalizeInfo.rmse:F3}, t={state.LastSuccessTime:F2}s, ARSession={ARSession.state}, carga nativa={state.Loaded}. Alineación física NO verificada.");
            }
        }

        void LogFailureThrottled(string message)
        {
            if (Time.realtimeSinceStartup - lastFailureLog < FailureLogSeconds)
                return;
            lastFailureLog = Time.realtimeSinceStartup;
            Debug.Log(message);
        }

        void LogHeartbeat()
        {
            var status = sdk != null && sdk.IsReady ? sdk.TrackingStatus : null;
            var perMap = new StringBuilder();
            foreach (var state in states.Values)
                perMap.Append($" [{state.Map.mapId}: {(state.Loaded ? "cargado" : "SIN cargar")}, {state.Attempts} intentos/{state.Successes} aciertos, conf {state.LastConfidence}]");
            Debug.Log($"{Tag} Latido t={Time.realtimeSinceStartup - sceneStart:F0}s: ARSession={ARSession.state}, motivo sin tracking={ARSession.notTrackingReason}, " +
                      $"SDK {(sdk != null && sdk.IsReady ? "listo" : "NO listo")}, mapas registrados={(MapManager.HasRegisteredMaps ? "sí" : "NO")}, " +
                      $"calidad {status?.TrackingQuality ?? 0}/3, éxitos SDK {status?.LocalizationSuccessCount ?? 0},{perMap} | fallos {failedAttempts}, ciclos vacíos {emptyCycles} | " +
                      $"caja {(frame != null && frame.IsVisible ? "visible" : "oculta")}{(positionHeld ? " (mantenida)" : "")}.");
        }

        void Update()
        {
            if (Debug.isDebugBuild && Time.realtimeSinceStartup >= nextHeartbeat)
            {
                nextHeartbeat = Time.realtimeSinceStartup + HeartbeatSeconds;
                LogHeartbeat();
            }
            CheckSdkHealth();
            var status = sdk != null && sdk.IsReady ? sdk.TrackingStatus : null;
            bool tracking = ARSession.state == ARSessionState.SessionTracking;

            // TrackingAnalyzer updates quality after its result event; zero during that first frame is not a loss.
            if (!tracking || (status != null && hadTrackingQuality && status.TrackingQuality == 0))
            {
                accepted = false;
                waitingForSpacePose = false;
                hadTrackingQuality = false;
            }
            if (status != null && status.TrackingQuality > 0)
                hadTrackingQuality = true;

            DetectSpacePoseChange();

            // Never show the frame before XR Space received the pose of the accepted result: XR Space starts at
            // the world origin, so an earlier frame would flash there.
            bool fresh = accepted && !waitingForSpacePose && tracking && status != null &&
                         status.LocalizationSuccessCount > 0 && status.TrackingQuality > 0;
            if (fresh)
                placedOnce = true;
            // Losing the phone's own tracking invalidates the placed pose: a new localization is needed.
            if (!tracking)
                placedOnce = false;
            // XR Space keeps the last applied pose when Immersal quality drops, so the content can stay where it was.
            bool held = keepVisibleAfterFirstLocalization && placedOnce && tracking && !fresh;
            bool show = fresh || held;
            if (held != positionHeld)
            {
                positionHeld = held;
                Debug.Log(held
                    ? $"{Tag} Sin corrección reciente de Immersal (calidad {status?.TrackingQuality ?? 0}/3): la caja se mantiene con el tracking del teléfono."
                    : $"{Tag} Immersal volvió a corregir la posición.");
            }
            if (frame != null && frame.IsVisible != show)
            {
                frame.SetVisible(show);
                if (show && firstAccepted < 0f)
                    firstAccepted = Time.realtimeSinceStartup - sceneStart;
                Debug.Log(show
                    ? $"{Tag} Marco habilitado con la pose del mapa {lastPoseMapId} (confianza {lastPoseConfidence}); alineación física pendiente de medir."
                    : $"{Tag} Tracking/pose perdido: marco oculto hasta una nueva localización.");
            }

            if (show && frame != null && frame.IsInsideCamera && firstFrameVisible < 0f)
            {
                firstFrameVisible = Time.realtimeSinceStartup - sceneStart;
                Debug.Log($"{Tag} Primer marco en campo de cámara: t={firstFrameVisible:F2}s desde la escena, mapa {lastPoseMapId}. Alineación física NO verificada.");
            }
        }

        void DetectSpacePoseChange()
        {
            var t = space.transform;
            float moved = Vector3.Distance(t.position, lastSpacePosition);
            float turned = Quaternion.Angle(t.rotation, lastSpaceRotation);
            if (moved < MinChangeMeters && turned < MinChangeDegrees)
                return;

            spaceUpdates++;
            waitingForSpacePose = false;
            bool firstUpdate = spaceUpdates == 1;
            lastJumpMeters = moved;
            lastJumpDegrees = turned;
            // The first change starts from the world origin and is not a jump of the placed content.
            if (!firstUpdate)
                maxJumpMeters = Mathf.Max(maxJumpMeters, moved);

            bool mapChanged = previousPoseMapId >= 0 && previousPoseMapId != lastPoseMapId;
            if (mapChanged)
                mapSwitches++;
            string origin = cycleMaps.Length > 0 ? cycleMaps.ToString() : lastPoseMapId.ToString();
            string message = firstUpdate
                ? $"{Tag} Primera pose aplicada a XR Space desde el mapa {origin}."
                : $"{Tag} Cambio de pose: {moved * 100f:F1} cm, {turned:F2}°, mapa que la produjo {origin}" +
                  (mapChanged ? $" (CAMBIO DE MAPA {previousPoseMapId}→{lastPoseMapId}: este salto mide la alineación A↔B más la deriva)" : "") + ".";
            if (!firstUpdate && (moved > BigJumpMeters || turned > BigJumpDegrees))
                Debug.LogWarning(message + " SALTO BRUSCO.");
            else
                Debug.Log(message);

            previousPoseMapId = lastPoseMapId;
            lastSpacePosition = t.position;
            lastSpaceRotation = t.rotation;
        }

        void CheckSdkHealth()
        {
            if (reportedFailure || sdk == null)
                return;
            if (sdk.IsReady)
            {
                foreach (var state in states.Values)
                {
                    if (!state.Loaded)
                    {
                        error = $"SDK listo, pero no se confirmó la carga nativa local del mapa {state.Map.mapId}; revisar log.";
                        Debug.LogError($"{Tag} {error}");
                        reportedFailure = true;
                        return;
                    }
                }
            }
            else if (Time.realtimeSinceStartup - sceneStart > 15f)
            {
                error = "SDK no quedó listo en 15 s; revisar log de inicialización.";
                Debug.LogError($"{Tag} {error}");
                reportedFailure = true;
            }
        }

        void OnGUI()
        {
            // Invisible five-tap area for the team; the public never sees any UI.
            if (GUI.Button(new Rect(0, 0, 110, 110), GUIContent.none, GUIStyle.none))
            {
                teamTaps = Time.realtimeSinceStartup - lastTapTime < 1.5f ? teamTaps + 1 : 1;
                lastTapTime = Time.realtimeSinceStartup;
                if (teamTaps >= 5)
                {
                    hudVisible = !hudVisible;
                    teamTaps = 0;
                }
            }
            float top = 12f;
            if (showStatusBanner)
                top = DrawStatusBanner() + 12f;
            if (!hudVisible)
                return;

            var status = sdk != null && sdk.IsReady ? sdk.TrackingStatus : null;
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontSize = Mathf.Max(16, Screen.width / 52)
            };
            var text = new StringBuilder();
            text.AppendLine($"AncoRA • Edificio (EQUIPO) | v{Application.version} | build {Application.buildGUID}");
            text.AppendLine($"SDK {ImmersalSDK.sdkVersion} | ARSession: {ARSession.state} | SDK: {(sdk != null && sdk.IsReady ? "listo" : "esperando/error")} | calidad {status?.TrackingQuality ?? 0}/3");
            foreach (var state in states.Values)
                text.AppendLine($"Mapa {state.Map.mapId} {state.Map.mapName}: {(state.Loaded ? "cargado en plugin" : "SIN confirmar")} | intentos/éxitos {state.Attempts}/{state.Successes} | conf {state.LastConfidence} rmse {state.LastRmse:F3}");
            text.AppendLine($"Mapa que produjo la pose: {(lastPoseMapId >= 0 ? lastPoseMapId.ToString() : "ninguno")} | cambios de mapa: {mapSwitches}");
            text.AppendLine($"Último salto: {lastJumpMeters * 100f:F1} cm / {lastJumpDegrees:F2}° | máx.: {maxJumpMeters * 100f:F1} cm");
            text.AppendLine($"1.ª aceptación: {Seconds(firstAccepted)} | 1.er marco en cámara: {Seconds(firstFrameVisible)}");
            text.AppendLine($"Marco: {(frame != null && frame.IsVisible ? "visible por pose SDK" : "oculto / sin pose válida")}" +
                            (frame != null && !frame.PlacedByTeam ? " | POSICIÓN SIN AJUSTAR" : ""));
            if (selector != null)
                text.AppendLine($"Modo de mapas: {selector.ModeLabel} | mapas activos: {states.Count}");
            if (frame != null && frame.IsBox)
            {
                var t = frame.transform;
                text.AppendLine($"Caja: {frame.WidthMeters:F1} × {frame.HeightMeters:F1} × {frame.DepthMeters:F1} m | pos ({t.localPosition.x:F1}, {t.localPosition.y:F1}, {t.localPosition.z:F1}) | giro {t.localEulerAngles.y:F0}° | relleno {(frame.Solid ? "sólido" : "transparente")}");
            }
            text.Append("Alineación física: NO verificada");
            if (!string.IsNullOrEmpty(error))
                text.Append($"\nERROR: {error}");

            float width = Mathf.Min(Screen.width - 24, 950);
            float height = style.CalcHeight(new GUIContent(text.ToString()), width) + 12;
            GUI.Box(new Rect(12, top, width, height), text.ToString(), style);
            if (maps.Length > 0 && maps[0].Visualization != null &&
                GUI.Button(new Rect(12, top + height + 12, width, 52), XRMapVisualization.pointCloudVisible
                    ? "Ocultar nubes PLY (solo diagnóstico)"
                    : "Mostrar nubes PLY (solo diagnóstico)",
                    new GUIStyle(GUI.skin.button) { fontSize = style.fontSize }))
            {
                XRMapVisualization.pointCloudVisible = !XRMapVisualization.pointCloudVisible;
                Debug.Log($"{Tag} Nubes PLY {(XRMapVisualization.pointCloudVisible ? "visibles" : "ocultas")}: visualización, NO prueba localización.");
            }
        }

        /// <summary>Draws the status line at the top of the screen and returns the y of its bottom edge.</summary>
        float DrawStatusBanner()
        {
            int attempts = 0, successes = 0;
            string loading = null;
            foreach (var state in states.Values)
            {
                attempts += state.Attempts;
                successes += state.Successes;
                if (!state.Loaded && loading == null)
                    loading = $"{state.Map.mapId} {state.Map.mapName}";
            }
            string message = EdificioStatusText.Describe(new EdificioStatusText.Inputs
            {
                Error = error,
                ArUnsupported = ARSession.state == ARSessionState.Unsupported,
                SdkReady = sdk != null && sdk.IsReady,
                SecondsSinceStart = Time.realtimeSinceStartup - sceneStart,
                MapStillLoading = loading,
                Tracking = ARSession.state == ARSessionState.SessionTracking,
                EverLocalized = firstAccepted >= 0f,
                Localized = accepted,
                ContentShown = frame != null && frame.IsVisible,
                PositionHeld = positionHeld,
                Attempts = attempts,
                Successes = successes,
                LastMapId = lastPoseMapId,
                ContentName = frame != null && frame.IsBox ? "Caja" : "Marco"
            }, out var level);

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(20, Screen.width / 34)
            };
            style.normal.textColor = level == EdificioStatusLevel.Ok ? new Color(0.55f, 1f, 0.6f)
                : level == EdificioStatusLevel.Error ? new Color(1f, 0.55f, 0.55f)
                : new Color(1f, 0.93f, 0.55f);
            string mode = selector != null ? $"[{selector.ModeLabel}] " : "";
            string notice = selector != null && !string.IsNullOrEmpty(selector.Notice) ? "\n" + selector.Notice : "";
            var content = new GUIContent("AncoRA · " + mode + message + notice);
            float width = Screen.width - 24f;
            float height = style.CalcHeight(content, width) + 12f;
            GUI.Box(new Rect(12, 12, width, height), content, style);
            return 12f + height;
        }

        static string Seconds(float value) => value < 0f ? "pendiente" : $"{value:F2} s";
    }
}
