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
            AssetDatabase.DeleteAsset(Root);
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
            AssetDatabase.Refresh();
        }

    }
}
#endif
