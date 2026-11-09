using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>The eras of the era selector. «Actualidad» draws no model: the real building is what the camera sees.</summary>
    public enum PaseoEpoch
    {
        Y1950 = 0,
        Y1980 = 1,
        Y2000 = 2,
        Actualidad = 3
    }

    /// <summary>
    /// Camera look of an era. The colour part goes to a URP volume (PaseoSetup builds one profile per era); grain is URP
    /// film grain; scratches, dust and flicker are drawn by the interface (PaseoFilmOverlay), driven by PaseoEpochFilter.
    /// </summary>
    public readonly struct PaseoEpochLook
    {
        // Channel mixer rows, in URP percent (identity = 100/0/0, 0/100/0, 0/0/100).
        public readonly Vector3 Red, Green, Blue;
        public readonly float Saturation, Contrast, PostExposure;
        public readonly Color ColorFilter;
        public readonly float Grain, GrainResponse;
        public readonly float Vignette, VignetteSmoothness;
        public readonly float Scratches, Flicker;

        public PaseoEpochLook(Vector3 red, Vector3 green, Vector3 blue, float saturation, float contrast, float postExposure,
            Color colorFilter, float grain, float grainResponse, float vignette, float vignetteSmoothness, float scratches, float flicker)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Saturation = saturation;
            Contrast = contrast;
            PostExposure = postExposure;
            ColorFilter = colorFilter;
            Grain = grain;
            GrainResponse = grainResponse;
            Vignette = vignette;
            VignetteSmoothness = vignetteSmoothness;
            Scratches = scratches;
            Flicker = flicker;
        }

        public bool IsNeutral => Grain <= 0f && Vignette <= 0f && Scratches <= 0f && Flicker <= 0f &&
                                 Red == new Vector3(100f, 0f, 0f) && Green == new Vector3(0f, 100f, 0f) && Blue == new Vector3(0f, 0f, 100f) &&
                                 Mathf.Approximately(Saturation, 0f) && Mathf.Approximately(Contrast, 0f) && Mathf.Approximately(PostExposure, 0f) &&
                                 ColorFilter == Color.white;
    }

    /// <summary>One moment of an era change (<see cref="PaseoEpochs.Evaluate"/>).</summary>
    public readonly struct PaseoEpochStep
    {
        /// <summary>1 = the leaving model is whole; it comes apart from the top down to 0.</summary>
        public readonly float OutReveal;
        /// <summary>0 = the arriving model is not there yet; it builds up from the ground to 1.</summary>
        public readonly float InReveal;
        /// <summary>0 = the leaving era's camera look, 1 = the arriving one.</summary>
        public readonly float FilterBlend;
        public readonly bool Done;

        public PaseoEpochStep(float outReveal, float inReveal, float filterBlend, bool done)
        {
            OutReveal = outReveal;
            InReveal = inReveal;
            FilterBlend = filterBlend;
            Done = done;
        }
    }

    public static class PaseoEpochs
    {
        public const int Count = 4;
        /// <summary>First run opens on the oldest era: showing the building as it was is the point of the app.</summary>
        public const PaseoEpoch Default = PaseoEpoch.Y1950;

        // The leaving model comes down, a short pause with the real building, then the new one goes up.
        public const float OutSeconds = 0.6f;
        public const float GapSeconds = 0.15f;
        public const float InSeconds = 1.3f;
        public const float TotalSeconds = OutSeconds + GapSeconds + InSeconds;

        public static readonly PaseoEpoch[] All = { PaseoEpoch.Y1950, PaseoEpoch.Y1980, PaseoEpoch.Y2000, PaseoEpoch.Actualidad };

        public static string Label(PaseoEpoch epoch) => epoch switch
        {
            PaseoEpoch.Y1950 => "1950",
            PaseoEpoch.Y1980 => "1980",
            PaseoEpoch.Y2000 => "2000",
            _ => "Actualidad"
        };

        /// <summary>Stable key for PlayerPrefs and logs.</summary>
        public static string Key(PaseoEpoch epoch) => epoch switch
        {
            PaseoEpoch.Y1950 => "1950",
            PaseoEpoch.Y1980 => "1980",
            PaseoEpoch.Y2000 => "2000",
            _ => "actualidad"
        };

        public static PaseoEpoch Parse(string key, PaseoEpoch fallback = Default)
        {
            foreach (var epoch in All)
                if (Key(epoch) == key)
                    return epoch;
            return fallback;
        }

        /// <summary>Where an era change is <paramref name="seconds"/> after it started. Without a leaving model the new one rises at once.</summary>
        public static PaseoEpochStep Evaluate(float seconds, bool hasOutgoing)
        {
            float t = Mathf.Max(0f, seconds);
            float inStart = hasOutgoing ? OutSeconds + GapSeconds : 0f;
            float total = inStart + InSeconds;
            float outReveal = hasOutgoing ? 1f - Smooth(t / OutSeconds) : 0f;
            float inReveal = Smooth((t - inStart) / InSeconds);
            return new PaseoEpochStep(outReveal, inReveal, Smooth(t / total), t >= total);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static readonly Vector3 IdentityRed = new(100f, 0f, 0f), IdentityGreen = new(0f, 100f, 0f), IdentityBlue = new(0f, 0f, 100f);

        public static PaseoEpochLook Look(PaseoEpoch epoch) => epoch switch
        {
            // Old film camera: black and white with a hint of warmth, hard contrast, heavy grain, scratches and flicker.
            PaseoEpoch.Y1950 => new PaseoEpochLook(new Vector3(32f, 60f, 12f), new Vector3(30f, 58f, 11f), new Vector3(27f, 52f, 10f),
                0f, 28f, 0.1f, Color.white, 0.8f, 0.6f, 0.45f, 0.4f, 1f, 0.12f),
            // Sepia print: the classic sepia matrix, softer grain and a little wear.
            PaseoEpoch.Y1980 => new PaseoEpochLook(new Vector3(39f, 77f, 19f), new Vector3(35f, 69f, 17f), new Vector3(27f, 53f, 13f),
                0f, 8f, -0.15f, Color.white, 0.45f, 0.7f, 0.35f, 0.45f, 0.3f, 0.04f),
            // Early digital photo: washed-out colour with a warm cast, light grain, no film wear.
            PaseoEpoch.Y2000 => new PaseoEpochLook(IdentityRed, IdentityGreen, IdentityBlue,
                -30f, -12f, 0f, new Color(1f, 0.96f, 0.88f), 0.2f, 0.8f, 0.22f, 0.5f, 0f, 0f),
            _ => new PaseoEpochLook(IdentityRed, IdentityGreen, IdentityBlue, 0f, 0f, 0f, Color.white, 0f, 0.8f, 0f, 0.4f, 0f, 0f)
        };
    }
}
