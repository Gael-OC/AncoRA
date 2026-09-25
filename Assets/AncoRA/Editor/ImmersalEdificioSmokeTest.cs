#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AncorRA.AR;
using Immersal.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AncorRA.Editor
{
    /// <summary>
    /// Exercises the Prepare/Validate automation with the two SDK sample maps in a throw-away folder and scene.
    /// It proves the tooling only. The sample maps are NOT the building, and nothing it creates is kept.
    /// </summary>
    public static class ImmersalEdificioSmokeTest
    {
        const string Root = "Assets/AncoRA/_PruebaHumoEdificio";
        const string Tag = "[AncoRA Edificio][PRUEBA DE HUMO]";

        [MenuItem("AncoRA/Immersal/Edificio/Prueba de humo con mapas de ejemplo del SDK")]
        public static void Run()
        {
            var paths = new ImmersalEdificioPilotSetup.Paths
            {
                Scene = "Assets/Scenes/_PruebaHumoEdificio.unity",
                DataFolder = Root,
                RejectNonBuildingIds = false
            };
            var strict = new ImmersalEdificioPilotSetup.Paths { Scene = paths.Scene, DataFolder = Root };
            try
            {
                Cleanup(paths);
                Expect("sin datos, Prepare se detiene", () => ImmersalEdificioPilotSetup.PrepareCore(paths), "FALTAN DATOS REALES");
                Expect("sin datos, Validate se detiene", () => ImmersalEdificioPilotSetup.ValidateCore(paths), "FALTAN DATOS REALES");
                if (File.Exists(paths.Scene))
                    throw new Exception("Prepare sin datos no debía crear escena.");

                Stage(paths);
                Expect("IDs de ejemplo se rechazan en modo estricto", () => ImmersalEdificioPilotSetup.PrepareCore(strict), "no un mapa del edificio");

                ImmersalEdificioPilotSetup.PrepareCore(paths);
                ImmersalEdificioPilotSetup.ValidateCore(paths);
                Expect("Prepare no sobrescribe una escena existente", () => ImmersalEdificioPilotSetup.PrepareCore(paths), "ya existe");
                var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
                var maps = Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include);
                var space = Object.FindAnyObjectByType<XRSpace>();
                if (maps.Length != 2 || maps.Any(m => m.transform.parent != space.transform))
                    throw new Exception("Los dos XR Map deben ser hermanos bajo el mismo XR Space.");
                var frame = Object.FindAnyObjectByType<EdificioFacadeFrame>();
                if (frame.transform.parent != space.transform)
                    throw new Exception("El marco debe ser hijo del XR Space.");
                Debug.Log($"{Tag} Marco {frame.WidthMeters}×{frame.HeightMeters} m hijo de {frame.transform.parent.name}.");

                // Validate reopens the scene from disk, so every change is saved before validating.
                EditMapB(paths, (mapB, alignment) => alignment.Initialize(mapB, false, new Vector3(1.5f, 0f, -2f), new Vector3(0f, 30f, 0f)));
                ImmersalEdificioPilotSetup.ValidateCore(paths);
                // ImmersalMapAlignment.OnValidate re-applies the stored values whenever the scene loads, so an
                // ApplyAlignment that wiped the transform is repaired on reload (and in Awake at runtime).
                EditMapB(paths, (mapB, alignment) => mapB.ApplyAlignment());
                EditMapB(paths, (mapB, alignment) =>
                {
                    if ((mapB.transform.localPosition - new Vector3(1.5f, 0f, -2f)).sqrMagnitude > 1e-6f ||
                        Mathf.Abs(mapB.transform.localEulerAngles.y - 30f) > 0.01f)
                        throw new Exception($"{Tag} FALLO: la alineación guardada no se restauró tras ApplyAlignment (pos {mapB.transform.localPosition}).");
                });
                Debug.Log($"{Tag} OK: la alineación manual sobrevive a ApplyAlignment y a recargar la escena.");
                ImmersalEdificioPilotSetup.ValidateCore(paths);
                // Field adjustment pasted from the phone panel: written to the scene and flagged as done by the team.
                ImmersalEdificioPilotSetup.AddFieldAdjustCore(paths);
                File.WriteAllText(paths.DataFolder + "/ajuste-campo.json", JsonUtility.ToJson(new EdificioFieldAdjustmentData
                {
                    generado = "2026-01-01T00:00:00", build = "prueba",
                    mapaB = new EdificioFieldAdjustmentData.MapB { posicion = new[] { 3.25f, -1f, 7.5f }, giroY = 12.5f },
                    marco = new EdificioFieldAdjustmentData.Frame { posicion = new[] { 1f, 2f, 3f }, giroY = 40f, ancho = 30f, alto = 9f },
                    diagnostico = new EdificioFieldAdjustmentData.Diagnostic { mapaQueLocalizo = 90689, cambiosDeMapa = 2, ultimoSaltoCm = 15f, ultimoSaltoGrados = 0.5f }
                }));
                AssetDatabase.Refresh();
                ImmersalEdificioPilotSetup.ApplyFieldAdjustmentCore(paths);
                EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
                var adjustedB = Object.FindObjectsByType<ImmersalMapAlignment>(FindObjectsInactive.Include).Single(x => !x.IsReference);
                var adjustedFrame = Object.FindAnyObjectByType<EdificioFacadeFrame>();
                if ((adjustedB.Map.transform.localPosition - new Vector3(3.25f, -1f, 7.5f)).sqrMagnitude > 1e-6f || !adjustedB.AdjustedByTeam ||
                    Mathf.Abs(adjustedB.Map.transform.localEulerAngles.y - 12.5f) > 0.01f ||
                    (adjustedFrame.transform.localPosition - new Vector3(1f, 2f, 3f)).sqrMagnitude > 1e-6f ||
                    Mathf.Abs(adjustedFrame.WidthMeters - 30f) > 1e-4f || Mathf.Abs(adjustedFrame.HeightMeters - 9f) > 1e-4f || !adjustedFrame.PlacedByTeam)
                    throw new Exception($"{Tag} FALLO: el ajuste de campo no quedó aplicado en la escena.");
                ImmersalEdificioPilotSetup.ValidateCore(paths);
                Debug.Log($"{Tag} OK: el ajuste de campo se aplica a B y al marco y la escena sigue válida.");
                Debug.Log($"{Tag} OK: la automatización funciona con datos de ejemplo. Esto NO valida ningún dato del edificio.");

            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Cleanup(paths);
            }
        }


        static void EditMapB(ImmersalEdificioPilotSetup.Paths paths, Action<XRMap, ImmersalMapAlignment> edit)
        {
            var scene = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var mapB = Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include).Single(m => m.mapId == 90689);
            edit(mapB, mapB.GetComponent<ImmersalMapAlignment>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void Stage(ImmersalEdificioPilotSetup.Paths paths)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
            string source = Path.Combine(package.resolvedPath, "Samples~", "Core", "Map Data");
            CopyMap(source, "90687-SampleMapA", paths.MapFolderA);
            CopyMap(source, "90689-SampleMapB", paths.MapFolderB);
            File.WriteAllText(paths.MeasurementsFile,
                "{ \"frameWidthMeters\": 10.0, \"frameHeightMeters\": 4.0, \"note\": \"VALORES DE PRUEBA DE HUMO, no medidos\" }");
            AssetDatabase.Refresh();
        }

        static void CopyMap(string source, string baseName, string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (string suffix in new[] { ".bytes", "-metadata.json", "-sparse.ply" })
                File.Copy(Path.Combine(source, baseName + suffix), Path.Combine(folder, baseName + suffix), true);
        }

        static void Expect(string what, Action action, string expectedFragment)
        {
            try
            {
                action();
            }
            catch (Exception e) when (e.Message.Contains(expectedFragment))
            {
                Debug.Log($"{Tag} OK: {what}.");
                return;
            }
            throw new Exception($"{Tag} FALLO: {what} (no se produjo el error esperado '{expectedFragment}').");
        }

        static void Cleanup(ImmersalEdificioPilotSetup.Paths paths)
        {
            AssetDatabase.DeleteAsset(paths.Scene);
            AssetDatabase.DeleteAsset(paths.DataFolder);
            if (Directory.Exists(paths.DataFolder))
                Directory.Delete(paths.DataFolder, true);
            AssetDatabase.Refresh();
        }

        const string RootTeologia = "Assets/AncoRA/_PruebaHumoTeologia";
        const string RootTeologiaSelector = "Assets/AncoRA/_PruebaHumoTeologia2";

        /// <summary>
        /// Two-map Teologia scene with the map selector, from the two SDK sample maps in a throw-away folder and scene:
        /// wiring, per-map saved box, derived alignment for "both" and applying the values copied from the phone.
        /// It proves the tooling; the sample maps are NOT Teologia and nothing it creates is kept.
        /// </summary>
        [MenuItem("AncoRA/Immersal/Teologia/Prueba de humo (2 mapas + selector) con mapas de ejemplo del SDK")]
        public static void RunTeologiaSelector()
        {
            const string tag = "[AncoRA Teologia][PRUEBA DE HUMO]";
            const string testPrefix = "AncoRA.Test.v1.";
            var paths = new ImmersalEdificioPilotSetup.Paths
            {
                Scene = "Assets/Scenes/_PruebaHumoTeologia2.unity",
                DataFolder = RootTeologiaSelector,
                MapSelector = true,
                KeepVisible = true,
                ShowStatusBanner = true,
                HudVisibleAtStart = true,
                MeasurementsName = "teologia-medidas.json",
                RejectNonBuildingIds = false
            };
            try
            {
                Cleanup(paths);
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
                string source = Path.Combine(package.resolvedPath, "Samples~", "Core", "Map Data");
                CopyMap(source, "90687-SampleMapA", paths.MapFolderA);
                CopyMap(source, "90689-SampleMapB", paths.MapFolderB);
                File.WriteAllText(paths.MeasurementsFile,
                    "{ \"frameWidthMeters\": 12.0, \"frameHeightMeters\": 8.0, \"frameDepthMeters\": 25.0, " +
                    "\"framePosition\": [3.0, 4.0, 5.0], \"frameYawDegrees\": 20.0, " +
                    "\"framePositionB\": [10.0, 3.0, -2.0], \"frameYawDegreesB\": 65.0, \"estimated\": true }");
                AssetDatabase.Refresh();

                ImmersalEdificioPilotSetup.PrepareCore(paths);
                ImmersalEdificioPilotSetup.ValidateCore(paths);

                EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
                var maps = Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include);
                var selector = Object.FindAnyObjectByType<TeologiaMapSelector>();
                var diagnostics = new SerializedObject(Object.FindAnyObjectByType<ImmersalEdificioPilotDiagnostics>());
                var adjust = new SerializedObject(Object.FindAnyObjectByType<ImmersalEdificioFieldAdjust>());
                var ss = new SerializedObject(selector);
                if (maps.Length != 2 || !maps.All(m => m.gameObject.activeSelf))
                    throw new Exception($"{tag} FALLO: la escena debe guardar los dos XR Map activos (el selector apaga uno al ejecutar).");
                if (!diagnostics.FindProperty("keepVisibleAfterFirstLocalization").boolValue ||
                    diagnostics.FindProperty("selector").objectReferenceValue != selector ||
                    adjust.FindProperty("selector").objectReferenceValue != selector)
                    throw new Exception($"{tag} FALLO: el diagnóstico y el panel deben referenciar al selector y mantener la caja visible.");
                if ((ss.FindProperty("defaultPositionB").vector3Value - new Vector3(10f, 3f, -2f)).sqrMagnitude > 1e-6f ||
                    Mathf.Abs(ss.FindProperty("defaultYawB").floatValue - 65f) > 1e-4f ||
                    ss.FindProperty("mapA").objectReferenceValue == ss.FindProperty("mapB").objectReferenceValue)
                    throw new Exception($"{tag} FALLO: el selector no quedó con los dos mapas y la caja inicial del mapa 2.");
                Debug.Log($"{tag} OK: escena de 2 mapas con selector, diagnóstico y panel enlazados; caja inicial del mapa 2 (10, 3, -2) giro 65.");

                // Derived alignment: boxA = T * boxB must reproduce the pose the team placed in map 1.
                var t = Quaternion.Euler(0f, 30f, 0f);
                var posB = new Vector3(1f, 2f, 4f);
                var posA = t * posB + new Vector3(5f, 0.5f, -3f);
                TeologiaMapSelector.DeriveAlignment(posA, 110f, posB, 80f, out var dpos, out float dyaw);
                if ((dpos - new Vector3(5f, 0.5f, -3f)).magnitude > 1e-3f || Mathf.Abs(Mathf.DeltaAngle(dyaw, 30f)) > 1e-2f)
                    throw new Exception($"{tag} FALLO: alineación derivada {dpos}, giro {dyaw}; se esperaba (5, 0.5, -3) y 30°.");
                TeologiaMapSelector.DeriveAlignment(new Vector3(2f, 1f, 3f), 10f, new Vector3(2f, 1f, 3f), 80f, out dpos, out dyaw);
                var back = Quaternion.Euler(0f, dyaw, 0f) * new Vector3(2f, 1f, 3f) + dpos;
                if (Mathf.Abs(Mathf.DeltaAngle(dyaw, -70f)) > 1e-2f || (back - new Vector3(2f, 1f, 3f)).magnitude > 1e-3f)
                    throw new Exception($"{tag} FALLO: la alineación derivada no lleva la caja del mapa 2 al mapa 1 (giro {dyaw}, {back}).");
                Debug.Log($"{tag} OK: la alineación derivada del modo Ambos reproduce la pose de la caja en el mapa 1.");

                // Saved box per map, size and mode.
                var store = new TeologiaBoxStore(testPrefix);
                try
                {
                    store.ClearPose(90687); store.ClearPose(90689); store.ClearSize();
                    if (store.TryLoadPose(90687, out _, out _) || store.TryLoadSize(out _, out _))
                        throw new Exception($"{tag} FALLO: el almacén de prueba debía empezar vacío.");
                    store.SavePose(90687, new Vector3(1f, 2f, 3f), 45f);
                    store.SavePose(90689, new Vector3(-4f, 5f, 6f), 200f);
                    store.SaveSize(new Vector3(20f, 8f, 12f), false);
                    store.SaveMode(2);
                    if (!store.TryLoadPose(90687, out var p1, out float y1) || !store.TryLoadPose(90689, out var p2, out float y2) ||
                        (p1 - new Vector3(1f, 2f, 3f)).sqrMagnitude > 1e-6f || Mathf.Abs(y1 - 45f) > 1e-4f ||
                        (p2 - new Vector3(-4f, 5f, 6f)).sqrMagnitude > 1e-6f || Mathf.Abs(y2 - 200f) > 1e-4f ||
                        !store.TryLoadSize(out var size, out bool solid) || (size - new Vector3(20f, 8f, 12f)).sqrMagnitude > 1e-6f || solid ||
                        store.LoadMode(0) != 2)
                        throw new Exception($"{tag} FALLO: el almacén no devolvió lo guardado (una pose por mapa, tamaño y modo).");
                    store.ClearPose(90687);
                    if (store.TryLoadPose(90687, out _, out _) || !store.TryLoadPose(90689, out _, out _))
                        throw new Exception($"{tag} FALLO: borrar la pose de un mapa debía dejar intacta la del otro.");
                    Debug.Log($"{tag} OK: la caja se guarda por mapa, con tamaño y modo compartidos.");
                }
                finally
                {
                    store.ClearPose(90687); store.ClearPose(90689); store.ClearSize();
                    PlayerPrefs.DeleteKey(testPrefix + "Modo");
                    PlayerPrefs.Save();
                }

                // Values copied from the phone: the box of each map goes to the scene, size and fill to the frame.
                File.WriteAllText(paths.DataFolder + "/ajuste-campo.json", JsonUtility.ToJson(new EdificioFieldAdjustmentData
                {
                    generado = "2026-01-01T00:00:00", build = "prueba",
                    marco = new EdificioFieldAdjustmentData.Frame
                    {
                        posicion = new[] { 7f, 1f, -4f }, giroY = 100f, ancho = 30f, alto = 9f, profundidad = 14f, solido = false
                    },
                    teologia = new EdificioFieldAdjustmentData.Teologia
                    {
                        modo = 1, mapa1Id = 90687, mapa2Id = 90689,
                        posMapa1 = new[] { 1f, 2f, 3f }, giroMapa1 = 40f, fijadaMapa1 = true,
                        posMapa2 = new[] { 7f, 1f, -4f }, giroMapa2 = 100f, fijadaMapa2 = true
                    }
                }));
                AssetDatabase.Refresh();
                ImmersalEdificioPilotSetup.ApplyFieldAdjustmentCore(paths);
                EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
                var frame = Object.FindAnyObjectByType<EdificioFacadeFrame>();
                var applied = new SerializedObject(Object.FindAnyObjectByType<TeologiaMapSelector>());
                if ((frame.transform.localPosition - new Vector3(1f, 2f, 3f)).sqrMagnitude > 1e-6f ||
                    Mathf.Abs(frame.transform.localEulerAngles.y - 40f) > 0.01f ||
                    Mathf.Abs(frame.WidthMeters - 30f) > 1e-4f || Mathf.Abs(frame.HeightMeters - 9f) > 1e-4f ||
                    Mathf.Abs(frame.DepthMeters - 14f) > 1e-4f || frame.Solid || !frame.PlacedByTeam ||
                    (applied.FindProperty("defaultPositionB").vector3Value - new Vector3(7f, 1f, -4f)).sqrMagnitude > 1e-6f ||
                    Mathf.Abs(applied.FindProperty("defaultYawB").floatValue - 100f) > 1e-4f ||
                    !applied.FindProperty("defaultPoseASet").boolValue || !applied.FindProperty("defaultPoseBSet").boolValue)
                    throw new Exception($"{tag} FALLO: el ajuste de campo no dejó la caja de cada mapa en la escena.");
                ImmersalEdificioPilotSetup.ValidateCore(paths);
                Debug.Log($"{tag} OK: los valores copiados del teléfono dejan la caja de cada mapa y el tamaño en la escena, y sigue válida.");

                File.WriteAllText(paths.DataFolder + "/ajuste-campo.json", JsonUtility.ToJson(new EdificioFieldAdjustmentData
                {
                    marco = new EdificioFieldAdjustmentData.Frame { posicion = new[] { 0f, 0f, 0f }, ancho = 5f, alto = 5f }
                }));
                AssetDatabase.Refresh();
                Expect("sin el bloque de Teología el ajuste se rechaza", () => ImmersalEdificioPilotSetup.ApplyFieldAdjustmentCore(paths), "teologia");
                Debug.Log($"{tag} OK: la automatización de 2 mapas + selector funciona con los mapas de ejemplo. Esto NO valida Teología.");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Cleanup(paths);
            }
        }

        /// <summary>Checks the on-screen status line for every step of the flow, in order, without a phone.</summary>
        [MenuItem("AncoRA/Immersal/Teologia/Prueba del texto de estado")]
        public static void RunStatusText()
        {
            const string tag = "[AncoRA Teologia][PRUEBA DE ESTADO]";
            var ready = new EdificioStatusText.Inputs { SdkReady = true, ContentName = "Caja", LastMapId = 151714 };
            void Check(string what, EdificioStatusText.Inputs input, EdificioStatusLevel level, string fragment)
            {
                string text = EdificioStatusText.Describe(input, out var actual);
                if (actual != level || !text.Contains(fragment))
                    throw new Exception($"{tag} FALLO: {what}: nivel {actual}, texto '{text}' (se esperaba {level} con '{fragment}').");
                Debug.Log($"{tag} OK: {what} -> {text}");
            }

            var i = new EdificioStatusText.Inputs { ContentName = "Caja", SecondsSinceStart = 2f };
            Check("arranque", i, EdificioStatusLevel.Waiting, "Iniciando la cámara");
            i.SecondsSinceStart = 20f;
            Check("arranque lento avisa del permiso", i, EdificioStatusLevel.Waiting, "permiso de cámara");
            i.ArUnsupported = true;
            Check("teléfono sin ARCore", i, EdificioStatusLevel.Error, "no es compatible");
            i.Error = "Carga nativa del mapa 151714 sin puntos";
            Check("un error manda sobre todo", i, EdificioStatusLevel.Error, "ERROR: Carga nativa");

            i = ready;
            i.MapStillLoading = "151714 Teologia";
            Check("cargando el mapa", i, EdificioStatusLevel.Waiting, "Cargando el mapa 151714 Teologia");
            i.MapStillLoading = null;
            Check("sin tracking", i, EdificioStatusLevel.Waiting, "Mueve el teléfono despacio");
            i.Tracking = true;
            i.Attempts = 3;
            Check("buscando el edificio", i, EdificioStatusLevel.Waiting, "Intentos: 3, aciertos: 0");
            i.Localized = true;
            Check("aplicando la posición", i, EdificioStatusLevel.Waiting, "Ubicando");
            i.EverLocalized = true;
            i.ContentShown = true;
            Check("ubicado con la caja visible", i, EdificioStatusLevel.Ok, "Ubicado con el mapa 151714. Caja visible");
            i.Localized = false;
            i.ContentShown = false;
            Check("se perdió la ubicación", i, EdificioStatusLevel.Waiting, "Se perdió la ubicación");
            i.Localized = true;
            i.ContentShown = true;
            i.PositionHeld = true;
            Check("caja mantenida sin corrección", i, EdificioStatusLevel.Waiting, "posición mantenida por el tracking del teléfono");
            Debug.Log($"{tag} OK: todos los estados muestran un texto.");
        }

        /// <summary>
        /// Same idea for the single-map Teologia demo with a 3D box: one SDK sample map in a throw-away folder and scene.
        /// It proves the tooling and the box; the sample map is NOT Teologia and nothing it creates is kept.
        /// </summary>
        [MenuItem("AncoRA/Immersal/Teologia/Prueba de humo (1 mapa + caja) con mapa de ejemplo del SDK")]
        public static void RunTeologia()
        {
            const string tag = "[AncoRA Teologia][PRUEBA DE HUMO]";
            var paths = new ImmersalEdificioPilotSetup.Paths
            {
                Scene = "Assets/Scenes/_PruebaHumoTeologia.unity",
                DataFolder = RootTeologia,
                SingleMap = true,
                ShowStatusBanner = true,
                HudVisibleAtStart = true,
                MeasurementsName = "teologia-medidas.json",
                RejectNonBuildingIds = false
            };
            var strict = new ImmersalEdificioPilotSetup.Paths
            {
                Scene = paths.Scene, DataFolder = RootTeologia, SingleMap = true, MeasurementsName = paths.MeasurementsName
            };
            try
            {
                Cleanup(paths);
                Expect("sin datos, Prepare se detiene", () => ImmersalEdificioPilotSetup.PrepareCore(paths), "FALTAN DATOS REALES");

                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core");
                CopyMap(Path.Combine(package.resolvedPath, "Samples~", "Core", "Map Data"), "90687-SampleMapA", paths.MapFolderA);
                Directory.CreateDirectory(paths.DataFolder);
                File.WriteAllText(paths.MeasurementsFile,
                    "{ \"frameWidthMeters\": 12.0, \"frameHeightMeters\": 8.0, \"frameDepthMeters\": 25.0, " +
                    "\"framePosition\": [3.0, 4.0, 5.0], \"frameYawDegrees\": 20.0, \"estimated\": true, \"note\": \"PRUEBA DE HUMO\" }");
                AssetDatabase.Refresh();
                Expect("IDs de ejemplo se rechazan en modo estricto", () => ImmersalEdificioPilotSetup.PrepareCore(strict), "no un mapa del edificio");

                ImmersalEdificioPilotSetup.PrepareCore(paths);
                ImmersalEdificioPilotSetup.ValidateCore(paths);

                EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
                var maps = Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include);
                var space = Object.FindAnyObjectByType<XRSpace>();
                if (maps.Length != 1 || maps[0].transform.parent != space.transform)
                    throw new Exception($"{tag} FALLO: debe haber un solo XR Map hijo del XR Space (hay {maps.Length}).");
                var box = Object.FindAnyObjectByType<EdificioFacadeFrame>();
                var filter = box.GetComponent<MeshFilter>();
                if (!box.IsBox || !box.Solid || !box.HasSolidMaterial ||
                    Mathf.Abs(box.WidthMeters - 12f) > 1e-4f || Mathf.Abs(box.HeightMeters - 8f) > 1e-4f || Mathf.Abs(box.DepthMeters - 25f) > 1e-4f)
                    throw new Exception($"{tag} FALLO: la caja no quedó con 12×8×25 m, sólida y con material opaco.");
                if ((box.transform.localPosition - new Vector3(3f, 4f, 5f)).sqrMagnitude > 1e-6f ||
                    Mathf.Abs(box.transform.localEulerAngles.y - 20f) > 0.01f)
                    throw new Exception($"{tag} FALLO: la posición inicial de la caja no es la del JSON ({box.transform.localPosition}).");
                // Both submeshes are used and the mesh is exactly the box: 12 edge bars + 6 faces.
                if (filter.sharedMesh == null || filter.sharedMesh.subMeshCount != 2 || filter.sharedMesh.vertexCount != 12 * 8 + 24 + 8 ||
                    filter.sharedMesh.GetTriangles(0).Length != 12 * 36 + 12 || filter.sharedMesh.GetTriangles(1).Length != 36)
                    throw new Exception($"{tag} FALLO: la malla de la caja no tiene 12 aristas, la X delantera y 6 caras.");
                var bounds = filter.sharedMesh.bounds;
                if ((bounds.size - new Vector3(12.15f, 8.15f, 25.15f)).magnitude > 0.05f)
                    throw new Exception($"{tag} FALLO: los límites de la caja son {bounds.size}, se esperaban ~12,15×8,15×25,15 m.");
                var diagnostics = new SerializedObject(Object.FindAnyObjectByType<ImmersalEdificioPilotDiagnostics>());
                if (!diagnostics.FindProperty("showStatusBanner").boolValue || !diagnostics.FindProperty("hudVisibleAtStart").boolValue)
                    throw new Exception($"{tag} FALLO: el banner de estado y el HUD deben quedar visibles desde el inicio en la escena.");
                Debug.Log($"{tag} OK: banner de estado y HUD con la barra de ajuste visibles desde el inicio.");
                box.SetSolid(false);
                if (box.Solid || box.GetComponent<MeshRenderer>().sharedMaterials[1].color.a > 0.5f)
                    throw new Exception($"{tag} FALLO: SetSolid(false) no dejó el relleno translúcido.");
                box.SetSolid(true);
                if (!box.Solid || box.GetComponent<MeshRenderer>().sharedMaterials[1].color.a < 0.99f)
                    throw new Exception($"{tag} FALLO: SetSolid(true) no dejó el relleno opaco.");
                Debug.Log($"{tag} OK: caja {box.WidthMeters}×{box.HeightMeters}×{box.DepthMeters} m, malla {filter.sharedMesh.vertexCount} vértices, alterna sólido/translúcido.");

                // A depth of zero keeps the flat frame of the building pilot.
                box.SetSize(12f, 8f, 0f);
                if (box.IsBox || box.GetComponent<MeshFilter>().sharedMesh.vertexCount != 20)
                    throw new Exception($"{tag} FALLO: con profundidad 0 debe volver el marco plano de 20 vértices.");
                Debug.Log($"{tag} OK: profundidad 0 conserva el marco plano.");
                EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);

                Expect("Prepare no sobrescribe una escena existente", () => ImmersalEdificioPilotSetup.PrepareCore(paths), "ya existe");

                // Field adjustment pasted from the phone panel: single-map format, no map B.
                ImmersalEdificioPilotSetup.AddFieldAdjustCore(paths);
                File.WriteAllText(paths.DataFolder + "/ajuste-campo.json", JsonUtility.ToJson(new EdificioFieldAdjustmentData
                {
                    generado = "2026-01-01T00:00:00", build = "prueba",
                    marco = new EdificioFieldAdjustmentData.Frame
                    {
                        posicion = new[] { 1f, 2f, 3f }, giroY = 40f, ancho = 30f, alto = 9f, profundidad = 14f, solido = false
                    }
                }));
                AssetDatabase.Refresh();
                ImmersalEdificioPilotSetup.ApplyFieldAdjustmentCore(paths);
                EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
                var adjusted = Object.FindAnyObjectByType<EdificioFacadeFrame>();
                if ((adjusted.transform.localPosition - new Vector3(1f, 2f, 3f)).sqrMagnitude > 1e-6f ||
                    Mathf.Abs(adjusted.WidthMeters - 30f) > 1e-4f || Mathf.Abs(adjusted.HeightMeters - 9f) > 1e-4f ||
                    Mathf.Abs(adjusted.DepthMeters - 14f) > 1e-4f || adjusted.Solid || !adjusted.PlacedByTeam)
                    throw new Exception($"{tag} FALLO: el ajuste de campo no quedó aplicado a la caja.");
                ImmersalEdificioPilotSetup.ValidateCore(paths);
                Debug.Log($"{tag} OK: el ajuste de campo (1 mapa) se aplica a la caja y la escena sigue válida.");

                File.WriteAllText(paths.MeasurementsFile, "{ \"frameWidthMeters\": 12.0, \"frameHeightMeters\": 8.0, \"frameDepthMeters\": 0.05 }");
                AssetDatabase.Refresh();
                Expect("una profundidad menor que 0,1 m se rechaza", () => ImmersalEdificioPilotSetup.ValidateCore(paths), "frameDepthMeters");
                Debug.Log($"{tag} OK: la automatización de 1 mapa + caja funciona con el mapa de ejemplo. Esto NO valida Teología.");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Cleanup(paths);
            }
        }

    }
}
#endif
