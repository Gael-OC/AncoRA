using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// What the session log on the phone keeps and how each line reads. Xiaomi's logcat buffer is 2 MB and MIUI prunes the
    /// chattiest app first, so a field test left no tour lines in logcat: the tour writes its own file instead
    /// (PaseoSessionLog). It keeps the tour's lines, Immersal's, and any warning or error; one line each.
    /// </summary>
    public static class PaseoLogFormat
    {
        const string ImmersalMark = "com.immersal.core";

        public static bool Keep(string message, LogType type)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            return type != LogType.Log || message.StartsWith("[AncoRA", StringComparison.Ordinal) || message.Contains(ImmersalMark);
        }

        /// <summary>Local time, a level letter and the message's first line; errors add their first stack frame.</summary>
        public static string Line(DateTime time, LogType type, string message, string stackTrace)
        {
            string text = FirstLine(message);
            // Immersal prefixes the caller's compile-time path: "[.\Library\PackageCache\com.immersal.core@…\ImmersalSDK] …".
            if (text.StartsWith("[", StringComparison.Ordinal) && text.Contains(ImmersalMark))
            {
                int end = text.IndexOf(']');
                if (end > 0)
                {
                    string caller = text.Substring(1, end - 1);
                    caller = caller.Substring(caller.LastIndexOfAny(new[] { '\\', '/' }) + 1);
                    text = $"[Immersal {caller}]{text.Substring(end + 1)}";
                }
            }
            string frame = type is LogType.Error or LogType.Exception or LogType.Assert ? FirstLine(stackTrace) : "";
            return $"{time:HH:mm:ss.fff} {Level(type)} {text}{(frame.Length > 0 ? " @ " + frame : "")}";
        }

        /// <summary>
        /// One line per localization attempt (PaseoLocalization), key=value with invariant numbers: what it searched and
        /// why, the GPS distance to that building, how long the plugin took and what it found.
        /// </summary>
        public static string Attempt(PaseoAttempt a)
        {
            var inv = CultureInfo.InvariantCulture;
            var s = a.Search;
            string reason = s.Reason switch
            {
                PaseoSearchReason.Gps => "gps",
                PaseoSearchReason.InView => "a-la-vista",
                PaseoSearchReason.Order => "orden",
                _ => "todos"
            };
            string distance = double.IsNaN(s.GpsDistanceMeters) ? "-" : s.GpsDistanceMeters.ToString("F0", inv);
            string result = a.Matched ? a.MatchedMapId.ToString(inv) : "ninguno";
            return string.Format(inv, "[AncoRA Paseo] INTENTO_PASEO n={0} busca={1} mapas={2} motivo={3} dist_gps={4} seg={5:F2} resultado={6} conf={7}",
                a.Number, s.BuildingId ?? "todos", s.MapIds?.Length ?? 0, reason, distance, a.Seconds, result, a.Confidence);
        }

        /// <summary>
        /// Debug line of an attempt (PaseoDebug): the image size, the AR camera at capture (position and heading in the AR
        /// world), tracking, the building in view, the pace and the raw GPS, so a failed attempt can be placed afterwards.
        /// </summary>
        public static string AttemptDebug(PaseoAttempt a, PaseoDebugContext c)
        {
            var inv = CultureInfo.InvariantCulture;
            var p = a.CameraPosition;
            string gps = c.GpsRunning
                ? string.Format(inv, "gps={0:F6},{1:F6} prec={2:F0} edad={3:F1}", c.Latitude, c.Longitude, c.GpsAccuracy, c.GpsAge)
                : "gps=- prec=- edad=-";
            return string.Format(inv, "[AncoRA Paseo] DEPURACION_INTENTO n={0} img={1}x{2} cam={3:F2},{4:F2},{5:F2} rumbo={6:F0} tracking={7} vista={8} ritmo={9:F1} {10} gps_util={11}",
                a.Number, a.ImageWidth, a.ImageHeight, p.x, p.y, p.z, a.CameraYaw, c.Tracking ?? "-", c.InView ?? "-", c.Interval, gps, c.GpsUsable ? 1 : 0);
        }

        /// <summary>Debug line of a change of the AR session state or of its reason for not tracking.</summary>
        public static string TrackingChange(string state, string reason, float secondsSinceStart) =>
            string.Format(CultureInfo.InvariantCulture, "[AncoRA Paseo] DEPURACION_TRACKING estado={0} motivo={1} t={2:F1}", state, reason, secondsSinceStart);

        /// <summary>Debug line of the AR camera configuration and whether Immersal reduces the image (960 × 720 at most).</summary>
        public static string CameraConfig(int width, int height, int fps, bool reduced) =>
            string.Format(CultureInfo.InvariantCulture, "[AncoRA Paseo] DEPURACION_CAMARA config={0} reducida={1}",
                width > 0 && height > 0 ? $"{width}x{height}@{fps}" : "-", reduced ? 1 : 0);

        /// <summary>Debug line once a second: the camera in the AR world, how it moves, and each shown box (distance and bearing).</summary>
        public static string Pose(PaseoPoseSample s)
        {
            var inv = CultureInfo.InvariantCulture;
            string boxes = s.Boxes == null || s.Boxes.Count == 0
                ? "-"
                : string.Join(",", s.Boxes.Select(b => string.Format(inv, "{0}:{1:F1}:{2:F0}", b.Building, b.Distance, b.Bearing)));
            return string.Format(inv, "[AncoRA Paseo] DEPURACION_POSE t={0:F1} cam={1:F2},{2:F2},{3:F2} rumbo={4:F0} incl={5:F0} vel={6:F1} giro={7:F0} tracking={8} cajas={9}",
                s.Seconds, s.Camera.x, s.Camera.y, s.Camera.z, s.Yaw, s.Pitch, s.Speed, s.TurnSpeed, s.Tracking ?? "-", boxes);
        }

        /// <summary>Debug line every few seconds: frames per second, the slowest frame and the memory in use.</summary>
        public static string Performance(float fps, float worstFrameMs, float allocatedMb, float managedMb) =>
            string.Format(CultureInfo.InvariantCulture, "[AncoRA Paseo] DEPURACION_RENDIMIENTO fps={0:F1} peor_ms={1:F0} mem_mb={2:F0} gc_mb={3:F0}",
                fps, worstFrameMs, allocatedMb, managedMb);

        /// <summary>Debug line after the filter judged a localization: where it alone puts the box centre and where the filter goes.</summary>
        public static string BoxAfterLocalization(int mapId, Vector3 estimated, Vector3 filtered, float filteredYaw, bool confirmed) =>
            string.Format(CultureInfo.InvariantCulture,
                "[AncoRA Paseo] DEPURACION_CAJA mapa={0} estimada={1:F2},{2:F2},{3:F2} filtrada={4:F2},{5:F2},{6:F2} giro={7:F1} confirmada={8}",
                mapId, estimated.x, estimated.y, estimated.z, filtered.x, filtered.y, filtered.z, filteredYaw, confirmed ? 1 : 0);

        /// <summary>The tour part of the ESTADO_PASEO line.</summary>
        public static string TourState(bool begun, bool sdkReady, int shown, float interval, int attempts, int failures, float gpsAccuracyMeters)
        {
            var inv = CultureInfo.InvariantCulture;
            string gps = float.IsNaN(gpsAccuracyMeters) ? "-" : gpsAccuracyMeters.ToString("F0", inv);
            return string.Format(inv, "comenzado={0} sdk={1} visibles={2} ritmo={3:F1} intentos={4} fallos={5} gps={6}",
                begun ? 1 : 0, sdkReady ? 1 : 0, shown, interval, attempts, failures, gps);
        }

        /// <summary>The session files to delete so only the newest <paramref name="keep"/> remain (names sort by start time).</summary>
        public static List<string> FilesToDelete(IEnumerable<string> fileNames, int keep)
        {
            var sorted = fileNames.OrderBy(n => n, StringComparer.Ordinal).ToList();
            return sorted.Take(Math.Max(0, sorted.Count - keep)).ToList();
        }

        static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            int end = text.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? text : text.Substring(0, end);
        }

        static char Level(LogType type) => type switch
        {
            LogType.Warning => 'W',
            LogType.Error => 'E',
            LogType.Exception => 'X',
            LogType.Assert => 'A',
            _ => 'I'
        };
    }
}
