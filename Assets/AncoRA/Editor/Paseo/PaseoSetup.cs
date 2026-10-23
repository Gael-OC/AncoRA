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
using MultiSetManager = MultiSet.MultisetSdkManager;
using MultiSetLocalizer = MultiSet.SingleFrameLocalizationManager;
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
        // This branch (prueba/multiset) installs beside the normal app without touching its PlayerPrefs: other package id and name.
        const string ApplicationId = "com.ancora.ucnar.paseo.multiset";
        const string ProductName = "AncoRA MultiSet";
        const string AndroidApk = "Builds/Android/AncoRAMultiSet.apk";
        const string BundleVersion = "1.3.4-multiset";
        const string MultiSetRootName = "MultiSet";
        const string MultiSetSdkName = "MultiSet SDK";
        const string MultiSetMapSpaceName = "MultiSet Map Space";
        const string UiFolder = "Assets/AncoRA/Paseo/_Ui";
        const string UiStyleSheet = UiFolder + "/Paseo.uss";
        const string UiTheme = UiFolder + "/PaseoTema.tss";
        const string UiPanelSettings = UiFolder + "/PaseoPanel.asset";
        const string UiFontRegular = UiFolder + "/Fuentes/Inter-Regular.ttf";
        const string UiFontStrong = UiFolder + "/Fuentes/Inter-SemiBold.ttf";
        // Paints the real building over where a replacement stands (PaseoReplacement): one shader, two materials (mark and paint).
        const string MaskShaderPath = "Assets/AncoRA/Paseo/_Materiales/PaseoFondo.shader";
        const string MaskShaderName = "AncoRA/Paseo/Fondo";
        // The yellow boxes: flat colour that ignores the depth buffer, so the ARCore environment depth does not cut them.
        const string BoxShaderPath = "Assets/AncoRA/Paseo/_Materiales/PaseoCaja.shader";
        const string BoxShaderName = "AncoRA/Paseo/Caja";
        // Queues of the two draws of the mask: the mark first, then the paint, both before the models (Geometry, 2000).
        const int MarkRenderQueue = (int)RenderQueue.Geometry - 2;
        const int PaintRenderQueue = (int)RenderQueue.Geometry - 1;
        const string ReplacementLightName = "Luz de los reemplazos";
        static readonly Color ReplacementCubeColor = new(0.95f, 0.45f, 0.1f, 1f);
        static readonly Color ReplacementAmbient = new(0.55f, 0.55f, 0.58f, 1f);
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
            var cameraManager = Object.FindAnyObjectByType<ARCameraManager>() ?? throw new InvalidOperationException("SimpleSample no contiene ARCameraManager.");
            var occlusion = ConfigureOcclusion(cameraManager);

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
            bool anyReplacement = tour.Values.Any(b => b.mapas.Count > 0 && PaseoConfig.HasReplacement(b));
            Material mark = null, mask = null, cube = null;
            if (anyReplacement)
                CreateReplacementMaterials(paths, out mark, out mask, out cube);

            var root = new GameObject("Paseo");
            if (anyReplacement)
                CreateReplacementLight(root.transform);
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
                    ConfigureMap(map, AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath(paths, pair.Key, mapConfig)), mapConfig.id, localization);
                    var content = CreateContent(spaceObject.transform, pair.Key, pair.Value, mapConfig, border, fill, solid);
                    if (PaseoConfig.HasReplacement(pair.Value))
                        content.SetReplacement(CreateReplacement(paths, pair.Key, pair.Value.reemplazo, content, mark, mask, cube));
                    contents.Add(content);
                }
            }
            Object.DestroyImmediate(templateSpace.gameObject);

            var tourComponent = root.AddComponent<PaseoTour>();
            tourComponent.Configure(sdk, localizer, contents.ToArray());
            tourComponent.SetOcclusion(occlusion);
            root.AddComponent<PaseoSessionLog>().Configure(tourComponent);
            var interfaceComponent = CreateInterface(root.transform, tourComponent);
            interfaceComponent.SetMultiSet(CreateMultiSet(paths, tourComponent, cameraManager.GetComponent<Camera>()));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(paths.Scene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Escena creada: {paths.Scene} con {contents.Count} mapa(s) en {contents.Select(c => c.BuildingId).Distinct().Count()} edificio(s), un XR Space por mapa. Nada de esto está medido en terreno.");
        }

        // ARCore's environment depth, so real objects in front of a building hide its replacement (see PaseoReplacement). SimpleSample
        // already carries an AROcclusionManager on the camera, switched off and asking for human segmentation (ARCore has none);
        // this turns it on for the environment depth only. Depth stays optional in the ARCore settings: a phone without the Depth
        // API runs the tour without occlusion. The manager needs the camera background in Any or BeforeOpaques mode: ARCore
        // picks BeforeOpaques, where the background writes the depth before the opaques (ValidateScene checks it).
        static AROcclusionManager ConfigureOcclusion(ARCameraManager cameraManager)
        {
            var manager = cameraManager.GetComponent<AROcclusionManager>() ?? cameraManager.gameObject.AddComponent<AROcclusionManager>();
            var fields = new SerializedObject(manager);
            fields.FindProperty("m_EnvironmentDepthMode").intValue = (int)PaseoTour.OcclusionDepthMode;
            fields.FindProperty("m_EnvironmentDepthTemporalSmoothing").boolValue = true;
            fields.FindProperty("m_OcclusionPreferenceMode").intValue = (int)UnityEngine.XR.ARSubsystems.OcclusionPreferenceMode.PreferEnvironmentOcclusion;
            fields.FindProperty("m_HumanSegmentationStencilMode").intValue = (int)UnityEngine.XR.ARSubsystems.HumanSegmentationStencilMode.Disabled;
            fields.FindProperty("m_HumanSegmentationDepthMode").intValue = (int)UnityEngine.XR.ARSubsystems.HumanSegmentationDepthMode.Disabled;
            fields.ApplyModifiedPropertiesWithoutUndo();
            manager.enabled = true;
            return manager;
        }

        // The MultiSet test beside Immersal (PaseoMultiSetProbe). The SDK lives on an inactive child so a phone without
        // multiset.json runs exactly as before; the probe turns it on when it has the credentials. The credentials stay empty
        // here: they come from multiset.json on the phone. The map space is the object the SDK moves to where its map stands.
        static PaseoMultiSetProbe CreateMultiSet(PaseoPaths paths, PaseoTour tour, Camera arCamera)
        {
            var border = CreateMaterial(paths.MaterialsFolder + "/PaseoMultiSetBorde.mat", new Color(0.1f, 0.9f, 0.95f, 1f), transparent: false);
            var fill = CreateMaterial(paths.MaterialsFolder + "/PaseoMultiSetRelleno.mat", new Color(0.1f, 0.85f, 0.95f, 0.22f), transparent: true);

            var mapSpace = new GameObject(MultiSetMapSpaceName);
            mapSpace.SetActive(false);
            var root = new GameObject(MultiSetRootName);
            var sdkObject = new GameObject(MultiSetSdkName);
            sdkObject.transform.SetParent(root.transform, false);
            sdkObject.SetActive(false);

            var manager = sdkObject.AddComponent<MultiSetManager>();
            manager.clientId = "";
            manager.clientSecret = "";
            manager.runtimeAuthentication = true;
            var managerFields = new SerializedObject(manager);
            managerFields.FindProperty("showAlert").boolValue = false;
            managerFields.ApplyModifiedPropertiesWithoutUndo();

            var localizer = sdkObject.AddComponent<MultiSetLocalizer>();
            localizer.autoLocalize = false;
            localizer.relocalization = false;
            localizer.backgroundLocalization = false;
            localizer.showAlert = false;
            localizer.confidenceCheck = false;
            // Its default is on: the first failure would retry by itself without end, outside the probe's query count.
            localizer.firstLocalizationUntilSuccess = false;
            localizer.passGeoPose = false;
            var localizerFields = new SerializedObject(localizer);
            localizerFields.FindProperty("arCamera").objectReferenceValue = arCamera;
            localizerFields.FindProperty("mapSpace").objectReferenceValue = mapSpace;
            localizerFields.ApplyModifiedPropertiesWithoutUndo();

            var probe = root.AddComponent<PaseoMultiSetProbe>();
            probe.Configure(tour, sdkObject, manager, localizer, mapSpace, border, fill);
            return probe;
        }

        static PaseoUi CreateInterface(Transform parent, PaseoTour tour)
        {
            var session = Object.FindAnyObjectByType<ARSession>() ?? throw new InvalidOperationException("SimpleSample no contiene ARSession.");
            // AR starts after the welcome and the permissions (PaseoUi), so the camera dialog never meets a running session.
            session.enabled = false;
            var uiObject = new GameObject("Interfaz");
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<UIDocument>().panelSettings = LoadOrCreatePanelSettings();
            var ui = uiObject.AddComponent<PaseoUi>();
            ui.Configure(tour, session, Load<StyleSheet>(UiStyleSheet), Load<Font>(UiFontRegular), Load<Font>(UiFontStrong));
            return ui;
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
            // iPhone points as panel units: font sizes and margins read like iOS ones on any phone.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(390, 844);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static void ConfigureMap(XRMap map, TextAsset bytes, int expectedId, PaseoLocalization localization)
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
            // The map frame is the space frame: the box poses in edificio.json rely on it.
            map.transform.localPosition = Vector3.zero;
            map.transform.localRotation = Quaternion.identity;
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

        static PaseoMapContent CreateContent(Transform space, string buildingId, PaseoBuildingConfig building, PaseoMapConfig mapConfig,
            Material border, Material fill, Material solid)
        {
            var boxObject = new GameObject("Caja");
            boxObject.transform.SetParent(space, false);
            var box = boxObject.AddComponent<EdificioFacadeFrame>();
            box.Configure(building.tamano[0], building.tamano[1], building.tamano[2], border, fill, solid, building.solido);

            var content = boxObject.AddComponent<PaseoMapContent>();
            content.Configure(mapConfig.id, buildingId, building.nombre, building.descripcion, box);
            bool hasGps = PaseoConfig.HasGps(building);
            content.SetGps(hasGps, hasGps ? building.gps.latitud : 0.0, hasGps ? building.gps.longitud : 0.0);
            var p = mapConfig.caja.posicion;
            content.SetPose(new Vector3(p[0], p[1], p[2]), mapConfig.caja.giro, mapConfig.caja.colocada);
            return content;
        }

        // The replacement takes the box's place (PaseoMapContent.SetVisible): a mask that paints the building over and
        // the model inside it, a simple shape («forma») when edificio.json names no file.
        static PaseoReplacement CreateReplacement(PaseoPaths paths, string buildingId, PaseoReplacementConfig config, PaseoMapContent content,
            Material mark, Material mask, Material cube)
        {
            var standIn = new GameObject("Reemplazo");
            standIn.transform.SetParent(content.transform, false);
            GameObject model;
            if (string.IsNullOrWhiteSpace(config.modelo))
            {
                model = new GameObject();
                model.transform.SetParent(standIn.transform, false);
                PaseoConfig.TryShape(config.forma, out var type);
                var shapeObject = GameObject.CreatePrimitive(type);
                shapeObject.name = config.forma;
                Object.DestroyImmediate(shapeObject.GetComponent<Collider>());
                shapeObject.GetComponent<MeshRenderer>().sharedMaterial = cube;
                shapeObject.transform.SetParent(model.transform, false);
                // The primitive's pivot is its centre; a model's pivot is its base, which «posicion» places.
                shapeObject.transform.localPosition = new Vector3(0f, PaseoReplacementLayout.ShapeBaseHeight(type), 0f);
            }
            else
            {
                string path = $"{paths.DataRoot}/{buildingId}/{config.modelo}";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException($"Unity no importó {path} como modelo.");
                model = (GameObject)PrefabUtility.InstantiatePrefab(asset, standIn.transform);
            }
            model.name = "Modelo";
            foreach (var meshRenderer in model.GetComponentsInChildren<Renderer>(true))
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var p = config.posicion;
            var replacement = standIn.AddComponent<PaseoReplacement>();
            replacement.Configure(content.Box, mark, mask, config.ocultar, config.margen, config.holgura, model.transform, config.modelo, config.forma,
                config.escala, new Vector3(p[0], p[1], p[2]), config.giro, config.girar);
            return replacement;
        }

        // The mask is two draws of one shader (see PaseoFondo.shader). The slack is per building, so PaseoReplacement sets it on
        // the mark's renderer and these two stay shared by every building with a replacement.
        static void CreateReplacementMaterials(PaseoPaths paths, out Material mark, out Material mask, out Material cube)
        {
            Directory.CreateDirectory(paths.MaterialsFolder);
            var maskShader = LoadShader(MaskShaderPath);
            // Stencil only: no colour, no depth. Both faces, so a camera inside the volume still marks. It passes where the
            // slid box is not behind the depth buffer, which with ARCore occlusion holds the environment depth.
            mark = new Material(maskShader) { name = "PaseoReemplazoMarca" };
            mark.SetFloat("_Cull", (float)CullMode.Off);
            mark.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            mark.SetFloat("_ZWrite", 0f);
            mark.SetFloat("_ColorMask", 0f);
            mark.SetFloat("_StencilComp", (float)CompareFunction.Always);
            mark.SetFloat("_StencilPass", (float)StencilOp.Replace);
            mark.SetFloat("_Holgura", PaseoReplacementConfig.DefaultSlackMeters);
            mark.renderQueue = MarkRenderQueue;
            AssetDatabase.CreateAsset(mark, paths.MaterialsFolder + "/PaseoReemplazoMarca.mat");
            // Paints the inner faces where the mark is, whatever the depth buffer says, and writes their depth over the
            // environment depth of the real building, so the model inside is not hidden by it.
            mask = new Material(maskShader) { name = "PaseoReemplazoFondo" };
            mask.SetFloat("_Cull", (float)CullMode.Front);
            mask.SetFloat("_ZTest", (float)CompareFunction.Always);
            mask.SetFloat("_ZWrite", 1f);
            mask.SetFloat("_StencilComp", (float)CompareFunction.Equal);
            mask.SetFloat("_StencilPass", (float)StencilOp.Keep);
            mask.SetFloat("_Holgura", 0f);
            mask.renderQueue = PaintRenderQueue;
            AssetDatabase.CreateAsset(mask, paths.MaterialsFolder + "/PaseoReemplazoFondo.mat");
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("Falta el shader URP/Lit.");
            cube = new Material(lit) { name = "PaseoReemplazoCubo" };
            cube.SetColor("_BaseColor", ReplacementCubeColor);
            AssetDatabase.CreateAsset(cube, paths.MaterialsFolder + "/PaseoReemplazoCubo.mat");
        }

        static Shader LoadShader(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path) ?? throw new InvalidOperationException($"Falta {path}.");
            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException($"{path} no compila.");
            return shader;
        }

        // The models are lit (the boxes are not): one sun and a flat ambient so their shaded sides do not go black.
        static void CreateReplacementLight(Transform parent)
        {
            var lightObject = new GameObject(ReplacementLightName);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ReplacementAmbient;
        }

        static Material CreateMaterial(string path, Color color, bool transparent)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // URP/Unlit has no ZTest property, and with ARCore occlusion the depth buffer holds the environment depth, which
            // would cut the box wherever the real facade is a little in front of it. AncoRA/Paseo/Caja ignores the depth.
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
            // MultiSet's localizer talks to its cloud; the manifest needs INTERNET even if no other API of the app asks for it.
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.bundleVersion = BundleVersion;
            // The tour does not use occlusion, so Depth must not restrict supported ARCore phones.
            var arCore = UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings();
            arCore.depth = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arCore);
            // One logcat line per message: with stack traces every Debug.Log took 5–10 lines, and MIUI pruned the tour's
            // lines first. Errors keep their trace.
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
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
            var androidApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) || androidApis.Length != 1 || androidApis[0] != GraphicsDeviceType.OpenGLES3)
                errors.Add("Android debe usar solo OpenGLES3 (el SDK de MultiSet lo exige); correr Prepare.");
            if (!PlayerSettings.Android.forceInternetPermission)
                errors.Add("Android debe declarar el permiso de internet (forceInternetPermission): MultiSet consulta su nube; correr Prepare.");
            if (PlayerSettings.bundleVersion != BundleVersion)
                errors.Add($"La versión de la rama debe ser {BundleVersion} (es {PlayerSettings.bundleVersion}); correr Prepare.");
            if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.multiset.sdk")?.version != "1.15.2")
                errors.Add("MultiSet SDK debe ser 1.15.2.");
            if (PlayerSettings.GetStackTraceLogType(LogType.Log) != StackTraceLogType.None ||
                PlayerSettings.GetStackTraceLogType(LogType.Warning) != StackTraceLogType.None)
                errors.Add("Los logs normales y los avisos deben ir sin traza (una línea cada uno en logcat); correr Prepare.");
            if (UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings().depth !=
                UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional)
                errors.Add("ARCore: Depth debe ser opcional; si fuera obligatorio, los teléfonos sin la API de profundidad no podrían instalar la app (la oclusión es un extra).");

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
            ValidateOcclusion(errors);
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
                else if (!FiltersPoses(space))
                    errors.Add($"{label}: su XR Space debe filtrar la pose con su propio PaseoPoseProcessor (ProcessPoses encendido, único procesador).");
                if (map.mapFile == null || AssetDatabase.GetAssetPath(map.mapFile) != BytesPath(paths, building, mapConfig) ||
                    map.LocalizationMethod is not PaseoLocalization || options == null ||
                    options.m_SerializedDataSource != (int)MapDataSource.Embed || options.DownloadVisualizationAtRuntime)
                    errors.Add($"{label}: debe usar su .bytes embebido y PaseoLocalization.");
                if (map.transform.localPosition != Vector3.zero || map.transform.localRotation != Quaternion.identity ||
                    map.transform.localScale != Vector3.one)
                    errors.Add($"{label}: el XR Map debe quedar en identidad dentro de su XR Space.");

                var content = contents.FirstOrDefault(c => c.MapId == mapConfig.id);
                if (content == null || space == null || content.GetComponentInParent<XRSpace>() != space)
                {
                    errors.Add($"{label}: falta su caja dentro de su XR Space.");
                    continue;
                }
                if (content.BuildingId != building || content.BuildingName != config.nombre || content.Description != config.descripcion)
                    errors.Add($"{label}: la caja tiene edificio, nombre o descripción distintos a edificio.json; correr Prepare.");
                bool hasGps = PaseoConfig.HasGps(config);
                if (content.HasGps != hasGps || (hasGps && (Math.Abs(content.Latitude - config.gps.latitud) > 1e-9 ||
                                                            Math.Abs(content.Longitude - config.gps.longitud) > 1e-9)))
                    errors.Add($"{label}: la caja tiene un «gps» distinto a edificio.json; correr Prepare.");
                // edificio.json is the source of truth: a scene left behind by a hand edit must not reach the APK.
                var p = mapConfig.caja.posicion;
                if (content.Box != null && (
                        Vector3.Distance(content.SizeMeters, new Vector3(config.tamano[0], config.tamano[1], config.tamano[2])) > 1e-3f ||
                        content.Box.Solid != config.solido ||
                        Vector3.Distance(content.LocalPosition, new Vector3(p[0], p[1], p[2])) > 1e-3f ||
                        Mathf.Abs(Mathf.DeltaAngle(content.LocalYaw, mapConfig.caja.giro)) > 1e-2f ||
                        content.Placed != mapConfig.caja.colocada))
                    errors.Add($"{label}: la caja de la escena no coincide con edificio.json; correr Prepare.");
                var box = content.Box;
                var renderer = box != null ? box.GetComponent<MeshRenderer>() : null;
                if (box == null || !box.IsBox || !box.HasSolidMaterial || renderer == null || renderer.sharedMaterials.Length != 2 ||
                    renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name != BoxShaderName))
                    errors.Add($"{label}: la caja necesita sus tres materiales con el shader {BoxShaderName} (sin prueba de profundidad: la oclusión no debe cortarla).");
                if (!mapConfig.caja.colocada)
                    warnings.Add($"{label}: caja sin colocar; aparecerá delante de la cámara hasta ajustarla en terreno.");
                ValidateReplacement(label, config.reemplazo, PaseoConfig.HasReplacement(config), content, errors);
            }

            if (tour.Values.Any(b => b.mapas.Count > 0 && PaseoConfig.HasReplacement(b)) &&
                !Object.FindObjectsByType<Light>(all, none).Any(l => l.type == LightType.Directional && l.name == ReplacementLightName))
                errors.Add($"Hay reemplazos y falta la «{ReplacementLightName}» que ilumina sus modelos; correr Prepare.");

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
            ValidateMultiSet(tours, uis, errors);
        }

        // The MultiSet test: one SDK on an inactive child (so a phone without multiset.json runs as before), one probe, one map
        // space at the root, no credentials in the scene, and the localizer set to answer only when the probe asks.
        static void ValidateMultiSet(PaseoTour[] tours, PaseoUi[] uis, List<string> errors)
        {
            var all = FindObjectsInactive.Include;
            var none = FindObjectsSortMode.None;
            var managers = Object.FindObjectsByType<MultiSetManager>(all, none);
            var localizers = Object.FindObjectsByType<MultiSetLocalizer>(all, none);
            var probes = Object.FindObjectsByType<PaseoMultiSetProbe>(all, none);
            if (managers.Length != 1 || localizers.Length != 1 || probes.Length != 1)
            {
                errors.Add($"Debe haber un único MultisetSdkManager, SingleFrameLocalizationManager y PaseoMultiSetProbe (hay {managers.Length}, {localizers.Length} y {probes.Length}); correr Prepare.");
                return;
            }
            var manager = managers[0];
            var localizer = localizers[0];
            var probe = probes[0];
            if (probe.SdkObject == null || probe.SdkObject.activeSelf || probe.SdkObject != manager.gameObject || probe.SdkObject != localizer.gameObject ||
                probe.Manager != manager || probe.Localizer != localizer)
                errors.Add("MultiSet: el SDK (MultisetSdkManager y SingleFrameLocalizationManager) debe estar en un hijo apagado de la sonda, que lo enciende solo con multiset.json; correr Prepare.");
            if (!string.IsNullOrEmpty(manager.clientId) || !string.IsNullOrEmpty(manager.clientSecret) || !manager.runtimeAuthentication)
                errors.Add("MultiSet: la escena no debe llevar credenciales (clientId y clientSecret vacíos, runtimeAuthentication encendido); vienen de multiset.json en el teléfono.");
            if (localizer.autoLocalize || localizer.relocalization || localizer.backgroundLocalization || localizer.showAlert ||
                localizer.confidenceCheck || localizer.firstLocalizationUntilSuccess)
                errors.Add("MultiSet: el localizador solo debe consultar cuando la sonda lo pide (autoLocalize, relocalization, backgroundLocalization, showAlert, confidenceCheck y firstLocalizationUntilSuccess apagados); correr Prepare.");
            var fields = new SerializedObject(localizer);
            var camera = fields.FindProperty("arCamera").objectReferenceValue as Camera;
            var space = fields.FindProperty("mapSpace").objectReferenceValue as GameObject;
            if (camera == null || camera.GetComponent<ARCameraManager>() == null || space == null || space != probe.MapSpace ||
                space.transform.parent != null || space.activeSelf || space.transform.localScale != Vector3.one)
                errors.Add($"MultiSet: el localizador necesita la cámara AR y el «{MultiSetMapSpaceName}» (raíz de la escena, apagado, sin escala) que usa la sonda; correr Prepare.");
            if (tours.Length != 1 || probe.Tour != tours[0])
                errors.Add("MultiSet: la sonda debe apuntar al PaseoTour; correr Prepare.");
            if (uis.Length != 1 || uis[0].MultiSet != probe)
                errors.Add("MultiSet: la interfaz debe apuntar a la sonda para que la hoja del equipo la maneje; correr Prepare.");
            if (probe.BoxBorder == null || probe.BoxFill == null ||
                new[] { probe.BoxBorder, probe.BoxFill }.Any(m => m.shader == null || m.shader.name != BoxShaderName))
                errors.Add($"MultiSet: la caja cian necesita sus dos materiales con el shader {BoxShaderName} (sin prueba de profundidad); correr Prepare.");
        }

        // Exactly one occlusion manager, on the AR camera, asking for ARCore's environment depth with smoothing and no human
        // segmentation (ARCore has none), and the tour pointing at it. The camera background must stay in Any (ARCore picks
        // BeforeOpaques) or BeforeOpaques: only there does the background write the environment depth into the depth buffer
        // before the opaques, which is what the mask's mark tests against. After Opaques would compare against the scene
        // depth in its own shader and ignore the mark.
        static void ValidateOcclusion(List<string> errors)
        {
            var all = FindObjectsInactive.Include;
            var none = FindObjectsSortMode.None;
            var managers = Object.FindObjectsByType<AROcclusionManager>(all, none);
            var cameras = Object.FindObjectsByType<ARCameraManager>(all, none);
            var tours = Object.FindObjectsByType<PaseoTour>(all, none);
            var manager = managers.Length == 1 ? managers[0] : null;
            if (manager == null || cameras.Length != 1 || manager.gameObject != cameras[0].gameObject || !manager.enabled ||
                manager.requestedEnvironmentDepthMode != PaseoTour.OcclusionDepthMode || !manager.environmentDepthTemporalSmoothingRequested ||
                manager.requestedOcclusionPreferenceMode != UnityEngine.XR.ARSubsystems.OcclusionPreferenceMode.PreferEnvironmentOcclusion ||
                manager.requestedHumanStencilMode != UnityEngine.XR.ARSubsystems.HumanSegmentationStencilMode.Disabled ||
                manager.requestedHumanDepthMode != UnityEngine.XR.ARSubsystems.HumanSegmentationDepthMode.Disabled)
                errors.Add($"Debe haber un único AROcclusionManager, en la cámara AR, encendido, con profundidad del entorno {PaseoTour.OcclusionDepthMode}, " +
                           "suavizado temporal, preferencia por el entorno y sin segmentación de personas; correr Prepare.");
            if (tours.Length == 1 && tours[0].Occlusion != manager)
                errors.Add("El PaseoTour debe apuntar al AROcclusionManager de la cámara AR; correr Prepare.");
            if (cameras.Length == 1 && cameras[0].requestedBackgroundRenderingMode == CameraBackgroundRenderingMode.AfterOpaques)
                errors.Add("La cámara AR debe dibujar el fondo en modo Any o BeforeOpaques: en AfterOpaques la oclusión no pasa por el buffer de profundidad y la marca del reemplazo no la vería.");
        }

        static void ValidateReplacement(string label, PaseoReplacementConfig config, bool wanted, PaseoMapContent content, List<string> errors)
        {
            var standIn = content.Replacement;
            if (wanted != (standIn != null))
            {
                errors.Add($"{label}: {(wanted ? "falta" : "sobra")} el reemplazo de edificio.json; correr Prepare.");
                return;
            }
            if (standIn == null)
                return;
            var p = config.posicion;
            var offset = p.Length == 3 ? new Vector3(p[0], p[1], p[2]) : Vector3.positiveInfinity;
            if (standIn.transform.parent != content.transform || standIn.Model == null || !standIn.Model.IsChildOf(standIn.transform) ||
                standIn.HidesBuilding != config.ocultar || !Mathf.Approximately(standIn.MarginMeters, config.margen) ||
                standIn.ModelFile != config.modelo || standIn.Shape != config.forma || !Mathf.Approximately(standIn.ModelScale, config.escala) ||
                !(Vector3.Distance(standIn.ModelOffset, offset) <= 1e-3f) || Mathf.Abs(Mathf.DeltaAngle(standIn.ModelYaw, config.giro)) > 1e-2f ||
                !Mathf.Approximately(standIn.SpinDegreesPerSecond, config.girar) || !Mathf.Approximately(standIn.SlackMeters, config.holgura))
                errors.Add($"{label}: el reemplazo de la escena no coincide con edificio.json; correr Prepare.");
            // The two draws of the mask (PaseoFondo.shader): the mark writes only stencil where the box is not behind the depth
            // buffer, the paint draws the inner faces where the mark is and writes their depth over it.
            if (config.ocultar)
            {
                if (!IsMaskDraw(standIn.MarkMaterial, MarkRenderQueue, CullMode.Off, CompareFunction.LessEqual, zWrite: 0f, colorMask: 0f,
                        CompareFunction.Always, StencilOp.Replace))
                    errors.Add($"{label}: el reemplazo necesita su material de marca (stencil, sin color ni profundidad) con el shader {MaskShaderName}, sin errores; correr Prepare.");
                if (!IsMaskDraw(standIn.MaskMaterial, PaintRenderQueue, CullMode.Front, CompareFunction.Always, zWrite: 1f, colorMask: 15f,
                        CompareFunction.Equal, StencilOp.Keep))
                    errors.Add($"{label}: el reemplazo necesita su material de fondo (pinta donde está la marca) con el shader {MaskShaderName}, sin errores; correr Prepare.");
            }
        }

        static bool IsMaskDraw(Material material, int queue, CullMode cull, CompareFunction zTest, float zWrite, float colorMask,
            CompareFunction stencilComp, StencilOp stencilPass) =>
            material != null && material.shader != null && material.shader.name == MaskShaderName && !ShaderUtil.ShaderHasError(material.shader) &&
            material.renderQueue == queue &&
            Mathf.Approximately(material.GetFloat("_Cull"), (float)cull) && Mathf.Approximately(material.GetFloat("_ZTest"), (float)zTest) &&
            Mathf.Approximately(material.GetFloat("_ZWrite"), zWrite) && Mathf.Approximately(material.GetFloat("_ColorMask"), colorMask) &&
            Mathf.Approximately(material.GetFloat("_StencilComp"), (float)stencilComp) &&
            Mathf.Approximately(material.GetFloat("_StencilPass"), (float)stencilPass);

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
                // The build rewrites the iOS graphics override as automatic; put the committed values back before saving.
                EnsureAndroidSettings();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
