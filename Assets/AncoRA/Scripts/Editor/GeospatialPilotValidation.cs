#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AncorRA.AR;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.Internal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.Editor
{
    /// <summary>Deterministic checks for the Geospatial pilot that can run in Unity batch mode.</summary>
    public static class GeospatialPilotValidation
    {
        const string k_ScenePath = "Assets/Scenes/GeospatialPilot.unity";
        const string k_SitePath = "Assets/Settings/HouseGeospatialSite.asset";

        [MenuItem("AncoRA/Validar piloto Geospatial")]
        public static void ValidateFromCommandLine()
        {
            var errors = new List<string>();
            ValidateProjectSettings(errors);
            ValidateBuildScene(errors);
            ValidateSceneAndProfile(errors);
            ValidateDeterministicHelpers(errors);
            ValidateDiagnosticShader(errors);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Debug.LogError($"[AncoRA Geospatial Validation] {error}");
                throw new InvalidOperationException(
                    $"Geospatial pilot validation failed with {errors.Count} error(s).");
            }

            Debug.Log("[AncoRA Geospatial Validation] PASS");
        }

        static void ValidateProjectSettings(List<string> errors)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var playerSettingsPath = projectRoot == null
                ? string.Empty
                : Path.Combine(projectRoot, "ProjectSettings/ProjectSettings.asset");
            if (!File.Exists(playerSettingsPath))
            {
                errors.Add("No se encontró ProjectSettings/ProjectSettings.asset.");
            }
            else
            {
                var settingsText = File.ReadAllText(playerSettingsPath);
                if (!settingsText.Split('\n').Any(line => line.Trim() == "activeInputHandler: 2"))
                    errors.Add("Active Input Handling debe estar en Both (activeInputHandler: 2).");
            }

            var extensionsSettings = ARCoreExtensionsProjectSettings.Instance;
            if (!extensionsSettings.GeospatialEnabled)
                errors.Add("ARCore Extensions no tiene Geospatial habilitado.");
            if (!extensionsSettings.IsIOSSupportEnabled)
                errors.Add("ARCore Extensions no tiene soporte iOS habilitado.");
        }

        static void ValidateBuildScene(List<string> errors)
        {
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (enabledScenes.Length != 1 || enabledScenes[0].path != k_ScenePath)
                errors.Add($"La única escena habilitada debe ser {k_ScenePath}.");
        }

        static void ValidateSceneAndProfile(List<string> errors)
        {
            if (!File.Exists(k_ScenePath))
            {
                errors.Add($"No se encontró {k_ScenePath}.");
                return;
            }

            EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
            var probe = UnityEngine.Object.FindAnyObjectByType<GeospatialVpsProbe>(
                FindObjectsInactive.Include);
            if (probe == null)
            {
                errors.Add("La escena no contiene GeospatialVpsProbe.");
                return;
            }

            var serializedProbe = new SerializedObject(probe);
            ValidateReference(serializedProbe, "m_Extensions", errors);
            ValidateReference(serializedProbe, "m_EarthManager", errors);
            ValidateReference(serializedProbe, "m_AnchorManager", errors);
            ValidateReference(serializedProbe, "m_Site", errors);

            if (UnityEngine.Object.FindAnyObjectByType<ARSession>(FindObjectsInactive.Include) == null)
                errors.Add("La escena no contiene ARSession.");
            if (UnityEngine.Object.FindAnyObjectByType<AREarthManager>(FindObjectsInactive.Include) == null)
                errors.Add("La escena no contiene AREarthManager.");
            if (UnityEngine.Object.FindAnyObjectByType<ARAnchorManager>(FindObjectsInactive.Include) == null)
                errors.Add("La escena no contiene ARAnchorManager.");
            var mainCamera = Camera.main;
            if (mainCamera == null || mainCamera.GetComponent<ARCameraManager>() == null ||
                mainCamera.GetComponent<ARCameraBackground>() == null)
            {
                errors.Add("La Main Camera debe contener ARCameraManager y ARCameraBackground.");
            }
            else
            {
                var poseDriver = mainCamera.GetComponent<TrackedPoseDriver>();
                if (poseDriver == null)
                {
                    errors.Add("La Main Camera debe contener TrackedPoseDriver.");
                }
                else
                {
                    ValidateInputBinding(
                        poseDriver.positionInput.action,
                        "<XRHMD>/centerEyePosition",
                        "posición XR HMD",
                        errors);
                    ValidateInputBinding(
                        poseDriver.positionInput.action,
                        "<HandheldARInputDevice>/devicePosition",
                        "posición AR móvil",
                        errors);
                    ValidateInputBinding(
                        poseDriver.rotationInput.action,
                        "<XRHMD>/centerEyeRotation",
                        "rotación XR HMD",
                        errors);
                    ValidateInputBinding(
                        poseDriver.rotationInput.action,
                        "<HandheldARInputDevice>/deviceRotation",
                        "rotación AR móvil",
                        errors);
                }
            }

            var site = AssetDatabase.LoadAssetAtPath<GeospatialSiteProfile>(k_SitePath);
            if (site == null)
            {
                errors.Add($"No se encontró el perfil {k_SitePath}.");
                return;
            }

            if (!site.HasCoordinates)
                errors.Add("El perfil del sitio no contiene coordenadas válidas.");
            if (site.SizeMeters.x <= 0f || site.SizeMeters.y <= 0f || site.SizeMeters.z <= 0f)
                errors.Add("El volumen del perfil debe tener dimensiones positivas.");
        }

        static void ValidateInputBinding(
            InputAction action,
            string expectedPath,
            string label,
            List<string> errors)
        {
            if (action == null || !action.bindings.Any(binding => binding.path == expectedPath))
                errors.Add($"TrackedPoseDriver no contiene el binding requerido para {label}: {expectedPath}.");
        }

        static void ValidateReference(
            SerializedObject serializedObject,
            string propertyName,
            List<string> errors)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
                errors.Add($"Falta la referencia serializada {propertyName} en GeospatialVpsProbe.");
        }

        static void ValidateDeterministicHelpers(List<string> errors)
        {
            GeospatialVpsProbe.CalculateDistanceAndBearing(
                -29.9642065,
                -71.3494378,
                -29.96410422,
                -71.34963299,
                out var distance,
                out var bearing);
            if (distance < 20d || distance > 24d || bearing < 300d || bearing > 302d)
                errors.Add($"Distancia/rumbo inesperados: {distance:F2} m, {bearing:F2} grados.");

            var mesh = HouseMeshBuilder.Build(new Vector3(18f, 5f, 10f), 1.5f, true);
            try
            {
                if (Vector3.Distance(mesh.bounds.size, new Vector3(18f, 5f, 10f)) > 0.01f)
                    errors.Add($"Bounds inesperados en la malla: {mesh.bounds.size}.");
                if (Mathf.Abs(mesh.bounds.min.y) > 0.001f)
                    errors.Add($"La base de la malla debe estar en y=0, no {mesh.bounds.min.y:F3}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        static void ValidateDiagnosticShader(List<string> errors)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null || !shader.isSupported)
                errors.Add("El shader Universal Render Pipeline/Unlit no está disponible.");
        }
    }
}
#endif
