using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace AncorRA.AR
{
    /// <summary>
    /// TEMPORARY. Logs XR startup state at every RuntimeInitializeOnLoad stage.
    ///
    /// ARCore registers its session subsystem descriptor from a
    /// [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] guarded by Api.loaderPresent, which is
    /// a cached static evaluated exactly once and derived from XRGeneralSettings.Instance. That
    /// instance is only assigned in the preloaded asset's Awake. If registration runs before that
    /// Awake, the guard caches false forever, no descriptor is ever registered, and ARCoreLoader
    /// later reports "Failed to load session subsystem" - no session, no camera, no permission
    /// prompt, black screen.
    ///
    /// This tells us at which stage the settings, the manager and the descriptor actually appear.
    /// Delete once the cause is confirmed.
    /// </summary>
    static class XrStartupDiagnostics
    {
        const string k_Tag = "ANCORA-DIAG";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnSubsystemRegistration() => Report("SubsystemRegistration");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void OnAfterAssembliesLoaded() => Report("AfterAssembliesLoaded");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void OnBeforeSplashScreen() => Report("BeforeSplashScreen");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad() => Report("BeforeSceneLoad");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad() => Report("AfterSceneLoad");

        static void Report(string stage)
        {
            var settings = XRGeneralSettings.Instance;
            var manager = settings != null ? settings.Manager : null;

            var loaders = "n/a";
            if (manager != null && manager.activeLoaders != null)
            {
                var names = new List<string>();
                foreach (var loader in manager.activeLoaders)
                    names.Add(loader == null ? "<null>" : loader.GetType().Name);
                loaders = names.Count == 0 ? "<vacio>" : string.Join(" ", names);
            }

            var descriptors = new List<XRSessionSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(descriptors);
            var ids = new List<string>();
            foreach (var descriptor in descriptors)
                ids.Add(descriptor.id);

            Debug.Log(
                $"{k_Tag} [{stage}] settings={settings != null} manager={manager != null} " +
                $"loaders=[{loaders}] sessionDescriptors=[{(ids.Count == 0 ? "<vacio>" : string.Join(" ", ids))}]");
        }
    }
}
