namespace AncorRA.AR
{
    /// <summary>
    /// How a building with a replacement is drawn, cycled by the «Vista» button of the team's debug builds. The team checks
    /// the pieces one at a time: first that the box sits on the real building, then that the painted background hides it
    /// cleanly, and only then the virtual object.
    /// </summary>
    public enum PaseoViewMode
    {
        /// <summary>The real camera image with the yellow box over the building: mask and model hidden.</summary>
        Box,
        /// <summary>The mask alone: sky and ground painted over the building, with the occlusion, no model and no box.</summary>
        Background,
        /// <summary>The mask and the model in the building's place: the finished replacement.</summary>
        Object
    }

    /// <summary>What a building draws: its yellow box, the mask over the real building, and the model.</summary>
    public readonly struct PaseoViewParts
    {
        public readonly bool Box;
        public readonly bool Mask;
        public readonly bool Model;

        public PaseoViewParts(bool box, bool mask, bool model)
        {
            Box = box;
            Mask = mask;
            Model = model;
        }
    }

    public static class PaseoViewModes
    {
        /// <summary>
        /// The mode the app starts in. Development builds start on the box, so the team checks it first and moves on with the
        /// «Vista» button; a release build has no such button (PaseoDebug), so it starts on the finished replacement.
        /// </summary>
        public static PaseoViewMode AppStart => PaseoDebug.Enabled ? PaseoViewMode.Box : PaseoViewMode.Object;

        /// <summary>Box, then background, then object, then back to the box.</summary>
        public static PaseoViewMode Next(PaseoViewMode mode) => mode switch
        {
            PaseoViewMode.Box => PaseoViewMode.Background,
            PaseoViewMode.Background => PaseoViewMode.Object,
            _ => PaseoViewMode.Box
        };

        public static string Label(PaseoViewMode mode) => mode switch
        {
            PaseoViewMode.Background => "Fondo",
            PaseoViewMode.Object => "Objeto",
            _ => "Caja"
        };

        /// <summary>The text of the «Vista» button.</summary>
        public static string ButtonText(PaseoViewMode mode) => "Vista: " + Label(mode);

        /// <summary>The value of the DEPURACION_UI vista=… log line: the label in lower case.</summary>
        public static string LogName(PaseoViewMode mode) => Label(mode).ToLowerInvariant();

        /// <summary>
        /// What a building draws. Off screen it draws nothing. Without a replacement it is always its box, whatever the mode.
        /// The team sheet being open (adjusting) shows the box over every mode, so it can be aligned against the real building.
        /// </summary>
        public static PaseoViewParts PartsFor(PaseoViewMode mode, bool shown, bool adjusting, bool hasReplacement)
        {
            if (!shown)
                return new PaseoViewParts(false, false, false);
            if (!hasReplacement || adjusting)
                return new PaseoViewParts(true, false, false);
            return mode switch
            {
                PaseoViewMode.Background => new PaseoViewParts(false, true, false),
                PaseoViewMode.Object => new PaseoViewParts(false, true, true),
                _ => new PaseoViewParts(true, false, false)
            };
        }
    }
}
