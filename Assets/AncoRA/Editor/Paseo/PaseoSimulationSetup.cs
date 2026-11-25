using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AncorRA.AR;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace AncorRA.Editor
{
    /// <summary>
    /// Editor side of the simulated view (PaseoSimulation): the XR Simulation environment with a gray block per building, and
    /// the switch that turns the view on or off. Turning it on assigns AR Foundation's SimulationLoader to the Standalone
    /// XR settings (the Editor's Play mode uses those) and sets the flag PaseoSimulation reads; turning it off undoes both.
    /// The Android loaders are never touched.
    /// <para>
    /// The environment lives in Assets/AncoRA/Paseo/_Simulacion/EntornoCampus.prefab. Its blocks stand in the campus frame at
    /// the «campus» pose and «tamano» of each edificio.json, the same frame as the scene's XR Space at the origin, so the
    /// authored content and the blocks coincide. To swap the blocks for the real drone scans, replace the children named
    /// «Edificio …» of that prefab (or the whole environment) and keep its SimulationEnvironment component.
    /// </para>
    /// </summary>
    public static class PaseoSimulationSetup
    {
        const string Tag = "[AncoRA Paseo]";
        public const string FolderPath = "Assets/AncoRA/Paseo/_Simulacion";
        public const string EnvironmentPath = FolderPath + "/EntornoCampus.prefab";
        const string GroundMaterialPath = FolderPath + "/Suelo.mat";
        const string BlockMaterialPath = FolderPath + "/Bloque.mat";
        const string PreferencesPath = "Assets/XR/UserSimulationSettings/Resources/XRSimulationPreferences.asset";
        // SimulationEnvironment, XRSimulationPreferences and the assets manager are internal to AR Foundation: reached by name.
        const string EnvironmentTypeName = "UnityEngine.XR.Simulation.SimulationEnvironment, Unity.XR.Simulation";
        const string ManagerTypeName = "UnityEditor.XR.Simulation.SimulationEnvironmentAssetsManager, Unity.XR.Simulation.Editor";
        const string LoaderTypeName = "UnityEngine.XR.Simulation.SimulationLoader";
        /// <summary>The block of a building is a little smaller than its box, so the model shows over it.</summary>
        public const float BlockShrink = 0.97f;
        public const string BlockPrefix = "Edificio ";
        public const string DefaultBuildingId = "Teologia";
        const float GroundHalfSizeMeters = 400f;
        const float BoundsMarginMeters = 80f;

        // ---------------------------------------------------------------- environment

        /// <summary>
        /// Builds the environment as a scene object (not saved): a ground, a light and one gray block per building. The
        /// camera starts <paramref name="startPose"/>, and may move within <paramref name="movementBounds"/>. Materials may be null.
        /// </summary>
        public static GameObject BuildEnvironmentObject(IReadOnlyList<PaseoSimBuilding> buildings, float groundY, Pose startPose,
            Bounds movementBounds, Material groundMaterial, Material blockMaterial)
        {
            var root = new GameObject("EntornoCampus");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Suelo";
            ground.transform.SetParent(root.transform, false);
            var centre = buildings.Count == 0 ? Vector3.zero : buildings.Aggregate(Vector3.zero, (sum, b) => sum + b.Center) / buildings.Count;
            ground.transform.localPosition = new Vector3(centre.x, groundY - 0.1f, centre.z);
            ground.transform.localScale = new Vector3(GroundHalfSizeMeters * 2f, 0.2f, GroundHalfSizeMeters * 2f);
            if (groundMaterial != null)
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            foreach (var building in buildings)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = BlockPrefix + building.Id;
                block.transform.SetParent(root.transform, false);
                block.transform.localPosition = building.Center;
                block.transform.localRotation = Quaternion.Euler(0f, building.Yaw, 0f);
                block.transform.localScale = building.Size * BlockShrink;
                if (blockMaterial != null)
                    block.GetComponent<Renderer>().sharedMaterial = blockMaterial;
            }

            var lightObject = new GameObject("Luz");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
            // A fill light from the other side, so no face is left black (Teología's front looks NW, away from the key light).
            var fillObject = new GameObject("Luz de relleno");
            fillObject.transform.SetParent(root.transform, false);
            fillObject.transform.localRotation = Quaternion.Euler(35f, 150f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.8f;
            fill.shadows = LightShadows.None;

            var type = Type.GetType(EnvironmentTypeName);
            if (type == null)
            {
                Debug.LogWarning($"{Tag} SIMULACION no se encontró {EnvironmentTypeName}: el entorno queda sin SimulationEnvironment.");
                return root;
            }
            var environment = root.AddComponent(type);
            var fields = new SerializedObject(environment);
            SetPose(fields.FindProperty("m_CameraStartingPose"), startPose);
            SetPose(fields.FindProperty("m_DefaultViewPose"), startPose);
            var bounds = fields.FindProperty("m_CameraMovementBounds");
            if (bounds != null)
                bounds.boundsValue = movementBounds;
            var pivot = fields.FindProperty("m_DefaultViewPivot");
            if (pivot != null)
                pivot.vector3Value = centre;
            var size = fields.FindProperty("m_DefaultViewSize");
            if (size != null)
                size.floatValue = 60f;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static void SetPose(SerializedProperty pose, Pose value)
        {
            if (pose == null)
                return;
            var position = pose.FindPropertyRelative("position");
            var rotation = pose.FindPropertyRelative("rotation");
            if (position != null)
                position.vector3Value = value.position;
            if (rotation != null)
                rotation.quaternionValue = value.rotation;
        }

        /// <summary>
        /// Where the camera may go: every viewpoint at 15–50 m in front of each building, with a margin. The simulation
        /// pulls the camera back inside, so the hotkey viewpoints must lie within.
        /// </summary>
        public static Bounds MovementBounds(IReadOnlyList<PaseoSimBuilding> buildings, float groundY)
        {
            var bounds = new Bounds(new Vector3(0f, groundY + PaseoSimulationGeometry.EyeHeightMeters, 0f), Vector3.one);
            bool first = true;
            foreach (var building in buildings)
                foreach (float distance in new[] { PaseoSimulationGeometry.NearDistanceMeters, PaseoSimulationGeometry.FarDistanceMeters })
                {
                    var position = PaseoSimulationGeometry.Viewpoint(building, distance, groundY).position;
                    if (first)
                        bounds = new Bounds(position, Vector3.zero);
                    else
                        bounds.Encapsulate(position);
                    first = false;
                    bounds.Encapsulate(building.Center);
                }
            bounds.Expand(new Vector3(BoundsMarginMeters * 2f, 0f, BoundsMarginMeters * 2f));
            // From the ground to well above the roofs.
            bounds.Encapsulate(new Vector3(bounds.center.x, groundY + 0.2f, bounds.center.z));
            bounds.Encapsulate(new Vector3(bounds.center.x, groundY + 60f, bounds.center.z));
            return bounds;
        }

        /// <summary>The viewpoint of the environment's starting camera: 25 m in front of Teología (the demo), or the first building.</summary>
        public static Pose StartPose(IReadOnlyList<PaseoSimBuilding> buildings, float groundY)
        {
            if (buildings.Count == 0)
                return new Pose(new Vector3(0f, groundY + PaseoSimulationGeometry.EyeHeightMeters, 0f), Quaternion.identity);
            var demo = buildings.FirstOrDefault(b => b.Id == DefaultBuildingId);
            if (demo.Id == null)
                demo = buildings[0];
            float distance = PaseoSimulationGeometry.ClearDistance(demo, PaseoSimulationGeometry.DefaultDistanceMeters, buildings);
            return PaseoSimulationGeometry.Viewpoint(demo, distance, groundY);
        }

        [MenuItem("AncoRA/Paseo/Simulación/Generar entorno simulado")]
        public static void GenerateEnvironment()
        {
            var buildings = PaseoSimulationGeometry.LoadBuildings(PaseoSimulationGeometry.DataRoot);
            if (buildings.Count == 0)
                throw new InvalidOperationException("Ningún edificio de Assets/AncoRA/Paseo tiene «campus» en edificio.json; no hay nada que simular.");
            float groundY = PaseoSimulationGeometry.GroundY(buildings);
            Directory.CreateDirectory(FolderPath);
            AssetDatabase.Refresh();
            var root = BuildEnvironmentObject(buildings, groundY, StartPose(buildings, groundY), MovementBounds(buildings, groundY),
                LoadOrCreateMaterial(GroundMaterialPath, new Color(0.42f, 0.46f, 0.38f)),
                LoadOrCreateMaterial(BlockMaterialPath, new Color(0.62f, 0.62f, 0.64f)));
            GameObject prefab;
            try
            {
                prefab = PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            SelectEnvironment(prefab);
            Debug.Log($"{Tag} SIMULACION entorno generado en {EnvironmentPath}: {buildings.Count} bloque(s) ({string.Join(", ", buildings.Select(b => b.Id))}), " +
                      $"suelo a {groundY:F2} m; la cámara arranca a {PaseoSimulationGeometry.DefaultDistanceMeters:F0} m de {DefaultBuildingId}.");
        }

        static Material LoadOrCreateMaterial(string path, Color color)
        {
            // The park's shader has its own fixed light: URP Lit came out black, because the simulated environment is drawn
            // in a scene of its own where URP finds no main light.
            var shader = Shader.Find(PaseoParkBuilder.ShaderName) ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Makes the prefab the one XR Simulation loads (XRSimulationPreferences.m_EnvironmentPrefab) and refreshes its list.
        static void SelectEnvironment(GameObject prefab)
        {
            var preferences = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PreferencesPath);
            if (preferences == null)
            {
                Debug.LogWarning($"{Tag} SIMULACION no se encontró {PreferencesPath}: elegir el entorno a mano en Overlays > XR Environment.");
                return;
            }
            var fields = new SerializedObject(preferences);
            var property = fields.FindProperty("m_EnvironmentPrefab");
            if (property == null)
            {
                Debug.LogWarning($"{Tag} SIMULACION XRSimulationPreferences ya no tiene m_EnvironmentPrefab: elegir el entorno a mano en Overlays > XR Environment.");
                return;
            }
            property.objectReferenceValue = prefab;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preferences);
            AssetDatabase.SaveAssets();
            try
            {
                // The assets manager also notices new prefabs on import; this makes sure its list is current.
                var manager = Type.GetType(ManagerTypeName);
                var instance = manager?.BaseType?.GetProperty("Instance", System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null);
                manager?.GetMethod("CollectEnvironments")?.Invoke(instance, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} SIMULACION no se pudo refrescar la lista de entornos ({e.Message}); no afecta la selección.");
            }
        }

        // ---------------------------------------------------------------- on / off

        [MenuItem("AncoRA/Paseo/Simulación/Activar vista simulada")]
        public static void Activate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{Tag} SIMULACION: salir de Play antes de activar la vista simulada.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath) == null)
                GenerateEnvironment();
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings == null || settings.AssignedSettings == null)
            {
                Debug.LogError($"{Tag} SIMULACION: no hay XR Plug-in Management para Standalone; activar «XR Simulation» a mano en Project Settings > XR Plug-in Management.");
                return;
            }
            bool already = settings.AssignedSettings.activeLoaders.Any(l => l != null && l.GetType().FullName == LoaderTypeName);
            if (!already && !XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, LoaderTypeName, BuildTargetGroup.Standalone))
            {
                Debug.LogError($"{Tag} SIMULACION: no se pudo asignar {LoaderTypeName} a Standalone.");
                return;
            }
            PaseoSimulationSettings.Active = true;
            Debug.Log($"{Tag} SIMULACION vista simulada ACTIVADA: Play ya muestra la escena frente a un edificio ubicado (1–4 cambian de edificio). " +
                      "Quita el cargador con AncoRA/Paseo/Simulación/Desactivar vista simulada antes de volver a probar con el teléfono.");
        }

        [MenuItem("AncoRA/Paseo/Simulación/Desactivar vista simulada")]
        public static void Deactivate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{Tag} SIMULACION: salir de Play antes de desactivar la vista simulada.");
                return;
            }
            PaseoSimulationSettings.Active = false;
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings != null && settings.AssignedSettings != null &&
                settings.AssignedSettings.activeLoaders.Any(l => l != null && l.GetType().FullName == LoaderTypeName))
                XRPackageMetadataStore.RemoveLoader(settings.AssignedSettings, LoaderTypeName, BuildTargetGroup.Standalone);
            Debug.Log($"{Tag} SIMULACION vista simulada DESACTIVADA: Play vuelve a comportarse como antes.");
        }
    }
}
