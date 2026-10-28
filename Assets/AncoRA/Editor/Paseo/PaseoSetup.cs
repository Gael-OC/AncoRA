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
using UnityEngine.UIElements;
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
    /// Space per building, with every map of the building inside it (each XR Map at the local transform that puts it in
    /// the building frame, PaseoBuildingFrame) and the building's one box. When every building with maps carries
    /// «campus», it builds a single XR Space instead, with all the maps of all the buildings (each at the local
    /// transform that puts it in the campus frame) and the box of every building. edificio.json is the source of truth:
    /// Prepare rebuilds the scene from it every time.
    /// </summary>
    public static class PaseoSetup
    {
        const string Tag = "[AncoRA Paseo]";
        const string SampleScenePath = "Samples~/Core/Scenes/SimpleSample.unity";
        const string ApplicationId = "com.ancora.ucnar.paseo";
        const string ProductName = "AncoRA Paseo";
        const string AndroidApk = "Builds/Android/AncoRAPaseo.apk";
        const string UiFolder = "Assets/AncoRA/Paseo/_Ui";
        const string UiStyleSheet = UiFolder + "/Paseo.uss";
        const string UiTheme = UiFolder + "/PaseoTema.tss";
        const string UiPanelSettings = UiFolder + "/PaseoPanel.asset";
        const string UiFontRegular = UiFolder + "/Fuentes/Inter-Regular.ttf";
        const string UiFontStrong = UiFolder + "/Fuentes/Inter-SemiBold.ttf";
        // The yellow boxes: flat colour that ignores the depth buffer.
        const string BoxShaderPath = "Assets/AncoRA/Paseo/_Materiales/PaseoCaja.shader";
        const string BoxShaderName = "AncoRA/Paseo/Caja";
        // The SDK tries back to back only for its startup burst (15 s or 10 successes in all), then every 2 s: at the
        // second building, with a map that fails most tries, the two agreeing localizations the filter needs took long.
        // Starting value of the session interval; PaseoTour then follows PaseoPace (fast while searching, slow on a settled building).
        internal const float LocalizationIntervalSeconds = PaseoPace.FastSeconds;
        static readonly PaseoPaths Real = new();

        // ---------------------------------------------------------------- inputs

        [MenuItem("AncoRA/Paseo/Comprobar datos de entrada")]
        public static void CheckInputs() => CheckInputsCore(Real);

        internal static void CheckInputsCore(PaseoPaths paths)
        {
            var tour = LoadTour(paths, out var errors, out var warnings);
            foreach (var pair in tour)
                Debug.Log($"{Tag} {pair.Key} «{pair.Value.nombre}»{(PaseoConfig.HasCampus(pair.Value) ? " (campus)" : "")}: {(pair.Value.mapas.Count == 0 ? "sin mapas" : string.Join(", ", pair.Value.mapas.Select(m => $"{m.id} ({(m.caja.colocada ? "caja colocada" : "caja sin colocar")})")))}");
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
            // Keep the scene's .meta/GUID when rebuilding it for additional maps.
            File.Copy(Path.Combine(package.resolvedPath, SampleScenePath), paths.Scene, overwrite: true);
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
            var device = sdk.GetComponentInChildren<DeviceLocalization>(true) ?? throw new InvalidOperationException("SimpleSample no contiene DeviceLocalization.");
            var server = sdk.GetComponentInChildren<ServerLocalization>(true);
            if (server != null)
                Object.DestroyImmediate(server.gameObject);
            // The tour's own method takes DeviceLocalization's place: it searches one building per attempt instead of
            // every map (PaseoLocalization).
            var methodObject = device.gameObject;
            Object.DestroyImmediate(device);
            methodObject.name = "PaseoLocalization";
            var localization = methodObject.AddComponent<PaseoLocalization>();
            var methods = new SerializedObject(localizer).FindProperty("m_LocalizationMethodObjects");
            methods.arraySize = 1;
            methods.GetArrayElementAtIndex(0).objectReferenceValue = localization;
            methods.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            var session = Object.FindAnyObjectByType<ImmersalSession>() ?? throw new InvalidOperationException("SimpleSample no contiene ImmersalSession.");
            var sessionFields = new SerializedObject(session);
            sessionFields.FindProperty("m_SessionUpdateInterval").floatValue = LocalizationIntervalSeconds;
            sessionFields.ApplyModifiedPropertiesWithoutUndo();

            var origin = Object.FindAnyObjectByType<XROrigin>();
            origin.CameraYOffset = 0f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            var templateSpace = Object.FindAnyObjectByType<XRSpace>() ?? throw new InvalidOperationException("SimpleSample no contiene XR Space.");
            if (PrefabUtility.IsPartOfPrefabInstance(templateSpace))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(templateSpace), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // Every localization goes through PaseoPoseFilter (shown at the first one, confirmed by a second that agrees,
            // outliers dropped, average, slide). The sample's own filter and smoother go: the smoother starts at the world origin. Instantiate
            // remaps the processor reference, so each space keeps its own.
            templateSpace.ProcessPoses = true;
            var poseProcessor = templateSpace.gameObject.AddComponent<PaseoPoseProcessor>();
            var spaceFields = new SerializedObject(templateSpace);
            var processors = spaceFields.FindProperty("m_DataProcessors");
            processors.arraySize = 1;
            processors.GetArrayElementAtIndex(0).objectReferenceValue = poseProcessor;
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

            var root = new GameObject("Paseo");
            var contents = new List<PaseoMapContent>();
            int mapCount = 0;
            int spaceCount = 0;
            var withMaps = tour.Where(pair => pair.Value.mapas.Count > 0).ToList();
            bool campus = PaseoConfig.IsCampusTour(withMaps.Select(pair => pair.Value));
            if (campus)
            {
                // Campus mode: one XR Space for the whole tour. Every map sits in it at the transform that puts it in the
                // campus frame (the maps stay direct children: the SDK reads their local transform against the space), and
                // every building box is a child of it at its campus pose.
                var campusObject = new GameObject("Campus");
                campusObject.transform.SetParent(root.transform, false);
                var spaceObject = Object.Instantiate(templateSpace.gameObject, campusObject.transform);
                spaceObject.name = "XR Space Campus";
                var mapObjects = new List<XRMap> { spaceObject.GetComponentInChildren<XRMap>(true) };
                int totalMaps = withMaps.Sum(pair => pair.Value.mapas.Count);
                for (int i = 1; i < totalMaps; i++)
                    mapObjects.Add(Object.Instantiate(mapObjects[0].gameObject, spaceObject.transform).GetComponent<XRMap>());
                foreach (var pair in withMaps)
                {
                    var relations = PaseoBuildingFrame.CampusMapTransforms(pair.Value);
                    for (int i = 0; i < pair.Value.mapas.Count; i++)
                    {
                        var mapConfig = pair.Value.mapas[i];
                        ConfigureMap(mapObjects[mapCount], AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath(paths, pair.Key, mapConfig)), mapConfig.id,
                            localization, relations[i]);
                        mapCount++;
                    }
                    contents.Add(CreateContent(spaceObject.transform, pair.Key, pair.Value, border, fill, solid, campus: true));
                }
                spaceCount = 1;
            }
            else
            {
                foreach (var pair in withMaps)
                {
                    var buildingObject = new GameObject(pair.Key);
                    buildingObject.transform.SetParent(root.transform, false);
                    // One space per building; the instance brings one XR Map and the others are copies of it.
                    var spaceObject = Object.Instantiate(templateSpace.gameObject, buildingObject.transform);
                    spaceObject.name = $"XR Space {pair.Key}";
                    var firstMap = spaceObject.GetComponentInChildren<XRMap>(true);
                    var mapObjects = new List<XRMap> { firstMap };
                    for (int i = 1; i < pair.Value.mapas.Count; i++)
                        mapObjects.Add(Object.Instantiate(firstMap.gameObject, spaceObject.transform).GetComponent<XRMap>());
                    var relations = PaseoBuildingFrame.MapTransforms(pair.Value);
                    for (int i = 0; i < pair.Value.mapas.Count; i++)
                    {
                        var mapConfig = pair.Value.mapas[i];
                        ConfigureMap(mapObjects[i], AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath(paths, pair.Key, mapConfig)), mapConfig.id,
                            localization, relations[i]);
                        mapCount++;
                    }
                    contents.Add(CreateContent(spaceObject.transform, pair.Key, pair.Value, border, fill, solid, campus: false));
                    spaceCount++;
                }
            }
            Object.DestroyImmediate(templateSpace.gameObject);

            var tourComponent = root.AddComponent<PaseoTour>();
            tourComponent.Configure(sdk, localizer, contents.ToArray());
            root.AddComponent<PaseoSessionLog>().Configure(tourComponent);
            CreateInterface(root.transform, tourComponent);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(paths.Scene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Escena creada: {paths.Scene} con {mapCount} mapa(s) en {contents.Count} edificio(s), " +
                      (campus ? "modo campus: un solo XR Space para todos los edificios" : $"{spaceCount} XR Space(s), uno por edificio") +
                      ". Nada de esto está medido en terreno.");
        }

        static void CreateInterface(Transform parent, PaseoTour tour)
        {
            var session = Object.FindAnyObjectByType<ARSession>() ?? throw new InvalidOperationException("SimpleSample no contiene ARSession.");
            // AR starts after the welcome and the permissions (PaseoUi), so the camera dialog never meets a running session.
            session.enabled = false;
            var uiObject = new GameObject("Interfaz");
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<UIDocument>().panelSettings = LoadOrCreatePanelSettings();
            uiObject.AddComponent<PaseoUi>().Configure(tour, session, Load<StyleSheet>(UiStyleSheet), Load<Font>(UiFontRegular), Load<Font>(UiFontStrong));
        }

        static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Falta {path}.");

        static PanelSettings LoadOrCreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPanelSettings);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, UiPanelSettings);
            }
            settings.themeStyleSheet = Load<ThemeStyleSheet>(UiTheme);
            // A 390 × 844 reference: font sizes and margins read the same on any phone.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(390, 844);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static void ConfigureMap(XRMap map, TextAsset bytes, int expectedId, PaseoLocalization localization, Matrix4x4 relation)
        {
            // Configure takes id and name from the file name; without -metadata.json the SDK warns and keeps identity alignment.
            map.Configure(bytes);
            map.gameObject.name = $"XR Map {map.mapId}-{map.mapName}";
            map.LocalizationMethod = localization;
            map.MapOptions = new List<IMapOption>
            {
                new MapLoadingOption { m_SerializedDataSource = (int)MapDataSource.Embed, DownloadVisualizationAtRuntime = false }
            };
            map.SerializeMapOptions();
            // The local transform puts the map in the building frame (the SDK reads it once, when it registers the map, as
            // the map→space relation); the reference map sits at identity. Nothing else may touch it: XRMap.ApplyAlignment
            // would reset it, and it only runs from the Editor inspector and from maps created at runtime.
            PaseoBuildingFrame.Decompose(relation, out var relationPosition, out float relationYaw);
            map.transform.localPosition = relationPosition;
            map.transform.localRotation = Quaternion.Euler(0f, relationYaw, 0f);
            map.transform.localScale = Vector3.one;
            if (map.mapId != expectedId)
                throw new InvalidOperationException($"El XR Map quedó con ID {map.mapId} en vez de {expectedId}.");
        }

        static bool FiltersPoses(XRSpace space)
        {
            var processors = new SerializedObject(space).FindProperty("m_DataProcessors");
            return space.ProcessPoses && processors.arraySize == 1 &&
                   processors.GetArrayElementAtIndex(0).objectReferenceValue is PaseoPoseProcessor processor &&
                   processor.gameObject == space.gameObject;
        }

        static PaseoMapContent CreateContent(Transform space, string buildingId, PaseoBuildingConfig building,
            Material border, Material fill, Material solid, bool campus)
        {
            // Campus mode puts the boxes of all the buildings under one space.
            var boxObject = new GameObject(campus ? $"Caja {buildingId}" : "Caja");
            boxObject.transform.SetParent(space, false);
            var box = boxObject.AddComponent<EdificioFacadeFrame>();
            box.Configure(building.tamano[0], building.tamano[1], building.tamano[2], border, fill, solid, building.solido);

            var content = boxObject.AddComponent<PaseoMapContent>();
            // No reference map in campus mode (0): the box pose is in the campus frame, not in the frame of any map.
            content.Configure(building.mapas.Select(m => m.id).ToArray(), campus ? 0 : PaseoBuildingFrame.ReferenceMapId(building), buildingId,
                building.nombre, building.descripcion, box);
            bool hasGps = PaseoConfig.HasGps(building);
            content.SetGps(hasGps, hasGps ? building.gps.latitud : 0.0, hasGps ? building.gps.longitud : 0.0);
            if (campus)
            {
                // Every map of a campus building has its box placed (PaseoConfig.Validate), so the campus pose counts as placed.
                PaseoBuildingFrame.CampusBox(building, out var campusPosition, out float campusYaw);
                content.SetPose(campusPosition, campusYaw, placed: true);
                return content;
            }
            // The box of the reference map, which is the box in the building frame.
            PaseoBuildingFrame.ReferenceBox(building, out var position, out float yaw, out bool placed);
            content.SetPose(position, yaw, placed);
            return content;
        }

        static Shader LoadShader(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path) ?? throw new InvalidOperationException($"Falta {path}.");
            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException($"{path} no compila.");
            return shader;
        }

        static Material CreateMaterial(string path, Color color, bool transparent)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // URP/Unlit has no ZTest property; AncoRA/Paseo/Caja is a flat colour that ignores the depth buffer.
            var material = new Material(LoadShader(BoxShaderPath)) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Cull", (float)CullMode.Off); // visible from both sides
            if (transparent)
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
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
            // Depth must not restrict which ARCore phones can install the app (the tour does not ask for depth).
            var arCore = UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings();
            arCore.depth = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arCore);
            // One logcat line per message: with stack traces every Debug.Log took 5–10 lines, and MIUI pruned the tour's
            // lines first. Errors keep their trace.
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
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
            if (PlayerSettings.GetStackTraceLogType(LogType.Log) != StackTraceLogType.None ||
                PlayerSettings.GetStackTraceLogType(LogType.Warning) != StackTraceLogType.None)
                errors.Add("Los logs normales y los avisos deben ir sin traza (una línea cada uno en logcat); correr Prepare.");
            if (UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings().depth !=
                UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional)
                errors.Add("ARCore: Depth debe ser opcional; si fuera obligatorio, los teléfonos sin la API de profundidad no podrían instalar la app (la app no usa la profundidad).");

            var xrAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/XR/XRGeneralSettings.asset");
            // Standalone, Android and WebGL (iPhone was dropped); more means OneDrive duplicated objects.
            if (xrAssets.OfType<XRGeneralSettings>().Count() != 3)
                errors.Add("XRGeneralSettings debe tener exactamente tres entradas: Standalone, Android y WebGL (sin duplicados de OneDrive).");
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
                Object.FindObjectsByType<DeviceLocalization>(all, none).Length != 0 ||
                Object.FindObjectsByType<PaseoLocalization>(all, none).Length != 1)
                errors.Add("Debe haber un único PaseoLocalization (búsqueda por edificio), sin DeviceLocalization ni ServerLocalization; correr Prepare.");

            var sessions = Object.FindObjectsByType<ImmersalSession>(all, none);
            if (sessions.Length != 1 || !Mathf.Approximately(sessions[0].SessionUpdateInterval, LocalizationIntervalSeconds))
                errors.Add($"Immersal: debe haber una sesión que intente ubicarse cada {LocalizationIntervalSeconds} s; correr Prepare.");

            var sdks = Object.FindObjectsByType<ImmersalSDK>(all, none);
            if (sdks.Length != 1 || !string.IsNullOrEmpty(sdks[0].developerToken) ||
                sdks[0].Localizer.AvailableLocalizationMethods.Length != 1 ||
                sdks[0].Localizer.AvailableLocalizationMethods[0] is not PaseoLocalization)
                errors.Add("SDK: debe haber uno, sin token serializado y con PaseoLocalization como único método.");

            var maps = Object.FindObjectsByType<XRMap>(all, none);
            var spaces = Object.FindObjectsByType<XRSpace>(all, none);
            var contents = Object.FindObjectsByType<PaseoMapContent>(all, none);
            var expected = tour.Where(pair => pair.Value.mapas.Count > 0).ToList();
            int expectedMaps = expected.Sum(pair => pair.Value.mapas.Count);
            // Campus mode: every building with maps carries «campus», and the whole tour is one XR Space.
            bool campus = PaseoConfig.IsCampusTour(expected.Select(pair => pair.Value));
            int expectedSpaces = campus ? 1 : expected.Count;
            if (maps.Length != expectedMaps || spaces.Length != expectedSpaces || contents.Length != expected.Count)
                errors.Add($"Se esperaban {expectedMaps} mapas en {expectedSpaces} XR Space{(campus ? " (modo campus: uno solo para todos los edificios)" : "s (uno por edificio)")} y {expected.Count} cajas; hay {maps.Length}, {spaces.Length} y {contents.Length}. Correr Prepare.");

            XRSpace campusSpace = null;
            foreach (var pair in expected)
            {
                string building = pair.Key;
                var config = pair.Value;
                string label = $"Edificio {building}";
                var content = contents.FirstOrDefault(c => c.BuildingId == building);
                var space = content != null ? content.GetComponentInParent<XRSpace>() : null;
                if (content == null || space == null)
                {
                    errors.Add($"{label}: falta su caja dentro de su XR Space.");
                    continue;
                }
                if (campus)
                {
                    // The space-wide checks (its maps, its filter) run once, below.
                    if (campusSpace == null)
                        campusSpace = space;
                    else if (space != campusSpace)
                        errors.Add($"{label}: su caja debe estar en el XR Space del campus, el mismo de los demás edificios. Correr Prepare.");
                }
                else
                {
                    var inSpace = space.GetComponentsInChildren<XRMap>(true).Select(m => m.mapId).OrderBy(id => id).ToArray();
                    if (!inSpace.SequenceEqual(config.mapas.Select(m => m.id).OrderBy(id => id)))
                        errors.Add($"{label}: su XR Space debe contener exactamente sus mapas ({string.Join(", ", config.mapas.Select(m => m.id))}); tiene [{string.Join(", ", inSpace)}]. Correr Prepare.");
                    if (!FiltersPoses(space))
                        errors.Add($"{label}: su XR Space debe filtrar la pose con su propio PaseoPoseProcessor (ProcessPoses encendido, único procesador).");
                }

                // Every map sits in the building frame: its local transform is T = B_R · B_M⁻¹ (identity for the reference);
                // in campus mode, in the campus frame: T = B_C · B_M⁻¹ for every map.
                var relations = campus ? PaseoBuildingFrame.CampusMapTransforms(config) : PaseoBuildingFrame.MapTransforms(config);
                for (int i = 0; i < config.mapas.Count; i++)
                {
                    var mapConfig = config.mapas[i];
                    string mapLabel = $"{label}, mapa {mapConfig.id}";
                    var map = maps.FirstOrDefault(m => m.mapId == mapConfig.id);
                    if (map == null)
                    {
                        errors.Add($"{mapLabel}: no hay XR Map con ese ID.");
                        continue;
                    }
                    var options = map.MapOptions.OfType<MapLoadingOption>().SingleOrDefault();
                    if (map.GetComponentInParent<XRSpace>() != space)
                        errors.Add($"{mapLabel}: el XR Map debe estar dentro del XR Space {(campus ? "del campus" : "de su edificio")}.");
                    if (map.mapFile == null || AssetDatabase.GetAssetPath(map.mapFile) != BytesPath(paths, building, mapConfig) ||
                        map.LocalizationMethod is not PaseoLocalization || options == null ||
                        options.m_SerializedDataSource != (int)MapDataSource.Embed || options.DownloadVisualizationAtRuntime)
                        errors.Add($"{mapLabel}: debe usar su .bytes embebido y PaseoLocalization.");
                    PaseoBuildingFrame.Decompose(relations[i], out var relationPosition, out float relationYaw);
                    if (Vector3.Distance(map.transform.localPosition, relationPosition) > 1e-3f ||
                        !IsTurnAroundVertical(map.transform.localEulerAngles, relationYaw) ||
                        map.transform.localScale != Vector3.one)
                        errors.Add($"{mapLabel}: el XR Map no está donde lo deja edificio.json dentro del marco {(campus ? "del campus" : "del edificio")} (posición {relationPosition}, giro {relationYaw:F2}°); correr Prepare.");
                }

                if (content.BuildingName != config.nombre || content.Description != config.descripcion)
                    errors.Add($"{label}: la caja tiene nombre o descripción distintos a edificio.json; correr Prepare.");
                // The campus has no reference map: its boxes carry 0.
                int expectedReference = campus ? 0 : PaseoBuildingFrame.ReferenceMapId(config);
                if (!content.MapIds.SequenceEqual(config.mapas.Select(m => m.id)) || content.ReferenceMapId != expectedReference)
                    errors.Add($"{label}: la caja tiene mapas o mapa de referencia distintos a edificio.json; correr Prepare.");
                bool hasGps = PaseoConfig.HasGps(config);
                if (content.HasGps != hasGps || (hasGps && (Math.Abs(content.Latitude - config.gps.latitud) > 1e-9 ||
                                                            Math.Abs(content.Longitude - config.gps.longitud) > 1e-9)))
                    errors.Add($"{label}: la caja tiene un «gps» distinto a edificio.json; correr Prepare.");
                // edificio.json is the source of truth: a scene left behind by a hand edit must not reach the APK.
                Vector3 boxPosition;
                float boxYaw;
                bool boxPlaced;
                if (campus)
                {
                    // The box pose is the campus pose, and counts as placed.
                    PaseoBuildingFrame.CampusBox(config, out boxPosition, out boxYaw);
                    boxPlaced = true;
                }
                else
                    PaseoBuildingFrame.ReferenceBox(config, out boxPosition, out boxYaw, out boxPlaced);
                if (content.Box != null && (
                        Vector3.Distance(content.SizeMeters, new Vector3(config.tamano[0], config.tamano[1], config.tamano[2])) > 1e-3f ||
                        content.Box.Solid != config.solido ||
                        Vector3.Distance(content.LocalPosition, boxPosition) > 1e-3f ||
                        Mathf.Abs(Mathf.DeltaAngle(content.LocalYaw, boxYaw)) > 1e-2f ||
                        content.Placed != boxPlaced))
                    errors.Add($"{label}: la caja de la escena no coincide con edificio.json; correr Prepare.");
                var box = content.Box;
                var renderer = box != null ? box.GetComponent<MeshRenderer>() : null;
                if (box == null || !box.IsBox || !box.HasSolidMaterial || renderer == null || renderer.sharedMaterials.Length != 2 ||
                    renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name != BoxShaderName))
                    errors.Add($"{label}: la caja necesita sus tres materiales con el shader {BoxShaderName} (sin prueba de profundidad).");
                if (!boxPlaced)
                    warnings.Add($"{label}: caja sin colocar; aparecerá delante de la cámara hasta ajustarla en terreno.");
            }

            if (campus && campusSpace != null)
            {
                var inCampus = campusSpace.GetComponentsInChildren<XRMap>(true).Select(m => m.mapId).OrderBy(id => id).ToArray();
                var allIds = expected.SelectMany(pair => pair.Value.mapas.Select(m => m.id)).OrderBy(id => id).ToArray();
                if (!inCampus.SequenceEqual(allIds))
                    errors.Add($"Campus: su XR Space debe contener los mapas de todos los edificios ({string.Join(", ", allIds)}); tiene [{string.Join(", ", inCampus)}]. Correr Prepare.");
                if (!FiltersPoses(campusSpace))
                    errors.Add("Campus: su XR Space debe filtrar la pose con su PaseoPoseProcessor (ProcessPoses encendido, único procesador).");
                if (Object.FindObjectsByType<PaseoPoseProcessor>(all, none).Length != 1)
                    errors.Add("Campus: debe haber un único PaseoPoseProcessor en la escena, el del XR Space del campus. Correr Prepare.");
            }

            var tours = Object.FindObjectsByType<PaseoTour>(all, none);
            if (tours.Length != 1 || tours[0].Sdk == null || tours[0].Localizer == null ||
                tours[0].Contents.Count != contents.Length || tours[0].Contents.Any(c => c == null))
                errors.Add("Debe haber un único PaseoTour con SDK, Localizer y todas las cajas asignadas.");
            var logs = Object.FindObjectsByType<PaseoSessionLog>(all, none);
            if (logs.Length != 1 || tours.Length != 1 || logs[0].Tour != tours[0])
                errors.Add("Debe haber un único registro de sesión (PaseoSessionLog) conectado al PaseoTour; correr Prepare.");
            var uis = Object.FindObjectsByType<PaseoUi>(all, none);
            var arSessions = Object.FindObjectsByType<ARSession>(all, none);
            var document = uis.Length == 1 ? uis[0].GetComponent<UIDocument>() : null;
            if (uis.Length != 1 || tours.Length != 1 || uis[0].Tour != tours[0] || arSessions.Length != 1 ||
                uis[0].ArSession != arSessions[0] || uis[0].StyleSheet == null || uis[0].RegularFont == null ||
                uis[0].StrongFont == null || document == null || document.panelSettings == null ||
                document.panelSettings.themeStyleSheet == null)
                errors.Add("Debe haber una única interfaz (PaseoUi + UIDocument con PanelSettings y tema) conectada al PaseoTour, la ARSession, el estilo y las fuentes; correr Prepare.");
            if (arSessions.Length == 1 && arSessions[0].enabled)
                errors.Add("La ARSession debe quedar apagada en la escena: la enciende la bienvenida después de los permisos; correr Prepare.");
            if (Object.FindObjectsByType<TextMesh>(all, none).Length != 0)
                errors.Add("Quedan textos 3D (TextMesh) en la escena: los nombres van en la interfaz; correr Prepare.");
        }

        // Euler angles, not Quaternion.Angle: its float acos cannot resolve 0.01°.
        static bool IsTurnAroundVertical(Vector3 euler, float yaw) =>
            Mathf.Abs(Mathf.DeltaAngle(euler.y, yaw)) <= 1e-2f && Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)) <= 1e-2f &&
            Mathf.Abs(Mathf.DeltaAngle(euler.z, 0f)) <= 1e-2f;

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
