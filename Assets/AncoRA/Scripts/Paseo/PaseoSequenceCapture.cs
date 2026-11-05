using System;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AncorRA.AR
{
    /// <summary>
    /// A five-second ARCore recording, independent of the Immersal attempt loop. Copies raw R8 sensor images in
    /// LateUpdate, after tracking updated the camera, and releases every CPU image before any background PNG write.
    /// Raw Unity camera poses are preserved, not advertised as calibrated raw-sensor optical poses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoSequenceCapture : MonoBehaviour
    {
        public const float Duration = 5f;
        public const float Interval = 1f / 6f;
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
        public string Summary => recorder == null ? "" : $"Barrido: {recorder.Saved} imágenes · {recorder.Notice}";

        public bool Begin(PaseoRecordSelection selection, out string reason)
        {
            reason = "";
            if (Busy) reason = "Espera a que termine el barrido y se guarden sus imágenes.";
            else if (!Debug.isDebugBuild) reason = "Los barridos solo existen en la versión de desarrollo.";
            else if (selection == null || !selection.Complete) reason = "Elige edificio, punto, distancia y luz.";
            else if (selection.Meters < 10 || selection.Meters > 25) reason = "El barrido debe estar entre 10 y 25 m.";
            else if (ARSession.state != ARSessionState.SessionTracking) reason = "Espera a que ARCore siga el entorno.";
            if (reason.Length > 0) return false;
            Stop();
            var arCamera = Camera.main;
            cameraManager = arCamera != null ? arCamera.GetComponent<ARCameraManager>() : null;
            if (cameraManager == null)
            {
                reason = "No se encontró la cámara AR.";
                return false;
            }
            epoch = "barrido-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            recorder = new PaseoRecorder(Path.Combine(PaseoSessionLog.RecordingsRoot, epoch),
                () => PaseoRecorder.FreeBytes(Application.persistentDataPath));
            recorder.Selection.SetBuilding(selection.Building);
            recorder.Selection.SetPoint(selection.Point);
            recorder.Selection.SetMeters(selection.Meters);
            recorder.Selection.SetLight(selection.Light);
            if (!recorder.TryEnable(Time.realtimeSinceStartup, out reason)) return false;
            number = 0;
            frameTimestamp = lastTimestamp = double.NaN;
            displayMatrix = null;
            nextAt = Time.realtimeSinceStartup;
            stopAt = nextAt + Duration;
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
                Stop(ARSession.state == ARSessionState.SessionTracking ? "" : "Se perdió el seguimiento; repetir el barrido.");
                return;
            }
            if (now < nextAt || !recorder.WantsFrame(now) || !cameraManager.TryGetIntrinsics(out var intrinsics)) return;
            if (intrinsics.resolution.x <= 0 || intrinsics.resolution.y <= 0 ||
                intrinsics.focalLength.x <= 0 || intrinsics.focalLength.y <= 0) return;
            if (!cameraManager.TryAcquireLatestCpuImage(out var image)) return;
            using (image)
            {
                if (image.timestamp == lastTimestamp || double.IsNaN(frameTimestamp) || Math.Abs(image.timestamp - frameTimestamp) > 0.05) return;
                lastTimestamp = image.timestamp;
                nextAt = now + Interval;
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
                    var meta = new PaseoRecordMeta
                    {
                        Attempt = ++number, File = PaseoRecorder.FileName(number, sel.Label),
                        Time = DateTime.Now, Realtime = now, CaptureRealtime = now,
                        SensorTimestamp = image.timestamp, PoseSensorTimestamp = frameTimestamp,
                        CaptureKind = "barrido-arcore", TrackingEpoch = epoch, Session = epoch,
                        SensorDisplayMatrix = displayMatrix, ScreenOrientationName = Screen.orientation.ToString(),
                        Point = sel.Label, Building = sel.Building, Light = sel.Light,
                        Width = frame.Width, Height = frame.Height, Channels = 1,
                        Intrinsics = frame.Intrinsics, CameraPosition = position, CameraRotation = rotation,
                        ScreenOrientation = Quaternion.identity, ImageOrientation = 0,
                        Tracking = ARSession.state.ToString(), Rmse = double.NaN, GpsDistance = double.NaN,
                        Phone = SystemInfo.deviceModel, Android = SystemInfo.operatingSystem,
                        Version = Application.version, Build = Application.buildGUID
                    };
                    _ = recorder.Save(frame, meta);
                }
                catch (Exception error)
                {
                    Stop("No se pudo capturar el barrido: " + error.Message);
                }
            }
        }

        public void Stop(string reason = "")
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
            recorder?.Disable(reason);
        }

        void OnDisable() => Stop();
        void OnApplicationPause(bool paused) { if (paused) Stop("Barrido interrumpido; repetir sin pausar la aplicación."); }
    }
}
