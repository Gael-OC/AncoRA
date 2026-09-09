using System;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine.XR.ARCore;
#endif

namespace AncorRA.AR
{
    /// <summary>
    /// Selects the camera configuration that gives image recognition the most pixels to work with.
    ///
    /// ARCore runs feature recognition on the CPU image, not on the preview texture, and that image
    /// defaults to VGA - 640x480 - however good the phone's sensor is. Detection needs the target to
    /// fill roughly a quarter of the frame, so those 640 pixels across are what actually caps how far
    /// away a sign can be recognised. Requesting a larger CPU stream is the one lever on that which
    /// does not involve reprinting the sign.
    ///
    /// The configuration list only exists once the session is running, and the sensor's own megapixel
    /// count is irrelevant: what can be asked for is exactly what ARCore publishes for that device,
    /// which is why this reports the whole list instead of assuming a ceiling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraConfigurationTuner : MonoBehaviour
    {
        /// <summary>Which configuration to ask the provider for.</summary>
        public enum Preference
        {
            /// <summary>Leave whatever the provider chose.</summary>
            DeviceDefault,

            /// <summary>The configuration whose CPU image has the most pixels.</summary>
            MaxCpuResolution,
        }

        [SerializeField]
        [Tooltip("Which camera configuration to request once the session is running.")]
        Preference m_Preference = Preference.MaxCpuResolution;

        // Setting a configuration can fail for reasons that clear up on a later frame - the session
        // still starting, a CPU image not yet disposed - but it can also fail permanently. Retrying
        // forever would restart the camera every frame, so the attempts are capped.
        const int k_MaxAttempts = 3;

        ARCameraManager m_CameraManager;
        ARSession m_Session;

        int m_Attempts;
        bool m_Settled;

        /// <summary>Resolution of the CPU image currently in use, or zero while it is unknown.</summary>
        public Vector2Int CurrentCpuResolution { get; private set; }

        /// <summary>Frame rate of the active configuration, or zero while it is unknown.</summary>
        public int CurrentFramerate { get; private set; }

        /// <summary>One line describing what happened, for the debug panel.</summary>
        public string Status { get; private set; } = "Esperando a que arranque la sesión...";

        /// <summary>Every configuration the device published, for the log.</summary>
        public string ConfigurationsReport { get; private set; } = string.Empty;

        /// <summary>Which configuration is being requested. Changing it re-runs the selection.</summary>
        public Preference Mode
        {
            get => m_Preference;
            set
            {
                if (m_Preference == value)
                    return;

                m_Preference = value;
                m_Attempts = 0;
                m_Settled = false;
            }
        }

        void Awake()
        {
            m_CameraManager = FindAnyObjectByType<ARCameraManager>();
            m_Session = FindAnyObjectByType<ARSession>();

            if (m_CameraManager == null)
                Status = "No encontré ARCameraManager: no puedo cambiar la configuración.";
        }

        void Update()
        {
            if (m_Settled || m_CameraManager == null)
                return;

            if (m_CameraManager.subsystem is not { running: true })
                return;

            using var configurations = m_CameraManager.GetConfigurations(Allocator.Temp);

            // The provider publishes nothing until the session has actually started, so an empty list
            // is a "not yet", not a "never".
            if (configurations.Length == 0)
                return;

            var report = new StringBuilder();
            report.AppendLine($"Configuraciones de cámara disponibles ({configurations.Length}):");

            var bestIndex = -1;
            var bestPixels = -1;
            var bestFramerate = -1;

            for (var i = 0; i < configurations.Length; i++)
            {
                var configuration = configurations[i];
                var cpu = CpuResolution(configuration);
                var framerate = configuration.framerate ?? 0;
                var pixels = cpu.x * cpu.y;

                report.AppendLine(
                    $"  [{i}] CPU {cpu.x}x{cpu.y} | textura {configuration.resolution.x}x{configuration.resolution.y}" +
                    $" | {framerate} fps");

                if (pixels > bestPixels || (pixels == bestPixels && framerate > bestFramerate))
                {
                    bestIndex = i;
                    bestPixels = pixels;
                    bestFramerate = framerate;
                }
            }

            ConfigurationsReport = report.ToString();

            var active = m_CameraManager.currentConfiguration;
            if (active.HasValue)
            {
                CurrentCpuResolution = CpuResolution(active.Value);
                CurrentFramerate = active.Value.framerate ?? 0;
            }

            if (m_Preference == Preference.DeviceDefault || bestIndex < 0)
            {
                m_Settled = true;
                Status = $"Configuración del dispositivo: CPU {CurrentCpuResolution.x}x{CurrentCpuResolution.y}.";
                Debug.Log($"{Status}\n{ConfigurationsReport}", this);
                return;
            }

            var target = configurations[bestIndex];

            // Applying a configuration restarts the camera, so skip it when it would change nothing.
            if (active.HasValue && active.Value.Equals(target))
            {
                m_Settled = true;
                Status = $"Ya estaba en la máxima: CPU {CurrentCpuResolution.x}x{CurrentCpuResolution.y}" +
                         $" a {CurrentFramerate} fps.";
                Debug.Log($"{Status}\n{ConfigurationsReport}", this);
                return;
            }

            m_Attempts++;

            try
            {
                m_CameraManager.currentConfiguration = target;
            }
            catch (Exception exception)
            {
                // Documented failures include the session not being valid yet and CPU images still
                // being held, both of which can clear up on a later frame.
                if (m_Attempts >= k_MaxAttempts)
                {
                    m_Settled = true;
                    Status = $"No pude cambiar la configuración: {exception.Message}";
                    Debug.LogWarning($"{Status}\n{ConfigurationsReport}", this);
                }

                return;
            }

            m_Settled = true;

            var applied = m_CameraManager.currentConfiguration;
            CurrentCpuResolution = CpuResolution(applied ?? target);
            CurrentFramerate = (applied ?? target).framerate ?? 0;

            Status = $"CPU {CurrentCpuResolution.x}x{CurrentCpuResolution.y} a {CurrentFramerate} fps.";
            Debug.Log($"Configuración de cámara aplicada: {Status}\n{ConfigurationsReport}", this);
        }

        /// <summary>
        /// Resolution of the CPU image for a configuration, asked of ARCore directly.
        /// </summary>
        /// <remarks>
        /// <see cref="XRCameraConfiguration.resolution"/> is the provider's single headline number and
        /// is not guaranteed to be the CPU image, which is the one recognition runs on. ARCore exposes
        /// the two separately, so the specific query beats the generic property here, and the generic
        /// one stays as the fallback for providers that have no such distinction.
        /// </remarks>
        Vector2Int CpuResolution(XRCameraConfiguration configuration)
        {
#if UNITY_ANDROID || UNITY_EDITOR
            if (m_Session != null && m_Session.subsystem is ARCoreSessionSubsystem arcore)
            {
                var (width, height) = configuration.AsArCameraConfig().GetImageDimensions(arcore.session);
                if (width > 0 && height > 0)
                    return new Vector2Int(width, height);
            }
#endif
            return configuration.resolution;
        }
    }
}
