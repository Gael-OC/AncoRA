using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace AncorRA.AR
{
    /// <summary>What the tour knew when an attempt that was recorded ended; PaseoRecorder.MetaLine writes it as one JSON line.</summary>
    public struct PaseoRecordMeta
    {
        public int Attempt;
        public string File;
        public DateTime Time;
        public float Realtime;
        public string Session;
        public string Point;
        public string Building;
        public string Light;
        public int Width;
        public int Height;
        public int Channels;
        public bool ReducedByPlugin;
        public Vector4 Intrinsics;
        public Vector3 CameraPosition;
        public Quaternion CameraRotation;
        public Quaternion ScreenOrientation;
        public uint ImageOrientation;
        public float Yaw;
        public float Pitch;
        public string Tracking;
        public string InView;
        public bool GpsRunning;
        public bool GpsUsable;
        public double Latitude;
        public double Longitude;
        public float GpsAccuracy;
        public double GpsAge;
        public string SearchedBuilding;
        public int[] SearchedMaps;
        public string SearchReason;
        public double GpsDistance;
        public int MatchedMap;
        public int Confidence;
        public double Rmse;
        public float Seconds;
        public string Phone;
        public string Android;
        public string Version;
        public string Build;
    }

    /// <summary>
    /// Record mode of the team sheet: for every localization attempt it saves the image the plugin received (lossless PNG,
    /// full resolution, sensor orientation) in recordings/&lt;session&gt;, next to the session log, and one JSON line per image
    /// in intentos.jsonl with the camera, the GPS, what was searched, what was found and the recording point the team picked.
    /// The point is what makes the data usable: the same fixed spots at different hours show what the light does to Immersal.
    ///
    /// It never changes the search: the image is copied and written off the main thread, at most <see cref="MaxPending"/>
    /// writes wait (a slow disk drops frames, it does not slow the tour), and a disk that fails, a full quota or a nearly
    /// full phone turn it off with a notice instead of raising anything.
    /// </summary>
    public sealed class PaseoRecorder
    {
        const string Tag = "[AncoRA Paseo]";
        public const string MetaFile = "intentos.jsonl";
        // ~1.2 MB per image: some 800 images. The phones have 128 GB or more; this keeps one session from eating the rest.
        public const long DefaultMaxBytes = 1000L * 1024 * 1024;
        public const long DefaultMinFreeBytes = 400L * 1024 * 1024;
        public const int MaxPending = 3;
        const float FreeCheckSeconds = 10f;

        readonly string folder;
        readonly Func<long> freeBytes;
        readonly long maxBytes;
        readonly long minFreeBytes;
        readonly object writeLock = new();
        int pending;
        long bytes;
        int saved;
        int dropped;
        volatile string failure;
        bool enabled;
        float nextFreeCheck = float.NegativeInfinity;
        int pointVersion = -1;
        float pointSince;
        int pointQueued;

        public PaseoRecorder(string folder, Func<long> freeBytes = null, long maxBytes = DefaultMaxBytes, long minFreeBytes = DefaultMinFreeBytes)
        {
            this.folder = folder ?? "";
            this.freeBytes = freeBytes;
            this.maxBytes = maxBytes;
            this.minFreeBytes = minFreeBytes;
        }

        public PaseoRecordSelection Selection { get; } = new();
        /// <summary>This session's folder of recordings.</summary>
        public string Folder => folder;
        /// <summary>The mode only exists in development builds (PaseoDebug) and when the session has a folder.</summary>
        public bool Available => PaseoDebug.Enabled && folder.Length > 0;
        public bool Enabled => enabled;
        public int Saved => Volatile.Read(ref saved);
        public int Dropped => Volatile.Read(ref dropped);
        public long Bytes => Interlocked.Read(ref bytes);
        public long MaxBytes => maxBytes;
        /// <summary>Free bytes at the last check (while recording, every few seconds); -1 before the first one or when unknown.</summary>
        public long LastFreeBytes { get; private set; } = -1;
        /// <summary>Why it turned itself off (shown on screen); empty while nothing happened.</summary>
        public string Notice { get; private set; } = "";
        /// <summary>Background thread, before each write; tests hold it to simulate a slow disk.</summary>
        public Action BeforeWrite { get; set; }
        /// <summary>Images queued for the point being recorded now, and the seconds since it began.</summary>
        public int PointImages => pointQueued;

        public float PointSeconds(float now) => enabled ? now - pointSince : 0f;

        /// <summary>Turns recording on; false (and <paramref name="reason"/>) when it cannot, e.g. the point is not chosen yet.</summary>
        public bool TryEnable(float now, out string reason)
        {
            reason = "";
            if (enabled)
                return true;
            if (!Available)
                reason = "Grabar solo existe en la versión de desarrollo.";
            else if (!Selection.Complete)
                reason = "Elige edificio, punto y luz antes de grabar.";
            else if (Interlocked.Read(ref bytes) >= maxBytes)
                reason = "Se llegó al tope de espacio de esta sesión; cierra la app y ábrela para seguir.";
            else if (failure != null)
                reason = "La grabación falló en esta sesión: " + failure;
            else if (LowOnSpace(now, force: true, out long free))
                reason = $"Queda poco espacio en el teléfono ({free / (1024 * 1024)} MB).";
            if (reason.Length > 0)
                return false;
            enabled = true;
            Notice = "";
            pointVersion = -1;
            Debug.Log($"{Tag} GRABACION_INICIO punto={Selection.Label} luz={Selection.Light} carpeta={folder}");
            return true;
        }

        public void Disable(string notice = "")
        {
            if (!enabled)
                return;
            enabled = false;
            if (notice.Length > 0)
            {
                Notice = notice;
                Debug.LogWarning($"{Tag} GRABACION_ALTO {notice}");
            }
            else
                Debug.Log($"{Tag} GRABACION_FIN imagenes={saved} bytes={Bytes}");
        }

        /// <summary>Forgets the counts after the recordings were deleted.</summary>
        public void ResetCounts()
        {
            Interlocked.Exchange(ref bytes, 0);
            Interlocked.Exchange(ref saved, 0);
            Interlocked.Exchange(ref dropped, 0);
            failure = null;
            Notice = "";
            pointQueued = 0;
        }

        /// <summary>Main thread, as an attempt starts: whether this attempt's image is to be saved. Turns recording off when a limit is reached.</summary>
        public bool WantsFrame(float now)
        {
            if (!enabled)
                return false;
            if (!Selection.Complete)
                return false;
            if (failure != null)
            {
                Disable("la grabación se detuvo: " + failure);
                return false;
            }
            if (Interlocked.Read(ref bytes) >= maxBytes)
            {
                Disable($"Grabación detenida: tope de {maxBytes / (1024 * 1024)} MB de esta sesión.");
                return false;
            }
            if (LowOnSpace(now, force: false, out long free))
            {
                Disable($"Grabación detenida: queda poco espacio en el teléfono ({free / (1024 * 1024)} MB).");
                return false;
            }
            if (Volatile.Read(ref pending) >= MaxPending)
            {
                Interlocked.Increment(ref dropped);
                return false;
            }
            if (pointVersion != Selection.Version)
            {
                pointVersion = Selection.Version;
                pointSince = now;
                pointQueued = 0;
            }
            return true;
        }

        bool LowOnSpace(float now, bool force, out long free)
        {
            free = -1;
            if (freeBytes == null || (!force && now < nextFreeCheck))
                return false;
            nextFreeCheck = now + FreeCheckSeconds;
            try
            {
                free = freeBytes();
            }
            catch (Exception)
            {
                free = -1;
            }
            LastFreeBytes = free;
            // An unknown amount (-1) does not block: the write itself reports a full disk.
            return free >= 0 && free < minFreeBytes;
        }

        /// <summary>Queues the image and its line for writing; the returned task never faults.</summary>
        public Task Save(PaseoFrame frame, PaseoRecordMeta meta)
        {
            pointQueued++;
            Interlocked.Increment(ref pending);
            string line = MetaLine(meta);
            string imagePath = Path.Combine(folder, meta.File);
            return Task.Run(() =>
            {
                try
                {
                    BeforeWrite?.Invoke();
                    Directory.CreateDirectory(folder);
                    byte[] image = EncodeImage(frame, out string extension);
                    if (extension != Path.GetExtension(imagePath))
                    {
                        imagePath = Path.ChangeExtension(imagePath, extension);
                        line = line.Replace($"\"archivo\":{Quote(meta.File)}", $"\"archivo\":{Quote(Path.GetFileName(imagePath))}");
                    }
                    File.WriteAllBytes(imagePath, image);
                    lock (writeLock)
                        File.AppendAllText(Path.Combine(folder, MetaFile), line + "\n", new UTF8Encoding(false));
                    Interlocked.Add(ref bytes, image.Length + line.Length + 1);
                    Interlocked.Increment(ref saved);
                }
                catch (Exception e)
                {
                    failure = e.Message;
                }
                finally
                {
                    Interlocked.Decrement(ref pending);
                }
            });
        }

        public static string FileName(int attempt, string label) => $"{attempt:D4}-{label}.png";

        /// <summary>
        /// Lossless PNG of the buffer in its sensor orientation; if the encoder cannot, a PGM/PPM, which is the raw buffer with a
        /// header. Either way the saved pixels are exactly the ones the plugin got.
        /// </summary>
        public static byte[] EncodeImage(PaseoFrame frame, out string extension)
        {
            try
            {
                var png = EncodePng(frame.Pixels, frame.Width, frame.Height, frame.Channels);
                extension = ".png";
                return png;
            }
            catch (Exception)
            {
                extension = frame.Channels == 1 ? ".pgm" : ".ppm";
                return EncodePnm(frame.Pixels, frame.Width, frame.Height, frame.Channels);
            }
        }

        public static byte[] EncodePng(byte[] pixels, int width, int height, int channels)
        {
            GraphicsFormat format = channels switch
            {
                1 => GraphicsFormat.R8_UNorm,
                3 => GraphicsFormat.R8G8B8_UNorm,
                4 => GraphicsFormat.R8G8B8A8_UNorm,
                _ => throw new ArgumentException($"{channels} canales no se pueden guardar como PNG.")
            };
            // Unity's encoders read the rows from the bottom; the buffer has its first row on top.
            return ImageConversion.EncodeArrayToPNG(FlipRows(pixels, width * channels, height), format, (uint)width, (uint)height);
        }

        public static byte[] FlipRows(byte[] pixels, int rowBytes, int height)
        {
            var flipped = new byte[pixels.Length];
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(pixels, y * rowBytes, flipped, (height - 1 - y) * rowBytes, rowBytes);
            return flipped;
        }

        // Binary PNM: P5 for grey, P6 for colour (a 4th channel is dropped).
        public static byte[] EncodePnm(byte[] pixels, int width, int height, int channels)
        {
            bool grey = channels == 1;
            byte[] header = Encoding.ASCII.GetBytes($"{(grey ? "P5" : "P6")}\n{width} {height}\n255\n");
            int outChannels = grey ? 1 : 3;
            var data = new byte[header.Length + width * height * outChannels];
            Buffer.BlockCopy(header, 0, data, 0, header.Length);
            if (channels == outChannels)
            {
                Buffer.BlockCopy(pixels, 0, data, header.Length, width * height * outChannels);
                return data;
            }
            for (int i = 0; i < width * height; i++)
            for (int c = 0; c < outChannels; c++)
                data[header.Length + i * outChannels + c] = pixels[i * channels + Math.Min(c, channels - 1)];
            return data;
        }

        /// <summary>One JSON object on one line, invariant numbers, Spanish keys like the log lines.</summary>
        public static string MetaLine(PaseoRecordMeta m)
        {
            var s = new StringBuilder(768);
            s.Append('{');
            s.Append("\"n\":").Append(m.Attempt);
            s.Append(",\"archivo\":").Append(Quote(m.File));
            s.Append(",\"hora\":").Append(Quote(m.Time.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture)));
            s.Append(",\"t\":").Append(Num(m.Realtime));
            s.Append(",\"sesion\":").Append(Quote(m.Session));
            s.Append(",\"punto\":").Append(Quote(m.Point));
            s.Append(",\"edificio_punto\":").Append(Quote(m.Building));
            s.Append(",\"luz\":").Append(Quote(m.Light));
            s.Append(",\"ancho\":").Append(m.Width).Append(",\"alto\":").Append(m.Height).Append(",\"canales\":").Append(m.Channels);
            s.Append(",\"imagen_reducida_por_plugin\":").Append(m.ReducedByPlugin ? "true" : "false");
            s.Append(",\"intrinsecos\":{\"fx\":").Append(Num(m.Intrinsics.x)).Append(",\"fy\":").Append(Num(m.Intrinsics.y))
                .Append(",\"cx\":").Append(Num(m.Intrinsics.z)).Append(",\"cy\":").Append(Num(m.Intrinsics.w)).Append('}');
            s.Append(",\"pos_camara\":").Append(Vec(m.CameraPosition));
            s.Append(",\"rot_camara\":").Append(Quat(m.CameraRotation));
            s.Append(",\"rot_pantalla\":").Append(Quat(m.ScreenOrientation));
            s.Append(",\"orientacion_imagen\":").Append(m.ImageOrientation);
            s.Append(",\"rumbo\":").Append(Num(m.Yaw)).Append(",\"inclinacion\":").Append(Num(m.Pitch));
            s.Append(",\"tracking\":").Append(Quote(m.Tracking));
            s.Append(",\"a_la_vista\":").Append(Quote(m.InView));
            s.Append(",\"gps\":{\"activo\":").Append(m.GpsRunning ? "true" : "false").Append(",\"util\":").Append(m.GpsUsable ? "true" : "false");
            s.Append(",\"lat\":").Append(m.GpsRunning ? Num(m.Latitude, "F7") : "null");
            s.Append(",\"lon\":").Append(m.GpsRunning ? Num(m.Longitude, "F7") : "null");
            s.Append(",\"precision\":").Append(m.GpsRunning ? Num(m.GpsAccuracy) : "null");
            s.Append(",\"edad\":").Append(m.GpsRunning ? Num(m.GpsAge) : "null").Append('}');
            s.Append(",\"busca\":{\"edificio\":").Append(Quote(m.SearchedBuilding)).Append(",\"mapas\":[");
            if (m.SearchedMaps != null)
                for (int i = 0; i < m.SearchedMaps.Length; i++)
                    s.Append(i > 0 ? "," : "").Append(m.SearchedMaps[i]);
            s.Append("],\"motivo\":").Append(Quote(m.SearchReason)).Append(",\"dist_gps\":").Append(Num(m.GpsDistance)).Append('}');
            s.Append(",\"resultado\":{\"mapa\":").Append(m.MatchedMap > 0 ? m.MatchedMap.ToString(CultureInfo.InvariantCulture) : "null");
            s.Append(",\"conf\":").Append(m.Confidence).Append(",\"rmse\":").Append(Num(m.Rmse)).Append(",\"seg\":").Append(Num(m.Seconds)).Append('}');
            s.Append(",\"telefono\":").Append(Quote(m.Phone)).Append(",\"android\":").Append(Quote(m.Android));
            s.Append(",\"version\":").Append(Quote(m.Version)).Append(",\"build\":").Append(Quote(m.Build));
            s.Append('}');
            return s.ToString();
        }

        static string Num(double value, string format = "G9") =>
            double.IsNaN(value) || double.IsInfinity(value) ? "null" : value.ToString(format, CultureInfo.InvariantCulture);

        static string Vec(Vector3 v) => $"[{Num(v.x)},{Num(v.y)},{Num(v.z)}]";

        static string Quat(Quaternion q) => $"[{Num(q.x)},{Num(q.y)},{Num(q.z)},{Num(q.w)}]";

        static string Quote(string text)
        {
            if (text == null)
                return "null";
            var s = new StringBuilder(text.Length + 2);
            s.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': s.Append("\\\""); break;
                    case '\\': s.Append("\\\\"); break;
                    case '\n': s.Append("\\n"); break;
                    case '\r': s.Append("\\r"); break;
                    case '\t': s.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            s.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            s.Append(c);
                        break;
                }
            }
            return s.Append('"').ToString();
        }

        /// <summary>Free bytes where the recordings go (Android StatFs); -1 when it cannot be read.</summary>
        public static long FreeBytes(string path)
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var stat = new AndroidJavaObject("android.os.StatFs", path);
                return stat.Call<long>("getAvailableBytes");
#else
                string root = Path.GetPathRoot(Path.GetFullPath(path));
                return string.IsNullOrEmpty(root) ? -1 : new DriveInfo(root).AvailableFreeSpace;
#endif
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}
