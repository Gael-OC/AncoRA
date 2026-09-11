#if UNITY_EDITOR
using AncorRA.AR;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.Internal;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.Editor
{
    public static class GeospatialPilotSetup
    {
        const string k_ConfigPath = "Assets/Settings/AncoRAGeospatialConfig.asset";
        const string k_SitePath = "Assets/Settings/HouseGeospatialSite.asset";
        const string k_ScenePath = "Assets/Scenes/GeospatialPilot.unity";

        [MenuItem("AncoRA/Crear escena limpia Geospatial")]
        public static void CreateCleanScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sessionObject = new GameObject("AR Session");
            sessionObject.AddComponent<ARInputManager>();
            var session = sessionObject.AddComponent<ARSession>();

            var originObject = new GameObject("XR Origin");
            var origin = originObject.AddComponent<XROrigin>();
            origin.Origin = originObject;

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform, false);
            origin.CameraFloorOffsetObject = cameraOffset;
            origin.CameraYOffset = 0f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1000f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var cameraManager = cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();

            var poseDriver = cameraObject.AddComponent<TrackedPoseDriver>();
            poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            poseDriver.ignoreTrackingState = false;
            var positionAction = new InputAction(
                "Device Position", InputActionType.PassThrough,
                "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            poseDriver.positionInput = new InputActionProperty(positionAction);

            var rotationAction = new InputAction(
                "Device Rotation", InputActionType.PassThrough,
                "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            poseDriver.rotationInput = new InputActionProperty(rotationAction);
            poseDriver.trackingStateInput = new InputActionProperty(new InputAction(
                "Tracking State", InputActionType.PassThrough,
                "<XRHMD>/trackingState", expectedControlType: "Integer"));
            origin.Camera = camera;

            var anchorManager = originObject.AddComponent<ARAnchorManager>();
            var earthManager = originObject.AddComponent<AREarthManager>();
            var probe = originObject.AddComponent<GeospatialVpsProbe>();

            var extensionsObject = new GameObject("ARCore Extensions");
            var extensions = extensionsObject.AddComponent<ARCoreExtensions>();
            extensions.Session = session;
            extensions.Origin = origin;
            extensions.CameraManager = cameraManager;
            extensions.ARCoreExtensionsConfig = LoadOrCreateConfig();
            probe.Configure(extensions, earthManager, anchorManager, LoadOrCreateSite());

            ConfigureProjectSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, k_ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(k_ScenePath, true) };
            AssetDatabase.SaveAssets();

            Selection.activeObject = probe;
            Debug.Log($"Escena Geospatial mínima creada y puesta en Build Settings: {k_ScenePath}");
        }

        [MenuItem("AncoRA/Configurar piloto Geospatial")]
        public static void ConfigureActiveScene()
        {
            var session = Object.FindAnyObjectByType<ARSession>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var cameraManager = Object.FindAnyObjectByType<ARCameraManager>();
            var anchorManager = Object.FindAnyObjectByType<ARAnchorManager>();

            if (session == null || origin == null || cameraManager == null || anchorManager == null)
            {
                Debug.LogError("No se encontró ARSession, XROrigin, ARCameraManager o ARAnchorManager.");
                return;
            }

            var config = LoadOrCreateConfig();
            var site = LoadOrCreateSite();

            var extensions = Object.FindAnyObjectByType<ARCoreExtensions>();
            if (extensions == null)
            {
                var extensionsObject = new GameObject("ARCore Extensions");
                Undo.RegisterCreatedObjectUndo(extensionsObject, "Create ARCore Extensions");
                extensions = extensionsObject.AddComponent<ARCoreExtensions>();
            }

            Undo.RecordObject(extensions, "Configure ARCore Extensions");
            extensions.Session = session;
            extensions.Origin = origin;
            extensions.CameraManager = cameraManager;
            extensions.ARCoreExtensionsConfig = config;

            var earthManager = origin.GetComponent<AREarthManager>();
            if (earthManager == null)
                earthManager = Undo.AddComponent<AREarthManager>(origin.gameObject);

            var probe = origin.GetComponent<GeospatialVpsProbe>();
            if (probe == null)
                probe = Undo.AddComponent<GeospatialVpsProbe>(origin.gameObject);
            Undo.RecordObject(probe, "Configure Geospatial VPS probe");
            probe.Configure(extensions, earthManager, anchorManager, site);

            ConfigureProjectSettings();

            EditorUtility.SetDirty(extensions);
            EditorUtility.SetDirty(earthManager);
            EditorUtility.SetDirty(probe);
            EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Selection.activeObject = site;
            Debug.Log(
                "Piloto Geospatial configurado. Falta habilitar ARCore API/keyless en Google Cloud " +
                "e ingresar latitud, longitud y heading levantados en HouseGeospatialSite.asset.");
        }

        static ARCoreExtensionsConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ARCoreExtensionsConfig>(k_ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ARCoreExtensionsConfig>();
                AssetDatabase.CreateAsset(config, k_ConfigPath);
            }

            config.GeospatialMode = GeospatialMode.Enabled;
            config.CloudAnchorMode = CloudAnchorMode.Disabled;
            config.SemanticMode = SemanticMode.Disabled;
            config.StreetscapeGeometryMode = StreetscapeGeometryMode.Disabled;
            EditorUtility.SetDirty(config);
            return config;
        }

        static GeospatialSiteProfile LoadOrCreateSite()
        {
            var site = AssetDatabase.LoadAssetAtPath<GeospatialSiteProfile>(k_SitePath);
            if (site != null)
                return site;

            site = ScriptableObject.CreateInstance<GeospatialSiteProfile>();
            AssetDatabase.CreateAsset(site, k_SitePath);
            return site;
        }

        static void ConfigureProjectSettings()
        {
            var settings = ARCoreExtensionsProjectSettings.Instance;
            settings.GeospatialEnabled = true;
            settings.AndroidAuthenticationStrategySetting = AndroidAuthenticationStrategy.Keyless;
            settings.Save();

            PlayerSettings.Android.forceInternetPermission = true;
            const string locationReason =
                "AncoRA usa tu ubicación precisa para alinear contenido de realidad aumentada con el lugar.";
            PlayerSettings.iOS.locationUsageDescription = locationReason;
        }
    }
}
#endif
