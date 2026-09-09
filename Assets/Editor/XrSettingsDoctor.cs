using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace AncorRA.AR.EditorTools
{
    /// <summary>
    /// Inspects Assets/XR/XRGeneralSettings.asset for duplicated settings objects.
    ///
    /// Preloading one object out of a .asset loads every object in that file, so duplicated
    /// XRGeneralSettings objects all run Awake, and Awake assigns the static Instance - last one
    /// wins. If the winner is an orphan whose script type does not resolve, Instance is unusable,
    /// ARCore's Api.loaderPresent caches false, its session descriptor is never registered, and
    /// ARCoreLoader reports "Failed to load session subsystem": no session, no camera, no
    /// permission prompt.
    /// </summary>
    public static class XrSettingsDoctor
    {
        const string k_Path = "Assets/XR/XRGeneralSettings.asset";

        [MenuItem("AncoRA/Diagnosticar configuracion XR")]
        public static void Diagnose()
        {
            var report = new StringBuilder();
            report.AppendLine($"=== Objetos dentro de {k_Path} ===");

            var objects = AssetDatabase.LoadAllAssetsAtPath(k_Path);
            if (objects == null || objects.Length == 0)
            {
                Debug.LogError($"No pude cargar {k_Path}.");
                return;
            }

            var settingsByName = new Dictionary<string, int>();

            foreach (var obj in objects)
            {
                if (obj == null)
                {
                    report.AppendLine("  <objeto NULO: el tipo de script no resuelve>");
                    continue;
                }

                var kind = obj.GetType().Name;
                report.AppendLine($"  [{kind}] '{obj.name}'");

                if (obj is XRGeneralSettings settings)
                {
                    settingsByName.TryGetValue(obj.name, out var seen);
                    settingsByName[obj.name] = seen + 1;

                    var manager = settings.Manager;
                    if (manager == null)
                    {
                        report.AppendLine("      Manager: NULO");
                        continue;
                    }

                    var loaders = new List<string>();
                    if (manager.activeLoaders != null)
                    {
                        foreach (var loader in manager.activeLoaders)
                            loaders.Add(loader == null ? "<null>" : loader.GetType().Name);
                    }

                    report.AppendLine(
                        $"      Manager: '{manager.name}' initOnStart={settings.InitManagerOnStart} " +
                        $"loaders=[{(loaders.Count == 0 ? "vacio" : string.Join(" ", loaders))}]");
                }
            }

            report.AppendLine();
            report.AppendLine("=== Duplicados de XRGeneralSettings por nombre ===");
            var anyDuplicate = false;
            foreach (var pair in settingsByName)
            {
                report.AppendLine($"  '{pair.Key}': {pair.Value}");
                if (pair.Value > 1)
                    anyDuplicate = true;
            }

            report.AppendLine();
            report.AppendLine("=== Objeto que EditorBuildSettings tiene configurado ===");
            if (EditorBuildSettings.TryGetConfigObject(
                    XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) &&
                perTarget != null)
            {
                var android = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
                report.AppendLine(android == null
                    ? "  Android: NO hay settings asignados"
                    : $"  Android: '{android.name}' manager='{(android.Manager != null ? android.Manager.name : "NULO")}'");
            }
            else
            {
                report.AppendLine("  NO hay XRGeneralSettingsPerBuildTarget registrado en EditorBuildSettings.");
            }

            if (anyDuplicate)
            {
                report.AppendLine();
                report.AppendLine(
                    "VEREDICTO: hay objetos XRGeneralSettings duplicados. Al precargarse el asset " +
                    "despiertan todos y el ultimo gana la asignacion de Instance. Esa es la causa " +
                    "candidata de 'Failed to load session subsystem'.");
            }

            Debug.Log(report.ToString());
        }

        static readonly BuildTargetGroup[] k_Groups =
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
            BuildTargetGroup.WebGL,
            BuildTargetGroup.VisionOS,
        };

        /// <summary>
        /// Removes every XRGeneralSettings / XRManagerSettings object in the asset that the
        /// per-build-target dictionary does not reference.
        ///
        /// Only the referenced objects are reachable configuration; the rest are leftovers that
        /// still wake up with the asset and fight over the static Instance.
        /// </summary>
        [MenuItem("AncoRA/Reparar configuracion XR (quitar duplicados)")]
        public static void Repair()
        {
            if (!EditorBuildSettings.TryGetConfigObject(
                    XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) ||
                perTarget == null)
            {
                Debug.LogError("No hay XRGeneralSettingsPerBuildTarget registrado. No toco nada.");
                return;
            }

            var keep = new HashSet<Object> { perTarget };
            foreach (var group in k_Groups)
            {
                var settings = perTarget.SettingsForBuildTarget(group);
                if (settings == null)
                    continue;

                keep.Add(settings);
                if (settings.Manager != null)
                    keep.Add(settings.Manager);
            }

            var report = new StringBuilder();
            report.AppendLine("Objetos conservados:");
            foreach (var kept in keep)
                report.AppendLine($"  [{kept.GetType().Name}] '{kept.name}'");

            var removed = 0;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(k_Path))
            {
                if (obj == null || keep.Contains(obj))
                    continue;

                if (obj is not XRGeneralSettings && obj is not XRManagerSettings)
                    continue;

                report.AppendLine($"  QUITADO [{obj.GetType().Name}] '{obj.name}'");
                AssetDatabase.RemoveObjectFromAsset(obj);
                Object.DestroyImmediate(obj, true);
                removed++;
            }

            if (removed == 0)
            {
                Debug.Log("No habia duplicados que quitar.\n" + report);
                return;
            }

            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine($"Total quitados: {removed}");
            Debug.Log(report.ToString());

            Diagnose();
        }
    }
}
