using System;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AncorRA.AR
{
    /// <summary>What kind of ARCore capture <see cref="PaseoSequenceCapture"/> makes: its length, its pace and what the manifest calls it.</summary>
    public sealed class PaseoCaptureMode
    {
        /// <summary>Five seconds, ~6 images/s, waving the camera over the facade from one spot.</summary>
        public static readonly PaseoCaptureMode Sweep = new("barrido-arcore", "barrido-", "Barrido", 5f, 1f / 6f, false);
        /// <summary>Up to 60 s, ~2 images/s, walking along the facade on the pavement; measures the distance walked.</summary>
        public static readonly PaseoCaptureMode Walk = new("recorrido-arcore", "recorrido-", "Recorrido", 60f, 0.5f, true);

        PaseoCaptureMode(string kind, string folderPrefix, string label, float duration, float interval, bool tracksTravel)
        {
            Kind = kind;
            FolderPrefix = folderPrefix;
            Label = label;
            Duration = duration;
            Interval = interval;
            TracksTravel = tracksTravel;
        }

        /// <summary>«tipo_captura» in each row and in the manifest.</summary>
        public string Kind { get; }
        public string FolderPrefix { get; }
        /// <summary>Shown on screen.</summary>
        public string Label { get; }
        public float Duration { get; }
        public float Interval { get; }
        public bool TracksTravel { get; }
    }

    /// <summary>
    /// A short ARCore recording (<see cref="PaseoCaptureMode"/>: the five-second sweep or the walk along the facade), independent of the Immersal attempt loop. Copies raw R8 sensor images in
    /// LateUpdate, after tracking updated the camera, and releases every CPU image before any background PNG write.
    /// Raw Unity camera poses are preserved, not advertised as calibrated raw-sensor optical poses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoSequenceCapture : MonoBehaviour
    {
        public const float Duration = 5f;
        public const float Interval = 1f / 6f;
        public const string WalkAdvice = "Camina a lo largo de la fachada";
        PaseoCaptureMode mode = PaseoCaptureMode.Sweep;
        readonly PaseoTravelTracker travel = new();
        Vector3 lastCameraPosition;
        bool hasCameraPosition;
        ARCameraManager cameraManager;
        PaseoRecorder recorder;
        string epoch;
        double frameTimestamp = double.NaN;
        double lastTimestamp = double.NaN;
        Matrix4x4? displayMatrix;
        float stopAt;
        float nextAt;
        int number;
        public bool Busy => recorder != null && (recorder.Enabled || recorder.Writing);
        public PaseoCaptureMode Mode => mode;
        public PaseoRecorder Recorder => recorder;
        /// <summary>Distance walked by the camera in the last capture (walk only).</summary>
        public double TravelMeters => travel.Meters;

        /// <summary>What the sheet shows: the advice and the count while recording, the result (and a short-walk warning) after.</summary>
        public string Summary
        {
            get
            {
                if (recorder == null) return "";
                string text = $"{mode.Label}: {recorder.Saved} imágenes";
                if (mode.TracksTravel)
                {
                    text += $" · {travel.Meters:F1} m";
                    if (recorder.Enabled)
                        text += $" · {WalkAdvice}, quedan {Mathf.Max(0f, stopAt - Time.realtimeSinceStartup):F0} s";
                    else if (travel.Short)
                        text += $" · Recorrido corto (menos de {PaseoTravelTracker.MinUsefulMeters:F0} m): no sirve para construir; repítelo caminando a lo largo de la fachada.";
                }
                return recorder.Notice.Length > 0 ? text + " · " + recorder.Notice : text;
            }
        }

        public bool Begin(PaseoRecordSelection selection, out string reason) => Begin(selection, PaseoCaptureMode.Sweep, out reason);

        public bool Begin(PaseoRecordSelection selection, PaseoCaptureMode captureMode, out string reason)
        {
            reason = "";
            var requested = captureMode ?? PaseoCaptureMode.Sweep;
            if (Busy) reason = "Espera a que termine la captura y se guarden sus imágenes.";
            else if (!Debug.isDebugBuild) reason = "Las capturas solo existen en la versión de desarrollo.";
            else if (selection == null || !selection.Complete) reason = "Elige edificio, punto, distancia y luz.";
            else if (selection.Meters < 10 || selection.Meters > 25) reason = $"El {requested.Label.ToLowerInvariant()} debe estar entre 10 y 25 m.";
            else if (ARSession.state != ARSessionState.SessionTracking) reason = "Espera a que ARCore siga el entorno.";
            if (reason.Length > 0) return false;
            Stop();
            mode = requested;
            var arCamera = Camera.main;
            cameraManager = arCamera != null ? arCamera.GetComponent<ARCameraManager>() : null;
            if (cameraManager == null)
            {
                reason = "No se encontró la cámara AR.";
                return false;
            }
            epoch = mode.FolderPrefix + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            recorder = new PaseoRecorder(Path.Combine(PaseoSessionLog.RecordingsRoot, epoch),
                () => PaseoRecorder.FreeBytes(Application.persistentDataPath));
            recorder.Selection.SetBuilding(selection.Building);
            recorder.Selection.SetPoint(selection.Point);
            recorder.Selection.SetMeters(selection.Meters);
            recorder.Selection.SetLight(selection.Light);
            recorder.CaptureKind = mode.Kind;
            if (!recorder.TryEnable(Time.realtimeSinceStartup, out reason)) return false;
            number = 0;
            frameTimestamp = lastTimestamp = double.NaN;
            displayMatrix = null;
            travel.Reset();
            hasCameraPosition = false;
            if (mode.TracksTravel) recorder.SetTravel(0, true);
            nextAt = Time.realtimeSinceStartup;
            stopAt = nextAt + mode.Duration;
            cameraManager.frameReceived += OnFrame;
            return true;
        }

        void OnFrame(ARCameraFrameEventArgs args)
        {
            frameTimestamp = args.timestampNs.HasValue ? args.timestampNs.Value * 1e-9 : double.NaN;
            displayMatrix = args.displayMatrix;
        }

        void LateUpdate()
        {
            if (recorder == null) return;
            if (!recorder.Enabled) { Stop(); return; }
            float now = Time.realtimeSinceStartup;
            if (now >= stopAt || ARSession.state != ARSessionState.SessionTracking)
            {
                Stop(ARSession.state == ARSessionState.SessionTracking ? "" : $"Se perdió el seguimiento; repetir el {mode.Label.ToLowerInvariant()}.");
                return;
            }
            lastCameraPosition = cameraManager.transform.position;
            hasCameraPosition = true;
            if (now < nextAt || !recorder.WantsFrame(now) || !cameraManager.TryGetIntrinsics(out var intrinsics)) return;
            if (intrinsics.resolution.x <= 0 || intrinsics.resolution.y <= 0 ||
                intrinsics.focalLength.x <= 0 || intrinsics.focalLength.y <= 0) return;
            if (!cameraManager.TryAcquireLatestCpuImage(out var image)) return;
            using (image)
            {
                if (image.timestamp == lastTimestamp || double.IsNaN(frameTimestamp) || Math.Abs(image.timestamp - frameTimestamp) > 0.05) return;
                lastTimestamp = image.timestamp;
                nextAt = now + mode.Interval;
                try
                {
                    var cameraTransform = cameraManager.transform;
                    Vector3 position = cameraTransform.position;
                    Quaternion rotation = cameraTransform.rotation;
                    var conversion = new XRCpuImage.ConversionParams
                    {
                        inputRect = new RectInt(0, 0, image.width, image.height),
                        outputDimensions = new Vector2Int(image.width, image.height),
                        outputFormat = TextureFormat.R8,
                        transformation = XRCpuImage.Transformation.None
                    };
                    using var buffer = new NativeArray<byte>(image.GetConvertedDataSize(conversion), Allocator.Temp);
                    image.Convert(conversion, new NativeSlice<byte>(buffer));
                    var frame = new PaseoFrame
                    {
                        Pixels = buffer.ToArray(), Width = image.width, Height = image.height, Channels = 1,
                        CameraPosition = position, CameraRotation = rotation,
                        Intrinsics = new Vector4(intrinsics.focalLength.x * image.width / intrinsics.resolution.x,
                            intrinsics.focalLength.y * image.height / intrinsics.resolution.y,
                            intrinsics.principalPoint.x * image.width / intrinsics.resolution.x,
                            intrinsics.principalPoint.y * image.height / intrinsics.resolution.y)
                    };
                    var sel = recorder.Selection;
                    if (mode.TracksTravel)
                    {
                        travel.Add(position);
                        recorder.SetTravel(travel.Meters, travel.Short);
                    }
                    var meta = new PaseoRecordMeta
                    {
                        Attempt = ++number, File = PaseoRecorder.FileName(number, sel.Label),
                        Time = DateTime.Now, Realtime = now, CaptureRealtime = now,
                        SensorTimestamp = image.timestamp, PoseSensorTimestamp = frameTimestamp,
                        CaptureKind = mode.Kind, TrackingEpoch = epoch, Session = epoch,
                        SensorDisplayMatrix = displayMatrix, ScreenOrientationName = Screen.orientation.ToString(),
                        Point = sel.Label, Building = sel.Building, Light = sel.Light,
                        Width = frame.Width, Height = frame.Height, Channels = 1,
                        Intrinsics = frame.Intrinsics, CameraPosition = position, CameraRotation = rotation,
                        ScreenOrientation = Quaternion.identity, ImageOrientation = 0,
                        Tracking = ARSession.state.ToString(), Rmse = double.NaN, GpsDistance = double.NaN,
                        Phone = SystemInfo.deviceModel, Android = SystemInfo.operatingSystem,
                        Version = Application.version, Build = Application.buildGUID,
                        TravelMeters = mode.TracksTravel ? travel.Meters : null
                    };
                    _ = recorder.Save(frame, meta);
                }
                catch (Exception error)
                {
                    Stop($"No se pudo capturar el {mode.Label.ToLowerInvariant()}: " + error.Message);
                }
            }
        }

        public void Stop(string reason = "")
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
            if (recorder != null && recorder.Enabled && mode.TracksTravel)
            {
                // The stretch walked since the last image counts too.
                if (hasCameraPosition) travel.Add(lastCameraPosition);
                recorder.SetTravel(travel.Meters, travel.Short);
            }
            recorder?.Disable(reason);
        }

        void OnDisable() => Stop();
        void OnApplicationPause(bool paused) { if (paused) Stop($"{mode.Label} interrumpido; repetir sin pausar la aplicación."); }
    }
}
