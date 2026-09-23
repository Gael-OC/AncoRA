#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AncorRA.AR;
using Google.XR.ARCoreExtensions;
using Immersal;
using Immersal.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace AncorRA.Editor
{
    public static class ImmersalFuentePilotSetup
    {
        const string Scene = "Assets/Scenes/ImmersalFuentePilot.unity";
        const string GeospatialScene = "Assets/Scenes/GeospatialPilot.unity";
        const string DataFolder = "Assets/AncoRA/ImmersalFuente";
        const string BaseName = "151649-Fuente";
        const string MaterialPath = DataFolder + "/CuboFuenteURP.mat";

        [MenuItem("AncoRA/Immersal/Crear piloto Fuente desde SimpleSample")]
        public static void Create()
        {
            if (File.Exists(Scene))
                throw new InvalidOperationException($"La escena {Scene} ya existe; no se sobrescribirá el ajuste del equipo.");

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
            if (package == null || package.version != "2.4.0")
                throw new InvalidOperationException("Se necesita Immersal Core 2.4.0 antes de importar SimpleSample.");

            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets/AncoRA", "ImmersalFuente");

            string source = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "probarMapaFuente");
            foreach (string suffix in new[] { ".bytes", "-metadata.json", "-sparse.ply" })
            {
                string from = Path.Combine(source, BaseName + suffix);
                string to = Path.Combine(DataFolder, BaseName + suffix);
                if (!File.Exists(from))
                    throw new FileNotFoundException("Falta archivo original del mapa Fuente.", from);
                if (File.Exists(to))
                    throw new InvalidOperationException($"El archivo {to} ya existe; no se sobrescribirá.");
                File.Copy(from, to);
            }
            AssetDatabase.Refresh();

            string sample = Path.Combine(package.resolvedPath, "Samples~", "Core", "Scenes", "SimpleSample.unity");
            File.Copy(sample, Scene);
            AssetDatabase.ImportAsset(Scene);
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            Lightmapping.lightingSettings = null;
            UseDirectMapPose();

            // Keep the official sample's session, AR camera, SDK prefab and XR Space wiring.
            foreach (string unwanted in new[] { "Canvas", "EventSystem", "Directional Light" })
            {
                var go = GameObject.Find(unwanted);
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            var sdk = Object.FindAnyObjectByType<ImmersalSDK>();
            if (sdk == null)
                throw new InvalidOperationException("SimpleSample no contiene el prefab ImmersalSDK.");
            PrefabUtility.UnpackPrefabInstance(sdk.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            sdk.developerToken = "";
            var localizer = sdk.GetComponentInChildren<Localizer>(true);
            var device = sdk.GetComponentInChildren<DeviceLocalization>(true);
            var server = sdk.GetComponentInChildren<ServerLocalization>(true);
            if (server != null)
                Object.DestroyImmediate(server.gameObject);
            var methods = new SerializedObject(localizer).FindProperty("m_LocalizationMethodObjects");
            methods.arraySize = 1;
            methods.GetArrayElementAtIndex(0).objectReferenceValue = device;
            methods.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var origin = Object.FindAnyObjectByType<XROrigin>();
            origin.CameraYOffset = 0f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            var map = Object.FindAnyObjectByType<XRMap>();
            var bytes = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + "/" + BaseName + ".bytes");
            if (map == null || bytes == null)
                throw new InvalidOperationException("Falta XR Map o el TextAsset local del mapa.");

            // Configure reads the adjacent unmodified metadata in the Editor.
            map.Configure(bytes);
            map.LocalizationMethod = device;
            map.MapOptions = new List<IMapOption>
            {
                new MapLoadingOption { m_SerializedDataSource = (int)MapDataSource.Embed, DownloadVisualizationAtRuntime = false }
            };
            map.SerializeMapOptions();
            map.ApplyAlignment();
            if (map.mapId != 151649 || map.mapName != "Fuente" || map.mapAlignment.scale != 1d)
                throw new InvalidOperationException("El archivo local o su metadata no configuran el mapa Fuente 151649.");

            // This is the same LoadPly call made by XRMapVisualizationEditor's local PLY button.
            map.CreateVisualization(XRMapVisualization.RenderMode.EditorAndRuntime);
            map.Visualization.LoadPly(Path.Combine(DataFolder, BaseName + "-sparse.ply"));
            Debug.Log($"[AncoRA Immersal] PLY local para editar: {map.Visualization.Mesh.vertexCount} puntos, bounds {map.Visualization.Mesh.bounds}. No demuestra localización.");

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cubo prueba Fuente (ajustar en Editor)";
            cube.transform.SetParent(map.transform, false);
            // The green circular rim in the local PLY is at roughly X=-4, Y=-8, Z=-17.
            cube.transform.localPosition = new Vector3(-4.2f, -7.3f, -16.5f);
            cube.transform.localScale = Vector3.one * 0.5f;
            cube.isStatic = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("Falta el shader URP/Unlit para el cubo.");
            var material = new Material(shader) { name = "Cubo Fuente URP opaco" };
            material.SetColor("_BaseColor", new Color(1f, 0.28f, 0.02f, 1f));
            AssetDatabase.CreateAsset(material, MaterialPath);
            var cubeRenderer = cube.GetComponent<MeshRenderer>();
            cubeRenderer.sharedMaterial = material;

            var diagnostics = map.gameObject.AddComponent<ImmersalFuentePilotDiagnostics>();
            var fields = new SerializedObject(diagnostics);
            fields.FindProperty("map").objectReferenceValue = map;
            fields.FindProperty("sdk").objectReferenceValue = sdk;
            fields.FindProperty("localizer").objectReferenceValue = localizer;
            fields.FindProperty("cube").objectReferenceValue = cube;
            fields.FindProperty("cubeRenderer").objectReferenceValue = cubeRenderer;
            fields.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AncoRA Immersal] Piloto creado desde SimpleSample: {Scene}; .bytes Embed, DeviceLocalization, sin token. Ajustar cubo mirando la nube en Scene View.");
        }

        static void UseDirectMapPose()
        {
            // The sample smoother starts at the world origin; first localization must not display content there.
            var space = Object.FindAnyObjectByType<XRSpace>();
            space.ProcessPoses = false;
            var serialized = new SerializedObject(space);
            serialized.FindProperty("m_DataProcessors").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (string name in new[] { "PoseFilter", "PoseSmoother" })
            {
                var child = space.transform.Find(name);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        public static void ConfigureDirectPoseInScene()
        {
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            UseDirectMapPose();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AncoRA Immersal] XR Space aplicará la pose del mapa sin retraso del suavizador de SimpleSample.");
        }

        [MenuItem("AncoRA/Immersal/Activar piloto Fuente en Build Settings")]
        public static void ConfigureBuildSettings()
        {
            if (!File.Exists(Scene))
                throw new FileNotFoundException("Falta la escena del piloto Immersal.", Scene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Scene, true),
                new EditorBuildSettingsScene(GeospatialScene, false)
            };
            PlayerSettings.allowUnsafeCode = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            AssetDatabase.SaveAssets();
            Debug.Log("[AncoRA Immersal] Escena Fuente habilitada; Geospatial deshabilitada. Unsafe, IL2CPP, ARM64, GLES3 y Metal configurados.");
        }

        [MenuItem("AncoRA/Immersal/Validar piloto Fuente")]
        public static void Validate()
        {
            var errors = new List<string>();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            if (scenes.Length != 1 || scenes[0].path != Scene)
                errors.Add("Solo ImmersalFuentePilot.unity debe estar habilitada en Build Settings.");
            if (!PlayerSettings.allowUnsafeCode ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS) != ScriptingImplementation.IL2CPP)
                errors.Add("Faltan unsafe o IL2CPP en Android/iOS.");
            // Unity 6000.6 reports iOS automatic=true via the API even with the saved override disabled.
            string settingsYaml = File.ReadAllText("ProjectSettings/ProjectSettings.asset").Replace("\r", "");
            bool iosOverrideSaved = settingsYaml.Contains(
                "m_BuildTarget: iOSSupport\n    m_APIs: 10000000i\n    m_Automatic: 0");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64 ||
                PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) || !iosOverrideSaved ||
                PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).FirstOrDefault() != GraphicsDeviceType.OpenGLES3 ||
                PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS).FirstOrDefault() != GraphicsDeviceType.Metal)
                errors.Add($"Gráficos/ABI inesperados: arquitectura={PlayerSettings.Android.targetArchitectures}, " +
                           $"Android(auto={PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android)}, APIs={string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}), " +
                           $"iOS(auto guardado desactivado={iosOverrideSaved}, auto API={PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.iOS)}, APIs={string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS))}).");
            if (iosOverrideSaved && PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.iOS))
                Debug.LogWarning("[AncoRA Immersal] Unity 6000.6 informa iOS Graphics API automática pese a m_Automatic: 0 guardado; iOS solo usa Metal. Comprobar en Player Settings del Editor.");

            var xrAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/XR/XRGeneralSettings.asset");
            var settings = xrAssets.OfType<XRGeneralSettings>().ToArray();
            if (settings.Length != 4 || settings.Count(s => s.name == "Android Settings") != 1 ||
                settings.Count(s => s.name == "iPhone Settings") != 1)
                errors.Add("XRGeneralSettings debe conservar exactamente cuatro entradas, sin Android/iPhone duplicados.");
            var xr = xrAssets.OfType<XRManagerSettings>().ToArray();
            foreach (var platform in new[] { ("Android Providers", "ARCoreLoader"), ("iPhone Providers", "ARKitLoader") })
            {
                var managers = xr.Where(m => m.name == platform.Item1).ToArray();
                if (managers.Length != 1 || managers[0].activeLoaders.Count != 1 ||
                    managers[0].activeLoaders[0].GetType().Name != platform.Item2)
                    errors.Add($"XR: {platform.Item1} debe contener solo {platform.Item2} (sin OpenXR).");
            }

            if (!File.Exists(Scene))
                errors.Add("Falta la escena Immersal Fuente.");
            else
            {
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                var map = Object.FindAnyObjectByType<XRMap>(FindObjectsInactive.Include);
                var sdk = Object.FindAnyObjectByType<ImmersalSDK>(FindObjectsInactive.Include);
                var cube = map != null ? map.transform.Find("Cubo prueba Fuente (ajustar en Editor)") : null;
                var mlo = map?.MapOptions.OfType<MapLoadingOption>().SingleOrDefault();
                if (Object.FindObjectsByType<ARSession>(FindObjectsInactive.Include).Length != 1 ||
                    Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include).Length != 1 ||
                    Object.FindObjectsByType<ARCameraManager>(FindObjectsInactive.Include).Length != 1 ||
                    Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).Length != 1 ||
                    Object.FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include).Length != 1 ||
                    Object.FindObjectsByType<AREarthManager>(FindObjectsInactive.Include).Length != 0 ||
                    Object.FindObjectsByType<GeospatialVpsProbe>(FindObjectsInactive.Include).Length != 0)
                    errors.Add("Debe haber una sola ARSession, XROrigin, cámara AR y pose driver; sin GeospatialVpsProbe.");
                if (map == null || !map.IsConfigured || map.mapId != 151649 || map.mapName != "Fuente" ||
                    map.LocalizationMethod is not DeviceLocalization || map.mapFile == null ||
                    AssetDatabase.GetAssetPath(map.mapFile) != DataFolder + "/" + BaseName + ".bytes" ||
                    mlo == null || mlo.m_SerializedDataSource != (int)MapDataSource.Embed ||
                    mlo.DownloadVisualizationAtRuntime || (mlo.Bytes != null && mlo.Bytes.Length > 0))
                    errors.Add("XR Map debe referenciar el .bytes 151649, DeviceLocalization y Embed, sin descarga ni bytes alternativos.");
                if (map != null && map.GetComponentInParent<XRSpace>()?.ProcessPoses != false)
                    errors.Add("XR Space debe aplicar la primera pose directamente (sin mostrar el cubo en el origen).");
                if (map?.Visualization == null || !map.Visualization.IsVisualized ||
                    map.Visualization.renderMode != XRMapVisualization.RenderMode.EditorAndRuntime ||
                    map.Visualization.Mesh == null || map.Visualization.Mesh.vertexCount != 4630)
                    errors.Add("La visualización PLY local de 4630 puntos no quedó guardada en la escena.");
                if (sdk == null || !string.IsNullOrEmpty(sdk.developerToken) ||
                    sdk.Session == null || sdk.PlatformSupport == null || sdk.SceneUpdater == null ||
                    sdk.TrackingAnalyzer == null ||
                    sdk.Localizer.AvailableLocalizationMethods.Length != 1 ||
                    sdk.Localizer.AvailableLocalizationMethods[0] is not DeviceLocalization)
                    errors.Add("SDK: faltan referencias, DeviceLocalization único o hay un token serializado.");
                if (cube == null || cube.gameObject.isStatic || cube.localScale != Vector3.one * 0.5f ||
                    cube.GetComponent<MeshRenderer>()?.sharedMaterial?.shader?.name != "Universal Render Pipeline/Unlit" ||
                    cube.GetComponent<MeshRenderer>().sharedMaterial.color.a < 1f ||
                    map.GetComponent<ImmersalFuentePilotDiagnostics>() == null)
                    errors.Add("Falta el cubo opaco no Static, el material URP o el diagnóstico.");
            }

            if (errors.Count > 0)
                throw new InvalidOperationException("Piloto Immersal inválido:\n" + string.Join("\n", errors));
            Debug.Log("[AncoRA Immersal] Validación de escena, mapa, material y loaders: OK. No verifica localización física.");
        }

        public static void CheckNativeMapInEditor()
        {
            Validate();
            var map = Object.FindAnyObjectByType<XRMap>();
            int handle = Core.LoadMap(map.mapId, map.mapFile.bytes);
            try
            {
                int points = Core.GetPointCloudSize(map.mapId);
                if (handle < 0 || points <= 0)
                    throw new InvalidOperationException($"El plugin no cargó el .bytes 151649: handle={handle}, puntos={points}.");
                Debug.Log($"[AncoRA Immersal] Plugin nativo macOS cargó .bytes 151649: handle={handle}, puntos={points}. Android/iOS pendientes de dispositivo.");
            }
            finally
            {
                Core.FreeMap(map.mapId);
            }
        }

        public static void BuildAndroid()
        {
            ConfigureBuildSettings();
            try
            {
                Validate();
                EditorUserBuildSettings.buildAppBundle = false;
                Build("Builds/Android/ImmersalFuentePilot.apk", BuildTarget.Android);
            }
            finally { ConfigureBuildSettings(); }
        }

        public static void ExportIos()
        {
            ConfigureBuildSettings();
            try
            {
                Validate();
                Build("Builds/iOS_ImmersalFuentePilot", BuildTarget.iOS);
            }
            finally { ConfigureBuildSettings(); }
        }

        static void Build(string path, BuildTarget target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene }, locationPathName = path,
                target = target, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build Immersal {target}: {report.summary.result} ({report.summary.totalErrors} errores).");
            Debug.Log($"[AncoRA Immersal] Build {target} OK: {report.summary.outputPath}. No demuestra precisión en terreno.");
        }
    }
}
#endif
