using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AncorRA.AR.EditorTools
{
    /// <summary>
    /// Adds the occlusion manager to the AR camera in the scene.
    ///
    /// This goes through the Unity API rather than editing the scene YAML by hand because the AR rig
    /// is a prefab instance: adding a component to it means registering an override on that instance,
    /// which the serializer knows how to write and a text edit gets subtly wrong.
    /// </summary>
    public static class AncoraOcclusionSetup
    {
        const string k_ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("AncoRA/Activar oclusión por profundidad en la cámara")]
        public static void Enable()
        {
            var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogError("No encontré un XROrigin en la escena.");
                return;
            }

            var camera = origin.Camera;
            if (camera == null)
            {
                Debug.LogError("El XROrigin no tiene cámara asignada.");
                return;
            }

            var manager = camera.GetComponent<AROcclusionManager>();
            var added = manager == null;
            if (added)
                manager = camera.gameObject.AddComponent<AROcclusionManager>();

            // Best asks the provider for the highest quality depth it offers. The provider falls back
            // on its own when the device cannot manage it, so this is a request and not an assumption
            // about the hardware.
            manager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
            manager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"AROcclusionManager {(added ? "agregado" : "ya estaba")} en '{camera.name}'.\n" +
                $"  requestedEnvironmentDepthMode = {manager.requestedEnvironmentDepthMode}\n" +
                $"  requestedOcclusionPreferenceMode = {manager.requestedOcclusionPreferenceMode}\n" +
                "Recuerda que el soporte real depende del dispositivo: revisa el log de " +
                "OcclusionSupportReporter al arrancar en el teléfono.");
        }
    }
}
