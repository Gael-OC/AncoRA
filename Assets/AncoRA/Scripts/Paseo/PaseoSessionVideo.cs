using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.XR.ARCore;
#endif

namespace AncorRA.AR
{
    /// <summary>ARCore datasets contain raw camera/sensor input, never UI or previously rendered boxes.</summary>
    public sealed class PaseoSessionVideo
    {
        public const string FileName = "sesion-arcore.mp4";
        public bool Recording { get; private set; }
        public bool Playing { get; private set; }
        public string Error { get; private set; } = "";
#if UNITY_ANDROID && !UNITY_EDITOR
        ARCoreSessionSubsystem subsystem;
#endif

        public bool Start(string folder, out string reason)
        {
            reason = "";
            Error = "";
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var session = UnityEngine.Object.FindFirstObjectByType<ARSession>();
                subsystem = session != null ? session.subsystem as ARCoreSessionSubsystem : null;
                if (subsystem == null || subsystem.session == null)
                    throw new InvalidOperationException("ARCore aún no está listo.");
                using var config = new ArRecordingConfig(subsystem.session);
                config.SetMp4DatasetUri(subsystem.session, new Uri(Path.Combine(folder, FileName)).AbsoluteUri);
                config.SetAutoStopOnPause(subsystem.session, true);
                var status = subsystem.StartRecording(config);
                if (status != ArStatus.Success) throw new IOException("ARCore: " + status);
                Recording = true;
                return true;
            }
            catch (Exception error) { reason = Error = error.Message; return false; }
#else
            reason = Error = "El video ARCore se graba y reproduce en Android. Puedes probar Solo fotos en el Editor.";
            return false;
#endif
        }

        public void Stop()
        {
            if (!Recording) return;
            Recording = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // AutoStopOnPause may already have finalized the file.
                if (subsystem.recordingStatus == ArRecordingStatus.None) return;
                var status = subsystem.StopRecording();
                if (status != ArStatus.Success) Error = "No se pudo cerrar el video: " + status;
            }
            catch (Exception error) { Error = "No se pudo cerrar el video: " + error.Message; }
#endif
        }

        public string RecordingProblem()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Recording && subsystem.recordingStatus != ArRecordingStatus.Ok)
                return "ARCore dejó de grabar video: " + subsystem.recordingStatus;
#endif
            return "";
        }

        public bool Play(string path, out string reason)
        {
            reason = "";
            Error = "";
            if (!File.Exists(path)) { reason = "No se encontró el video de esta sesión."; return false; }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var session = UnityEngine.Object.FindFirstObjectByType<ARSession>();
                subsystem = session != null ? session.subsystem as ARCoreSessionSubsystem : null;
                if (subsystem == null) throw new InvalidOperationException("Espera a que ARCore esté listo.");
                var status = subsystem.StartPlaybackUri(new Uri(path).AbsoluteUri);
                if (status != ArStatus.Success) throw new IOException("No se pudo reproducir: " + status);
                Playing = true;
                return true;
            }
            catch (Exception error) { reason = Error = error.Message; return false; }
#else
            reason = "La reproducción de sensores ARCore funciona en un teléfono Android.";
            return false;
#endif
        }

        public string PollPlayback()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Playing && (subsystem.playbackStatus == ArPlaybackStatus.Finished || subsystem.playbackStatus == ArPlaybackStatus.IOError))
            {
                string result = subsystem.playbackStatus == ArPlaybackStatus.Finished ? "Reproducción terminada. Cámara en vivo." : "Error al leer el video ARCore.";
                StopPlayback();
                return Playing ? Error : result;
            }
#endif
            return "";
        }

        public void StopPlayback()
        {
            if (!Playing) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var status = subsystem.StopPlaybackUri();
                if (status != ArStatus.Success) { Error = "No se pudo volver a cámara: " + status; return; }
            }
            catch (Exception error) { Error = error.Message; return; }
#endif
            Playing = false;
        }
    }
}
