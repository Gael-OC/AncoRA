using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Writes the session's log to a file on the phone, because logcat lost every tour line of a field test (Xiaomi's 2 MB
    /// buffer, pruned chattiest app first). One file per session in persistentDataPath/registro (the newest
    /// <see cref="KeepFiles"/> stay), lines filtered and formatted by PaseoLogFormat, plus an ESTADO_PASEO line every
    /// <see cref="StatusSeconds"/> s with battery, thermal state and what the tour is doing. Pull it with
    /// adb pull /sdcard/Android/data/com.ancora.ucnar.paseo/files/registro.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class PaseoSessionLog : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        public const int KeepFiles = 10;
        public const float StatusSeconds = 10f;
        // Debug lines (PaseoDebug): the camera every second, frames and memory every few seconds.
        public const float PoseSeconds = 1f;
        public const float PerformanceSeconds = 5f;
        const float FlushSeconds = 1f;

        [SerializeField] PaseoTour tour;

        // Unity calls the threaded log callback from any thread (the SDK localizes on worker threads).
        readonly ConcurrentQueue<string> pending = new();
        StreamWriter writer;
        float nextFlush;
        float nextStatus;
        float nextPose;
        float performanceSince;
        int frames;
        float worstFrame;
        static PaseoSessionLog current;

        public PaseoTour Tour => tour;
        public static string CurrentPath { get; private set; } = "";
        /// <summary>Where the logs of every session live; their debug photos go under imagenes/&lt;session&gt;.</summary>
        public static string Folder { get; private set; } = "";
        public static string CurrentImageFolder { get; private set; } = "";

        /// <summary>Writes out what is pending now, e.g. before the log is zipped to share it (PaseoShare).</summary>
        public static void FlushCurrent()
        {
            if (current != null)
                current.Flush();
        }

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(PaseoTour paseoTour) => tour = paseoTour;

        void Awake()
        {
            current = this;
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "registro");
                Folder = folder;
                Directory.CreateDirectory(folder);
                foreach (string old in PaseoLogFormat.FilesToDelete(Directory.GetFiles(folder, "paseo-*.log").Select(Path.GetFileName), KeepFiles - 1))
                    File.Delete(Path.Combine(folder, old));
                string stamp = $"{DateTime.Now:yyyyMMdd-HHmmss}";
                CurrentPath = Path.Combine(folder, $"paseo-{stamp}.log");
                CurrentImageFolder = Path.Combine(folder, "imagenes", stamp);
                DeleteOrphanPhotos(folder, stamp);
                writer = new StreamWriter(CurrentPath, false, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                writer = null;
                Debug.LogWarning($"{Tag} No se pudo abrir el registro de la sesión: {e.Message}");
            }
            Application.logMessageReceivedThreaded += OnLog;
            Debug.Log($"{Tag} Registro de la sesión en {CurrentPath}; versión {Application.version}, {SystemInfo.deviceModel}, Android {SystemInfo.operatingSystem}.");
        }

        // The photos of a session go with its log: once the log is gone, so are they.
        static void DeleteOrphanPhotos(string folder, string currentStamp)
        {
            string photos = Path.Combine(folder, "imagenes");
            if (!Directory.Exists(photos))
                return;
            var kept = Directory.GetFiles(folder, "paseo-*.log").Select(f => PaseoShare.SessionStamp(Path.GetFileName(f))).ToHashSet();
            foreach (string dir in Directory.GetDirectories(photos))
            {
                string stamp = Path.GetFileName(dir);
                if (stamp != currentStamp && !kept.Contains(stamp))
                    Directory.Delete(dir, true);
            }
        }

        void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Flush();
            writer?.Dispose();
            writer = null;
            if (current == this)
                current = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (PaseoDebug.Enabled)
                Debug.Log($"{Tag} DEPURACION_APP pausa={(paused ? 1 : 0)} t={Time.realtimeSinceStartup:F1}");
            if (paused)
                Flush();
        }

        void OnApplicationFocus(bool focused)
        {
            if (PaseoDebug.Enabled)
                Debug.Log($"{Tag} DEPURACION_APP foco={(focused ? 1 : 0)} t={Time.realtimeSinceStartup:F1}");
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (writer != null && PaseoLogFormat.Keep(message, type))
                pending.Enqueue(PaseoLogFormat.Line(DateTime.Now, type, message, stackTrace));
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now >= nextStatus)
            {
                nextStatus = now + StatusSeconds;
                Debug.Log(StatusLine());
            }
            if (PaseoDebug.Enabled)
                LogDebug(now);
            if (now >= nextFlush)
            {
                nextFlush = now + FlushSeconds;
                Flush();
            }
        }

        void LogDebug(float now)
        {
            frames++;
            worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
            if (now >= nextPose)
            {
                nextPose = now + PoseSeconds;
                if (tour != null && tour.TryPoseSample(out var sample))
                    Debug.Log(PaseoLogFormat.Pose(sample));
            }
            float elapsed = now - performanceSince;
            if (elapsed < PerformanceSeconds)
                return;
            const float Mb = 1024f * 1024f;
            Debug.Log(PaseoLogFormat.Performance(frames / elapsed, worstFrame * 1000f,
                UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / Mb, GC.GetTotalMemory(false) / Mb));
            performanceSince = now;
            frames = 0;
            worstFrame = 0f;
        }

        void Flush()
        {
            if (writer == null)
                return;
            try
            {
                while (pending.TryDequeue(out string line))
                    writer.WriteLine(line);
                writer.Flush();
            }
            catch (Exception e)
            {
                // Stop writing rather than fail every second; the warning still reaches logcat.
                writer = null;
                Debug.LogWarning($"{Tag} El registro de la sesión dejó de escribirse: {e.Message}");
            }
        }

        // key=value with invariant numbers, like UBICACION_PASEO, so the file can be parsed.
        string StatusLine()
        {
            var inv = CultureInfo.InvariantCulture;
            string tourState = tour == null ? "sin-paseo"
                : PaseoLogFormat.TourState(tour.Begun, tour.SdkReady, tour.ShownCount, tour.LocalizationInterval,
                    tour.LocalizationAttempts, tour.LocalizationFailures, tour.GpsAccuracyMeters);
            return string.Format(inv, "{0} ESTADO_PASEO bateria={1:F0} cargando={2} termico={3} margen={4:F2} tracking={5} {6}", Tag,
                SystemInfo.batteryLevel * 100f, SystemInfo.batteryStatus == BatteryStatus.Charging ? 1 : 0, ThermalStatus(), ThermalHeadroom(),
                UnityEngine.XR.ARFoundation.ARSession.state, tourState);
        }

        // PowerManager.getCurrentThermalStatus (API 29): 0 none, 1 light, 2 moderate, 3 severe (throttling), 4+ critical.
        static int ThermalStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var power = PowerManager();
                return power.Call<int>("getCurrentThermalStatus");
            }
            catch (Exception)
            {
                return -1;
            }
#else
            return -1;
#endif
        }

        // PowerManager.getThermalHeadroom (API 30): forecast in 10 s; 1.0 means throttling, NaN when the phone does not say.
        static float ThermalHeadroom()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var power = PowerManager();
                return power.Call<float>("getThermalHeadroom", 10);
            }
            catch (Exception)
            {
                return float.NaN;
            }
#else
            return float.NaN;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject PowerManager()
        {
            // Unity owns currentActivity: disposing it throws, which left termico=-1 and margen=NaN in the 1.2.2 log.
            var activity = UnityEngine.Android.AndroidApplication.currentActivity;
            return activity.Call<AndroidJavaObject>("getSystemService", "power");
        }
#endif
    }
}
