using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Compilacion del APK de AncoRA desde linea de comandos.
///   Unity.exe -quit -batchmode -projectPath . -executeMethod BuildAndroid.Build -logFile build.log
/// </summary>
public static class BuildAndroid
{
    const string OutputDir = "Builds/Android";

    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new Exception("No hay escenas habilitadas en Build Settings.");

        // ARCore exige IL2CPP + ARM64.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // APK suelto para instalar por USB, no un App Bundle de Play Store.
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;

        Directory.CreateDirectory(OutputDir);
        var apkPath = Path.Combine(OutputDir, "AncoRA.apk");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        Debug.Log($"[BuildAndroid] Compilando {scenes.Length} escena(s) -> {apkPath}");

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildAndroid] OK: {summary.outputPath} " +
                      $"({summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalMinutes:F1} min)");
            EditorApplication.Exit(0);
        }
        else
        {
            foreach (var step in report.steps)
                foreach (var msg in step.messages.Where(m => m.type is LogType.Error or LogType.Exception))
                    Debug.LogError($"[BuildAndroid] {step.name}: {msg.content}");

            Debug.LogError($"[BuildAndroid] FALLO: {summary.result}, {summary.totalErrors} error(es)");
            EditorApplication.Exit(1);
        }
    }
}
