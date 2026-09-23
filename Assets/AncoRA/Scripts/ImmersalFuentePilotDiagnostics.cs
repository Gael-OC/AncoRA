using Immersal;
using Immersal.XR;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    [DefaultExecutionOrder(-1000)]
    public sealed class ImmersalFuentePilotDiagnostics : MonoBehaviour
    {
        const int MapId = 151649;

        [SerializeField] XRMap map;
        [SerializeField] ImmersalSDK sdk;
        [SerializeField] Localizer localizer;
        [SerializeField] GameObject cube;
        [SerializeField] Renderer cubeRenderer;

        float sceneStart;
        float firstLocalization = -1f;
        float firstCubeVisible = -1f;
        int attempts;
        int successes;
        bool mapLoaded;
        bool poseAccepted;
        bool hadTrackingQuality;
        bool reportedFailure;
        string error = "";

        void Awake()
        {
            sceneStart = Time.realtimeSinceStartup;
            XRMapVisualization.pointCloudVisible = false;
            if (cube != null)
                cube.SetActive(false);

            if (MapManager.MapRegisteredAndLoaded == null)
                MapManager.MapRegisteredAndLoaded = new UnityEvent<int>();
            MapManager.MapRegisteredAndLoaded.AddListener(OnMapLoaded);

            if (localizer != null)
                localizer.OnLocalizationResult.AddListener(OnLocalizationResult);

            Debug.Log($"[AncoRA Immersal] Inicio piloto Fuente; versión {Application.version}, build {Application.buildGUID}, Unity {Application.unityVersion}, SDK {ImmersalSDK.sdkVersion}; mapa {MapId}; sin token embebido.");
        }

        void OnDestroy()
        {
            MapManager.MapRegisteredAndLoaded?.RemoveListener(OnMapLoaded);
            if (localizer != null)
                localizer.OnLocalizationResult.RemoveListener(OnLocalizationResult);
        }

        void OnMapLoaded(int id)
        {
            if (id != MapId)
                return;

            // This event follows Core.LoadMap; a visible PLY or imported TextAsset alone is not proof of loading.
            int nativePoints = Core.GetPointCloudSize(id);
            mapLoaded = nativePoints > 0;
            error = mapLoaded ? "" : $"Carga nativa del mapa {id} sin puntos (resultado {nativePoints}).";
            Debug.Log(mapLoaded
                ? $"[AncoRA Immersal] Mapa {id} cargado en plugin local: {nativePoints} puntos."
                : $"[AncoRA Immersal] ERROR: {error}");
        }

        void OnLocalizationResult(ILocalizationResults results)
        {
            foreach (var result in results.Results)
            {
                attempts++;
                if (!result.Success || result.MapId != MapId)
                    continue;

                successes++;
                if (mapLoaded && ARSession.state == ARSessionState.SessionTracking)
                {
                    poseAccepted = true;
                    if (firstLocalization < 0f)
                        firstLocalization = Time.realtimeSinceStartup - sceneStart;
                }

                Debug.Log($"[AncoRA Immersal] SDK devolvió localización: mapa {result.MapId}, intento {attempts}, éxito {successes}, t={Time.realtimeSinceStartup - sceneStart:F2}s; ARSession={ARSession.state}; carga nativa={mapLoaded}. Alineación física NO verificada.");
            }
        }

        void Update()
        {
            if (sdk != null && sdk.IsReady && !mapLoaded && !reportedFailure)
            {
                error = $"SDK listo, pero no se confirmó carga nativa local del mapa {MapId}; revisar log.";
                Debug.LogError($"[AncoRA Immersal] {error}");
                reportedFailure = true;
            }
            else if (sdk != null && !sdk.IsReady && !reportedFailure && Time.realtimeSinceStartup - sceneStart > 15f)
            {
                error = "SDK no quedó listo en 15 s; revisar log de inicialización.";
                Debug.LogError($"[AncoRA Immersal] {error}");
                reportedFailure = true;
            }

            var status = sdk != null && sdk.IsReady ? sdk.TrackingStatus : null;
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            // TrackingAnalyzer updates quality after its result event; zero during that first frame is not a loss.
            if (!tracking || (status != null && hadTrackingQuality && status.TrackingQuality == 0))
            {
                poseAccepted = false;
                hadTrackingQuality = false;
            }
            if (status != null && status.TrackingQuality > 0)
                hadTrackingQuality = true;

            bool show = poseAccepted && mapLoaded && tracking && status != null &&
                        status.LocalizationSuccessCount > 0 && status.TrackingQuality > 0;
            if (cube != null && cube.activeSelf != show)
            {
                cube.SetActive(show);
                Debug.Log(show
                    ? "[AncoRA Immersal] Cubo habilitado tras pose del SDK y tracking; alineación física pendiente de medir."
                    : "[AncoRA Immersal] Tracking/pose perdido: cubo oculto hasta nueva localización.");
            }

            if (show && cubeRenderer != null && cubeRenderer.isVisible && firstCubeVisible < 0f)
            {
                firstCubeVisible = Time.realtimeSinceStartup - sceneStart;
                Debug.Log($"[AncoRA Immersal] Primer cubo en campo de cámara: t={firstCubeVisible:F2}s desde la escena. Alineación física NO verificada.");
            }
        }

        void OnGUI()
        {
            var status = sdk != null && sdk.IsReady ? sdk.TrackingStatus : null;
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontSize = Mathf.Max(16, Screen.width / 48)
            };
            string text = $"AncoRA • Fuente | versión {Application.version} | build {Application.buildGUID}\nSDK {ImmersalSDK.sdkVersion}\n" +
                          $"ARSession: {ARSession.state} | SDK: {(sdk != null && sdk.IsReady ? "listo" : "esperando/error")}\n" +
                          $"Mapa {MapId} local: {(mapLoaded ? "cargado en plugin" : "SIN confirmar")}\n" +
                          $"Intentos/éxitos SDK: {attempts}/{successes} | calidad SDK: {status?.TrackingQuality ?? 0}/3\n" +
                          $"1.ª localización: {Seconds(firstLocalization)} | 1.er cubo en cámara: {Seconds(firstCubeVisible)}\n" +
                          $"Cubo: {(cube != null && cube.activeSelf ? "habilitado por pose SDK" : "oculto / sin pose válida")}\n" +
                          "Alineación física: NO verificada" + (string.IsNullOrEmpty(error) ? "" : $"\nERROR: {error}");
            float width = Mathf.Min(Screen.width - 24, 850);
            float height = style.CalcHeight(new GUIContent(text), width) + 12;
            GUI.Box(new Rect(12, 12, width, height), text, style);
            if (map != null && map.Visualization != null &&
                GUI.Button(new Rect(12, height + 24, width, 52), XRMapVisualization.pointCloudVisible
                    ? "Ocultar nube PLY (solo diagnóstico)"
                    : "Mostrar nube PLY (solo diagnóstico)",
                    new GUIStyle(GUI.skin.button) { fontSize = style.fontSize }))
            {
                XRMapVisualization.pointCloudVisible = !XRMapVisualization.pointCloudVisible;
                Debug.Log($"[AncoRA Immersal] Nube PLY {(XRMapVisualization.pointCloudVisible ? "visible" : "oculta")}: es visualización, NO prueba localización.");
            }
        }

        static string Seconds(float value) => value < 0f ? "pendiente" : $"{value:F2} s";
    }
}
