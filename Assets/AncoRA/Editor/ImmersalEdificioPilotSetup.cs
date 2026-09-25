#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// <summary>
    /// Editor automation for the building-facade pilot (two Immersal maps A and B under one XR Space).
    /// It never invents maps: without the real .bytes/metadata/PLY of both maps and the measured frame size,
    /// Prepare, Validate and the builds stop and list exactly what is missing. The Fuente pilot scene and
    /// its validator are untouched.
    /// </summary>
    public static class ImmersalEdificioPilotSetup
    {
        internal sealed class Paths
        {
            public string Scene = "Assets/Scenes/ImmersalEdificioPilot.unity";
            public string DataFolder = "Assets/AncoRA/ImmersalEdificio";
            // Maps known not to belong to the building: the Fuente pilot and the SDK sample maps.
            public bool RejectNonBuildingIds = true;
            // A single-map scene (Teologia demo) has one map in <DataFolder>/Mapa and no map B.
            public bool SingleMap;
            // Teologia demo: status banner and team HUD (with the adjust bar) are on from the first frame.
            public bool ShowStatusBanner;
            public bool HudVisibleAtStart;
            // Teologia demo with two maps: adds the map selector (menu, per-map box, derived alignment for "both").
            public bool MapSelector;
            // Keep the content visible after the first localization while the phone keeps tracking.
            public bool KeepVisible;
            // Development player, so Debug.Log reaches logcat.
            public bool DevelopmentBuild;
            public string MeasurementsName = "edificio-medidas.json";
            public string BuildIdSuffix = BuildIdSuffixEdificio;
            public string ProductSuffix = " Edificio";
            public string MapFolderA => DataFolder + (SingleMap ? "/Mapa" : "/MapaA");
            public string MapFolderB => DataFolder + "/MapaB";
            public string MeasurementsFile => DataFolder + "/" + MeasurementsName;
            public string BorderMaterial => DataFolder + "/EdificioMarcoBorde.mat";
            public string FillMaterial => DataFolder + "/EdificioMarcoRelleno.mat";
            public string SolidMaterial => DataFolder + "/EdificioCajaSolida.mat";
        }

        [Serializable]
        sealed class Measurements
        {
            public float frameWidthMeters;
            public float frameHeightMeters;
            // 0 (or absent) = flat frame; greater than 0 = 3D box that stands in for the building.
            public float frameDepthMeters;
            // Optional starting placement under XR Space (Unity-local metres and yaw); not a verified alignment.
            public float[] framePosition;
            public float frameYawDegrees;
            // Same, in the frame of map 2 (map selector scenes only): a pose only means something inside its own map.
            public float[] framePositionB;
            public float frameYawDegreesB;
            public string note;
            public bool estimated;
        }

        sealed class MapInput
        {
            public string Label;
            public string Folder;
            public int Id;
            public string Name;
            public string BaseName;
            public string BytesPath;
            public string MetadataPath;
            public string PlyPath;
        }

        static readonly Paths Real = new Paths();
        static readonly HashSet<int> NonBuildingMapIds = new HashSet<int> { 151649, 90687, 90688, 90689, 90690 };
        const string SampleScenePath = "Samples~/Core/Scenes/SimpleSample.unity";
        const string BuildIdSuffixEdificio = ".edificio";
        const string AndroidApk = "Builds/Android/ImmersalEdificioPilot.apk";
        const string IosFolder = "Builds/iOS_ImmersalEdificioPilot";

        // ---------------------------------------------------------------- inputs

        [MenuItem("AncoRA/Immersal/Edificio/Comprobar datos de entrada")]
        public static void CheckInputs() => CheckInputsCore(Real);

        internal static void CheckInputsCore(Paths paths)
        {
            var missing = FindInputProblems(paths, out _, out _, out _);
            if (missing.Count > 0)
                throw new InvalidOperationException(MissingMessage(paths, missing));
            Debug.Log(paths.SingleMap
                ? "[AncoRA Edificio] Datos de entrada completos (mapa único con .bytes, metadata y PLY, más medidas de la caja)."
                : "[AncoRA Edificio] Datos de entrada completos (mapas A y B con .bytes, metadata y PLY, más medidas del marco).");
        }

        static string MissingMessage(Paths paths, List<string> problems) =>
            "FALTAN DATOS REALES DEL EDIFICIO; la integración se detiene aquí. El equipo debe entregar:\n - " +
            string.Join("\n - ", problems) +
            $"\nRutas esperadas: {paths.MapFolderA}/<id>-<nombre>.bytes (+ -metadata.json y -sparse.ply), " +
            (paths.SingleMap ? "" : $"{paths.MapFolderB}/... ") + $"y {paths.MeasurementsFile}. Ver EDIFICIO_GUIA_CAPTURA.md.";

        static List<string> FindInputProblems(Paths paths, out MapInput a, out MapInput b, out Measurements measurements)
        {
            var problems = new List<string>();
            a = ReadMapInput("A", paths.MapFolderA, paths, problems);
            b = paths.SingleMap ? null : ReadMapInput("B", paths.MapFolderB, paths, problems);
            if (a != null && b != null && a.Id == b.Id)
                problems.Add($"Los mapas A y B tienen el mismo ID ({a.Id}); deben ser dos mapas distintos.");

            measurements = null;
            if (!File.Exists(paths.MeasurementsFile))
                problems.Add($"Falta {paths.MeasurementsFile} con frameWidthMeters y frameHeightMeters medidos en la fachada.");
            else
            {
                try
                {
                    measurements = JsonUtility.FromJson<Measurements>(File.ReadAllText(paths.MeasurementsFile));
                }
                catch (Exception e)
                {
                    problems.Add($"{paths.MeasurementsFile} no es un JSON válido: {e.Message}");
                }
                if (measurements != null && (!(measurements.frameWidthMeters > 0.1f) || !(measurements.frameHeightMeters > 0.1f) ||
                                             float.IsInfinity(measurements.frameWidthMeters) || float.IsInfinity(measurements.frameHeightMeters)))
                    problems.Add($"{paths.MeasurementsFile}: frameWidthMeters y frameHeightMeters deben ser medidas reales mayores que 0,1 m.");
                if (measurements != null && (measurements.frameDepthMeters < 0f || float.IsNaN(measurements.frameDepthMeters) ||
                                             float.IsInfinity(measurements.frameDepthMeters) ||
                                             (measurements.frameDepthMeters > 0f && measurements.frameDepthMeters < 0.1f)))
                    problems.Add($"{paths.MeasurementsFile}: frameDepthMeters debe ser 0 (marco plano) o una profundidad de al menos 0,1 m.");
                if (measurements?.framePosition != null && measurements.framePosition.Length != 3)
                    problems.Add($"{paths.MeasurementsFile}: framePosition debe tener tres valores (x, y, z) o faltar.");
                if (measurements?.framePositionB != null && measurements.framePositionB.Length != 3)
                    problems.Add($"{paths.MeasurementsFile}: framePositionB debe tener tres valores (x, y, z) o faltar.");
            }
            return problems;
        }

        static MapInput ReadMapInput(string label, string folder, Paths paths, List<string> problems)
        {
            if (!Directory.Exists(folder))
            {
                problems.Add($"Mapa {label}: falta la carpeta {folder} con su .bytes, metadata JSON y PLY sparse.");
                return null;
            }
            var bytesFiles = Directory.GetFiles(folder, "*.bytes");
            if (bytesFiles.Length != 1)
            {
                problems.Add($"Mapa {label}: {folder} debe contener exactamente un .bytes de Immersal (hay {bytesFiles.Length}).");
                return null;
            }
            string bytesPath = bytesFiles[0].Replace('\\', '/');
            string fileName = Path.GetFileName(bytesPath);
            var match = Regex.Match(fileName, @"^(\d+)-(.+)\.bytes$");
            if (!match.Success)
            {
                problems.Add($"Mapa {label}: el archivo {fileName} debe llamarse <id>-<nombre>.bytes, como lo exporta el portal.");
                return null;
            }
            var input = new MapInput
            {
                Label = label,
                Folder = folder,
                Id = int.Parse(match.Groups[1].Value),
                Name = match.Groups[2].Value,
                BaseName = Path.GetFileNameWithoutExtension(fileName),
                BytesPath = bytesPath
            };
            input.MetadataPath = $"{folder}/{input.BaseName}-metadata.json";
            input.PlyPath = $"{folder}/{input.BaseName}-sparse.ply";
            if (new FileInfo(bytesPath).Length < 1024)
                problems.Add($"Mapa {label}: {fileName} pesa menos de 1 KB; no parece un mapa real.");
            if (paths.RejectNonBuildingIds && NonBuildingMapIds.Contains(input.Id))
                problems.Add($"Mapa {label}: el ID {input.Id} es el de la Fuente o de un mapa de ejemplo del SDK, no un mapa del edificio.");
            if (!File.Exists(input.MetadataPath))
                problems.Add($"Mapa {label}: falta {input.MetadataPath}.");
            else
            {
                try
                {
                    var meta = JsonUtility.FromJson<XRMap.MetadataFile>(File.ReadAllText(input.MetadataPath));
                    if (meta.id != input.Id)
                        problems.Add($"Mapa {label}: el metadata declara el ID {meta.id} pero el .bytes es {input.Id}.");
                }
                catch (Exception e)
                {
                    problems.Add($"Mapa {label}: metadata ilegible ({e.Message}).");
                }
            }
            if (!File.Exists(input.PlyPath))
                problems.Add($"Mapa {label}: falta {input.PlyPath}.");
            return input;
        }

        // ---------------------------------------------------------------- prepare

        [MenuItem("AncoRA/Immersal/Edificio/Preparar escena del edificio")]
        public static void Prepare() => PrepareCore(Real);

        internal static void PrepareCore(Paths paths)
        {
            var problems = FindInputProblems(paths, out var a, out var b, out var measurements);
            if (problems.Count > 0)
                throw new InvalidOperationException(MissingMessage(paths, problems));
            if (File.Exists(paths.Scene))
                throw new InvalidOperationException($"La escena {paths.Scene} ya existe; no se sobrescribe la alineación guardada por el equipo.");

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
            if (package == null || package.version != "2.4.0")
                throw new InvalidOperationException("Se necesita Immersal Core 2.4.0 antes de importar SimpleSample.");

            EnsurePlayerSettings();
            AssetDatabase.Refresh();
            foreach (var input in paths.SingleMap ? new[] { a } : new[] { a, b })
                foreach (string path in new[] { input.BytesPath, input.MetadataPath, input.PlyPath })
                    if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                        throw new InvalidOperationException($"Unity no importó {path}.");

            // Same base as the Fuente pilot: the official SimpleSample copied into a new scene.
            File.Copy(Path.Combine(package.resolvedPath, SampleScenePath), paths.Scene);
            AssetDatabase.ImportAsset(paths.Scene);
            var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            Lightmapping.lightingSettings = null;

            var space = Object.FindAnyObjectByType<XRSpace>();
            // Apply the map pose directly; the sample smoother starts at the world origin.
            space.ProcessPoses = false;
            var spaceFields = new SerializedObject(space);
            spaceFields.FindProperty("m_DataProcessors").arraySize = 0;
            spaceFields.ApplyModifiedPropertiesWithoutUndo();
            foreach (string name in new[] { "PoseFilter", "PoseSmoother" })
            {
                var child = space.transform.Find(name);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
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
            if (paths.DevelopmentBuild)
            {
                // ImmersalLogger.LoggingLevel.Verbose (1): the SDK then logs session start, registered maps and each localization.
                var sdkFields = new SerializedObject(sdk);
                sdkFields.FindProperty("m_LoggingLevel").intValue = 1;
                sdkFields.ApplyModifiedPropertiesWithoutUndo();
            }
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

            var mapA = Object.FindAnyObjectByType<XRMap>();
            if (mapA == null)
                throw new InvalidOperationException("SimpleSample no contiene un XR Map para usar como base.");
            if (mapA.transform.parent != space.transform)
                mapA.transform.SetParent(space.transform, false);
            // Sibling of A under the same XR Space, cloned before any visualization exists (two-map scenes only).
            XRMap mapB = paths.SingleMap ? null : Object.Instantiate(mapA.gameObject, space.transform).GetComponent<XRMap>();

            ConfigureMap(mapA, a, device, paths, isReference: true, new Color(0.57f, 0.93f, 0.12f));
            if (mapB != null)
                ConfigureMap(mapB, b, device, paths, isReference: false, new Color(0.95f, 0.25f, 0.85f));

            bool isBox = measurements.frameDepthMeters > 0f;
            var borderMaterial = CreateFrameMaterial(paths.BorderMaterial,
                isBox ? new Color(1f, 0.9f, 0.1f, 1f) : new Color(1f, 0.45f, 0.05f, 1f), transparent: false);
            var fillMaterial = CreateFrameMaterial(paths.FillMaterial, new Color(1f, 0.45f, 0.05f, 0.28f), transparent: true);
            var solidMaterial = isBox ? CreateFrameMaterial(paths.SolidMaterial, new Color(0.62f, 0.25f, 0.03f, 1f), transparent: false) : null;
            var frameObject = new GameObject(isBox ? "Caja edificio (ajustar en Editor)" : "Marco fachada (ajustar en Editor)");
            frameObject.transform.SetParent(space.transform, false);
            frameObject.isStatic = false;
            var frame = frameObject.AddComponent<EdificioFacadeFrame>();
            frame.Configure(measurements.frameWidthMeters, measurements.frameHeightMeters, measurements.frameDepthMeters,
                borderMaterial, fillMaterial, solidMaterial, startSolid: isBox);
            if (measurements.framePosition != null && measurements.framePosition.Length == 3)
            {
                var p = measurements.framePosition;
                frameObject.transform.localPosition = new Vector3(p[0], p[1], p[2]);
                frameObject.transform.localRotation = Quaternion.Euler(0f, measurements.frameYawDegrees, 0f);
            }

            var diagnosticsObject = new GameObject("Diagnóstico Edificio (solo equipo)");
            var diagnostics = diagnosticsObject.AddComponent<ImmersalEdificioPilotDiagnostics>();
            var fields = new SerializedObject(diagnostics);
            var mapsProperty = fields.FindProperty("maps");
            mapsProperty.arraySize = mapB != null ? 2 : 1;
            mapsProperty.GetArrayElementAtIndex(0).objectReferenceValue = mapA;
            if (mapB != null)
                mapsProperty.GetArrayElementAtIndex(1).objectReferenceValue = mapB;
            fields.FindProperty("space").objectReferenceValue = space;
            fields.FindProperty("sdk").objectReferenceValue = sdk;
            fields.FindProperty("localizer").objectReferenceValue = localizer;
            fields.FindProperty("frame").objectReferenceValue = frame;
            fields.FindProperty("showStatusBanner").boolValue = paths.ShowStatusBanner;
            fields.FindProperty("hudVisibleAtStart").boolValue = paths.HudVisibleAtStart;
            fields.FindProperty("keepVisibleAfterFirstLocalization").boolValue = paths.KeepVisible;

            TeologiaMapSelector selector = null;
            if (paths.MapSelector)
            {
                if (mapB == null)
                    throw new InvalidOperationException("El selector de mapas necesita dos mapas (SingleMap = false).");
                var selectorObject = new GameObject("Selector de mapas (Teologia)");
                selector = selectorObject.AddComponent<TeologiaMapSelector>();
                var sf = new SerializedObject(selector);
                sf.FindProperty("mapA").objectReferenceValue = mapA;
                sf.FindProperty("mapB").objectReferenceValue = mapB;
                sf.FindProperty("alignmentB").objectReferenceValue = mapB.GetComponent<ImmersalMapAlignment>();
                sf.FindProperty("frame").objectReferenceValue = frame;
                if (measurements.framePositionB != null && measurements.framePositionB.Length == 3)
                {
                    var pb = measurements.framePositionB;
                    sf.FindProperty("defaultPositionB").vector3Value = new Vector3(pb[0], pb[1], pb[2]);
                    sf.FindProperty("defaultYawB").floatValue = measurements.frameYawDegreesB;
                }
                sf.ApplyModifiedPropertiesWithoutUndo();
                fields.FindProperty("selector").objectReferenceValue = selector;
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            WireFieldAdjust(diagnosticsObject, diagnostics, mapB, frame, selector);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            string boxSize = isBox ? $"caja {measurements.frameWidthMeters:F1}×{measurements.frameHeightMeters:F1}×{measurements.frameDepthMeters:F1} m" : $"marco {measurements.frameWidthMeters:F1}×{measurements.frameHeightMeters:F1} m";
            if (selector != null)
                Debug.Log($"[AncoRA Edificio] Escena creada: {paths.Scene}. Dos mapas ({a.Id}-{a.Name} y {b.Id}-{b.Name}) con selector de modo; {boxSize} ({(measurements.estimated ? "ESTIMADO" : "medido")}); la alineación de «Ambos» se deriva de las dos cajas colocadas por el equipo; nada está medido en terreno.");
            else
            Debug.Log(mapB == null
                ? $"[AncoRA Edificio] Escena creada: {paths.Scene}. Mapa único {a.Id}-{a.Name}; {boxSize} ({(measurements.estimated ? "ESTIMADO" : "medido")}); nada de esto está medido en terreno."
                : $"[AncoRA Edificio] Escena creada: {paths.Scene}. A={a.Id}-{a.Name} (referencia), B={b.Id}-{b.Name} (hermano bajo el mismo XR Space, alineación SIN ajustar). " +
                  "Alinear B con A y colocar el marco en Scene View según la guía; nada de esto está medido en terreno.");
        }

        static void ConfigureMap(XRMap map, MapInput input, DeviceLocalization device, Paths paths, bool isReference, Color pointColor)
        {
            var bytes = AssetDatabase.LoadAssetAtPath<TextAsset>(input.BytesPath);
            map.gameObject.name = $"XR Map {input.Label} {input.BaseName}";
            // Configure reads the adjacent metadata JSON in the Editor and sets id, name and alignment fields.
            map.Configure(bytes);
            map.LocalizationMethod = device;
            map.MapOptions = new List<IMapOption>
            {
                new MapLoadingOption { m_SerializedDataSource = (int)MapDataSource.Embed, DownloadVisualizationAtRuntime = false }
            };
            map.SerializeMapOptions();
            if (map.mapId != input.Id)
                throw new InvalidOperationException($"El XR Map {input.Label} quedó con ID {map.mapId} en vez de {input.Id}.");

            // The metadata pose is discarded on purpose: the manual alignment component owns the transform.
            map.ApplyAlignment();
            var alignment = map.gameObject.GetComponent<ImmersalMapAlignment>() ?? map.gameObject.AddComponent<ImmersalMapAlignment>();
            alignment.Initialize(map, isReference, Vector3.zero, Vector3.zero);

            map.CreateVisualization(pointColor, XRMapVisualization.RenderMode.EditorAndRuntime);
            map.Visualization.LoadPly(input.PlyPath);
            Debug.Log($"[AncoRA Edificio] Mapa {input.Label} {input.BaseName}: PLY con {map.Visualization.Mesh.vertexCount} puntos para inspeccionar la alineación; no es un mapa de localización.");
        }

        static Material CreateFrameMaterial(string path, Color color, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("Falta el shader URP/Unlit para el marco.");
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
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------------------------------------------------------------- validate

        static void WireFieldAdjust(GameObject host, ImmersalEdificioPilotDiagnostics diagnostics, XRMap mapB, EdificioFacadeFrame frame,
            TeologiaMapSelector selector = null)
        {
            var adjust = host.GetComponent<ImmersalEdificioFieldAdjust>() ?? host.AddComponent<ImmersalEdificioFieldAdjust>();
            var fields = new SerializedObject(adjust);
            fields.FindProperty("diagnostics").objectReferenceValue = diagnostics;
            fields.FindProperty("mapB").objectReferenceValue = mapB;
            fields.FindProperty("frame").objectReferenceValue = frame;
            fields.FindProperty("selector").objectReferenceValue = selector;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("AncoRA/Immersal/Edificio/Agregar panel de ajuste de campo a la escena")]
        public static void AddFieldAdjust() => AddFieldAdjustCore(Real);

        /// <summary>Idempotent: adds the team-only field adjustment panel to an already prepared scene.</summary>
        internal static void AddFieldAdjustCore(Paths paths)
        {
            if (!File.Exists(paths.Scene))
                throw new FileNotFoundException("Falta la escena del edificio; ejecutar Prepare primero.", paths.Scene);
            var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var diagnostics = Object.FindObjectsByType<ImmersalEdificioPilotDiagnostics>(FindObjectsInactive.Include).Single();
            var mapB = paths.SingleMap ? null : Object.FindObjectsByType<ImmersalMapAlignment>(FindObjectsInactive.Include).Single(a => !a.IsReference).Map;
            var frame = Object.FindObjectsByType<EdificioFacadeFrame>(FindObjectsInactive.Include).Single();
            var selector = paths.MapSelector ? Object.FindObjectsByType<TeologiaMapSelector>(FindObjectsInactive.Include).Single() : null;
            WireFieldAdjust(diagnostics.gameObject, diagnostics, mapB, frame, selector);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AncoRA Edificio] Panel de ajuste de campo listo en la escena (visible solo con el HUD del equipo: 5 toques arriba a la izquierda).");
        }

        [MenuItem("AncoRA/Immersal/Edificio/Aplicar ajuste de campo desde ajuste-campo.json")]
        public static void ApplyFieldAdjustment() => ApplyFieldAdjustmentCore(Real);

        /// <summary>
        /// Applies the values copied from the phone panel (file ajuste-campo.json in the data folder) to map B and to the
        /// frame. The team did the adjustment, so both are flagged as adjusted/placed by the team; that says who did it,
        /// not that it is physically verified.
        /// </summary>
        internal static void ApplyFieldAdjustmentCore(Paths paths)
        {
            string file = paths.DataFolder + "/ajuste-campo.json";
            if (!File.Exists(file))
                throw new FileNotFoundException("Falta ajuste-campo.json (pegar el JSON que copia el panel 'Copiar valores').", file);
            var data = JsonUtility.FromJson<EdificioFieldAdjustmentData>(File.ReadAllText(file));
            if ((!paths.SingleMap && !paths.MapSelector && (data?.mapaB?.posicion == null || data.mapaB.posicion.Length != 3)) ||
                data?.marco?.posicion == null || data.marco.posicion.Length != 3 ||
                !(data.marco.ancho > 0.1f) || !(data.marco.alto > 0.1f))
                throw new InvalidOperationException($"{file} no tiene el formato del panel de ajuste ({(paths.SingleMap ? "marco" : paths.MapSelector ? "marco, teologia" : "mapaB, marco")}).");
            if (paths.MapSelector && (data.teologia?.posMapa1 == null || data.teologia.posMapa1.Length != 3 ||
                                      data.teologia.posMapa2 == null || data.teologia.posMapa2.Length != 3))
                throw new InvalidOperationException($"{file} no trae el bloque «teologia» con la caja de cada mapa; copiar los valores con el APK de dos mapas.");
            if (!File.Exists(paths.Scene))
                throw new FileNotFoundException("Falta la escena del edificio; ejecutar Prepare primero.", paths.Scene);

            var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var frame = Object.FindObjectsByType<EdificioFacadeFrame>(FindObjectsInactive.Include).Single();

            float[] b = null;
            if (!paths.SingleMap && !paths.MapSelector)
            {
                var alignment = Object.FindObjectsByType<ImmersalMapAlignment>(FindObjectsInactive.Include).Single(x => !x.IsReference);
                b = data.mapaB.posicion;
                var a = new SerializedObject(alignment);
                a.FindProperty("localPosition").vector3Value = new Vector3(b[0], b[1], b[2]);
                a.FindProperty("localEulerAngles").vector3Value = new Vector3(0f, data.mapaB.giroY, 0f);
                a.FindProperty("adjustedByTeam").boolValue = true;
                a.FindProperty("fieldNotes").stringValue =
                    $"AJUSTE DE CAMPO del {data.generado} (build {data.build}): pos ({b[0]:F2}, {b[1]:F2}, {b[2]:F2}) m, giro {data.mapaB.giroY:F1}°. " +
                    $"Hecho a ojo por el equipo con el panel de la app; NO verificado contra medidas físicas. Último salto al cambiar de mapa: {data.diagnostico?.ultimoSaltoCm:F1} cm / {data.diagnostico?.ultimoSaltoGrados:F2}°.";
                a.ApplyModifiedPropertiesWithoutUndo();
                alignment.Apply();
            }

            var f = data.marco.posicion;
            var euler = frame.transform.localEulerAngles;
            if (paths.MapSelector)
            {
                // The frame transform is the pose in the frame of map 1; the pose in map 2 lives in the selector.
                var t = data.teologia;
                frame.transform.localPosition = new Vector3(t.posMapa1[0], t.posMapa1[1], t.posMapa1[2]);
                frame.transform.localRotation = Quaternion.Euler(euler.x, t.giroMapa1, euler.z);
                var selector = Object.FindObjectsByType<TeologiaMapSelector>(FindObjectsInactive.Include).Single();
                var ss = new SerializedObject(selector);
                ss.FindProperty("defaultPositionB").vector3Value = new Vector3(t.posMapa2[0], t.posMapa2[1], t.posMapa2[2]);
                ss.FindProperty("defaultYawB").floatValue = t.giroMapa2;
                ss.FindProperty("defaultPoseASet").boolValue = t.fijadaMapa1;
                ss.FindProperty("defaultPoseBSet").boolValue = t.fijadaMapa2;
                ss.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                frame.transform.localPosition = new Vector3(f[0], f[1], f[2]);
                frame.transform.localRotation = Quaternion.Euler(euler.x, data.marco.giroY, euler.z);
            }
            var fs = new SerializedObject(frame);
            fs.FindProperty("widthMeters").floatValue = data.marco.ancho;
            fs.FindProperty("heightMeters").floatValue = data.marco.alto;
            if (frame.IsBox && data.marco.profundidad > 0.1f)
            {
                fs.FindProperty("depthMeters").floatValue = data.marco.profundidad;
                fs.FindProperty("solid").boolValue = data.marco.solido && frame.HasSolidMaterial;
            }
            fs.FindProperty("placedByTeam").boolValue = !paths.MapSelector || data.teologia.fijadaMapa1;
            fs.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(frame.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AncoRA Edificio] Ajuste de campo aplicado: {(b == null ? "sin mapa B" : $"B pos ({b[0]:F2}, {b[1]:F2}, {b[2]:F2}) m giro {data.mapaB.giroY:F1}°")}; marco pos ({f[0]:F2}, {f[1]:F2}, {f[2]:F2}) m giro {data.marco.giroY:F1}°, {data.marco.ancho:F2} × {data.marco.alto:F2} m. Marcados como ajustados por el equipo; NO verificados físicamente.");
        }

        [MenuItem("AncoRA/Immersal/Edificio/Aplicar medidas del marco desde el JSON")]
        public static void ApplyMeasurements() => ApplyMeasurementsCore(Real);

        /// <summary>Updates only width/height of the existing frame; its placement and flags are preserved.</summary>
        internal static void ApplyMeasurementsCore(Paths paths)
        {
            var problems = FindInputProblems(paths, out _, out _, out var measurements);
            if (problems.Count > 0)
                throw new InvalidOperationException(MissingMessage(paths, problems));
            if (!File.Exists(paths.Scene))
                throw new FileNotFoundException("Falta la escena del edificio; ejecutar Prepare primero.", paths.Scene);

            var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var frames = Object.FindObjectsByType<EdificioFacadeFrame>(FindObjectsInactive.Include);
            if (frames.Length != 1)
                throw new InvalidOperationException($"Debe haber un único marco de fachada (hay {frames.Length}).");
            var fields = new SerializedObject(frames[0]);
            fields.FindProperty("widthMeters").floatValue = measurements.frameWidthMeters;
            fields.FindProperty("heightMeters").floatValue = measurements.frameHeightMeters;
            fields.FindProperty("depthMeters").floatValue = measurements.frameDepthMeters;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AncoRA Edificio] Marco actualizado a {measurements.frameWidthMeters:F2} × {measurements.frameHeightMeters:F2} m " +
                      $"({(measurements.estimated ? "ESTIMADO, no medido" : "medido")}); posición y rotación del marco sin cambios.");
        }

        [Serializable]
        sealed class EstimatedCandidate
        {
            public float[] positionMeters;
            public float yawDegrees;
            public float inlierRatio;
        }

        [Serializable]
        sealed class EstimatedAlignment
        {
            public bool verified;
            public EstimatedCandidate best;
            public EstimatedCandidate secondBest;
            public float ratioBestToSecond;
        }

        [MenuItem("AncoRA/Immersal/Edificio/Aplicar alineación estimada A-B desde el JSON")]
        public static void ApplyEstimatedAlignment() => ApplyEstimatedAlignmentCore(Real);

        /// <summary>
        /// Writes the point-cloud registration estimate (Tools/EstimarAlineacionPly.js) into the alignment component of
        /// map B as a starting point. It does NOT mark the alignment as adjusted by the team: that stays a human step.
        /// </summary>
        internal static void ApplyEstimatedAlignmentCore(Paths paths)
        {
            string file = paths.DataFolder + "/alineacion-estimada.json";
            if (!File.Exists(file))
                throw new FileNotFoundException("Falta la alineación estimada; generarla con Tools/EstimarAlineacionPly.js.", file);
            if (!File.Exists(paths.Scene))
                throw new FileNotFoundException("Falta la escena del edificio; ejecutar Prepare primero.", paths.Scene);
            var estimate = JsonUtility.FromJson<EstimatedAlignment>(File.ReadAllText(file));
            if (estimate?.best?.positionMeters == null || estimate.best.positionMeters.Length != 3)
                throw new InvalidOperationException($"{file} no contiene un candidato válido.");

            var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var alignment = Object.FindObjectsByType<ImmersalMapAlignment>(FindObjectsInactive.Include).Single(a => !a.IsReference);
            var p = estimate.best.positionMeters;
            var fields = new SerializedObject(alignment);
            fields.FindProperty("localPosition").vector3Value = new Vector3(p[0], p[1], p[2]);
            fields.FindProperty("localEulerAngles").vector3Value = new Vector3(0f, estimate.best.yawDegrees, 0f);
            fields.FindProperty("fieldNotes").stringValue =
                $"ESTIMADO por registro de nubes PLY (Tools/EstimarAlineacionPly.js), NO verificado: giro {estimate.best.yawDegrees:F1}°, " +
                $"pos ({p[0]:F2}, {p[1]:F2}, {p[2]:F2}) m, acierto {estimate.best.inlierRatio * 100f:F0} % (mejor/segundo {estimate.ratioBestToSecond:F2}). " +
                "Giro y desnivel fiables; el desplazamiento X/Z es ambiguo (varios metros). Afinar con detalles físicos y distancias reales.";
            fields.ApplyModifiedPropertiesWithoutUndo();
            alignment.Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AncoRA Edificio] Alineación ESTIMADA aplicada a B: pos ({p[0]:F2}, {p[1]:F2}, {p[2]:F2}) m, giro {estimate.best.yawDegrees:F1}°. NO verificada ni marcada como ajustada por el equipo.");
        }

        [MenuItem("AncoRA/Immersal/Edificio/Validar proyecto (sin mapas)")]
        public static void ValidateProject()
        {
            var errors = new List<string>();
            ValidateProjectSettings(errors);
            if (errors.Count > 0)
                throw new InvalidOperationException("Proyecto inválido para el piloto del edificio:\n" + string.Join("\n", errors));
            Debug.Log("[AncoRA Edificio] Ajustes de proyecto, loaders XR y paquete Immersal: OK. No valida mapas, escena ni localización.");
        }

        [MenuItem("AncoRA/Immersal/Edificio/Validar piloto del edificio")]
        public static void Validate() => ValidateCore(Real);

        internal static void ValidateCore(Paths paths)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            ValidateProjectSettings(errors);

            var problems = FindInputProblems(paths, out var a, out var b, out var measurements);
            if (problems.Count > 0)
                errors.Add(MissingMessage(paths, problems));

            if (measurements != null && measurements.estimated)
                warnings.Add("Las medidas del marco están marcadas como ESTIMADAS (no medidas en el sitio): reemplazarlas antes de evaluar la alineación.");

            if (!File.Exists(paths.Scene))
                errors.Add($"Falta la escena {paths.Scene}; se crea con AncorRA.Editor.ImmersalEdificioPilotSetup.Prepare cuando estén los datos reales.");
            else if (a != null && (paths.SingleMap || b != null))
                ValidateScene(paths, a, b, errors, warnings);

            foreach (string warning in warnings)
                Debug.LogWarning("[AncoRA Edificio] AVISO: " + warning);
            if (errors.Count > 0)
                throw new InvalidOperationException("Piloto del edificio inválido:\n" + string.Join("\n", errors));
            Debug.Log("[AncoRA Edificio] Validación de escena, mapas, marco y loaders: OK. No verifica localización ni alineación física.");
        }

        static void ValidateProjectSettings(List<string> errors)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
            if (package == null || package.version != "2.4.0")
                errors.Add($"Immersal Core debe ser 2.4.0 (actual: {package?.version ?? "ausente"}).");
            if (!PlayerSettings.allowUnsafeCode ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS) != ScriptingImplementation.IL2CPP)
                errors.Add("Faltan unsafe o IL2CPP en Android/iOS.");
            // Unity 6000.6 reports iOS automatic=true via the API even with the saved override disabled.
            string settingsYaml = File.ReadAllText("ProjectSettings/ProjectSettings.asset").Replace("\r", "");
            bool iosOverrideSaved = settingsYaml.Contains("m_BuildTarget: iOSSupport\n    m_APIs: 10000000i\n    m_Automatic: 0");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64 ||
                PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) || !iosOverrideSaved ||
                PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).FirstOrDefault() != GraphicsDeviceType.OpenGLES3 ||
                PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS).FirstOrDefault() != GraphicsDeviceType.Metal)
                errors.Add("Gráficos/ABI inesperados (se esperan ARM64, OpenGLES3 en Android y Metal en iOS con override guardado).");

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
        }

        static void ValidateScene(Paths paths, MapInput a, MapInput b, List<string> errors, List<string> warnings)
        {
            EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var all = FindObjectsInactive.Include;
            var maps = Object.FindObjectsByType<XRMap>(all);
            var spaces = Object.FindObjectsByType<XRSpace>(all);
            var sdk = Object.FindAnyObjectByType<ImmersalSDK>(all);
            var frames = Object.FindObjectsByType<EdificioFacadeFrame>(all);
            var diagnostics = Object.FindObjectsByType<ImmersalEdificioPilotDiagnostics>(all);

            if (Object.FindObjectsByType<ARSession>(all).Length != 1 ||
                Object.FindObjectsByType<XROrigin>(all).Length != 1 ||
                Object.FindObjectsByType<ARCameraManager>(all).Length != 1 ||
                Object.FindObjectsByType<Camera>(all).Length != 1 ||
                Object.FindObjectsByType<TrackedPoseDriver>(all).Length != 1 ||
                Object.FindObjectsByType<AREarthManager>(all).Length != 0 ||
                Object.FindObjectsByType<GeospatialVpsProbe>(all).Length != 0 ||
                Object.FindObjectsByType<ImmersalFuentePilotDiagnostics>(all).Length != 0)
                errors.Add("Debe haber una sola ARSession, XROrigin, cámara AR y pose driver; sin Geospatial ni diagnóstico de la Fuente.");
            if (Object.FindObjectsByType<ServerLocalization>(all).Length != 0 ||
                Object.FindObjectsByType<DeviceLocalization>(all).Length != 1)
                errors.Add("Debe existir un único DeviceLocalization y ningún ServerLocalization.");
            if (spaces.Length != 1)
            {
                errors.Add($"Debe haber un único XR Space (hay {spaces.Length}).");
                return;
            }
            var space = spaces[0];
            if (space.ProcessPoses)
                errors.Add("XR Space debe aplicar la primera pose directamente (sin mostrar el marco en el origen).");

            int expectedMaps = paths.SingleMap ? 1 : 2;
            if (maps.Length != expectedMaps)
                errors.Add($"Debe{(expectedMaps == 1 ? " existir exactamente un XR Map" : "n existir exactamente dos XR Map")} (hay {maps.Length}).");
            else
            {
                foreach (var input in paths.SingleMap ? new[] { a } : new[] { a, b })
                {
                    var map = maps.FirstOrDefault(m => m.mapId == input.Id);
                    if (map == null)
                    {
                        errors.Add($"Ningún XR Map tiene el ID {input.Id} (mapa {input.Label}).");
                        continue;
                    }
                    ValidateMap(map, input, space, errors, warnings, paths.MapSelector);
                }
            }

            if (sdk == null || !string.IsNullOrEmpty(sdk.developerToken) || sdk.Session == null ||
                sdk.PlatformSupport == null || sdk.SceneUpdater == null || sdk.TrackingAnalyzer == null ||
                sdk.Localizer.AvailableLocalizationMethods.Length != 1 ||
                sdk.Localizer.AvailableLocalizationMethods[0] is not DeviceLocalization)
                errors.Add("SDK: faltan referencias, DeviceLocalization único o hay un token serializado.");

            if (frames.Length != 1)
                errors.Add($"Debe haber un único marco de fachada (hay {frames.Length}).");
            else
            {
                var frame = frames[0];
                var renderer = frame.GetComponent<MeshRenderer>();
                if (frame.transform.parent != space.transform || frame.GetComponentInParent<XRMap>() != null)
                    errors.Add("El marco debe ser hijo directo de XR Space, no de un XR Map.");
                if (frame.gameObject.isStatic)
                    errors.Add("El marco no debe ser Static.");
                if (!(frame.WidthMeters > 0.1f) || !(frame.HeightMeters > 0.1f))
                    errors.Add("El marco necesita ancho y alto reales mayores que 0,1 m.");
                if (renderer == null || renderer.sharedMaterials.Length != 2 ||
                    renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Unlit"))
                    errors.Add("El marco necesita dos materiales URP/Unlit (borde y relleno).");
                if (frame.IsBox && !frame.HasSolidMaterial)
                    errors.Add("La caja necesita su material sólido opaco para el modo que tapa el edificio.");
                if (frame.IsBox && !(frame.DepthMeters > 0.1f))
                    errors.Add("La caja necesita una profundidad real mayor que 0,1 m.");
                if (!frame.PlacedByTeam)
                    warnings.Add("El marco aún no está marcado como colocado por el equipo (posición/rotación por defecto).");
                Debug.Log($"[AncoRA Edificio] {(frame.IsBox ? $"Caja: {frame.WidthMeters:F2} × {frame.HeightMeters:F2} × {frame.DepthMeters:F2} m" : $"Marco: {frame.WidthMeters:F2} × {frame.HeightMeters:F2} m")}, local pos {frame.transform.localPosition}, euler {frame.transform.localEulerAngles}.");
            }

            if (diagnostics.Length != 1)
                errors.Add($"Debe haber un único diagnóstico del edificio (hay {diagnostics.Length}).");
            else
            {
                var d = new SerializedObject(diagnostics[0]);
                var list = d.FindProperty("maps");
                bool mapsWired = list.arraySize == expectedMaps;
                for (int i = 0; mapsWired && i < expectedMaps; i++)
                    mapsWired = list.GetArrayElementAtIndex(i).objectReferenceValue != null;
                if (!mapsWired ||
                    d.FindProperty("space").objectReferenceValue != space ||
                    d.FindProperty("frame").objectReferenceValue == null ||
                    d.FindProperty("sdk").objectReferenceValue == null ||
                    d.FindProperty("localizer").objectReferenceValue == null)
                    errors.Add("El diagnóstico del edificio tiene referencias sin asignar.");
            }

            if (paths.MapSelector)
            {
                var selectors = Object.FindObjectsByType<TeologiaMapSelector>(all);
                if (selectors.Length != 1)
                    errors.Add($"Debe haber un único selector de mapas (hay {selectors.Length}).");
                else
                {
                    var sel = new SerializedObject(selectors[0]);
                    var selA = sel.FindProperty("mapA").objectReferenceValue as XRMap;
                    var selB = sel.FindProperty("mapB").objectReferenceValue as XRMap;
                    if (selA == null || selB == null || selA == selB || !maps.Contains(selA) || !maps.Contains(selB) ||
                        selA.mapId != a.Id || (b != null && selB.mapId != b.Id))
                        errors.Add("El selector de mapas debe apuntar a los dos XR Map de la escena (mapa 1 = A, mapa 2 = B).");
                    if (sel.FindProperty("frame").objectReferenceValue != (frames.Length == 1 ? frames[0] : null) ||
                        sel.FindProperty("alignmentB").objectReferenceValue == null)
                        errors.Add("El selector de mapas necesita la caja y la alineación del mapa 2 asignadas.");
                    if (diagnostics.Length == 1 && new SerializedObject(diagnostics[0]).FindProperty("selector").objectReferenceValue != selectors[0])
                        errors.Add("El diagnóstico debe referenciar al selector de mapas para mostrar el modo.");
                    if (!maps.All(m => m.gameObject.activeSelf))
                        errors.Add("Ambos XR Map deben quedar activos en la escena guardada; el selector apaga en tiempo de ejecución el que no se usa.");
                }
            }

            var adjusters = Object.FindObjectsByType<ImmersalEdificioFieldAdjust>(all);
            if (adjusters.Length != 1)
                errors.Add($"Debe haber un único panel de ajuste de campo (hay {adjusters.Length}); ejecutar AncorRA.Editor.ImmersalEdificioPilotSetup.AddFieldAdjust.");
            else
            {
                var f = new SerializedObject(adjusters[0]);
                if (f.FindProperty("diagnostics").objectReferenceValue == null ||
                    (!paths.SingleMap && f.FindProperty("mapB").objectReferenceValue == null) ||
                    (paths.MapSelector && f.FindProperty("selector").objectReferenceValue == null) ||
                    f.FindProperty("frame").objectReferenceValue == null)
                    errors.Add("El panel de ajuste de campo tiene referencias sin asignar.");
            }
        }

        static void ValidateMap(XRMap map, MapInput input, XRSpace space, List<string> errors, List<string> warnings, bool derivedAlignment)
        {
            string label = $"Mapa {input.Label} ({input.Id})";
            var options = map.MapOptions.OfType<MapLoadingOption>().SingleOrDefault();
            if (!map.IsConfigured || map.mapName != input.Name || map.LocalizationMethod is not DeviceLocalization ||
                map.mapFile == null || AssetDatabase.GetAssetPath(map.mapFile) != input.BytesPath ||
                options == null || options.m_SerializedDataSource != (int)MapDataSource.Embed ||
                options.DownloadVisualizationAtRuntime || (options.Bytes != null && options.Bytes.Length > 0))
                errors.Add($"{label}: debe referenciar su .bytes, DeviceLocalization y Embed, sin descarga ni bytes alternativos.");
            if (map.transform.parent != space.transform)
                errors.Add($"{label}: el XR Map debe ser hijo directo del mismo XR Space.");
            if (Mathf.Abs((float)map.mapAlignment.scale - 1f) > 1e-6f)
                errors.Add($"{label}: la metadata trae escala {map.mapAlignment.scale}; este piloto ignora la metadata y exige escala 1.");
            if (map.Visualization == null || !map.Visualization.IsVisualized ||
                map.Visualization.renderMode != XRMapVisualization.RenderMode.EditorAndRuntime || map.Visualization.Mesh == null)
                errors.Add($"{label}: la visualización PLY no quedó guardada en la escena.");

            var alignment = map.GetComponent<ImmersalMapAlignment>();
            if (alignment == null || alignment.Map != map)
                errors.Add($"{label}: falta ImmersalMapAlignment (la alineación manual se perdería con ApplyAlignment).");
            else
            {
                if (alignment.IsReference != (input.Label == "A"))
                    errors.Add($"{label}: solo el mapa A es la referencia.");
                if (!alignment.TransformMatchesStoredValues())
                    errors.Add($"{label}: la transformación del XR Map no coincide con la alineación guardada (¿ApplyAlignment la sobrescribió?). Usar 'Aplicar alineación manual' en el componente.");
                if (input.Label == "B")
                {
                    if (!derivedAlignment && !alignment.AdjustedByTeam)
                        warnings.Add($"{label}: la alineación A↔B aún no está marcada como ajustada por el equipo (puede ser identidad o solo la estimación por nubes PLY).");
                    Debug.Log($"[AncoRA Edificio] Alineación B respecto de A: pos {alignment.LocalPosition.ToString("F4")} m, rot {alignment.LocalRotation.eulerAngles.ToString("F3")}°.");
                }
            }
        }

        [MenuItem("AncoRA/Immersal/Edificio/Comprobar carga nativa de los mapas")]
        public static void CheckNativeMapsInEditor() => CheckNativeMapsCore(Real);

        internal static void CheckNativeMapsCore(Paths paths)
        {
            ValidateCore(paths);
            foreach (var map in Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include))
            {
                int handle = Core.LoadMap(map.mapId, map.mapFile.bytes);
                try
                {
                    int points = Core.GetPointCloudSize(map.mapId);
                    if (handle < 0 || points <= 0)
                        throw new InvalidOperationException($"El plugin no cargó el .bytes {map.mapId}: handle={handle}, puntos={points}.");
                    Debug.Log($"[AncoRA Edificio] Plugin nativo del Editor cargó el mapa {map.mapId}: handle={handle}, puntos={points}. Android/iOS pendientes de dispositivo.");
                }
                finally
                {
                    Core.FreeMap(map.mapId);
                }
            }
        }

        // ---------------------------------------------------------------- build

        public static void BuildAndroid() => BuildCore(Real, AndroidApk, BuildTarget.Android);

        public static void ExportIos() => BuildCore(Real, IosFolder, BuildTarget.iOS);

        internal static void BuildCore(Paths paths, string output, BuildTarget target)
        {
            // Validate first: without the real maps this stops here and no artifact is produced.
            ValidateCore(paths);

            // A distinct application id keeps the Fuente pilot installed next to this one.
            var group = target == BuildTarget.Android ? NamedBuildTarget.Android : NamedBuildTarget.iOS;
            string originalId = PlayerSettings.GetApplicationIdentifier(group);
            string originalName = PlayerSettings.productName;
            try
            {
                EnsurePlayerSettings();
                PlayerSettings.SetApplicationIdentifier(group, originalId + paths.BuildIdSuffix);
                PlayerSettings.productName = originalName + paths.ProductSuffix;
                EditorUserBuildSettings.buildAppBundle = false;
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { paths.Scene },
                    locationPathName = output,
                    target = target,
                    options = paths.DevelopmentBuild ? BuildOptions.Development : BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Build edificio {target}: {report.summary.result} ({report.summary.totalErrors} errores).");
                Debug.Log($"[AncoRA Edificio] Build {target} OK: {report.summary.outputPath}. Un build correcto NO demuestra precisión en terreno.");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(group, originalId);
                PlayerSettings.productName = originalName;
                EnsurePlayerSettings();
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Restores the explicit settings that a platform switch can rewrite; scene list is left untouched.</summary>
        static void EnsurePlayerSettings()
        {
            PlayerSettings.allowUnsafeCode = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
        }
    }
}
#endif
