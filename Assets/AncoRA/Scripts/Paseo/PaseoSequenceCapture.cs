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
    /// Short query sweeps or longer construction walks, independent of the Immersal attempt loop. Copies raw R8 images in
    /// LateUpdate, after tracking updated the camera, and releases every CPU image before any background PNG write.
    /// Raw Unity camera poses are preserved, not advertised as calibrated raw-sensor optical poses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoSequenceCapture : MonoBehaviour
    {
        public const float Duration = 5f;
        public const float Interval = 1f / 6f;
        public const float ConstructionDuration = 60f;
        public const float ConstructionInterval = 0.5f;
        ARCameraManager cameraManager;
        PaseoRecorder recorder;
        string epoch;
        double frameTimestamp = double.NaN;
        double lastTimestamp = double.NaN;
        Matrix4x4? displayMatrix;
        double? exposureDuration;
        float? exposureOffset;
        float stopAt;
        float nextAt;
        int number;
        PaseoCaptureMode mode = PaseoCaptureMode.Sweep;
        readonly PaseoTravelTracker travel = new();
        public const string WalkAdvice = "Camina a lo largo de la fachada";
        public PaseoCaptureMode Mode => mode;
        public PaseoRecorder Recorder => recorder;
        public double TravelMeters => travel.Meters;
        bool construction;
        float startedAt;
        float endedAt;
        float interval;
        float seconds;
        float nextSpaceCheck;
        bool framesStarted;
        bool sessionOpen;
        string stopReason = "";
        PaseoCaptureSession session;
        PaseoSessionVideo video = new();
        public string SessionFolder => recorder?.Folder ?? "";
        public bool Successful { get; private set; }
        public bool Playing => video.Playing;
        public bool Recording => recorder != null && recorder.Enabled;
        public bool Busy => recorder != null && (recorder.Enabled || recorder.Writing || sessionOpen);
        public string Summary => recorder == null ? "" :
            $"{(construction ? "Recorrido" : "Toma")}: {(Recording ? Time.realtimeSinceStartup : endedAt) - startedAt:F0}/{seconds:F0} s · " +
            $"{recorder.Saved} guardadas · {recorder.Dropped} omitidas · " +
            (Recording ? (framesStarted ? "grabando" : "preparando cámara, espera") : Busy ? "guardando, espera" :
                recorder.Failure.Length > 0 ? "error al guardar: " + recorder.Failure :
                stopReason.Length > 0 ? stopReason :
                video.Error.Length > 0 ? video.Error :
                recorder.Notice.Length > 0 ? recorder.Notice : recorder.Saved > 0 ? "lista para copiar" : "sin imágenes; repetir");

        public bool Begin(PaseoRecordSelection selection, out string reason, bool forConstruction = false,
            PaseoCaptureShot shot = null, bool withVideo = false, string notes = "", PaseoCaptureMode captureMode = null)
        {
            reason = "";
            var requested = captureMode ?? (forConstruction ? PaseoCaptureMode.Walk : PaseoCaptureMode.Sweep);
            if (!Busy) Successful = false;
            if (Busy) reason = "Espera a que termine el barrido y se guarden sus imágenes.";
            else if (Playing) reason = "Vuelve a cámara en vivo antes de grabar.";
            else if (!Debug.isDebugBuild) reason = "Los barridos solo existen en la versión de desarrollo.";
            else if (selection == null || !selection.Complete) reason = "Elige edificio, punto, distancia y luz.";
            else if (shot == null && (selection.Meters < 10 || selection.Meters > 25)) reason = $"El {requested.Label.ToLowerInvariant()} debe estar entre 10 y 25 m.";
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
            mode = requested;
            construction = requested.TracksTravel;
            travel.Reset();
            interval = construction ? ConstructionInterval : Interval;
            epoch = (construction ? "construccion-" : "barrido-") + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            recorder = new PaseoRecorder(Path.Combine(PaseoSessionLog.RecordingsRoot, epoch),
                () => PaseoRecorder.FreeBytes(Application.persistentDataPath));
            recorder.Selection.SetBuilding(selection.Building);
            recorder.Selection.SetPoint(selection.Point);
            recorder.Selection.SetMeters(selection.Meters);
            recorder.Selection.SetLight(selection.Light);
            recorder.CaptureKind = construction ? (shot == null ? mode.Kind : "construccion-arcore") :
                shot == null || shot.Role == "consulta" ? "barrido-arcore" : shot.Role + "-arcore";
            if (construction) recorder.SetTravel(0, true);
            if (!recorder.TryEnable(Time.realtimeSinceStartup, out reason)) return false;
            video = new PaseoSessionVideo();
            stopReason = "";
            session = new PaseoCaptureSession
            {
                id = epoch, toma = shot?.Id ?? (construction ? "recorrido-libre" : "barrido-libre"),
                titulo = shot?.Title ?? selection.Describe(), rol = shot?.Role ?? (construction ? "construccion" : "consulta"),
                instrucciones = shot?.Instructions ?? "", edificio = selection.Building, luz = selection.Light, notas = notes,
                inicio = DateTimeOffset.Now.ToString("O"), estado = "iniciando", motivo = "",
                telefono = SystemInfo.deviceModel, sistema = SystemInfo.operatingSystem, version = Application.version,
                build = Application.buildGUID, registro_origen = Path.GetFileName(PaseoSessionLog.CurrentPath),
                segundos_objetivo = shot?.Seconds ?? (construction ? (int)ConstructionDuration : (int)Duration),
                distancia_referencia_aproximada_m = selection.Meters, video_solicitado = withVideo
            };
            try
            {
                Directory.CreateDirectory(recorder.Folder);
                session.Save(recorder.Folder);
                PaseoSessionLog.FlushCurrent();
                PaseoCaptureSession.AttachLog(recorder.Folder, PaseoSessionLog.CurrentPath);
                if (withVideo && !video.Start(recorder.Folder, out reason))
                {
                    recorder.Disable();
                    session.estado = "fallida";
                    session.motivo = reason;
                    session.error_video = reason;
                    session.Save(recorder.Folder);
                    return false;
                }
                sessionOpen = true;
                session.estado = "grabando";
                session.Save(recorder.Folder);
            }
            catch (Exception error)
            {
                video.Stop();
                recorder.Disable();
                sessionOpen = false;
                reason = "No se pudo preparar la sesión: " + error.Message;
                return false;
            }
            number = 0;
            frameTimestamp = lastTimestamp = double.NaN;
            displayMatrix = null;
            startedAt = endedAt = nextAt = Time.realtimeSinceStartup;
            seconds = session.segundos_objetivo;
            stopAt = nextAt + 5f;
            nextSpaceCheck = nextAt;
            framesStarted = false;
            cameraManager.frameReceived += OnFrame;
            return true;
        }

        public bool Begin(PaseoRecordSelection selection, PaseoCaptureMode captureMode, out string reason) =>
            Begin(selection, out reason, captureMode: captureMode);

        void OnFrame(ARCameraFrameEventArgs args)
        {
            frameTimestamp = args.timestampNs.HasValue ? args.timestampNs.Value * 1e-9 : double.NaN;
            displayMatrix = args.displayMatrix;
            exposureDuration = args.exposureDuration;
            exposureOffset = args.exposureOffset;
        }

        void LateUpdate()
        {
            if (recorder == null) return;
            if (!recorder.Enabled)
            {
                Stop();
                if (sessionOpen && !recorder.Writing) FinishSession();
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (!framesStarted && now >= stopAt) { Stop("No llegaron imágenes en 5 s; repetir."); return; }
            if (framesStarted && (now >= stopAt || ARSession.state != ARSessionState.SessionTracking))
            {
                Stop(ARSession.state == ARSessionState.SessionTracking ? "" : "Se perdió el seguimiento; repetir el barrido.");
                return;
            }
            // Starting/stopping a native recording briefly pauses ARCore. Wait before timing the actual take.
            if (ARSession.state != ARSessionState.SessionTracking) return;
            if (now >= nextSpaceCheck)
            {
                nextSpaceCheck = now + 1f;
                long free = PaseoRecorder.FreeBytes(Application.persistentDataPath);
                if (free >= 0 && free < PaseoRecorder.DefaultMinFreeBytes)
                { Stop("Poco espacio libre; copia a Descargas antes de continuar."); return; }
                string movie = Path.Combine(recorder.Folder, PaseoSessionVideo.FileName);
                if (recorder.Bytes + (File.Exists(movie) ? new FileInfo(movie).Length : 0) >= PaseoRecorder.DefaultMaxBytes)
                { Stop("La toma llegó a 1 GB; se conserva. Continúa en una nueva toma."); return; }
                if (framesStarted && video.RecordingProblem() is string problem && problem.Length > 0)
                { Stop(problem); return; }
            }
            if (now < nextAt || !recorder.WantsFrame(now) || !cameraManager.TryGetIntrinsics(out var intrinsics)) return;
            if (intrinsics.resolution.x <= 0 || intrinsics.resolution.y <= 0 ||
                intrinsics.focalLength.x <= 0 || intrinsics.focalLength.y <= 0) return;
            if (!cameraManager.TryAcquireLatestCpuImage(out var image)) return;
            using (image)
            {
                if (image.timestamp == lastTimestamp || double.IsNaN(frameTimestamp) || Math.Abs(image.timestamp - frameTimestamp) > 0.05) return;
                lastTimestamp = image.timestamp;
                nextAt = now + interval;
                try
                {
                    if (!framesStarted)
                    {
                        framesStarted = true;
                        startedAt = now;
                        stopAt = now + seconds;
                    }
                    var cameraTransform = cameraManager.transform;
                    double poseReadAt = Time.realtimeSinceStartupAsDouble;
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
                    if (construction)
                    {
                        travel.Add(position);
                        recorder.SetTravel(travel.Meters, travel.Short);
                    }
                    var sel = recorder.Selection;
                    var meta = new PaseoRecordMeta
                    {
                        Attempt = ++number, File = PaseoRecorder.FileName(number, sel.Label),
                        Time = DateTime.Now, Realtime = now, CaptureRealtime = now,
                        SensorTimestamp = image.timestamp, PoseSensorTimestamp = frameTimestamp,
                        PoseReadRealtime = poseReadAt, ExposureDuration = exposureDuration, ExposureOffset = exposureOffset,
                        CaptureRole = session.rol, CaptureShot = session.toma,
                        CaptureKind = recorder.CaptureKind, TravelMeters = construction ? travel.Meters : (double?)null,
                        TrackingEpoch = epoch, Session = epoch,
                        SensorDisplayMatrix = displayMatrix, ScreenOrientationName = Screen.orientation.ToString(),
                        Point = sel.Label, Building = sel.Building, Light = sel.Light,
                        Width = frame.Width, Height = frame.Height, Channels = 1,
                        Intrinsics = frame.Intrinsics, CameraPosition = position, CameraRotation = rotation,
                        ScreenOrientation = Quaternion.identity, ImageOrientation = 0,
                        Tracking = ARSession.state.ToString(), Rmse = double.NaN, GpsDistance = double.NaN,
                        Phone = SystemInfo.deviceModel, Android = SystemInfo.operatingSystem,
                        Version = Application.version, Build = Application.buildGUID
                    };
                    if (Input.location.status == LocationServiceStatus.Running)
                    {
                        var gps = Input.location.lastData;
                        meta.GpsRunning = true;
                        meta.Latitude = gps.latitude; meta.Longitude = gps.longitude;
                        meta.GpsAccuracy = gps.horizontalAccuracy;
                        meta.GpsAge = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - gps.timestamp;
                        meta.GpsUsable = gps.horizontalAccuracy > 0 && Math.Abs(meta.GpsAge) < 10;
                    }
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
            if (reason.Length > 0 && stopReason.Length == 0) stopReason = reason;
            if (recorder != null && (Recording || endedAt == startedAt)) endedAt = Time.realtimeSinceStartup;
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
            recorder?.Disable(reason);
            video.Stop();
            if (sessionOpen && !recorder.Writing) FinishSession();
        }

        void FinishSession()
        {
            sessionOpen = false;
            try
            {
                string movie = Path.Combine(recorder.Folder, PaseoSessionVideo.FileName);
                session.fin = DateTimeOffset.Now.ToString("O");
                session.fotos_guardadas = recorder.Saved;
                session.fotos_omitidas = recorder.Dropped;
                session.video = File.Exists(movie) ? PaseoSessionVideo.FileName : "";
                session.video_bytes = File.Exists(movie) ? new FileInfo(movie).Length : 0;
                session.error_video = video.Error;
                session.video_cerrado = session.video_solicitado && session.video_bytes > 0 && video.Error.Length == 0;
                session.motivo = recorder.Failure.Length > 0 ? recorder.Failure : stopReason.Length > 0 ? stopReason : recorder.Notice;
                session.estado = session.motivo.Length == 0 && recorder.Saved > 0 &&
                    (!session.video_solicitado || session.video_cerrado) ? "completa" : "interrumpida";
                session.Save(recorder.Folder);
                PaseoSessionLog.FlushCurrent();
                PaseoCaptureSession.AttachLog(recorder.Folder, PaseoSessionLog.CurrentPath);
                Successful = session.Complete;
            }
            catch (Exception error) { stopReason = "No se pudo cerrar el paquete: " + error.Message; session.estado = "fallida"; }
        }

        public bool Play(string folder, out string reason)
        {
            reason = "";
            if (Busy) { reason = "Espera a que termine el guardado."; return false; }
            return video.Play(Path.Combine(folder, PaseoSessionVideo.FileName), out reason);
        }

        public string StopPlayback() { video.StopPlayback(); return video.Error; }
        public string PollPlayback() => video.PollPlayback();

        public bool PrepareCamera(out string description)
        {
            description = "Esperando cámara ARCore.";
            var cam = Camera.main;
            var manager = cam != null ? cam.GetComponent<ARCameraManager>() : null;
            if (manager == null || manager.subsystem == null || !manager.subsystem.running) return false;
            try
            {
                using var configs = manager.GetConfigurations(Allocator.Temp);
                if (!configs.IsCreated || configs.Length == 0) return false;
                var best = configs[0];
                foreach (var config in configs)
                    if (Math.Abs((long)config.width * config.height - 1920L * 1080) <
                        Math.Abs((long)best.width * best.height - 1920L * 1080)) best = config;
                if (!manager.currentConfiguration.HasValue || !manager.currentConfiguration.Value.Equals(best))
                    manager.currentConfiguration = best;
                description = $"Cámara {best.width}×{best.height}. " +
                    (best.width * best.height < 1920 * 1080 ? "Este teléfono ofrece menor resolución; se conserva la original disponible." : "Se conservan las imágenes originales.");
                Debug.Log("[AncoRA Paseo] CAPTURA_CAMARA " + description);
                return true;
            }
            catch (Exception error) { description = "No se pudo configurar la cámara: " + error.Message; return false; }
        }
        void OnDisable() { Stop("Captura interrumpida al cerrar la vista."); video.StopPlayback(); }
        void OnApplicationPause(bool paused) { if (paused) Stop("Barrido interrumpido; repetir sin pausar la aplicación."); }
    }
}
