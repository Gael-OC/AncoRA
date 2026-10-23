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

        // Credentials of the MultiSet test (multiset.json) that must never reach the session file, which the team shares.
        static readonly List<string> Secrets = new();

        /// <summary>Registers a credential: any line that carries it is written with «***» in its place (Redact).</summary>
        public static void RegisterSecret(string secret)
        {
            lock (Secrets)
                if (!string.IsNullOrWhiteSpace(secret) && !Secrets.Contains(secret))
                    Secrets.Add(secret);
        }

        public static void ForgetSecrets()
        {
            lock (Secrets)
                Secrets.Clear();
        }

        /// <summary>The text with every registered credential replaced by «***».</summary>
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            lock (Secrets)
                foreach (string secret in Secrets)
                    text = text.Replace(secret, "***");
            return text;
        }

        /// <summary>Local time, a level letter and the message's first line; errors add their first stack frame.</summary>
        public static string Line(DateTime time, LogType type, string message, string stackTrace)
        {
            message = Redact(message);
            stackTrace = Redact(stackTrace);
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

        /// <summary>
        /// Debug line of the ARCore occlusion once AR tracks: whether the phone has the Depth API, the environment depth mode the
        /// subsystem runs in (Disabled without it), the temporal smoothing and the camera background mode (the mask relies on
        /// BeforeOpaques, where the background writes the depth before the opaques).
        /// </summary>
        public static string Occlusion(bool supported, string mode, bool smoothing, string background, float secondsSinceStart) =>
            string.Format(CultureInfo.InvariantCulture, "[AncoRA Paseo] DEPURACION_OCLUSION soportada={0} modo={1} suavizado={2} fondo={3} t={4:F1}",
                supported ? 1 : 0, mode ?? "-", smoothing ? 1 : 0, background ?? "-", secondsSinceStart);

        /// <summary>
        /// Debug line (an interface event, like the others of DEPURACION_UI) when the team changes the view of the buildings with a
        /// replacement: caja, fondo or objeto. The time is the app clock, as in the other interface lines.
        /// </summary>
        public static string ViewMode(PaseoViewMode mode, float seconds) =>
            string.Format(CultureInfo.InvariantCulture, "[AncoRA Paseo] DEPURACION_UI vista={0} t={1:F1}", PaseoViewModes.LogName(mode), seconds);

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

        /// <summary>
        /// State of the MultiSet side of the test: autenticando, autenticado, error, sin-config (no multiset.json), apagado
        /// (the team paused it) or tope (the session's query cap was reached), with the queries sent so far. t = seconds since
        /// «Comenzar», like the tour's own lines.
        /// </summary>
        public static string MultiSetState(string state, int queries, float secondsSinceStart) =>
            string.Format(CultureInfo.InvariantCulture, "[AncoRA Paseo] MULTISET_ESTADO estado={0} consultas={1} t={2:F1}", state, queries, secondsSinceStart);

        /// <summary>One line per MultiSet query, key=value with invariant numbers, to put its seconds and hits beside INTENTO_PASEO's.</summary>
        public static string MultiSetAttempt(PaseoMultiSetAttempt a) =>
            string.Format(CultureInfo.InvariantCulture,
                "[AncoRA Paseo] INTENTO_MULTISET n={0} seg={1:F2} resultado={2} conf={3:F2} aceptada={4} edificio={5} gps={6} t={7:F1}",
                a.Number, a.Seconds, a.Matched ? Token(a.MapCode) : "ninguno", a.Confidence, a.Accepted ? 1 : 0,
                string.IsNullOrEmpty(a.BuildingId) ? "-" : a.BuildingId, a.GpsHint ? 1 : 0, a.At);

        /// <summary>
        /// An accepted MultiSet location of a building: whether the box relation to Immersal was just saved (nueva), already
        /// there (guardada) or impossible for lack of a solid Immersal box (sin-immersal), how far MultiSet's box is from
        /// Immersal's (largest corner move; «-» when not comparable) and the camera's distance to the box centre.
        /// </summary>
        public static string MultiSetLocation(string building, string mapCode, float confidence, PaseoMultiSetRelationState relation,
            float differenceMeters, float distanceMeters, float secondsSinceStart)
        {
            string state = relation switch
            {
                PaseoMultiSetRelationState.New => "nueva",
                PaseoMultiSetRelationState.Saved => "guardada",
                _ => "sin-immersal"
            };
            return string.Format(CultureInfo.InvariantCulture,
                "[AncoRA Paseo] UBICACION_MULTISET edificio={0} mapa={1} conf={2:F2} relacion={3} dif_caja={4} dist={5} t={6:F1}",
                string.IsNullOrEmpty(building) ? "-" : building, Token(mapCode), confidence, state,
                float.IsNaN(differenceMeters) ? "-" : differenceMeters.ToString("F2", CultureInfo.InvariantCulture),
                float.IsNaN(distanceMeters) ? "-" : distanceMeters.ToString("F1", CultureInfo.InvariantCulture), secondsSinceStart);
        }

        /// <summary>The first time each system (immersal or multiset) located each building in the session: the field test's headline number.</summary>
        public static string FirstLocation(string system, string building, float secondsSinceStart) =>
            string.Format(CultureInfo.InvariantCulture, "[AncoRA Paseo] PRIMERA_UBICACION sistema={0} edificio={1} t={2:F1}", system, building, secondsSinceStart);

        // One token for key=value lines: no spaces.
        static string Token(string text) => string.IsNullOrEmpty(text) ? "-" : text.Replace(' ', '_');

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
