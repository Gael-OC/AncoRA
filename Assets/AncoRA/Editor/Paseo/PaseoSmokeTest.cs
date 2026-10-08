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
