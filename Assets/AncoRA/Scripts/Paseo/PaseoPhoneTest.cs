using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Immersal;
using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// One of the camera images bundled for the phone test (Resources/PaseoPrueba): the 1-channel buffer exactly as the
    /// plugin takes it, the camera that took it, the maps to search and what must come out.
    /// </summary>
    public sealed class PaseoPhoneTestImage
    {
        public string Name;
        public int Width;
        public int Height;
        /// <summary>fx, fy, cx, cy.</summary>
        public Vector4 Intrinsics;
        public int[] MapIds;
        /// <summary>The map the image must locate in; 0 when it must not locate at all.</summary>
        public int ExpectedMap;
        public byte[] Pixels;
    }

    /// <summary>One timed localization: the map it found (0 when none) and the seconds of the native call alone.</summary>
    public readonly struct PaseoPhoneTestShot
    {
        public readonly int MapId;
        public readonly double Seconds;

        public PaseoPhoneTestShot(int mapId, double seconds)
        {
            MapId = mapId;
            Seconds = seconds;
        }
    }

    /// <summary>What the test found out about one size: measured, or only predicted from the previous one.</summary>
    public sealed class PaseoPhoneTestStep
    {
        public int MaxPixels;
        public bool Measured;
        /// <summary>Mean seconds of an attempt over the images; only when measured.</summary>
        public double MeanSeconds;
        /// <summary>What the mean was predicted to be; only when not measured.</summary>
        public double PredictedSeconds;
        public int Images;
        /// <summary>Images that located in some map.</summary>
        public int Located;
        /// <summary>Images whose result is the expected one: the expected map, or none when none was expected.</summary>
        public int AsExpected;
    }

    public sealed class PaseoPhoneTestResult
    {
        /// <summary>One step per size, from the fewest pixels to the most.</summary>
        public readonly List<PaseoPhoneTestStep> Steps = new();
        /// <summary>Images that gave a different result than expected (the choice still follows the time rule).</summary>
        public readonly List<string> Warnings = new();
        public int Chosen = PaseoResolution.Reduced;
        public double TotalSeconds;
        /// <summary>Why the test did not run to the end; empty when it did.</summary>
        public string Failure = "";
        public bool Cancelled;

        public bool Completed => Failure.Length == 0 && !Cancelled;

        /// <summary>The lines under the button: one per size, then the choice and any warning.</summary>
        public List<string> Lines()
        {
            var lines = new List<string>();
            foreach (var step in Steps)
                lines.Add(step.Measured
                    ? $"{PaseoResolution.ShortName(step.MaxPixels)}: {Comma(step.MeanSeconds, 1)} s · ubica {step.Located}/{step.Images}"
                    : $"{PaseoResolution.ShortName(step.MaxPixels)}: no se probó (≈ {Comma(step.PredictedSeconds, step.PredictedSeconds < 10 ? 1 : 0)} s)");
            if (Completed)
                lines.Add($"Elegida: {PaseoResolution.ShortName(Chosen)}");
            lines.AddRange(Warnings);
            return lines;
        }

        /// <summary>A number with the Spanish decimal comma.</summary>
        public static string Comma(double value, int digits) =>
            value.ToString("F" + digits, CultureInfo.InvariantCulture).Replace('.', ',');
    }

    /// <summary>
    /// The phone test: times Immersal's localizer on this phone with four bundled camera images and picks the largest image
    /// size whose attempt stays within <see cref="BudgetSeconds"/>. The time per attempt grows with the pixels (4.5 times
    /// from 960 × 720 to the full image on the Xiaomi 14) and phones differ by 4 to 5 times, so one size does not fit all.
    /// To keep it short it measures only what the previous size predicts will fit: 960 × 720 always, 1440 × 810 when twice its
    /// time is within the budget, the full image when 2.2 times that is. The native call is injectable, so the plan runs on
    /// a fake in tests. It touches the plugin's global setting, so the live search must be suspended meanwhile
    /// (<see cref="RunWithoutSearchAsync"/>).
    /// </summary>
    public sealed class PaseoPhoneTest
    {
        /// <summary>The mean time of an attempt a size may take over the four images and still be chosen.</summary>
        public const float BudgetSeconds = 5f;

        // Seconds per attempt on the Xiaomi 14 over these same four images: 2.08 s at 960 × 720, 4.14 s at 1440 × 810 and
        // 9.10 s at full size. The ratios predict whether the next size is worth measuring.
        public const double RatioMedium = 2.0;
        public const double RatioFull = 2.2;

        public const string WaitForMaps = "Espera a que carguen los mapas y vuelve a probar.";
        public const string NoImages = "No se encontraron las imágenes de la prueba en esta versión.";
        public const float PauseTimeoutSeconds = 30f;
        public const string ResourceFolder = "PaseoPrueba/";

        /// <summary>One timed localization of an image at a size; it runs on a worker thread.</summary>
        public delegate PaseoPhoneTestShot ShotCall(PaseoPhoneTestImage image, int maxPixels);

        readonly IReadOnlyList<PaseoPhoneTestImage> images;
        readonly ShotCall shot;
        readonly Func<int, bool> hasHandle;
        readonly Func<double> clock;
        readonly Action<string> log;
        readonly string model;

        /// <param name="images">The bundled images (<see cref="LoadImages"/>).</param>
        /// <param name="shot">The native call; the plugin's by default.</param>
        /// <param name="hasHandle">Whether the plugin has loaded a map; default MapHandleMapping.</param>
        /// <param name="clock">Seconds, for the total; default a monotonic clock.</param>
        /// <param name="log">Where the PRUEBA_TELEFONO lines go; default Debug.Log.</param>
        public PaseoPhoneTest(IReadOnlyList<PaseoPhoneTestImage> images, ShotCall shot = null, Func<int, bool> hasHandle = null,
            Func<double> clock = null, Action<string> log = null, string model = null)
        {
            this.images = images;
            this.shot = shot ?? NativeShot;
            this.hasHandle = hasHandle ?? (id => MapHandleMapping.TryGetHandle(id, out _));
            this.clock = clock ?? (() => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
            this.log = log ?? (message => Debug.Log(message));
            this.model = model;
        }

        /// <summary>
        /// Runs the plan: 960 × 720 first, then each larger size only when the one before predicts it fits the budget.
        /// Progress is reported on the calling thread; a cancelled run returns what it has, flagged Cancelled.
        /// </summary>
        public async Task<PaseoPhoneTestResult> RunAsync(Action<string> progress, CancellationToken token)
        {
            var result = new PaseoPhoneTestResult();
            if (images.Count == 0)
            {
                result.Failure = NoImages;
                return result;
            }
            if (images.Any(image => image.MapIds.Any(id => !hasHandle(id))))
            {
                result.Failure = WaitForMaps;
                return result;
            }

            double begin = clock();
            var low = await MeasureAsync(PaseoResolution.Reduced, result, progress, token);
            if (low == null)
                return Cancel(result, begin);
            result.Steps.Add(low);

            double predictedMedium = low.MeanSeconds * RatioMedium;
            PaseoPhoneTestStep medium;
            if (predictedMedium <= BudgetSeconds)
            {
                medium = await MeasureAsync(PaseoResolution.Medium, result, progress, token);
                if (medium == null)
                    return Cancel(result, begin);
            }
            else
                medium = Predicted(PaseoResolution.Medium, predictedMedium);
            result.Steps.Add(medium);

            double predictedFull = (medium.Measured ? medium.MeanSeconds : predictedMedium) * RatioFull;
            PaseoPhoneTestStep full;
            if (medium.Measured && predictedFull <= BudgetSeconds)
            {
                full = await MeasureAsync(PaseoResolution.Full, result, progress, token);
                if (full == null)
                    return Cancel(result, begin);
            }
            else
                full = Predicted(PaseoResolution.Full, predictedFull);
            result.Steps.Add(full);

            // The most pixels that fit; 960 × 720 when not even that does.
            result.Chosen = result.Steps
                .Where(step => step.Measured && step.MeanSeconds <= BudgetSeconds)
                .OrderByDescending(step => PaseoResolution.PixelRank(step.MaxPixels))
                .Select(step => step.MaxPixels)
                .DefaultIfEmpty(PaseoResolution.Reduced)
                .First();
            result.TotalSeconds = clock() - begin;
            log(PaseoLogFormat.PhoneTestChosen(result.Chosen, result.TotalSeconds, model));
            return result;
        }

        PaseoPhoneTestResult Cancel(PaseoPhoneTestResult result, double begin)
        {
            result.Cancelled = true;
            result.TotalSeconds = clock() - begin;
            return result;
        }

        static PaseoPhoneTestStep Predicted(int maxPixels, double seconds) =>
            new() { MaxPixels = maxPixels, PredictedSeconds = seconds };

        // Null when cancelled.
        async Task<PaseoPhoneTestStep> MeasureAsync(int maxPixels, PaseoPhoneTestResult result, Action<string> progress, CancellationToken token)
        {
            var step = new PaseoPhoneTestStep { MaxPixels = maxPixels, Measured = true, Images = images.Count };
            double total = 0;
            for (int i = 0; i < images.Count; i++)
            {
                if (token.IsCancellationRequested)
                    return null;
                progress?.Invoke($"Probando {PaseoResolution.ShortName(maxPixels).ToLowerInvariant()}: imagen {i + 1} de {images.Count}…");
                var image = images[i];
                PaseoPhoneTestShot taken;
                try
                {
                    taken = await Task.Run(() => shot(image, maxPixels), token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                total += taken.Seconds;
                if (taken.MapId > 0)
                    step.Located++;
                if (taken.MapId == image.ExpectedMap || (image.ExpectedMap == 0 && taken.MapId <= 0))
                    step.AsExpected++;
                else
                    result.Warnings.Add(Mismatch(maxPixels, i + 1, taken.MapId, image.ExpectedMap));
            }
            step.MeanSeconds = total / images.Count;
            log(PaseoLogFormat.PhoneTestStep(maxPixels, step.MeanSeconds, step.Images, step.Located, step.AsExpected));
            return step;
        }

        static string Mismatch(int maxPixels, int number, int found, int expected)
        {
            string name = PaseoResolution.ShortName(maxPixels);
            if (found > 0 && expected <= 0)
                return $"Aviso: a {name} la imagen {number} ubicó en el mapa {found} y no debía ubicar.";
            if (found <= 0)
                return $"Aviso: a {name} la imagen {number} no ubicó y debía ubicar en el mapa {expected}.";
            return $"Aviso: a {name} la imagen {number} ubicó en el mapa {found} y debía ubicar en el mapa {expected}.";
        }

        /// <summary>
        /// Runs the test with the live search paused: suspends the localization, waits for the attempt in flight to end (the
        /// plugin's setting is global, and a live attempt would also slow the timings), runs, and resumes the search whatever
        /// happens.
        /// </summary>
        public static async Task<PaseoPhoneTestResult> RunWithoutSearchAsync(PaseoLocalization localization, PaseoPhoneTest test,
            Action<string> progress, CancellationToken token, Func<float> now = null)
        {
            now ??= () => Time.realtimeSinceStartup;
            localization.Suspended = true;
            try
            {
                progress?.Invoke("Esperando que termine el intento en curso…");
                float giveUp = now() + PauseTimeoutSeconds;
                while (localization.Busy)
                {
                    if (token.IsCancellationRequested)
                        return new PaseoPhoneTestResult { Cancelled = true };
                    if (now() >= giveUp)
                        return new PaseoPhoneTestResult { Failure = "No se pudo pausar la búsqueda; vuelve a probar." };
                    await Task.Yield();
                }
                return await test.RunAsync(progress, token);
            }
            finally
            {
                localization.Suspended = false;
            }
        }

        /// <summary>
        /// The real call: sets the plugin's size, then times Core.LocalizeImage alone (pixels pinned, identity rotation, the
        /// default solver, the maps of the image). It runs on a worker thread, as the tour's own attempts do.
        /// </summary>
        public static PaseoPhoneTestShot NativeShot(PaseoPhoneTestImage image, int maxPixels)
        {
            Core.SetInteger("LocalizationMaxPixels", maxPixels);
            Vector4 intrinsics = image.Intrinsics;
            Quaternion rotation = Quaternion.identity;
            var pinned = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
            try
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                LocalizeInfo info = Core.LocalizeImage(image.MapIds.Length, image.MapIds, image.Width, image.Height, ref intrinsics,
                    pinned.AddrOfPinnedObject(), 1, (int)SolverType.Default, ref rotation);
                clock.Stop();
                return new PaseoPhoneTestShot(info.mapId > 0 ? info.mapId : 0, clock.Elapsed.TotalSeconds);
            }
            finally
            {
                pinned.Free();
            }
        }

        // ------------------------------------------------------------------ bundled images

        // Only what is read of prueba.json (JsonUtility ignores the rest, and cannot read the xiaomi14_ms dictionary anyway).
        [Serializable]
        sealed class Manifest
        {
            public Entry[] imagenes;
        }

        [Serializable]
        sealed class Entry
        {
            public string archivo;
            public int ancho;
            public int alto;
            public float fx, fy, cx, cy;
            public int[] mapas;
            public int espera_mapa;
        }

        /// <summary>
        /// The images of Resources/PaseoPrueba: prueba.json lists them and each prueba-N.bytes is the gzip of the raw 1-channel
        /// buffer. Empty when anything is missing or damaged (then the test says so instead of running on a partial set).
        /// </summary>
        public static List<PaseoPhoneTestImage> LoadImages()
        {
            var loaded = new List<PaseoPhoneTestImage>();
            try
            {
                var manifestAsset = Resources.Load<TextAsset>(ResourceFolder + "prueba");
                if (manifestAsset == null)
                    return loaded;
                var manifest = JsonUtility.FromJson<Manifest>(manifestAsset.text);
                Resources.UnloadAsset(manifestAsset);
                foreach (var entry in manifest?.imagenes ?? Array.Empty<Entry>())
                {
                    var asset = Resources.Load<TextAsset>(ResourceFolder + entry.archivo);
                    if (asset == null)
                        throw new FileNotFoundException(entry.archivo);
                    byte[] compressed = asset.bytes;
                    Resources.UnloadAsset(asset);
                    loaded.Add(new PaseoPhoneTestImage
                    {
                        Name = entry.archivo,
                        Width = entry.ancho,
                        Height = entry.alto,
                        Intrinsics = new Vector4(entry.fx, entry.fy, entry.cx, entry.cy),
                        MapIds = entry.mapas ?? Array.Empty<int>(),
                        ExpectedMap = entry.espera_mapa,
                        Pixels = Gunzip(compressed, entry.ancho * entry.alto)
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AncoRA Paseo] No se pudieron leer las imágenes de la prueba del teléfono: {e.Message}");
                loaded.Clear();
            }
            return loaded;
        }

        /// <summary>Decompresses a gzip that must hold exactly <paramref name="expectedLength"/> bytes.</summary>
        public static byte[] Gunzip(byte[] compressed, int expectedLength)
        {
            var bytes = new byte[expectedLength];
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            int read = 0;
            while (read < bytes.Length)
            {
                int count = gzip.Read(bytes, read, bytes.Length - read);
                if (count <= 0)
                    break;
                read += count;
            }
            if (read != expectedLength || gzip.ReadByte() != -1)
                throw new InvalidDataException($"La imagen de la prueba no mide {expectedLength} bytes.");
            return bytes;
        }
    }
}
