#if UNITY_EDITOR
using UnityEditor;

namespace AncorRA.Editor
{
    /// <summary>
    /// Entry points of the Teologia demo: two Immersal maps of the same building (151714 and 151716, chosen from an
    /// in-app menu: map 1, map 2 or both) and one editable 3D box that stands in for the building, to judge whether the
    /// phone localizes and the box stays in place. It reuses the building pilot automation; the two-map building pilot
    /// and the Fuente are untouched. Nothing here proves physical alignment.
    /// </summary>
    public static class ImmersalTeologiaDemoSetup
    {
        internal static readonly ImmersalEdificioPilotSetup.Paths Teologia = new ImmersalEdificioPilotSetup.Paths
        {
            Scene = "Assets/Scenes/ImmersalTeologiaDemo.unity",
            DataFolder = "Assets/AncoRA/ImmersalTeologia",
            // Two maps (151714 and 151716) behind a map selector menu; each map alone or both.
            MapSelector = true,
            ShowStatusBanner = true,
            HudVisibleAtStart = true,
            KeepVisible = true,
            // Development player: Debug.Log reaches logcat, so the localization attempts can be read with adb.
            DevelopmentBuild = true,
            MeasurementsName = "teologia-medidas.json",
            BuildIdSuffix = ".teologia",
            ProductSuffix = " Teologia"
        };

        const string AndroidApk = "Builds/Android/ImmersalTeologiaDemo.apk";

        [MenuItem("AncoRA/Immersal/Teologia/Comprobar datos de entrada")]
        public static void CheckInputs() => ImmersalEdificioPilotSetup.CheckInputsCore(Teologia);

        [MenuItem("AncoRA/Immersal/Teologia/Preparar escena de la demo")]
        public static void Prepare() => ImmersalEdificioPilotSetup.PrepareCore(Teologia);

        [MenuItem("AncoRA/Immersal/Teologia/Validar demo")]
        public static void Validate() => ImmersalEdificioPilotSetup.ValidateCore(Teologia);

        [MenuItem("AncoRA/Immersal/Teologia/Comprobar carga nativa del mapa")]
        public static void CheckNativeMapsInEditor() => ImmersalEdificioPilotSetup.CheckNativeMapsCore(Teologia);

        [MenuItem("AncoRA/Immersal/Teologia/Aplicar medidas de la caja desde el JSON")]
        public static void ApplyMeasurements() => ImmersalEdificioPilotSetup.ApplyMeasurementsCore(Teologia);

        [MenuItem("AncoRA/Immersal/Teologia/Aplicar ajuste de campo desde ajuste-campo.json")]
        public static void ApplyFieldAdjustment() => ImmersalEdificioPilotSetup.ApplyFieldAdjustmentCore(Teologia);

        public static void BuildAndroid() => ImmersalEdificioPilotSetup.BuildCore(Teologia, AndroidApk, BuildTarget.Android);
    }
}
#endif
