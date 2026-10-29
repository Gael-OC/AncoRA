using System;
using System.Globalization;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The size at which Immersal's localizer works on each camera image, as its «LocalizationMaxPixels» setting: the plugin
    /// shrinks the image (keeping its shape) to at most that many pixels, 0 meaning the image as the camera gave it. A bigger
    /// image finds more matches and costs more time: on the Xiaomi 14, 64 field images took 2.6 s each on average and located
    /// 15 at 960 × 720, 4.2 s and 20 at 1440 × 810, 8.5 s and 25 at full size. The team picks the size on the sheet, or the
    /// phone test (PaseoPhoneTest) picks the best one this phone can afford.
    /// </summary>
    public static class PaseoResolution
    {
        /// <summary>960 × 720, the SDK's own default: a 1920 × 1080 image shrinks to 1109 × 624.</summary>
        public const int Reduced = 960 * 720;
        /// <summary>1440 × 810: a 1920 × 1080 image shrinks to 1440 × 810.</summary>
        public const int Medium = 1440 * 810;
        /// <summary>No limit: the plugin gets the full image.</summary>
        public const int Full = 0;
        public const int Default = Medium;

        public const string OriginDefault = "por defecto";
        public const string OriginChosen = "elegida";
        public const string OriginTest = "prueba";

        /// <summary>From the fewest pixels to the most.</summary>
        public static readonly int[] Options = { Reduced, Medium, Full };

        public static bool IsKnown(int maxPixels) => Array.IndexOf(Options, maxPixels) >= 0;

        /// <summary>«Full» has no limit, so it is the most pixels of all; for ordering options.</summary>
        public static long PixelRank(int maxPixels) => maxPixels <= 0 ? long.MaxValue : maxPixels;

        /// <summary>The size of an image after the plugin's reduction: the shape is kept, so both sides scale by the square root of the ratio.</summary>
        public static (int Width, int Height) ReducedSize(int maxPixels, int width, int height)
        {
            if (maxPixels <= 0 || width <= 0 || height <= 0 || (long)width * height <= maxPixels)
                return (width, height);
            double scale = Math.Sqrt((double)maxPixels / ((double)width * height));
            return ((int)Math.Round(width * scale), (int)Math.Round(height * scale));
        }

        /// <summary>«WxH» of a camera image after the reduction (a 1920 × 1080 one unless said otherwise), for log lines.</summary>
        public static string SizeText(int maxPixels, int width = 1920, int height = 1080)
        {
            var (w, h) = ReducedSize(maxPixels, width, height);
            return w + "x" + h;
        }

        /// <summary>The name on the sheet's buttons and result lines.</summary>
        public static string ShortName(int maxPixels) => maxPixels switch
        {
            Reduced => "960×720",
            Medium => "1440×810",
            Full => "Completa",
            _ => maxPixels + " px"
        };

        public static string Name(int maxPixels) => maxPixels == Full ? "Completa (1920×1080)" : ShortName(maxPixels);

        /// <summary>The origin as one token of a log line: a space would split the key=value pair.</summary>
        public static string OriginToken(string origin) => origin == OriginDefault ? "por-defecto" : origin;

        /// <summary>The line of the sheet that says what is in use and why.</summary>
        public static string Describe(PaseoResolutionChoice choice)
        {
            string how;
            if (choice.Origin == OriginTest)
                how = "elegida por la prueba" + (choice.HasDate ? $" ({choice.Date.ToString("dd-MM", CultureInfo.InvariantCulture)})" : "");
            else if (choice.Origin == OriginChosen)
                how = "elegida a mano";
            else
                how = "por defecto, sin probar en este teléfono";
            return $"Ahora: {ShortName(choice.MaxPixels)} · {how}";
        }
    }

    /// <summary>A size and how it came to be in use.</summary>
    public readonly struct PaseoResolutionChoice
    {
        public readonly int MaxPixels;
        /// <summary>One of <see cref="PaseoResolution.OriginDefault"/>, <see cref="PaseoResolution.OriginChosen"/>, <see cref="PaseoResolution.OriginTest"/>.</summary>
        public readonly string Origin;
        /// <summary>When it was chosen; default when unknown.</summary>
        public readonly DateTime Date;

        public PaseoResolutionChoice(int maxPixels, string origin, DateTime date = default)
        {
            MaxPixels = maxPixels;
            Origin = origin;
            Date = date;
        }

        public bool HasDate => Date != default;

        public static PaseoResolutionChoice Default => new(PaseoResolution.Default, PaseoResolution.OriginDefault);
    }

    /// <summary>
    /// The size picked on this phone, kept in PlayerPrefs (three keys under the box store's prefix): the value, how it was
    /// chosen and the date. Nothing saved, or a value this version does not offer, reads as the default.
    /// </summary>
    public sealed class PaseoResolutionStore
    {
        const string DateFormat = "yyyy-MM-dd";

        readonly string prefix;

        public PaseoResolutionStore(string keyPrefix = PaseoBoxStore.DefaultPrefix) => prefix = keyPrefix;

        string ValueKey => prefix + "Resolucion";
        string OriginKey => prefix + "ResolucionOrigen";
        string DateKey => prefix + "ResolucionFecha";

        public PaseoResolutionChoice Load()
        {
            if (!PlayerPrefs.HasKey(ValueKey))
                return PaseoResolutionChoice.Default;
            int value = PlayerPrefs.GetInt(ValueKey, PaseoResolution.Default);
            if (!PaseoResolution.IsKnown(value))
                return PaseoResolutionChoice.Default;
            string origin = PlayerPrefs.GetString(OriginKey, PaseoResolution.OriginChosen);
            if (origin != PaseoResolution.OriginTest)
                origin = PaseoResolution.OriginChosen;
            DateTime.TryParseExact(PlayerPrefs.GetString(DateKey, ""), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
            return new PaseoResolutionChoice(value, origin, date);
        }

        /// <param name="origin"><see cref="PaseoResolution.OriginChosen"/> or <see cref="PaseoResolution.OriginTest"/>.</param>
        public void Save(int maxPixels, string origin, DateTime date)
        {
            PlayerPrefs.SetInt(ValueKey, maxPixels);
            PlayerPrefs.SetString(OriginKey, origin == PaseoResolution.OriginTest ? PaseoResolution.OriginTest : PaseoResolution.OriginChosen);
            PlayerPrefs.SetString(DateKey, date.ToString(DateFormat, CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(ValueKey);
            PlayerPrefs.DeleteKey(OriginKey);
            PlayerPrefs.DeleteKey(DateKey);
            PlayerPrefs.Save();
        }
    }
}
