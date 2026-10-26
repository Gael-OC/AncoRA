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
    /// two buildings, one of them with two maps (one XR Space, one box and one pose filter per building). Works in a
    /// temporary folder and scene that it deletes, and restores the Build Settings scene list. Proves the scene wiring,
    /// not localization.
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
                AssertOneSpacePerBuilding(paths);
                AssertValidateCatchesStaleScene(paths);
                ApplyAdjustmentAndCheck(paths);
                Debug.Log($"{Tag} OK: Prepare, Validate y carga nativa con 3 mapas en 2 XR Spaces (2 edificios). No prueba localización.");
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
            // A building with several maps needs the box placed in each (that is what relates the maps to one another).
            WriteBuilding("EdificioA", "Edificio A", source, false, "90687-SampleMapA.bytes");
            WriteBuilding("EdificioB", "Edificio B", source, true, "90689-SampleMapB.bytes", "90690-SampleMapC.bytes");
            AssetDatabase.Refresh();
        }

        static void WriteBuilding(string id, string name, string source, bool placed, params string[] files)
        {
            string folder = Path.Combine(Root, id);
            Directory.CreateDirectory(folder);
            var config = new PaseoBuildingConfig { nombre = name };
            foreach (string file in files)
            {
                File.Copy(Path.Combine(source, file), Path.Combine(folder, file));
                int index = config.mapas.Count;
                config.mapas.Add(new PaseoMapConfig
                {
                    id = PaseoConfig.IdFromFileName(file).Value,
                    archivo = file,
                    caja = new PaseoBoxPose { posicion = new[] { 2f - 5f * index, 0f, 3f + 4f * index }, giro = 30f + 70f * index, colocada = placed }
                });
            }
            File.WriteAllText(Path.Combine(folder, PaseoConfig.FileName), PaseoConfig.Serialize(config));
        }

        // edificio.json edited by hand without running Prepare: Validate (and so BuildAndroid) must refuse the old scene.
        static void AssertValidateCatchesStaleScene(PaseoPaths paths)
        {
            string file = Path.Combine(paths.DataRoot, "EdificioA", PaseoConfig.FileName);
            string original = File.ReadAllText(file);
            var config = PaseoConfig.Parse(original);
            config.tamano = new[] { 33f, 9f, 14f };
            config.mapas[0].caja.posicion = new[] { 4f, 0f, 20f };
            File.WriteAllText(file, PaseoConfig.Serialize(config));
            try
            {
                PaseoSetup.ValidateCore(paths);
                throw new InvalidOperationException("Validate aceptó una escena con cajas distintas a edificio.json.");
            }
            catch (InvalidOperationException e) when (e.Message.Contains("no coincide"))
            {
                Debug.Log($"{Tag} Validate detectó la escena desactualizada.");
            }
            finally
            {
                File.WriteAllText(file, original);
            }
            PaseoSetup.ValidateCore(paths);
        }

        static void ApplyAdjustmentAndCheck(PaseoPaths paths)
        {
            string file = Path.Combine(paths.DataRoot, "EdificioB", PaseoConfig.FileName);
            var before = PaseoConfig.Parse(File.ReadAllText(file));
            var relationBefore = PaseoBuildingFrame.MapTransforms(before);
            var adjustment = new PaseoAdjustment { generado = "prueba" };
            adjustment.edificios.Add(new PaseoAdjustmentBuilding
            {
                id = "EdificioB", referencia = 90689, tamano = new[] { 30f, 10f, 15f }, solido = true,
                posicion = new[] { 1f, 2f, 3f }, giro = 45f, colocada = true
            });
            File.WriteAllText(paths.AdjustmentFile, JsonUtility.ToJson(adjustment));
            AssetDatabase.Refresh();

            PaseoSetup.ApplyFieldAdjustmentCore(paths);

            var contents = Object.FindObjectsByType<PaseoMapContent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var content = contents.Single(c => c.BuildingId == "EdificioB");
            if (Vector3.Distance(content.LocalPosition, new Vector3(1f, 2f, 3f)) > 1e-3f || Mathf.Abs(Mathf.DeltaAngle(content.LocalYaw, 45f)) > 1e-2f || !content.Placed)
                throw new InvalidOperationException($"La caja de EdificioB no quedó en (1,2,3) / 45° colocada: {content.LocalPosition} / {content.LocalYaw}.");
            if (Mathf.Abs(content.SizeMeters.x - 30f) > 1e-3f || !content.Box.Solid)
                throw new InvalidOperationException("El tamaño y el relleno son del edificio y debían llegar a su caja.");
            var after = PaseoConfig.Parse(File.ReadAllText(file));
            var reference = after.mapas.Single(m => m.id == 90689).caja;
            if (Vector3.Distance(new Vector3(reference.posicion[0], reference.posicion[1], reference.posicion[2]), new Vector3(1f, 2f, 3f)) > 1e-3f)
                throw new InvalidOperationException("edificio.json de EdificioB no guardó la caja nueva en el mapa de referencia.");
            // Moving the building box moves the box of the other map the same way: the maps keep their place in the building.
            var relationAfter = PaseoBuildingFrame.MapTransforms(after);
            for (int i = 0; i < relationAfter.Length; i++)
            {
                PaseoBuildingFrame.Decompose(relationBefore[i], out var p0, out float y0);
                PaseoBuildingFrame.Decompose(relationAfter[i], out var p1, out float y1);
                if (Vector3.Distance(p0, p1) > 1e-3f || Mathf.Abs(Mathf.DeltaAngle(y0, y1)) > 1e-2f)
                    throw new InvalidOperationException($"El mapa {after.mapas[i].id} cambió de lugar dentro del edificio al aplicar el ajuste.");
            }
            Debug.Log($"{Tag} Ajuste de campo aplicado a edificio.json y a la escena.");
        }

        // The core claim of the design: ImmersalSDK registers each map with its nearest parent ISceneUpdateable, so the
        // two maps of a building find the same XR Space and the other building finds its own.
        static void AssertOneSpacePerBuilding(PaseoPaths paths)
        {
            var maps = Object.FindObjectsByType<XRMap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (maps.Length != 3)
                throw new InvalidOperationException($"Se esperaban 3 XR Map y hay {maps.Length}.");
            var parents = maps.Select(m => m.transform.GetComponentInParent<ISceneUpdateable>(true)).ToArray();
            if (parents.Any(p => p == null) || parents.Distinct().Count() != 2)
                throw new InvalidOperationException("Los 3 XR Map deben repartirse en 2 XR Spaces, uno por edificio.");
            var contents = Object.FindObjectsByType<PaseoMapContent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (contents.Length != 2)
                throw new InvalidOperationException($"Se esperaban 2 cajas (una por edificio) y hay {contents.Length}.");
            foreach (var content in contents)
            {
                var inSpace = content.GetComponentInParent<XRSpace>().GetComponentsInChildren<XRMap>(true).Select(m => m.mapId).OrderBy(id => id);
                if (!inSpace.SequenceEqual(content.MapIds.OrderBy(id => id)))
                    throw new InvalidOperationException($"La caja de {content.BuildingId} no está en el XR Space de sus mapas.");
            }
            // Seen from the second map of EdificioB, its box must land on the box of the reference map.
            var config = PaseoConfig.Parse(File.ReadAllText(Path.Combine(paths.DataRoot, "EdificioB", PaseoConfig.FileName)));
            var second = maps.Single(m => m.mapId == 90690);
            var relation = Matrix4x4.TRS(second.transform.localPosition, second.transform.localRotation, Vector3.one);
            var box = config.mapas[1].caja;
            PaseoBuildingFrame.Decompose(relation * PaseoBuildingFrame.BoxMatrix(new Vector3(box.posicion[0], box.posicion[1], box.posicion[2]), box.giro),
                out var seenPosition, out float seenYaw);
            var content2 = contents.Single(c => c.BuildingId == "EdificioB");
            if (Vector3.Distance(seenPosition, content2.LocalPosition) > 1e-3f || Mathf.Abs(Mathf.DeltaAngle(seenYaw, content2.LocalYaw)) > 1e-2f)
                throw new InvalidOperationException("La caja del segundo mapa de EdificioB no cae sobre la caja del edificio.");
        }
    }
}
#endif
