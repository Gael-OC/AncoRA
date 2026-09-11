using System;
using System.Collections;
using System.Collections.Generic;
using Google.XR.ARCoreExtensions;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace AncorRA.AR
{
    public enum GeospatialProbeState
    {
        Idle,
        Permission,
        Location,
        CheckingVps,
        Localizing,
        ResolvingAnchor,
        Anchored,
        Failed,
    }

    public enum GeospatialPoseQuality
    {
        Unavailable,
        OutsideDiagnostic,
        Diagnostic,
        Preview,
        Final,
    }

    /// <summary>
    /// Field pilot for measuring Geospatial/VPS localization and placing a surveyed site proxy.
    /// Terrain resolution, preview visibility and final-quality approval are deliberately separate
    /// so a failed field test still says which stage failed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeospatialVpsProbe : MonoBehaviour
    {
        const string k_DiagnosticBuildId = "geospatial-diagnostic-v3";
        const double k_GpsStaleSeconds = 10d;

        [Header("AR dependencies")]
        [SerializeField] ARCoreExtensions m_Extensions;
        [SerializeField] AREarthManager m_EarthManager;
        [SerializeField] ARAnchorManager m_AnchorManager;
        [SerializeField] GeospatialSiteProfile m_Site;

        [Header("Diagnostic threshold (Google sample baseline)")]
        [SerializeField, Min(0.1f)] float m_DiagnosticHorizontalAccuracy = 20f;
        [SerializeField, Min(0.1f)] float m_DiagnosticYawAccuracy = 25f;

        [Header("Preview gate")]
        [SerializeField, Min(0.1f)] float m_PreviewHorizontalAccuracy = 5f;
        [SerializeField, Min(0.1f)] float m_PreviewVerticalAccuracy = 10f;
        [SerializeField, Min(0.1f)] float m_PreviewYawAccuracy = 10f;
        [SerializeField, Min(0.1f)] float m_PreviewStableSeconds = 1f;

        [Header("Final candidate gate")]
        [SerializeField, Min(0.1f)] float m_MaxHorizontalAccuracy = 1f;
        [SerializeField, Min(0.1f)] float m_MaxVerticalAccuracy = 2f;
        [SerializeField, Min(0.1f)] float m_MaxYawAccuracy = 1.5f;
        [SerializeField, Min(0.1f)] float m_RequiredStableSeconds = 2f;
        [Tooltip("Only raises a warning. The probe keeps collecting diagnostics after this time.")]
        [SerializeField, Min(5f)] float m_LocalizationTimeoutSeconds = 180f;

        [Header("Pilot")]
        [SerializeField] bool m_RunOnStart = true;
        [SerializeField] bool m_ShowHud = true;

        [Header("Field alignment")]
        [SerializeField, Min(0.05f)] float m_PositionStepMeters = 0.5f;
        [SerializeField, Min(0.1f)] float m_RotationStepDegrees = 1f;
        [SerializeField, Min(0.1f)] float m_SizeStepMeters = 0.5f;

        public GeospatialProbeState State { get; private set; } = GeospatialProbeState.Idle;
        public GeospatialPoseQuality PoseQuality { get; private set; } =
            GeospatialPoseQuality.Unavailable;
        public VpsAvailability VpsStatus { get; private set; } = VpsAvailability.Unknown;
        public GeospatialPose CameraPose { get; private set; }
        public string Status { get; private set; } = "Piloto Geospatial detenido";
        public string LastError { get; private set; } = string.Empty;
        public string TerrainAnchorStatus { get; private set; } = "No solicitado";
        public float SecondsToQualityPose { get; private set; } = -1f;
        public ARGeospatialAnchor ResolvedAnchor { get; private set; }

        Coroutine m_Run;
        VpsAvailabilityPromise m_VpsPromise;
        ResolveAnchorOnTerrainPromise m_TerrainPromise;
        ResolveAnchorOnRooftopPromise m_RooftopPromise;
        int m_RunNumber;
        string m_RunId = string.Empty;
        float m_StartedAt;
        float m_VpsCheckStartedAt = -1f;
        float m_VpsCheckElapsedSeconds = -1f;
        float m_SecondsToVpsResult = -1f;
        float m_LocalizationStartedAt;
        float m_LastPoseAt = -1f;
        float m_PreviewQualityStartedAt = -1f;
        float m_FinalQualityStartedAt = -1f;
        float m_SecondsToEarthTracking = -1f;
        float m_SecondsToPreview = -1f;
        float m_AnchorResolveStartedAt = -1f;
        bool m_LocalizationWarningRaised;
        bool m_HasValidPose;
        bool m_StartedLocationService;
        bool m_StartedCompass;
        bool m_ForceUnreliablePreview;
        bool m_ShowAlignmentControls;
        string m_EarthState = "Unavailable";
        string m_EarthTrackingState = "Unavailable";
        string m_PreviousEarthState = string.Empty;
        string m_PreviousEarthTrackingState = string.Empty;
        string m_ARSessionState = "Unknown";
        string m_VpsPromiseState = "NotStarted";
        string m_AnchorTrackingState = "None";
        string m_AnchorPromiseState = "NotStarted";
        string m_NavigationSource = "Sin posición válida";
        string m_CopyConfirmation = string.Empty;
        float m_CopyConfirmationUntil;
        bool m_HasTargetLocation;
        bool m_HasDirection;
        double m_SourceLatitude;
        double m_SourceLongitude;
        double m_DistanceToAnchorMeters;
        double m_BearingToAnchorDegrees;
        float m_DeviceHeadingDegrees;
        float m_RelativeBearingDegrees;
        float m_CompassAccuracyDegrees;
        bool m_HasGpsSample;
        bool m_GpsSampleStale;
        double m_GpsLatitude;
        double m_GpsLongitude;
        double m_GpsTimestampSeconds;
        double m_GpsAgeSeconds = -1d;
        float m_GpsHorizontalAccuracyMeters = -1f;
        float m_WorldDistanceToAnchorMeters = -1f;
        float m_AnchorVerticalOffsetMeters;
        float m_AnchorForwardDot;
        Vector2 m_HudScroll;
        Vector3 m_RuntimeLocalPosition;
        Vector3 m_RuntimeLocalEulerAngles;
        Vector3 m_RuntimeSize;
        Camera m_Camera;
        GameObject m_Content;
        Mesh m_HouseMesh;
        Mesh m_EdgeMesh;
        MeshRenderer m_ContentRenderer;
        MeshRenderer m_EdgeRenderer;
        Material m_WallMaterial;
        Material m_RoofMaterial;
        Material m_EdgeMaterial;
        GeospatialPoseQuality m_LastAppearanceQuality = (GeospatialPoseQuality)(-1);
        bool m_LastAppearanceForced;
        readonly List<string> m_EventLog = new();
        GUIStyle m_TitleStyle;
        GUIStyle m_WarningStyle;
        GUIStyle m_ArrowStyle;

        public void Configure(
            ARCoreExtensions extensions,
            AREarthManager earthManager,
            ARAnchorManager anchorManager,
            GeospatialSiteProfile site)
        {
            m_Extensions = extensions;
            m_EarthManager = earthManager;
            m_AnchorManager = anchorManager;
            m_Site = site;
        }

        void Awake()
        {
            m_Camera = Camera.main;
            ResetRuntimeAlignment();
        }

        void Start()
        {
            if (m_RunOnStart)
                Retry();
        }

        void Update()
        {
            UpdateLiveDiagnostics();
            UpdatePoseQuality();
            UpdateNavigation();
            UpdateContentVisibility();
        }

        void OnDisable()
        {
            StopRun();
            if (m_StartedLocationService && Input.location.status == LocationServiceStatus.Running)
                Input.location.Stop();
            if (m_StartedCompass)
                Input.compass.enabled = false;

            m_StartedLocationService = false;
            m_StartedCompass = false;
        }

        void OnDestroy()
        {
            ReleaseGeneratedContent();
        }

        public void Retry()
        {
            StopRun();
            DestroyResolvedAnchor();
            ResetRuntimeAlignment();
            m_RunNumber++;
            m_RunId = Guid.NewGuid().ToString("N");
            m_StartedAt = Time.realtimeSinceStartup;
            ResetMeasurements();
            RecordEvent($"Inicio ejecución #{m_RunNumber} ({m_RunId})");
            m_Run = StartCoroutine(RunPilot());
        }

        IEnumerator RunPilot()
        {
            if (!ValidateConfiguration())
                yield break;

            SetState(GeospatialProbeState.Permission, "Solicitando ubicación precisa…");
            yield return EnsureLocationPermission();
            if (State == GeospatialProbeState.Failed)
                yield break;

            Status = "Comprobando compatibilidad Geospatial del dispositivo…";
            yield return CheckDeviceSupport();
            if (State == GeospatialProbeState.Failed)
                yield break;

            SetState(GeospatialProbeState.Location, "Obteniendo GPS inicial…");
            yield return StartLocationService();
            if (State == GeospatialProbeState.Failed)
                yield break;

            var checkLatitude = m_Site != null && m_Site.HasCoordinates
                ? m_Site.Latitude
                : Input.location.lastData.latitude;
            var checkLongitude = m_Site != null && m_Site.HasCoordinates
                ? m_Site.Longitude
                : Input.location.lastData.longitude;

            SetState(GeospatialProbeState.CheckingVps, "Consultando cobertura VPS…");
            m_VpsCheckStartedAt = Time.realtimeSinceStartup;
            m_VpsCheckElapsedSeconds = 0f;
            m_VpsPromiseState = PromiseState.Pending.ToString();
            m_VpsPromise = AREarthManager.CheckVpsAvailabilityAsync(checkLatitude, checkLongitude);
            while (m_VpsPromise.State == PromiseState.Pending)
            {
                m_VpsCheckElapsedSeconds = Time.realtimeSinceStartup - m_VpsCheckStartedAt;
                Status = $"VPS Pending — {m_VpsCheckElapsedSeconds:F1} s";
                yield return null;
            }

            m_VpsCheckElapsedSeconds = Time.realtimeSinceStartup - m_VpsCheckStartedAt;
            m_VpsPromiseState = m_VpsPromise.State.ToString();
            if (m_VpsPromise.State == PromiseState.Cancelled)
            {
                m_VpsPromise = null;
                Fail("La consulta de cobertura VPS fue cancelada.");
                yield break;
            }

            VpsStatus = m_VpsPromise.Result;
            m_SecondsToVpsResult = m_VpsCheckElapsedSeconds;
            m_VpsPromise = null;
            RecordEvent($"VPS: {VpsStatus} en {m_SecondsToVpsResult:F1} s");

            m_LocalizationStartedAt = Time.realtimeSinceStartup;
            SetState(
                GeospatialProbeState.Localizing,
                VpsStatus == VpsAvailability.Available
                    ? "VPS disponible; esperando EarthTrackingState.Tracking…"
                    : $"VPS: {VpsStatus}; esperando pose Geospatial…");

            while (!HasEarthTracking())
            {
                if (IsFatalEarthState())
                {
                    Fail($"EarthState impide localizar: {m_EarthState}");
                    yield break;
                }

                var elapsed = Time.realtimeSinceStartup - m_LocalizationStartedAt;
                if (elapsed >= m_LocalizationTimeoutSeconds)
                {
                    if (!m_LocalizationWarningRaised)
                    {
                        m_LocalizationWarningRaised = true;
                        RecordEvent(
                            $"Advertencia: EarthTrackingState no llegó a Tracking en {elapsed:F0} s");
                        Debug.LogWarning(
                            $"[AncoRA Geospatial] Sin Tracking en {elapsed:F0} s; " +
                            "el diagnóstico continúa.", this);
                    }

                    Status = $"SIN TRACKING tras {elapsed:F0} s — continúa midiendo; " +
                        $"Earth {m_EarthState}/{m_EarthTrackingState}";
                }
                else
                {
                    Status = $"Esperando Tracking {elapsed:F0}/{m_LocalizationTimeoutSeconds:F0} s — " +
                        $"Earth {m_EarthState}/{m_EarthTrackingState}";
                }

                yield return null;
            }

            m_SecondsToEarthTracking = Time.realtimeSinceStartup - m_StartedAt;
            RecordEvent($"Primer Earth Tracking a los {m_SecondsToEarthTracking:F1} s");

            if (m_Site == null || !m_Site.HasCoordinates)
            {
                SetState(
                    GeospatialProbeState.Idle,
                    "Pose disponible, pero falta una coordenada válida en el perfil del sitio.");
                m_Run = null;
                yield break;
            }

            // Terrain resolution requires Earth tracking, but it does not require this app's
            // strict final-quality gate. Resolve now so Terrain failures and placement can be
            // diagnosed independently from pose quality.
            yield return ResolveSiteAnchor();
            m_Run = null;
        }

        IEnumerator ResolveSiteAnchor()
        {
            ResetRuntimeAlignment();
            m_AnchorResolveStartedAt = Time.realtimeSinceStartup;
            m_AnchorPromiseState = "Pending";
            TerrainAnchorStatus = $"{m_Site.AltitudeMode}: Pending";
            SetState(
                GeospatialProbeState.ResolvingAnchor,
                $"Resolviendo ancla {m_Site.AltitudeMode}…");

            var rotation = Quaternion.AngleAxis(180f - m_Site.HeadingDegrees, Vector3.up);
            if (m_Site.AltitudeMode == GeospatialAltitudeMode.Rooftop)
            {
                m_RooftopPromise = m_AnchorManager.ResolveAnchorOnRooftopAsync(
                    m_Site.Latitude, m_Site.Longitude, m_Site.AltitudeAboveSurface, rotation);
                while (m_RooftopPromise.State == PromiseState.Pending)
                {
                    Status = $"Rooftop Pending — " +
                        $"{Time.realtimeSinceStartup - m_AnchorResolveStartedAt:F1} s";
                    yield return null;
                }

                m_AnchorPromiseState = m_RooftopPromise.State.ToString();
                if (m_RooftopPromise.State == PromiseState.Cancelled)
                {
                    TerrainAnchorStatus = "Rooftop: Cancelled";
                    Fail("La resolución del ancla Rooftop fue cancelada.");
                    yield break;
                }

                var result = m_RooftopPromise.Result;
                TerrainAnchorStatus = $"Rooftop: {result.RooftopAnchorState}";
                if (result.RooftopAnchorState != RooftopAnchorState.Success || result.Anchor == null)
                {
                    Fail($"Falló ancla Rooftop: {result.RooftopAnchorState}");
                    yield break;
                }

                ResolvedAnchor = result.Anchor;
                m_RooftopPromise = null;
            }
            else
            {
                m_TerrainPromise = m_AnchorManager.ResolveAnchorOnTerrainAsync(
                    m_Site.Latitude, m_Site.Longitude, m_Site.AltitudeAboveSurface, rotation);
                while (m_TerrainPromise.State == PromiseState.Pending)
                {
                    Status = $"Terrain Pending — " +
                        $"{Time.realtimeSinceStartup - m_AnchorResolveStartedAt:F1} s";
                    yield return null;
                }

                m_AnchorPromiseState = m_TerrainPromise.State.ToString();
                if (m_TerrainPromise.State == PromiseState.Cancelled)
                {
                    TerrainAnchorStatus = "Terrain: Cancelled";
                    Fail("La resolución del ancla Terrain fue cancelada.");
                    yield break;
                }

                var result = m_TerrainPromise.Result;
                TerrainAnchorStatus = $"Terrain: {result.TerrainAnchorState}";
                if (result.TerrainAnchorState != TerrainAnchorState.Success || result.Anchor == null)
                {
                    Fail($"Falló ancla Terrain: {result.TerrainAnchorState}");
                    yield break;
                }

                ResolvedAnchor = result.Anchor;
                m_TerrainPromise = null;
            }

            BuildContent(ResolvedAnchor.transform);
            RecordEvent(
                $"{m_Site.AltitudeMode} resuelta en " +
                $"{Time.realtimeSinceStartup - m_AnchorResolveStartedAt:F1} s");
            SetState(
                GeospatialProbeState.Anchored,
                "Ancla resuelta; la visibilidad depende del umbral de previsualización.");
        }

        void UpdateLiveDiagnostics()
        {
            m_ARSessionState = ARSession.state.ToString();
            if (m_EarthManager == null)
            {
                m_EarthState = "ManagerMissing";
                m_EarthTrackingState = "ManagerMissing";
                m_HasValidPose = false;
                return;
            }

            m_EarthState = m_EarthManager.EarthState.ToString();
            var trackingState = m_EarthManager.EarthTrackingState;
            m_EarthTrackingState = trackingState.ToString();
            if (m_EarthState != m_PreviousEarthState)
            {
                RecordEvent($"EarthState: {m_EarthState}");
                m_PreviousEarthState = m_EarthState;
            }

            if (m_EarthTrackingState != m_PreviousEarthTrackingState)
            {
                RecordEvent($"EarthTrackingState: {m_EarthTrackingState}");
                m_PreviousEarthTrackingState = m_EarthTrackingState;
            }

            m_HasValidPose = trackingState == TrackingState.Tracking;
            if (m_HasValidPose)
            {
                CameraPose = m_EarthManager.CameraGeospatialPose;
                m_LastPoseAt = Time.realtimeSinceStartup;
            }

            m_AnchorTrackingState = ResolvedAnchor != null
                ? ResolvedAnchor.trackingState.ToString()
                : "None";
        }

        void UpdatePoseQuality()
        {
            if (!m_HasValidPose)
            {
                PoseQuality = GeospatialPoseQuality.Unavailable;
                m_PreviewQualityStartedAt = -1f;
                m_FinalQualityStartedAt = -1f;
                return;
            }

            var now = Time.realtimeSinceStartup;
            var passesDiagnostic =
                CameraPose.HorizontalAccuracy <= m_DiagnosticHorizontalAccuracy &&
                CameraPose.OrientationYawAccuracy <= m_DiagnosticYawAccuracy;
            var passesPreview = PosePassesQuality(
                CameraPose,
                m_PreviewHorizontalAccuracy,
                m_PreviewVerticalAccuracy,
                m_PreviewYawAccuracy);
            var passesFinal = PosePassesQuality(
                CameraPose,
                m_MaxHorizontalAccuracy,
                m_MaxVerticalAccuracy,
                m_MaxYawAccuracy);

            m_PreviewQualityStartedAt = UpdateStableStart(m_PreviewQualityStartedAt, passesPreview, now);
            m_FinalQualityStartedAt = UpdateStableStart(m_FinalQualityStartedAt, passesFinal, now);

            var previewStable = m_PreviewQualityStartedAt >= 0f &&
                now - m_PreviewQualityStartedAt >= m_PreviewStableSeconds;
            var finalStable = m_FinalQualityStartedAt >= 0f &&
                now - m_FinalQualityStartedAt >= m_RequiredStableSeconds;

            var previous = PoseQuality;
            if (finalStable)
                PoseQuality = GeospatialPoseQuality.Final;
            else if (previewStable)
                PoseQuality = GeospatialPoseQuality.Preview;
            else if (passesDiagnostic)
                PoseQuality = GeospatialPoseQuality.Diagnostic;
            else
                PoseQuality = GeospatialPoseQuality.OutsideDiagnostic;

            if (PoseQuality == GeospatialPoseQuality.Preview && m_SecondsToPreview < 0f)
            {
                m_SecondsToPreview = now - m_StartedAt;
                RecordEvent($"Umbral Preview estable a los {m_SecondsToPreview:F1} s");
            }

            if (PoseQuality == GeospatialPoseQuality.Final && SecondsToQualityPose < 0f)
            {
                SecondsToQualityPose = now - m_StartedAt;
                RecordEvent($"Umbral Final estable a los {SecondsToQualityPose:F1} s");
            }

            if (State == GeospatialProbeState.Anchored)
            {
                if (PoseQuality == GeospatialPoseQuality.Final)
                    Status = "ANCLA FINAL CANDIDATA — confirma alineación visual en terreno";
                else if (PoseQuality == GeospatialPoseQuality.Preview)
                    Status = "PREVISUALIZACIÓN — pose aproximada, no aprobada";
                else if (m_ForceUnreliablePreview)
                    Status = "POSE NO CONFIABLE — bloque forzado solo para diagnóstico";
                else
                    Status = "Ancla resuelta; esperando calidad Preview o activa modo diagnóstico";
            }

            if (previous != PoseQuality)
                RecordEvent($"Calidad de pose: {PoseQuality}");
        }

        void UpdateNavigation()
        {
            m_HasTargetLocation = false;
            m_HasDirection = false;
            m_NavigationSource = "Sin posición válida";
            m_SourceLatitude = 0d;
            m_SourceLongitude = 0d;
            m_DeviceHeadingDegrees = 0f;
            m_RelativeBearingDegrees = 0f;
            m_CompassAccuracyDegrees = -1f;
            m_DistanceToAnchorMeters = 0d;
            m_BearingToAnchorDegrees = 0d;
            m_WorldDistanceToAnchorMeters = -1f;
            m_AnchorVerticalOffsetMeters = 0f;
            m_AnchorForwardDot = 0f;
            UpdateGpsDiagnostics();

            if (m_Site == null || !m_Site.HasCoordinates)
                return;

            if (m_HasValidPose)
            {
                m_SourceLatitude = CameraPose.Latitude;
                m_SourceLongitude = CameraPose.Longitude;
                m_NavigationSource = "Pose Geospatial";
                m_HasTargetLocation = true;
                if (TryHeadingFromEunRotation(CameraPose.EunRotation, out var heading))
                {
                    m_DeviceHeadingDegrees = heading;
                    m_HasDirection = true;
                }
            }
            else if (m_HasGpsSample)
            {
                m_SourceLatitude = m_GpsLatitude;
                m_SourceLongitude = m_GpsLongitude;
                m_NavigationSource = m_GpsSampleStale
                    ? "GPS/compás ANTIGUO (sin pose Earth)"
                    : "GPS/compás aproximado (sin pose Earth)";
                m_HasTargetLocation = true;
                m_CompassAccuracyDegrees = Input.compass.headingAccuracy;
                if (Input.compass.enabled && m_CompassAccuracyDegrees >= 0f)
                {
                    m_DeviceHeadingDegrees = Input.compass.trueHeading;
                    m_HasDirection = true;
                }
            }

            if (!m_HasTargetLocation)
                return;

            CalculateDistanceAndBearing(
                m_SourceLatitude,
                m_SourceLongitude,
                m_Site.Latitude,
                m_Site.Longitude,
                out m_DistanceToAnchorMeters,
                out m_BearingToAnchorDegrees);

            if (ResolvedAnchor != null && m_Camera != null)
            {
                var cameraToAnchor = ResolvedAnchor.transform.position - m_Camera.transform.position;
                m_WorldDistanceToAnchorMeters = cameraToAnchor.magnitude;
                m_AnchorVerticalOffsetMeters = cameraToAnchor.y;
                m_AnchorForwardDot = cameraToAnchor.sqrMagnitude > 0.0001f
                    ? Vector3.Dot(m_Camera.transform.forward, cameraToAnchor.normalized)
                    : 0f;
                var cameraForward = Vector3.ProjectOnPlane(m_Camera.transform.forward, Vector3.up);
                var toAnchor = Vector3.ProjectOnPlane(
                    cameraToAnchor,
                    Vector3.up);
                if (cameraForward.sqrMagnitude > 0.0001f && toAnchor.sqrMagnitude > 0.0001f)
                {
                    m_RelativeBearingDegrees = Vector3.SignedAngle(
                        cameraForward, toAnchor, Vector3.up);
                    m_HasDirection = true;
                    return;
                }
            }

            if (m_HasDirection)
            {
                m_RelativeBearingDegrees = Mathf.DeltaAngle(
                    m_DeviceHeadingDegrees, (float)m_BearingToAnchorDegrees);
            }
        }

        void UpdateGpsDiagnostics()
        {
            m_HasGpsSample = false;
            m_GpsSampleStale = false;
            m_GpsLatitude = 0d;
            m_GpsLongitude = 0d;
            m_GpsTimestampSeconds = 0d;
            m_GpsAgeSeconds = -1d;
            m_GpsHorizontalAccuracyMeters = -1f;

            if (Input.location.status != LocationServiceStatus.Running)
                return;

            var location = Input.location.lastData;
            m_GpsLatitude = location.latitude;
            m_GpsLongitude = location.longitude;
            m_GpsTimestampSeconds = location.timestamp;
            m_GpsHorizontalAccuracyMeters = location.horizontalAccuracy;
            m_HasGpsSample = CoordinatesAreValid(m_GpsLatitude, m_GpsLongitude);

            if (m_GpsTimestampSeconds > 0d)
            {
                var unixNow = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
                m_GpsAgeSeconds = Math.Max(0d, unixNow - m_GpsTimestampSeconds);
            }

            m_GpsSampleStale = !m_HasGpsSample ||
                m_GpsAgeSeconds < 0d ||
                m_GpsAgeSeconds > k_GpsStaleSeconds;
        }

        static bool CoordinatesAreValid(double latitude, double longitude) =>
            latitude >= -90d && latitude <= 90d &&
            longitude >= -180d && longitude <= 180d &&
            !(Math.Abs(latitude) < 0.000001d && Math.Abs(longitude) < 0.000001d);

        bool ValidateConfiguration()
        {
            if (m_Extensions == null || m_EarthManager == null || m_AnchorManager == null)
            {
                Fail("Faltan ARCore Extensions, AREarthManager o ARAnchorManager.");
                return false;
            }

            if (m_Site == null)
            {
                Fail("Falta la referencia al GeospatialSiteProfile en la escena.");
                return false;
            }

            if (m_Extensions.ARCoreExtensionsConfig == null ||
                m_Extensions.ARCoreExtensionsConfig.GeospatialMode != GeospatialMode.Enabled)
            {
                Fail("El asset ARCoreExtensionsConfig no tiene Geospatial habilitado.");
                return false;
            }

            return true;
        }

        IEnumerator EnsureLocationPermission()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
                var deadline = Time.realtimeSinceStartup + 15f;
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) &&
                    Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                {
                    Fail("Se necesita permiso de ubicación precisa para VPS.");
                    yield break;
                }
            }
#endif
            yield return null;
        }

        IEnumerator StartLocationService()
        {
            if (!Input.location.isEnabledByUser)
            {
                Fail("Activa la ubicación precisa del dispositivo.");
                yield break;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Input.location.Start(1f, 0.5f);
                m_StartedLocationService = true;
            }

            if (!Input.compass.enabled)
            {
                Input.compass.enabled = true;
                m_StartedCompass = true;
            }

            var deadline = Time.realtimeSinceStartup + 15f;
            while (Input.location.status == LocationServiceStatus.Initializing &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
                Fail($"GPS no disponible: {Input.location.status}");
        }

        IEnumerator CheckDeviceSupport()
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            var support = FeatureSupported.Unknown;

            while (support == FeatureSupported.Unknown && Time.realtimeSinceStartup < deadline)
            {
                support = m_EarthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
                if (support == FeatureSupported.Unknown)
                    yield return null;
            }

            if (support == FeatureSupported.Unsupported)
                Fail("Este dispositivo no admite ARCore Geospatial.");
            else if (support == FeatureSupported.Unknown)
                Fail("No se pudo confirmar compatibilidad Geospatial en 10 s.");
        }

        bool HasEarthTracking() =>
            m_EarthManager != null &&
            m_EarthManager.EarthState == EarthState.Enabled &&
            m_EarthManager.EarthTrackingState == TrackingState.Tracking;

        bool IsFatalEarthState()
        {
            if (m_EarthManager == null)
                return true;

            var earthState = m_EarthManager.EarthState;
            return earthState != EarthState.Enabled &&
                earthState != EarthState.ErrorEarthNotReady &&
                earthState != EarthState.ErrorSessionNotReady;
        }

        static float UpdateStableStart(float startedAt, bool passes, float now)
        {
            if (!passes)
                return -1f;
            return startedAt < 0f ? now : startedAt;
        }

        static bool PosePassesQuality(
            GeospatialPose pose,
            float horizontal,
            float vertical,
            float yaw) =>
            pose.HorizontalAccuracy <= horizontal &&
            pose.VerticalAccuracy <= vertical &&
            pose.OrientationYawAccuracy <= yaw;

        static bool TryHeadingFromEunRotation(Quaternion rotation, out float heading)
        {
            var forward = rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                heading = 0f;
                return false;
            }

            heading = Mathf.Repeat(
                Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
                360f);
            return true;
        }

        public static void CalculateDistanceAndBearing(
            double fromLatitude,
            double fromLongitude,
            double toLatitude,
            double toLongitude,
            out double distanceMeters,
            out double bearingDegrees)
        {
            const double earthRadiusMeters = 6371000d;
            var fromLat = fromLatitude * Math.PI / 180d;
            var toLat = toLatitude * Math.PI / 180d;
            var deltaLat = (toLatitude - fromLatitude) * Math.PI / 180d;
            var deltaLon = (toLongitude - fromLongitude) * Math.PI / 180d;
            var sinLat = Math.Sin(deltaLat * 0.5d);
            var sinLon = Math.Sin(deltaLon * 0.5d);
            var a = sinLat * sinLat + Math.Cos(fromLat) * Math.Cos(toLat) * sinLon * sinLon;
            var centralAngle = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0d, 1d - a)));
            distanceMeters = earthRadiusMeters * centralAngle;

            var y = Math.Sin(deltaLon) * Math.Cos(toLat);
            var x = Math.Cos(fromLat) * Math.Sin(toLat) -
                Math.Sin(fromLat) * Math.Cos(toLat) * Math.Cos(deltaLon);
            bearingDegrees = (Math.Atan2(y, x) * 180d / Math.PI + 360d) % 360d;
        }

        void BuildContent(Transform anchor)
        {
            ReleaseGeneratedContent();

            m_Content = new GameObject("Geospatial Building Probe");
            m_Content.transform.SetParent(anchor, false);
            m_Content.transform.SetLocalPositionAndRotation(
                m_RuntimeLocalPosition, Quaternion.Euler(m_RuntimeLocalEulerAngles));

            m_HouseMesh = HouseMeshBuilder.Build(
                m_RuntimeSize, m_Site.RoofHeight, m_Site.RidgeAlongWidth);
            m_Content.AddComponent<MeshFilter>().sharedMesh = m_HouseMesh;
            m_ContentRenderer = m_Content.AddComponent<MeshRenderer>();
            // The field proxy is a measurement instrument, so it must remain legible even when
            // the AR scene has no light estimation or directional light.
            m_WallMaterial = PipelineMaterials.CreateUnlit(new Color(0.15f, 0.55f, 0.95f, 0.42f));
            m_RoofMaterial = PipelineMaterials.CreateUnlit(new Color(0.08f, 0.3f, 0.65f, 0.52f));
            m_ContentRenderer.sharedMaterials = new[] { m_WallMaterial, m_RoofMaterial };

            var edges = new GameObject("Edges");
            edges.transform.SetParent(m_Content.transform, false);
            m_EdgeMesh = HouseMeshBuilder.BuildEdges(
                m_RuntimeSize, m_Site.RoofHeight, m_Site.RidgeAlongWidth);
            edges.AddComponent<MeshFilter>().sharedMesh = m_EdgeMesh;
            m_EdgeMaterial = PipelineMaterials.CreateUnlit(new Color(0.02f, 0.08f, 0.15f));
            m_EdgeRenderer = edges.AddComponent<MeshRenderer>();
            m_EdgeRenderer.sharedMaterial = m_EdgeMaterial;
            m_LastAppearanceQuality = (GeospatialPoseQuality)(-1);
            UpdateContentVisibility();
        }

        void UpdateContentVisibility()
        {
            if (m_Content == null)
                return;

            var shouldShow = PoseQuality >= GeospatialPoseQuality.Preview ||
                m_ForceUnreliablePreview;
            if (m_Content.activeSelf != shouldShow)
                m_Content.SetActive(shouldShow);

            if (!shouldShow ||
                (m_LastAppearanceQuality == PoseQuality &&
                    m_LastAppearanceForced == m_ForceUnreliablePreview))
            {
                return;
            }

            var unreliable = m_ForceUnreliablePreview &&
                PoseQuality < GeospatialPoseQuality.Preview;
            if (unreliable)
            {
                PipelineMaterials.SetColor(m_WallMaterial, new Color(1f, 0.05f, 0.45f, 0.58f));
                PipelineMaterials.SetColor(m_RoofMaterial, new Color(1f, 0.55f, 0.05f, 0.68f));
                PipelineMaterials.SetColor(m_EdgeMaterial, Color.white);
            }
            else if (PoseQuality == GeospatialPoseQuality.Preview)
            {
                PipelineMaterials.SetColor(m_WallMaterial, new Color(1f, 0.72f, 0.08f, 0.42f));
                PipelineMaterials.SetColor(m_RoofMaterial, new Color(1f, 0.42f, 0.05f, 0.52f));
                PipelineMaterials.SetColor(m_EdgeMaterial, new Color(0.2f, 0.08f, 0.01f));
            }
            else
            {
                PipelineMaterials.SetColor(m_WallMaterial, new Color(0.15f, 0.55f, 0.95f, 0.42f));
                PipelineMaterials.SetColor(m_RoofMaterial, new Color(0.08f, 0.3f, 0.65f, 0.52f));
                PipelineMaterials.SetColor(m_EdgeMaterial, new Color(0.02f, 0.08f, 0.15f));
            }

            m_LastAppearanceQuality = PoseQuality;
            m_LastAppearanceForced = m_ForceUnreliablePreview;
        }

        void ResetRuntimeAlignment()
        {
            if (m_Site == null)
                return;

            m_RuntimeLocalPosition = m_Site.LocalPosition;
            m_RuntimeLocalEulerAngles = m_Site.LocalEulerAngles;
            m_RuntimeSize = m_Site.SizeMeters;
        }

        void ResetMeasurements()
        {
            m_EventLog.Clear();
            State = GeospatialProbeState.Idle;
            PoseQuality = GeospatialPoseQuality.Unavailable;
            Status = "Reiniciando piloto…";
            LastError = string.Empty;
            TerrainAnchorStatus = "No solicitado";
            VpsStatus = VpsAvailability.Unknown;
            CameraPose = new GeospatialPose();
            m_LastPoseAt = -1f;
            m_VpsCheckStartedAt = -1f;
            m_VpsCheckElapsedSeconds = -1f;
            m_SecondsToVpsResult = -1f;
            m_LocalizationStartedAt = -1f;
            SecondsToQualityPose = -1f;
            m_SecondsToEarthTracking = -1f;
            m_SecondsToPreview = -1f;
            m_LocalizationWarningRaised = false;
            m_HasValidPose = false;
            m_ForceUnreliablePreview = false;
            m_PreviewQualityStartedAt = -1f;
            m_FinalQualityStartedAt = -1f;
            m_AnchorResolveStartedAt = -1f;
            m_VpsPromiseState = "NotStarted";
            m_AnchorPromiseState = "NotStarted";
            m_AnchorTrackingState = "None";
            m_EarthState = "Unavailable";
            m_EarthTrackingState = "Unavailable";
            m_PreviousEarthState = string.Empty;
            m_PreviousEarthTrackingState = string.Empty;
            m_ARSessionState = "Unknown";
            m_NavigationSource = "Sin posición válida";
            m_SourceLatitude = 0d;
            m_SourceLongitude = 0d;
            m_DistanceToAnchorMeters = 0d;
            m_BearingToAnchorDegrees = 0d;
            m_DeviceHeadingDegrees = 0f;
            m_RelativeBearingDegrees = 0f;
            m_HasTargetLocation = false;
            m_HasDirection = false;
            m_HasGpsSample = false;
            m_GpsSampleStale = false;
            m_GpsLatitude = 0d;
            m_GpsLongitude = 0d;
            m_GpsTimestampSeconds = 0d;
            m_GpsAgeSeconds = -1d;
            m_GpsHorizontalAccuracyMeters = -1f;
            m_CompassAccuracyDegrees = -1f;
            m_WorldDistanceToAnchorMeters = -1f;
            m_AnchorVerticalOffsetMeters = 0f;
            m_AnchorForwardDot = 0f;
            m_CopyConfirmation = string.Empty;
            m_CopyConfirmationUntil = 0f;
            m_ShowAlignmentControls = false;
        }

        void NudgePosition(Vector3 direction)
        {
            m_RuntimeLocalPosition += direction * m_PositionStepMeters;
            ApplyRuntimeTransform();
        }

        void NudgeRotation(float direction)
        {
            m_RuntimeLocalEulerAngles.y += direction * m_RotationStepDegrees;
            ApplyRuntimeTransform();
        }

        void NudgeSize(Vector3 direction)
        {
            m_RuntimeSize += direction * m_SizeStepMeters;
            m_RuntimeSize.x = Mathf.Max(0.5f, m_RuntimeSize.x);
            m_RuntimeSize.y = Mathf.Max(m_Site.RoofHeight + 0.1f, m_RuntimeSize.y);
            m_RuntimeSize.z = Mathf.Max(0.5f, m_RuntimeSize.z);

            if (ResolvedAnchor != null)
                BuildContent(ResolvedAnchor.transform);
        }

        void ApplyRuntimeTransform()
        {
            if (m_Content == null)
                return;

            m_Content.transform.SetLocalPositionAndRotation(
                m_RuntimeLocalPosition, Quaternion.Euler(m_RuntimeLocalEulerAngles));
        }

        void StopRun()
        {
            if (m_VpsPromise != null && m_VpsPromise.State == PromiseState.Pending)
                m_VpsPromise.Cancel();
            if (m_TerrainPromise != null && m_TerrainPromise.State == PromiseState.Pending)
                m_TerrainPromise.Cancel();
            if (m_RooftopPromise != null && m_RooftopPromise.State == PromiseState.Pending)
                m_RooftopPromise.Cancel();

            m_VpsPromise = null;
            m_TerrainPromise = null;
            m_RooftopPromise = null;
            if (m_Run == null)
                return;

            StopCoroutine(m_Run);
            m_Run = null;
        }

        void DestroyResolvedAnchor()
        {
            ReleaseGeneratedContent();
            if (ResolvedAnchor != null)
                Destroy(ResolvedAnchor.gameObject);
            ResolvedAnchor = null;
        }

        void ReleaseGeneratedContent()
        {
            if (m_Content != null)
            {
                m_Content.SetActive(false);
                Destroy(m_Content);
            }
            if (m_HouseMesh != null)
                Destroy(m_HouseMesh);
            if (m_EdgeMesh != null)
                Destroy(m_EdgeMesh);
            if (m_WallMaterial != null)
                Destroy(m_WallMaterial);
            if (m_RoofMaterial != null)
                Destroy(m_RoofMaterial);
            if (m_EdgeMaterial != null)
                Destroy(m_EdgeMaterial);

            m_Content = null;
            m_HouseMesh = null;
            m_EdgeMesh = null;
            m_ContentRenderer = null;
            m_EdgeRenderer = null;
            m_WallMaterial = null;
            m_RoofMaterial = null;
            m_EdgeMaterial = null;
        }

        void SetState(GeospatialProbeState state, string status)
        {
            if (State != state)
                RecordEvent($"Estado: {State} -> {state}");
            State = state;
            Status = status;
        }

        void Fail(string reason)
        {
            LastError = reason;
            SetState(GeospatialProbeState.Failed, reason);
            RecordEvent($"ERROR: {reason}");
            Debug.LogError($"[AncoRA Geospatial] {reason}", this);
            m_Run = null;
        }

        void RecordEvent(string message)
        {
            var elapsed = m_StartedAt > 0f ? Time.realtimeSinceStartup - m_StartedAt : 0f;
            m_EventLog.Add($"{elapsed:F1}s {message}");
            const int maxEvents = 40;
            if (m_EventLog.Count > maxEvents)
                m_EventLog.RemoveAt(0);
        }

        string FullDiagnosticJson()
        {
            var snapshot = new DiagnosticSnapshot
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                diagnosticBuildId = k_DiagnosticBuildId,
                runNumber = m_RunNumber,
                runId = m_RunId,
                applicationVersion = Application.version,
                buildGuid = Application.buildGUID,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                deviceModel = SystemInfo.deviceModel,
                operatingSystem = SystemInfo.operatingSystem,
                state = State.ToString(),
                status = Status,
                lastError = LastError,
                arSessionState = m_ARSessionState,
                earthState = m_EarthState,
                earthTrackingState = m_EarthTrackingState,
                hasValidEarthPose = m_HasValidPose,
                poseQuality = PoseQuality.ToString(),
                vpsAvailability = VpsStatus.ToString(),
                vpsPromiseState = m_VpsPromiseState,
                vpsCheckElapsedSeconds = m_VpsCheckElapsedSeconds,
                secondsToVpsResult = m_SecondsToVpsResult,
                locationStatus = Input.location.status.ToString(),
                hasGpsSample = m_HasGpsSample,
                gpsSampleStale = m_GpsSampleStale,
                gpsLatitude = m_GpsLatitude,
                gpsLongitude = m_GpsLongitude,
                gpsHorizontalAccuracyMeters = m_GpsHorizontalAccuracyMeters,
                gpsTimestampSeconds = m_GpsTimestampSeconds,
                gpsAgeSeconds = m_GpsAgeSeconds,
                terrainAnchorStatus = TerrainAnchorStatus,
                anchorPromiseState = m_AnchorPromiseState,
                anchorTrackingState = m_AnchorTrackingState,
                anchorResolved = ResolvedAnchor != null,
                contentExists = m_Content != null,
                contentVisible = m_Content != null && m_Content.activeInHierarchy,
                forcedUnreliablePreview = m_ForceUnreliablePreview,
                profileReferencePresent = m_Site != null,
                profileHasCoordinates = m_Site != null && m_Site.HasCoordinates,
                siteName = m_Site != null ? m_Site.SiteName : string.Empty,
                siteLatitude = m_Site != null ? m_Site.Latitude : 0d,
                siteLongitude = m_Site != null ? m_Site.Longitude : 0d,
                altitudeMode = m_Site != null ? m_Site.AltitudeMode.ToString() : string.Empty,
                altitudeAboveSurface = m_Site != null ? m_Site.AltitudeAboveSurface : 0d,
                headingDegrees = m_Site != null ? m_Site.HeadingDegrees : 0f,
                cameraLatitude = CameraPose.Latitude,
                cameraLongitude = CameraPose.Longitude,
                cameraAltitude = CameraPose.Altitude,
                horizontalAccuracy = CameraPose.HorizontalAccuracy,
                verticalAccuracy = CameraPose.VerticalAccuracy,
                yawAccuracy = CameraPose.OrientationYawAccuracy,
                poseAgeSeconds = m_LastPoseAt >= 0f
                    ? Time.realtimeSinceStartup - m_LastPoseAt
                    : -1f,
                navigationSource = m_NavigationSource,
                sourceLatitude = m_SourceLatitude,
                sourceLongitude = m_SourceLongitude,
                distanceToAnchorMeters = m_DistanceToAnchorMeters,
                bearingToAnchorDegrees = m_BearingToAnchorDegrees,
                deviceHeadingDegrees = m_DeviceHeadingDegrees,
                relativeBearingDegrees = m_RelativeBearingDegrees,
                compassAccuracyDegrees = m_CompassAccuracyDegrees,
                cameraWorldPosition = m_Camera != null ? m_Camera.transform.position : Vector3.zero,
                cameraWorldEulerAngles = m_Camera != null
                    ? m_Camera.transform.eulerAngles
                    : Vector3.zero,
                anchorWorldPosition = ResolvedAnchor != null
                    ? ResolvedAnchor.transform.position
                    : Vector3.zero,
                worldDistanceToAnchorMeters = m_WorldDistanceToAnchorMeters,
                anchorVerticalOffsetMeters = m_AnchorVerticalOffsetMeters,
                anchorForwardDot = m_AnchorForwardDot,
                secondsToEarthTracking = m_SecondsToEarthTracking,
                secondsToPreview = m_SecondsToPreview,
                secondsToFinal = SecondsToQualityPose,
                diagnosticHorizontalThreshold = m_DiagnosticHorizontalAccuracy,
                diagnosticYawThreshold = m_DiagnosticYawAccuracy,
                previewHorizontalThreshold = m_PreviewHorizontalAccuracy,
                previewVerticalThreshold = m_PreviewVerticalAccuracy,
                previewYawThreshold = m_PreviewYawAccuracy,
                previewStableSeconds = m_PreviewStableSeconds,
                finalHorizontalThreshold = m_MaxHorizontalAccuracy,
                finalVerticalThreshold = m_MaxVerticalAccuracy,
                finalYawThreshold = m_MaxYawAccuracy,
                finalStableSeconds = m_RequiredStableSeconds,
                localizationWarningSeconds = m_LocalizationTimeoutSeconds,
                localPosition = m_RuntimeLocalPosition,
                localEulerAngles = m_RuntimeLocalEulerAngles,
                sizeMeters = m_RuntimeSize,
                contentWorldPosition = m_Content != null ? m_Content.transform.position : Vector3.zero,
                contentBoundsCenter = m_ContentRenderer != null
                    ? m_ContentRenderer.bounds.center
                    : Vector3.zero,
                contentBoundsSize = m_ContentRenderer != null
                    ? m_ContentRenderer.bounds.size
                    : Vector3.zero,
                contentRendererEnabled = m_ContentRenderer != null && m_ContentRenderer.enabled,
                contentRendererIsVisible = m_ContentRenderer != null && m_ContentRenderer.isVisible,
                wallShader = ShaderDescription(m_WallMaterial),
                roofShader = ShaderDescription(m_RoofMaterial),
                edgeShader = ShaderDescription(m_EdgeMaterial),
                events = m_EventLog.ToArray(),
            };
            return JsonUtility.ToJson(snapshot, true);
        }

        static string ShaderDescription(Material material)
        {
            if (material == null)
                return "NotCreated";
            if (material.shader == null)
                return "Missing";
            return $"{material.shader.name} (supported={material.shader.isSupported})";
        }

        void CopyFullDiagnostic()
        {
            var json = FullDiagnosticJson();
            GUIUtility.systemCopyBuffer = json;
            Debug.Log($"[AncoRA Geospatial JSON]\n{json}", this);
            m_CopyConfirmation = "JSON completo copiado al portapapeles";
            m_CopyConfirmationUntil = Time.realtimeSinceStartup + 3f;
        }

        void OnGUI()
        {
            if (!m_ShowHud)
                return;

            EnsureGuiStyles();
            var scale = Mathf.Max(1f, Mathf.Min(Screen.width / 720f, Screen.height / 1280f));
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var safe = Screen.safeArea;
            var area = new Rect(
                safe.x / scale + 10f,
                (Screen.height - safe.yMax) / scale + 10f,
                safe.width / scale - 20f,
                safe.height / scale - 20f);
            GUILayout.BeginArea(area, GUI.skin.box);
            m_HudScroll = GUILayout.BeginScrollView(m_HudScroll);
            GUILayout.Label($"ANCoRA — DIAGNÓSTICO GEOSPATIAL\n{k_DiagnosticBuildId}", m_TitleStyle);
            GUILayout.Label($"Ejecución #{m_RunNumber} | {m_RunId}");
            var hasVpsError = VpsStatus.ToString().StartsWith("Error", StringComparison.Ordinal);
            GUILayout.Label(
                Status,
                State == GeospatialProbeState.Failed || hasVpsError
                    ? m_WarningStyle
                    : GUI.skin.label);
            if (m_ForceUnreliablePreview && PoseQuality < GeospatialPoseQuality.Preview)
                GUILayout.Label("⚠ POSE NO CONFIABLE ⚠", m_WarningStyle);

            var vpsElapsed = m_VpsCheckElapsedSeconds >= 0f
                ? $"{m_VpsCheckElapsedSeconds:F1} s"
                : "--";
            var gpsAccuracy = m_GpsHorizontalAccuracyMeters >= 0f
                ? $"±{m_GpsHorizontalAccuracyMeters:F1} m"
                : "--";
            var gpsAge = m_GpsAgeSeconds >= 0d ? $"{m_GpsAgeSeconds:F1} s" : "--";

            GUILayout.Label($"Flujo: {State} | Calidad: {PoseQuality} | VPS: {VpsStatus}");
            GUILayout.Label($"VPS Promise: {m_VpsPromiseState} | tiempo: {vpsElapsed}");
            GUILayout.Label($"ARSession: {m_ARSessionState}");
            GUILayout.Label(
                $"GPS: {Input.location.status} | muestra: {(m_HasGpsSample ? "válida" : "no válida")} | " +
                $"precisión {gpsAccuracy} | edad {gpsAge}" +
                (m_GpsSampleStale ? " | ANTIGUA" : string.Empty));
            GUILayout.Label($"EarthState: {m_EarthState}");
            GUILayout.Label($"EarthTrackingState: {m_EarthTrackingState}");
            GUILayout.Label(
                $"Pose {(m_HasValidPose ? "ACTUAL" : "ÚLTIMA")}: " +
                $"H {CameraPose.HorizontalAccuracy:F2} m | V {CameraPose.VerticalAccuracy:F2} m | " +
                $"yaw {CameraPose.OrientationYawAccuracy:F1}°");
            GUILayout.Label($"Lat/Lon cámara: {CameraPose.Latitude:F8}, {CameraPose.Longitude:F8}");
            GUILayout.Label(
                $"Terrain: {TerrainAnchorStatus} | Promise: {m_AnchorPromiseState} | " +
                $"Anchor tracking: {m_AnchorTrackingState}");
            GUILayout.Label(
                m_HasTargetLocation
                    ? $"Ancla: {m_DistanceToAnchorMeters:F1} m | rumbo {m_BearingToAnchorDegrees:F1}° | " +
                        $"fuente: {m_NavigationSource}"
                    : "Ancla: esperando GPS o pose Geospatial para calcular distancia y rumbo");
            if (ResolvedAnchor != null)
            {
                GUILayout.Label(
                    $"Ancla AR: {m_WorldDistanceToAnchorMeters:F1} m | " +
                    $"ΔY {m_AnchorVerticalOffsetMeters:+0.0;-0.0;0.0} m | " +
                    (m_AnchorForwardDot >= 0f ? "delante" : "detrás de cámara"));
            }

            DrawDirectionArrow();

            GUILayout.Label(
                $"Umbrales — Diagnóstico H≤{m_DiagnosticHorizontalAccuracy:F0} / " +
                $"yaw≤{m_DiagnosticYawAccuracy:F0} | Preview H≤{m_PreviewHorizontalAccuracy:F0}, " +
                $"V≤{m_PreviewVerticalAccuracy:F0}, yaw≤{m_PreviewYawAccuracy:F0} | " +
                $"Final H≤{m_MaxHorizontalAccuracy:F1}, V≤{m_MaxVerticalAccuracy:F1}, " +
                $"yaw≤{m_MaxYawAccuracy:F1}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reintentar", GUILayout.Height(46f)))
                Retry();
            if (GUILayout.Button("Copiar JSON completo", GUILayout.Height(46f)))
                CopyFullDiagnostic();
            GUILayout.EndHorizontal();

            if (GUILayout.Button(
                m_ForceUnreliablePreview
                    ? "Desactivar bloque forzado"
                    : "Mostrar bloque: POSE NO CONFIABLE",
                GUILayout.Height(46f)))
            {
                m_ForceUnreliablePreview = !m_ForceUnreliablePreview;
                RecordEvent(
                    m_ForceUnreliablePreview
                        ? "Modo POSE NO CONFIABLE activado"
                        : "Modo POSE NO CONFIABLE desactivado");
                UpdateContentVisibility();
            }

            if (Time.realtimeSinceStartup < m_CopyConfirmationUntil)
                GUILayout.Label(m_CopyConfirmation);

            if (ResolvedAnchor != null)
            {
                if (GUILayout.Button(
                    m_ShowAlignmentControls ? "Ocultar ajuste del bloque" : "Ajustar bloque",
                    GUILayout.Height(46f)))
                {
                    m_ShowAlignmentControls = !m_ShowAlignmentControls;
                }

                if (m_ShowAlignmentControls)
                    DrawAlignmentControls();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        void DrawDirectionArrow()
        {
            var rect = GUILayoutUtility.GetRect(120f, 78f, GUILayout.ExpandWidth(true));
            if (!m_HasDirection)
            {
                GUI.Label(rect, "↑\nRUMBO AÚN NO DISPONIBLE", m_ArrowStyle);
                return;
            }

            var previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(m_RelativeBearingDegrees, rect.center);
            GUI.Label(rect, "▲\nANCLA", m_ArrowStyle);
            GUI.matrix = previousMatrix;
            GUILayout.Label(
                $"Gira {Mathf.Abs(m_RelativeBearingDegrees):F0}° " +
                (m_RelativeBearingDegrees < 0f ? "a la izquierda" : "a la derecha"));
        }

        void DrawAlignmentControls()
        {
            GUILayout.Label(
                $"Posición local: X {m_RuntimeLocalPosition.x:F1} | " +
                $"Y {m_RuntimeLocalPosition.y:F1} | Z {m_RuntimeLocalPosition.z:F1} m");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("X −", GUILayout.Height(40f))) NudgePosition(Vector3.left);
            if (GUILayout.Button("X +", GUILayout.Height(40f))) NudgePosition(Vector3.right);
            if (GUILayout.Button("Z −", GUILayout.Height(40f))) NudgePosition(Vector3.back);
            if (GUILayout.Button("Z +", GUILayout.Height(40f))) NudgePosition(Vector3.forward);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Y −", GUILayout.Height(40f))) NudgePosition(Vector3.down);
            if (GUILayout.Button("Y +", GUILayout.Height(40f))) NudgePosition(Vector3.up);
            if (GUILayout.Button("Giro −", GUILayout.Height(40f))) NudgeRotation(-1f);
            if (GUILayout.Button("Giro +", GUILayout.Height(40f))) NudgeRotation(1f);
            GUILayout.EndHorizontal();

            GUILayout.Label(
                $"Tamaño: ancho {m_RuntimeSize.x:F1} | alto {m_RuntimeSize.y:F1} | " +
                $"fondo {m_RuntimeSize.z:F1} m");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ancho −", GUILayout.Height(40f))) NudgeSize(Vector3.left);
            if (GUILayout.Button("Ancho +", GUILayout.Height(40f))) NudgeSize(Vector3.right);
            if (GUILayout.Button("Alto −", GUILayout.Height(40f))) NudgeSize(Vector3.down);
            if (GUILayout.Button("Alto +", GUILayout.Height(40f))) NudgeSize(Vector3.up);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fondo −", GUILayout.Height(40f))) NudgeSize(Vector3.back);
            if (GUILayout.Button("Fondo +", GUILayout.Height(40f))) NudgeSize(Vector3.forward);
            if (GUILayout.Button("Copiar diagnóstico + ajuste", GUILayout.Height(40f)))
                CopyFullDiagnostic();
            GUILayout.EndHorizontal();

            GUILayout.Label(
                $"Pasos: posición {m_PositionStepMeters:F2} m | " +
                $"giro {m_RotationStepDegrees:F1}° | tamaño {m_SizeStepMeters:F2} m");
        }

        void EnsureGuiStyles()
        {
            if (m_TitleStyle != null)
                return;

            m_TitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            m_WarningStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            m_WarningStyle.normal.textColor = new Color(1f, 0.25f, 0.15f);
            m_ArrowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
        }

        [Serializable]
        sealed class DiagnosticSnapshot
        {
            public string capturedAtUtc;
            public string diagnosticBuildId;
            public int runNumber;
            public string runId;
            public string applicationVersion;
            public string buildGuid;
            public string unityVersion;
            public string platform;
            public string deviceModel;
            public string operatingSystem;
            public string state;
            public string status;
            public string lastError;
            public string arSessionState;
            public string earthState;
            public string earthTrackingState;
            public bool hasValidEarthPose;
            public string poseQuality;
            public string vpsAvailability;
            public string vpsPromiseState;
            public float vpsCheckElapsedSeconds;
            public float secondsToVpsResult;
            public string locationStatus;
            public bool hasGpsSample;
            public bool gpsSampleStale;
            public double gpsLatitude;
            public double gpsLongitude;
            public float gpsHorizontalAccuracyMeters;
            public double gpsTimestampSeconds;
            public double gpsAgeSeconds;
            public string terrainAnchorStatus;
            public string anchorPromiseState;
            public string anchorTrackingState;
            public bool anchorResolved;
            public bool contentExists;
            public bool contentVisible;
            public bool forcedUnreliablePreview;
            public bool profileReferencePresent;
            public bool profileHasCoordinates;
            public string siteName;
            public double siteLatitude;
            public double siteLongitude;
            public string altitudeMode;
            public double altitudeAboveSurface;
            public float headingDegrees;
            public double cameraLatitude;
            public double cameraLongitude;
            public double cameraAltitude;
            public double horizontalAccuracy;
            public double verticalAccuracy;
            public double yawAccuracy;
            public float poseAgeSeconds;
            public string navigationSource;
            public double sourceLatitude;
            public double sourceLongitude;
            public double distanceToAnchorMeters;
            public double bearingToAnchorDegrees;
            public float deviceHeadingDegrees;
            public float relativeBearingDegrees;
            public float compassAccuracyDegrees;
            public Vector3 cameraWorldPosition;
            public Vector3 cameraWorldEulerAngles;
            public Vector3 anchorWorldPosition;
            public float worldDistanceToAnchorMeters;
            public float anchorVerticalOffsetMeters;
            public float anchorForwardDot;
            public float secondsToEarthTracking;
            public float secondsToPreview;
            public float secondsToFinal;
            public float diagnosticHorizontalThreshold;
            public float diagnosticYawThreshold;
            public float previewHorizontalThreshold;
            public float previewVerticalThreshold;
            public float previewYawThreshold;
            public float previewStableSeconds;
            public float finalHorizontalThreshold;
            public float finalVerticalThreshold;
            public float finalYawThreshold;
            public float finalStableSeconds;
            public float localizationWarningSeconds;
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public Vector3 sizeMeters;
            public Vector3 contentWorldPosition;
            public Vector3 contentBoundsCenter;
            public Vector3 contentBoundsSize;
            public bool contentRendererEnabled;
            public bool contentRendererIsVisible;
            public string wallShader;
            public string roofShader;
            public string edgeShader;
            public string[] events;
        }
    }
}
