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
        public int LastPoseMapId => lastPoseMapId;
        public int MapSwitches => mapSwitches;
        public float LastJumpMeters => lastJumpMeters;
        public float LastJumpDegrees => lastJumpDegrees;

        void Awake()
        {
            sceneStart = Time.realtimeSinceStartup;
            hudVisible = hudVisibleAtStart;
            XRMapVisualization.pointCloudVisible = false;
            if (frame != null)
                frame.SetVisible(false);

            foreach (var map in maps)
                if (map != null)
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
            foreach (var result in results.Results)
            {
                if (!states.TryGetValue(result.MapId, out var state))
                    continue;
                state.Attempts++;
                if (!result.Success)
                    continue;

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

        void Update()
        {
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
            bool show = accepted && !waitingForSpacePose && tracking && status != null &&
                        status.LocalizationSuccessCount > 0 && status.TrackingQuality > 0;
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
            text.Append("Alineación física: NO verificada");
            if (!string.IsNullOrEmpty(error))
                text.Append($"\nERROR: {error}");

            float width = Mathf.Min(Screen.width - 24, 950);
            float height = style.CalcHeight(new GUIContent(text.ToString()), width) + 12;
            GUI.Box(new Rect(12, 12, width, height), text.ToString(), style);
            if (maps.Length > 0 && maps[0].Visualization != null &&
                GUI.Button(new Rect(12, height + 24, width, 52), XRMapVisualization.pointCloudVisible
                    ? "Ocultar nubes PLY (solo diagnóstico)"
                    : "Mostrar nubes PLY (solo diagnóstico)",
                    new GUIStyle(GUI.skin.button) { fontSize = style.fontSize }))
            {
                XRMapVisualization.pointCloudVisible = !XRMapVisualization.pointCloudVisible;
                Debug.Log($"{Tag} Nubes PLY {(XRMapVisualization.pointCloudVisible ? "visibles" : "ocultas")}: visualización, NO prueba localización.");
            }
        }

        static string Seconds(float value) => value < 0f ? "pendiente" : $"{value:F2} s";
    }
}
