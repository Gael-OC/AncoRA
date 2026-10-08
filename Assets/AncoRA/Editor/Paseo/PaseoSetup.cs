#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace AncorRA.Editor
{
    internal sealed class PaseoPaths
    {
        public string DataRoot = "Assets/AncoRA/Paseo";
        public string Scene = "Assets/Scenes/PaseoIngenieria.unity";
        // Off only for the smoke test, which uses the SDK sample maps.
        public bool RejectSampleIds = true;
        public string MaterialsFolder => DataRoot + "/_Materiales";
        public string AdjustmentFile => DataRoot + "/ajuste-campo.json";
    }

    /// <summary>
    /// Editor automation of the tour. Builds the scene from Assets/AncoRA/Paseo/&lt;Edificio&gt;/edificio.json: one XR
    /// Space per map, each with its XR Map at identity and the building box. edificio.json is the source of truth: Prepare
    /// rebuilds the scene from it every time.
    /// </summary>
    public static class PaseoSetup
    {
        const string Tag = "[AncoRA Paseo]";
        const string SampleScenePath = "Samples~/Core/Scenes/SimpleSample.unity";
        const string ApplicationId = "com.ancora.ucnar.paseo";
        const string ProductName = "AncoRA Paseo";
        const string AndroidApk = "Builds/Android/AncoRAPaseo.apk";
        static readonly PaseoPaths Real = new();

        // ---------------------------------------------------------------- inputs

        [MenuItem("AncoRA/Paseo/Comprobar datos de entrada")]
        public static void CheckInputs() => CheckInputsCore(Real);

        internal static void CheckInputsCore(PaseoPaths paths)
        {
            var tour = LoadTour(paths, out var errors, out var warnings);
            foreach (var pair in tour)
                Debug.Log($"{Tag} {pair.Key} «{pair.Value.nombre}»: {(pair.Value.mapas.Count == 0 ? "sin mapas" : string.Join(", ", pair.Value.mapas.Select(m => $"{m.id} ({(m.caja.colocada ? "caja colocada" : "caja sin colocar")})")))}");
            foreach (string warning in warnings)
                Debug.LogWarning($"{Tag} AVISO: {warning}");
            if (errors.Count > 0)
                throw new InvalidOperationException("Datos del paseo con errores:\n" + string.Join("\n", errors));
            Debug.Log($"{Tag} Datos de entrada OK.");
        }

        internal static SortedDictionary<string, PaseoBuildingConfig> LoadTour(PaseoPaths paths, out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();
            var tour = new SortedDictionary<string, PaseoBuildingConfig>(StringComparer.Ordinal);
            if (!Directory.Exists(paths.DataRoot))
            {
                errors.Add($"No existe {paths.DataRoot}.");
                return tour;
            }
            foreach (string folder in Directory.GetDirectories(paths.DataRoot).OrderBy(d => d, StringComparer.Ordinal))
            {
                string id = Path.GetFileName(folder);
                if (id.StartsWith("_"))
                    continue;
                string file = Path.Combine(folder, PaseoConfig.FileName);
                if (!File.Exists(file))
                {
                    errors.Add($"Edificio {id}: falta {PaseoConfig.FileName}.");
                    continue;
                }
                PaseoBuildingConfig config;
                try
                {
                    config = PaseoConfig.Parse(File.ReadAllText(file));
                }
                catch (FormatException e)
                {
                    errors.Add($"Edificio {id}: {e.Message}");
                    continue;
                }
                var files = Directory.GetFiles(folder).Select(Path.GetFileName).ToList();
                var validation = PaseoConfig.Validate(id, config, files, paths.RejectSampleIds);
                errors.AddRange(validation.Errors);
                warnings.AddRange(validation.Warnings);
                tour[id] = config;
            }
            errors.AddRange(PaseoConfig.ValidateTour(tour));
            if (errors.Count == 0 && tour.Values.All(c => c.mapas.Count == 0))
                errors.Add("Ningún edificio tiene mapas.");
            return tour;
        }

        static string BytesPath(PaseoPaths paths, string buildingId, PaseoMapConfig map) => $"{paths.DataRoot}/{buildingId}/{map.archivo}";

        // ---------------------------------------------------------------- prepare

        [MenuItem("AncoRA/Paseo/Preparar escena del paseo")]
        public static void Prepare() => PrepareCore(Real);

        internal static void PrepareCore(PaseoPaths paths)
        {
            var tour = LoadTour(paths, out var errors, out _);
            if (errors.Count > 0)
                throw new InvalidOperationException("Datos del paseo con errores:\n" + string.Join("\n", errors));
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
            if (package == null || package.version != "2.4.0")
                throw new InvalidOperationException("Se necesita Immersal Core 2.4.0 para copiar SimpleSample.");

            EnsureAndroidSettings();
            AssetDatabase.Refresh();
            foreach (var pair in tour)
                foreach (var map in pair.Value.mapas)
                    if (AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath(paths, pair.Key, map)) == null)
                        throw new InvalidOperationException($"Unity no importó {BytesPath(paths, pair.Key, map)} como TextAsset.");

            // Same base as the earlier pilots: the official SimpleSample copied into a new scene.
            Directory.CreateDirectory(Path.GetDirectoryName(paths.Scene));
            if (File.Exists(paths.Scene))
                AssetDatabase.DeleteAsset(paths.Scene);
            File.Copy(Path.Combine(package.resolvedPath, SampleScenePath), paths.Scene);
            AssetDatabase.ImportAsset(paths.Scene);
            var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            Lightmapping.lightingSettings = null;

            foreach (string unwanted in new[] { "Canvas", "EventSystem", "Directional Light" })
            {
                var go = GameObject.Find(unwanted);
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            var sdk = Object.FindAnyObjectByType<ImmersalSDK>() ?? throw new InvalidOperationException("SimpleSample no contiene ImmersalSDK.");
            PrefabUtility.UnpackPrefabInstance(sdk.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            sdk.developerToken = "";
            var sdkFields = new SerializedObject(sdk);
            // ImmersalLogger.LoggingLevel.Verbose (1): session start, registered maps and each localization reach logcat.
            sdkFields.FindProperty("m_LoggingLevel").intValue = 1;
            sdkFields.ApplyModifiedPropertiesWithoutUndo();
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

            var templateSpace = Object.FindAnyObjectByType<XRSpace>() ?? throw new InvalidOperationException("SimpleSample no contiene XR Space.");
            if (PrefabUtility.IsPartOfPrefabInstance(templateSpace))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(templateSpace), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // Apply each pose directly; the sample smoother starts at the world origin.
            templateSpace.ProcessPoses = false;
            var spaceFields = new SerializedObject(templateSpace);
            spaceFields.FindProperty("m_DataProcessors").arraySize = 0;
            spaceFields.ApplyModifiedPropertiesWithoutUndo();
            foreach (string name in new[] { "PoseFilter", "PoseSmoother" })
            {
                var child = templateSpace.transform.Find(name);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            var templateMap = templateSpace.GetComponentInChildren<XRMap>(true) ?? Object.FindAnyObjectByType<XRMap>()
                              ?? throw new InvalidOperationException("SimpleSample no contiene un XR Map.");
            if (templateMap.Visualization != null)
                templateMap.RemoveVisualization();
            templateMap.transform.SetParent(templateSpace.transform, false);

            var border = CreateMaterial(paths.MaterialsFolder + "/PaseoCajaBorde.mat", new Color(1f, 0.9f, 0.1f, 1f), transparent: false);
            var fill = CreateMaterial(paths.MaterialsFolder + "/PaseoCajaRelleno.mat", new Color(1f, 0.45f, 0.05f, 0.28f), transparent: true);
            var solid = CreateMaterial(paths.MaterialsFolder + "/PaseoCajaSolida.mat", new Color(0.62f, 0.25f, 0.03f, 1f), transparent: false);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("Paseo");
            var contents = new List<PaseoMapContent>();
            foreach (var pair in tour)
            {
                if (pair.Value.mapas.Count == 0)
                    continue;
                var buildingObject = new GameObject(pair.Key);
                buildingObject.transform.SetParent(root.transform, false);
                foreach (var mapConfig in pair.Value.mapas)
                {
                    var spaceObject = Object.Instantiate(templateSpace.gameObject, buildingObject.transform);
                    spaceObject.name = $"XR Space {mapConfig.id}";
                    var map = spaceObject.GetComponentInChildren<XRMap>(true);
                    ConfigureMap(map, AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath(paths, pair.Key, mapConfig)), mapConfig.id, device);
                    contents.Add(CreateContent(spaceObject.transform, pair.Key, pair.Value, mapConfig, border, fill, solid, font));
                }
            }
            Object.DestroyImmediate(templateSpace.gameObject);

            var tourComponent = root.AddComponent<PaseoTour>();
            tourComponent.Configure(sdk, localizer, contents.ToArray());
            var hud = root.AddComponent<PaseoHud>();
            hud.Configure(tourComponent);
            root.AddComponent<PaseoFieldAdjust>().Configure(tourComponent, hud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(paths.Scene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Escena creada: {paths.Scene} con {contents.Count} mapa(s) en {contents.Select(c => c.BuildingId).Distinct().Count()} edificio(s), un XR Space por mapa. Nada de esto está medido en terreno.");
        }

        static void ConfigureMap(XRMap map, TextAsset bytes, int expectedId, DeviceLocalization device)
        {
            // Configure takes id and name from the file name; without -metadata.json the SDK warns and keeps identity alignment.
            map.Configure(bytes);
            map.gameObject.name = $"XR Map {map.mapId}-{map.mapName}";
            map.LocalizationMethod = device;
            map.MapOptions = new List<IMapOption>
            {
                new MapLoadingOption { m_SerializedDataSource = (int)MapDataSource.Embed, DownloadVisualizationAtRuntime = false }
            };
            map.SerializeMapOptions();
            // The map frame is the space frame: the box poses in edificio.json rely on it.
            map.transform.localPosition = Vector3.zero;
            map.transform.localRotation = Quaternion.identity;
            map.transform.localScale = Vector3.one;
            if (map.mapId != expectedId)
                throw new InvalidOperationException($"El XR Map quedó con ID {map.mapId} en vez de {expectedId}.");
        }

        static PaseoMapContent CreateContent(Transform space, string buildingId, PaseoBuildingConfig building, PaseoMapConfig mapConfig,
            Material border, Material fill, Material solid, Font font)
        {
            var boxObject = new GameObject("Caja");
            boxObject.transform.SetParent(space, false);
            var box = boxObject.AddComponent<EdificioFacadeFrame>();
            box.Configure(building.tamano[0], building.tamano[1], building.tamano[2], border, fill, solid, building.solido);

            var labelObject = new GameObject("Nombre");
            labelObject.transform.SetParent(boxObject.transform, false);
            var text = labelObject.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 96;
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            var label = labelObject.AddComponent<PaseoLabel>();

            var content = boxObject.AddComponent<PaseoMapContent>();
            content.Configure(mapConfig.id, buildingId, building.nombre, box, label);
            var p = mapConfig.caja.posicion;
            content.SetPose(new Vector3(p[0], p[1], p[2]), mapConfig.caja.giro, mapConfig.caja.colocada);
            return content;
        }

        static Material CreateMaterial(string path, Color color, bool transparent)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("Falta el shader URP/Unlit.");
            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Cull", 0f); // visible from both sides
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            // CreateAsset replaces an existing asset at the same path.
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureAndroidSettings()
        {
            PlayerSettings.allowUnsafeCode = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            // Saving player settings makes Unity 6000.6 rewrite the iOS graphics override as automatic; re-assert the
            // committed iOS values (Metal, override on) so an Android-only run leaves iOS as it was.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
        }

        // ---------------------------------------------------------------- validate

        [MenuItem("AncoRA/Paseo/Validar escena del paseo")]
        public static void Validate() => ValidateCore(Real);

        internal static void ValidateCore(PaseoPaths paths)
        {
            var tour = LoadTour(paths, out var errors, out var warnings);
            ValidateProjectSettings(errors);
            if (!File.Exists(paths.Scene))
                errors.Add($"Falta la escena {paths.Scene}; correr Prepare.");
            else
                ValidateScene(paths, tour, errors, warnings);
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (enabled.Length != 1 || enabled[0] != paths.Scene)
                errors.Add($"Build Settings debe tener solo {paths.Scene} habilitada.");

            foreach (string warning in warnings)
                Debug.LogWarning($"{Tag} AVISO: {warning}");
            if (errors.Count > 0)
                throw new InvalidOperationException("Escena del paseo inválida:\n" + string.Join("\n", errors));
            Debug.Log($"{Tag} Validación de datos, escena y proyecto: OK. No verifica localización ni alineación física.");
        }

        static void ValidateProjectSettings(List<string> errors)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
            if (package == null || package.version != "2.4.0")
                errors.Add($"Immersal Core debe ser 2.4.0 (actual: {package?.version ?? "ausente"}).");
            if (!PlayerSettings.allowUnsafeCode ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                errors.Add("Android debe usar IL2CPP, ARM64 y código unsafe.");

            var xrAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/XR/XRGeneralSettings.asset");
            if (xrAssets.OfType<XRGeneralSettings>().Count() != 4)
                errors.Add("XRGeneralSettings debe tener exactamente cuatro entradas (sin duplicados de OneDrive).");
            var android = xrAssets.OfType<XRManagerSettings>().Where(m => m.name == "Android Providers").ToArray();
            if (android.Length != 1 || android[0].activeLoaders.Count != 1 || android[0].activeLoaders[0].GetType().Name != "ARCoreLoader")
                errors.Add("XR: Android debe tener solo ARCoreLoader (sin OpenXR).");
        }

        static void ValidateScene(PaseoPaths paths, SortedDictionary<string, PaseoBuildingConfig> tour, List<string> errors, List<string> warnings)
        {
            EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var all = FindObjectsInactive.Include;
            var none = FindObjectsSortMode.None;
            if (Object.FindObjectsByType<ARSession>(all, none).Length != 1 ||
                Object.FindObjectsByType<XROrigin>(all, none).Length != 1 ||
                Object.FindObjectsByType<ARCameraManager>(all, none).Length != 1 ||
                Object.FindObjectsByType<Camera>(all, none).Length != 1)
                errors.Add("Debe haber una sola ARSession, XROrigin y cámara AR.");
            if (Object.FindObjectsByType<ServerLocalization>(all, none).Length != 0 ||
                Object.FindObjectsByType<DeviceLocalization>(all, none).Length != 1)
                errors.Add("Debe haber un único DeviceLocalization y ningún ServerLocalization.");

            var sdks = Object.FindObjectsByType<ImmersalSDK>(all, none);
            if (sdks.Length != 1 || !string.IsNullOrEmpty(sdks[0].developerToken) ||
                sdks[0].Localizer.AvailableLocalizationMethods.Length != 1 ||
                sdks[0].Localizer.AvailableLocalizationMethods[0] is not DeviceLocalization)
                errors.Add("SDK: debe haber uno, sin token serializado y con DeviceLocalization como único método.");

            var maps = Object.FindObjectsByType<XRMap>(all, none);
            var spaces = Object.FindObjectsByType<XRSpace>(all, none);
            var contents = Object.FindObjectsByType<PaseoMapContent>(all, none);
            var expected = tour.SelectMany(pair => pair.Value.mapas.Select(m => (building: pair.Key, config: pair.Value, map: m))).ToList();
            if (maps.Length != expected.Count || spaces.Length != expected.Count || contents.Length != expected.Count)
                errors.Add($"Se esperaban {expected.Count} mapas, XR Spaces y cajas; hay {maps.Length}, {spaces.Length} y {contents.Length}. Correr Prepare.");

            foreach (var (building, config, mapConfig) in expected)
            {
                string label = $"{building}, mapa {mapConfig.id}";
                var map = maps.FirstOrDefault(m => m.mapId == mapConfig.id);
                if (map == null)
                {
                    errors.Add($"{label}: no hay XR Map con ese ID.");
                    continue;
                }
                var space = map.GetComponentInParent<XRSpace>();
                var options = map.MapOptions.OfType<MapLoadingOption>().SingleOrDefault();
                if (space == null || space.GetComponentsInChildren<XRMap>(true).Length != 1)
                    errors.Add($"{label}: el XR Map debe estar solo dentro de su propio XR Space.");
                else if (space.ProcessPoses)
                    errors.Add($"{label}: su XR Space debe aplicar la pose directamente (ProcessPoses apagado).");
                if (map.mapFile == null || AssetDatabase.GetAssetPath(map.mapFile) != BytesPath(paths, building, mapConfig) ||
                    map.LocalizationMethod is not DeviceLocalization || options == null ||
                    options.m_SerializedDataSource != (int)MapDataSource.Embed || options.DownloadVisualizationAtRuntime)
                    errors.Add($"{label}: debe usar su .bytes embebido y DeviceLocalization.");
                if (map.transform.localPosition != Vector3.zero || map.transform.localRotation != Quaternion.identity ||
                    map.transform.localScale != Vector3.one)
                    errors.Add($"{label}: el XR Map debe quedar en identidad dentro de su XR Space.");

                var content = contents.FirstOrDefault(c => c.MapId == mapConfig.id);
                if (content == null || space == null || content.GetComponentInParent<XRSpace>() != space)
                {
                    errors.Add($"{label}: falta su caja dentro de su XR Space.");
                    continue;
                }
                if (content.BuildingId != building || content.BuildingName != config.nombre)
                    errors.Add($"{label}: la caja tiene edificio/nombre distintos a edificio.json.");
                var box = content.Box;
                var renderer = box != null ? box.GetComponent<MeshRenderer>() : null;
                if (box == null || !box.IsBox || !box.HasSolidMaterial || renderer == null || renderer.sharedMaterials.Length != 2 ||
                    renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Unlit"))
                    errors.Add($"{label}: la caja necesita sus tres materiales URP/Unlit.");
                var text = content.Label != null ? content.Label.GetComponent<TextMesh>() : null;
                if (text == null || text.font == null || content.Label.GetComponent<MeshRenderer>().sharedMaterial == null)
                    errors.Add($"{label}: falta el letrero con fuente y material.");
                if (!mapConfig.caja.colocada)
                    warnings.Add($"{label}: caja sin colocar; aparecerá delante de la cámara hasta ajustarla en terreno.");
            }

            var tours = Object.FindObjectsByType<PaseoTour>(all, none);
            if (tours.Length != 1 || tours[0].Sdk == null || tours[0].Localizer == null ||
                tours[0].Contents.Count != contents.Length || tours[0].Contents.Any(c => c == null))
                errors.Add("Debe haber un único PaseoTour con SDK, Localizer y todas las cajas asignadas.");
            var huds = Object.FindObjectsByType<PaseoHud>(all, none);
            var adjusters = Object.FindObjectsByType<PaseoFieldAdjust>(all, none);
            if (huds.Length != 1 || tours.Length != 1 || huds[0].Tour != tours[0])
                errors.Add("Debe haber un único PaseoHud conectado al PaseoTour.");
            if (adjusters.Length != 1 || huds.Length != 1 || adjusters[0].Tour == null || adjusters[0].Hud != huds[0])
                errors.Add("Debe haber un único panel de ajuste conectado al PaseoTour y al PaseoHud.");
        }

        [MenuItem("AncoRA/Paseo/Comprobar carga nativa de los mapas")]
        public static void CheckNativeMapsInEditor() => CheckNativeMapsCore(Real);

        internal static void CheckNativeMapsCore(PaseoPaths paths)
        {
            ValidateCore(paths);
            foreach (var map in Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int handle = Core.LoadMap(map.mapId, map.mapFile.bytes);
                try
                {
                    int points = Core.GetPointCloudSize(map.mapId);
                    if (handle < 0 || points <= 0)
                        throw new InvalidOperationException($"El plugin no cargó el mapa {map.mapId}: handle={handle}, puntos={points}.");
                    Debug.Log($"{Tag} Plugin del Editor cargó el mapa {map.mapId}: {points} puntos. Android pendiente de dispositivo.");
                }
                finally
                {
                    Core.FreeMap(map.mapId);
                }
            }
        }

        // ---------------------------------------------------------------- field adjustment

        [MenuItem("AncoRA/Paseo/Aplicar ajuste de campo desde ajuste-campo.json")]
        public static void ApplyFieldAdjustment() => ApplyFieldAdjustmentCore(Real);

        internal static void ApplyFieldAdjustmentCore(PaseoPaths paths)
        {
            if (!File.Exists(paths.AdjustmentFile))
                throw new FileNotFoundException($"Pegar el JSON de «Copiar valores» en {paths.AdjustmentFile}.", paths.AdjustmentFile);
            PaseoAdjustment adjustment;
            try
            {
                adjustment = JsonUtility.FromJson<PaseoAdjustment>(File.ReadAllText(paths.AdjustmentFile));
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"{paths.AdjustmentFile} no es JSON válido: {e.Message}");
            }
            if (adjustment?.edificios == null || adjustment.edificios.Count == 0)
                throw new InvalidOperationException($"{paths.AdjustmentFile} no trae edificios.");

            var tour = LoadTour(paths, out var errors, out _);
            if (errors.Count > 0)
                throw new InvalidOperationException("Datos del paseo con errores:\n" + string.Join("\n", errors));
            var problems = adjustment.ApplyTo(tour);
            if (problems.Count > 0)
                throw new InvalidOperationException("El ajuste no se aplicó (no se cambió nada):\n" + string.Join("\n", problems));

            foreach (var building in adjustment.edificios)
                File.WriteAllText(Path.Combine(paths.DataRoot, building.id, PaseoConfig.FileName), PaseoConfig.Serialize(tour[building.id]));
            AssetDatabase.Refresh();
            Debug.Log($"{Tag} Ajuste de campo del {adjustment.generado} (build {adjustment.build}) escrito en edificio.json; se rearma la escena.");
            PrepareCore(paths);
        }

        // ---------------------------------------------------------------- build

        [MenuItem("AncoRA/Paseo/Compilar APK de Android")]
        public static void BuildAndroid()
        {
            // Validate first: with broken data or scene this stops here and no APK is produced.
            ValidateCore(Real);
            string originalId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string originalName = PlayerSettings.productName;
            try
            {
                EnsureAndroidSettings();
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
                PlayerSettings.productName = ProductName;
                EditorUserBuildSettings.buildAppBundle = false;
                Directory.CreateDirectory(Path.GetDirectoryName(AndroidApk));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Real.Scene },
                    locationPathName = AndroidApk,
                    target = BuildTarget.Android,
                    // Development: Debug.Log reaches logcat (release builds did not show it).
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Build Android: {report.summary.result} ({report.summary.totalErrors} errores).");
                long bytes = new FileInfo(AndroidApk).Length;
                Debug.Log($"{Tag} APK listo: {AndroidApk} ({bytes / 1048576f:F1} MB en disco). Un build correcto NO demuestra precisión en terreno.");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalId);
                PlayerSettings.productName = originalName;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
