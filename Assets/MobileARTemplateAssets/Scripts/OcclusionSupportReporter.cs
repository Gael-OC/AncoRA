using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// Reports whether the device actually produces environment depth.
    /// </summary>
    /// <remarks>
    /// Occlusion is requested, not guaranteed: <c>AROcclusionManager</c> asks for a depth mode and the
    /// provider gives what the hardware and the installed ARCore can manage. Without this, a phone
    /// that quietly refuses looks identical to one where the feature simply is not working, and the
    /// difference decides whether there is a bug to chase at all.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class OcclusionSupportReporter : MonoBehaviour
    {
        AROcclusionManager m_Manager;
        bool m_Reported;

        /// <summary>One line describing what the device offers, for the debug panel.</summary>
        public string Status { get; private set; } = "Oclusión: esperando la sesión...";

        void Awake()
        {
            m_Manager = FindAnyObjectByType<AROcclusionManager>();

            if (m_Manager == null)
                Status = "Oclusión: no hay AROcclusionManager en la escena.";
        }

        void Update()
        {
            if (m_Reported || m_Manager == null)
                return;

            var subsystem = m_Manager.subsystem;
            if (subsystem is not { running: true })
                return;

            var descriptor = subsystem.subsystemDescriptor;
            if (descriptor == null)
                return;

            m_Reported = true;

            var supported = descriptor.environmentDepthImageSupported;

            Status = supported switch
            {
                Supported.Supported => $"Oclusión activa ({m_Manager.currentEnvironmentDepthMode}).",
                Supported.Unsupported => "Oclusión NO soportada en este dispositivo.",
                _ => "Oclusión: el proveedor todavía no sabe si la soporta.",
            };

            Debug.Log(
                $"{Status}\n" +
                $"  pedido = {m_Manager.requestedEnvironmentDepthMode}\n" +
                $"  actual = {m_Manager.currentEnvironmentDepthMode}\n" +
                $"  preferencia = {m_Manager.currentOcclusionPreferenceMode}",
                this);
        }
    }
}
