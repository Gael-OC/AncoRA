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
    /// End-to-end check of the Editor automation with the SDK sample maps (only the .bytes, like the real campus maps):
    /// two buildings, one of them with two maps. Works in a temporary folder and scene that it deletes, and restores the
    /// Build Settings scene list. Proves the scene wiring, not localization.
    /// </summary>
    public static class PaseoSmokeTest
    {
        const string Root = "Assets/AncoRA/_PruebaHumoPaseo";
        const string Tag = "[AncoRA Paseo][PRUEBA DE HUMO]";

        [MenuItem("AncoRA/Paseo/Prueba de humo con mapas de ejemplo del SDK")]
        public static void Run()
        {
            var paths = new PaseoPaths { DataRoot = Root, Scene = Root + "/PruebaHumoPaseo.unity", RejectSampleIds = false };
            var originalScenes = EditorBuildSettings.scenes;
            try
            {
                CreateSampleTour();
                PaseoSetup.CheckInputsCore(paths);
                PaseoSetup.PrepareCore(paths);
                PaseoSetup.ValidateCore(paths);
                PaseoSetup.CheckNativeMapsCore(paths);
                AssertOneSpacePerMap();
                ApplyAdjustmentAndCheck(paths);
                Debug.Log($"{Tag} OK: Prepare, Validate y carga nativa con 3 mapas en 3 XR Spaces (2 edificios). No prueba localización.");
            }
            finally
            {
                EditorBuildSettings.scenes = originalScenes;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(Root);
                if (Directory.Exists(Root))
                    Directory.Delete(Root, true);
                AssetDatabase.Refresh();
            }
        }

        static void CreateSampleTour()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.immersal.core")
                          ?? throw new InvalidOperationException("Falta Immersal Core.");
            string source = Path.Combine(package.resolvedPath, "Samples~", "Core", "Map Data");
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
            WriteBuilding("EdificioA", "Edificio A", source, "90687-SampleMapA.bytes");
            WriteBuilding("EdificioB", "Edificio B", source, "90689-SampleMapB.bytes", "90690-SampleMapC.bytes");
            AssetDatabase.Refresh();
        }

        static void WriteBuilding(string id, string name, string source, params string[] files)
        {
            string folder = Path.Combine(Root, id);
            Directory.CreateDirectory(folder);
            var config = new PaseoBuildingConfig { nombre = name };
            foreach (string file in files)
            {
                File.Copy(Path.Combine(source, file), Path.Combine(folder, file));
                config.mapas.Add(new PaseoMapConfig { id = PaseoConfig.IdFromFileName(file).Value, archivo = file });
            }
            File.WriteAllText(Path.Combine(folder, PaseoConfig.FileName), PaseoConfig.Serialize(config));
        }

        static void ApplyAdjustmentAndCheck(PaseoPaths paths)
        {
            var adjustment = new PaseoAdjustment { generado = "prueba" };
            adjustment.edificios.Add(new PaseoAdjustmentBuilding
            {
                id = "EdificioB", tamano = new[] { 30f, 10f, 15f }, solido = true,
                mapas =
                {
                    new PaseoAdjustmentMap { id = 90689, posicion = new[] { 1f, 2f, 3f }, giro = 45f, colocada = true },
                    new PaseoAdjustmentMap { id = 90690, posicion = new[] { 9f, 9f, 9f }, giro = 90f, colocada = false }
                }
            });
            File.WriteAllText(paths.AdjustmentFile, JsonUtility.ToJson(adjustment));
            AssetDatabase.Refresh();

            PaseoSetup.ApplyFieldAdjustmentCore(paths);

            var contents = Object.FindObjectsByType<PaseoMapContent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var placed = contents.Single(c => c.MapId == 90689);
            var untouched = contents.Single(c => c.MapId == 90690);
            if (Vector3.Distance(placed.LocalPosition, new Vector3(1f, 2f, 3f)) > 1e-3f || Mathf.Abs(Mathf.DeltaAngle(placed.LocalYaw, 45f)) > 1e-2f || !placed.Placed)
                throw new InvalidOperationException($"La caja 90689 no quedó en (1,2,3) / 45° colocada: {placed.LocalPosition} / {placed.LocalYaw}.");
            if (untouched.Placed || Vector3.Distance(untouched.LocalPosition, new Vector3(9f, 9f, 9f)) < 1e-3f)
                throw new InvalidOperationException("La caja 90690 no se ajustó en terreno y no debía moverse.");
            if (Mathf.Abs(untouched.SizeMeters.x - 30f) > 1e-3f || !untouched.Box.Solid)
                throw new InvalidOperationException("El tamaño y el relleno son del edificio: también debían llegar a la caja 90690.");
            var config = PaseoConfig.Parse(File.ReadAllText(Path.Combine(paths.DataRoot, "EdificioB", PaseoConfig.FileName)));
            if (!config.mapas.Single(m => m.id == 90689).caja.colocada)
                throw new InvalidOperationException("edificio.json de EdificioB no guardó la caja colocada.");
            Debug.Log($"{Tag} Ajuste de campo aplicado a edificio.json y a la escena.");
        }

        // The core claim of the design: ImmersalSDK registers each map with its nearest parent ISceneUpdateable, so each
        // map must find its own, distinct XR Space.
        static void AssertOneSpacePerMap()
        {
            var maps = Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (maps.Length != 3)
                throw new InvalidOperationException($"Se esperaban 3 XR Map y hay {maps.Length}.");
            var parents = maps.Select(m => m.transform.GetComponentInParent<ISceneUpdateable>(true)).ToArray();
            if (parents.Any(p => p == null) || parents.Distinct().Count() != 3)
                throw new InvalidOperationException("Cada XR Map debe registrarse con su propio XR Space.");
            var contents = Object.FindObjectsByType<PaseoMapContent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (contents.Count(c => c.BuildingId == "EdificioB") != 2 || contents.Count(c => c.BuildingId == "EdificioA") != 1)
                throw new InvalidOperationException("EdificioB debe tener dos cajas (una por mapa) y EdificioA una.");
            foreach (var content in contents)
                if (content.GetComponentInParent<XRSpace>().GetComponentInChildren<XRMap>(true).mapId != content.MapId)
                    throw new InvalidOperationException($"La caja del mapa {content.MapId} no está en el XR Space de su mapa.");
        }
    }
}
#endif
