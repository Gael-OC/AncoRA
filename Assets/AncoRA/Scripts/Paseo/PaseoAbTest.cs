namespace AncorRA.AR
{
    /// <summary>
    /// How the team's A/B tests of 1.3.6 (contrast enhancement, gravity solver) apply to an attempt: always the normal way,
    /// always the variant, or alternating attempt by attempt so both run under the same light and viewpoint.
    /// </summary>
    public enum PaseoAbMode { Normal, Variant, Alternate }

    /// <summary>What one attempt does under the tests: enhance the contrast of the image, use the gravity solver.</summary>
    public readonly struct PaseoAbChoice
    {
        public readonly bool Contrast;
        public readonly bool Gravity;

        public PaseoAbChoice(bool contrast, bool gravity)
        {
            Contrast = contrast;
            Gravity = gravity;
        }
    }

    public static class PaseoAbTest
    {
        /// <summary>
        /// The variants an attempt uses. Alternating contrast enhances the odd attempts. Alternating the solver alone gives
        /// the gravity solver to the odd ones too; when both alternate, the contrast goes with the parity of the attempt and
        /// the solver with the parity of half of it (1, 2, 3, 4 give the pairs enhanced+default, plain+gravity,
        /// enhanced+gravity, plain+default), so the four combinations stay balanced.
        /// </summary>
        public static PaseoAbChoice Choose(PaseoAbMode contrast, PaseoAbMode solver, int attempt)
        {
            bool odd = (attempt & 1) == 1;
            bool secondBit = ((attempt >> 1) & 1) == 1;
            bool both = contrast == PaseoAbMode.Alternate && solver == PaseoAbMode.Alternate;
            bool enhance = contrast == PaseoAbMode.Variant || (contrast == PaseoAbMode.Alternate && odd);
            bool gravity = solver == PaseoAbMode.Variant || (solver == PaseoAbMode.Alternate && (both ? secondBit : odd));
            return new PaseoAbChoice(enhance, gravity);
        }

        /// <summary>The mode after a press of the team sheet button: normal, variant, alternate, normal…</summary>
        public static PaseoAbMode Next(PaseoAbMode mode) => mode switch
        {
            PaseoAbMode.Normal => PaseoAbMode.Variant,
            PaseoAbMode.Variant => PaseoAbMode.Alternate,
            _ => PaseoAbMode.Normal
        };

        /// <summary>Name of the contrast mode on the button and in DEPURACION_AJUSTE.</summary>
        public static string ContrastName(PaseoAbMode mode) => mode switch
        {
            PaseoAbMode.Variant => "realzado",
            PaseoAbMode.Alternate => "alterno",
            _ => "normal"
        };

        /// <summary>Name of the solver mode on the button and in DEPURACION_AJUSTE.</summary>
        public static string SolverName(PaseoAbMode mode) => mode switch
        {
            PaseoAbMode.Variant => "gravedad",
            PaseoAbMode.Alternate => "alterno",
            _ => "normal"
        };
    }
}
