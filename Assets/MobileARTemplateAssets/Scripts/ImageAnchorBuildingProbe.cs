using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AncorRA.AR
{
    /// <summary>
    /// How far along the placement flow the probe currently is.
    /// </summary>
    public enum ProbeState
    {
        /// <summary>No matching reference image is being tracked.</summary>
        Searching,

        /// <summary>The image is tracked but its pose has not settled yet.</summary>
        Stabilizing,

        /// <summary>The pose is frozen in world space, but no native anchor backs it.</summary>
        WorldLocked,

        /// <summary>The pose is backed by a platform anchor and survives device drift correction.</summary>
        NativeAnchored,
    }

    /// <summary>
    /// What the probe draws once it has locked onto the sign.
    /// </summary>
    public enum ContentShape
    {
        /// <summary>A plain box, for checking that a known real distance matches on screen.</summary>
        Box,

        /// <summary>A parametric gable building, sized in real metres.</summary>
        House,
    }

    /// <summary>
    /// Places one calibrated box relative to a recognized real-world sign, then anchors it so it
    /// stays put when you walk away.
    ///
    /// The box is positioned in a gravity-aligned basis derived from the sign rather than in the
    /// tracked image's own local space. Two reasons: AR Foundation's own documentation is ambiguous
    /// about which local axis is the image normal, and a building never leans with the roll noise of
    /// image tracking. The normal is detected instead of assumed, by picking whichever local axis
    /// points most directly at the camera - you have to be facing a sign to detect it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARTrackedImageManager))]
    public sealed class ImageAnchorBuildingProbe : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField]
        [Tooltip("Reference Image Library entry name. Leave empty to accept the first detected image.")]
        string m_TargetImageName = "PaceUcnVertical";

        [Header("Pose lock")]
        [SerializeField]
        [Min(2)]
        [Tooltip("Tracked poses collected before the box is allowed to lock.")]
        int m_StabilitySamples = 20;

        [SerializeField]
        [Min(0.001f)]
        [Tooltip("Maximum spread, in meters, across the collected poses before locking.")]
        float m_MaxPositionSpread = 0.04f;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Maximum heading spread, in degrees, across the collected poses before locking.")]
        float m_MaxAngleSpread = 4f;

        [SerializeField]
        [Min(1)]
        [Tooltip("Frames without tracking tolerated before the collected samples are discarded. " +
                 "A single dropped frame should not restart the whole measurement.")]
        int m_MaxNonTrackingGap = 20;

        [Header("Content")]
        [SerializeField]
        [Tooltip("Optional prefab. When empty, the component creates a plain Unity cube.")]
        GameObject m_ContentPrefab;

        [SerializeField]
        [Tooltip("Meters to the right of the sign, seen by someone facing it.")]
        float m_OffsetRight;

        [SerializeField]
        [Tooltip("Meters above the sign, along real gravity.")]
        float m_OffsetUp;

        [SerializeField]
        [Tooltip("Meters in front of the sign, horizontally away from the wall.")]
        float m_OffsetForward;

        [SerializeField]
        [Tooltip("Rotation around the vertical axis, in degrees.")]
        float m_YawDegrees;

        // Each face of the building is measured separately, as a distance from the sign, because the
        // sign is mounted at some arbitrary point on the facade - not at a corner, not at the middle
        // of anything in particular. Deriving the box from a size plus a fixed pivot forces every
        // building to be arranged around that pivot; six independent extents just say where the six
        // faces are, which is what you can actually walk up and measure.
        [SerializeField]
        [Min(0f)]
        [Tooltip("Meters the building extends to the right of the sign, seen by someone facing it.")]
        float m_ExtentRight = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Meters the building extends to the left of the sign, seen by someone facing it.")]
        float m_ExtentLeft = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Meters the building extends above the sign.")]
        float m_ExtentUp = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Meters the building extends below the sign.")]
        float m_ExtentDown = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Meters the building extends behind the sign, into the plot.")]
        float m_ExtentBack = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Meters the building extends in front of the sign, towards the street.")]
        float m_ExtentFront = 0.5f;

        [SerializeField]
        [Tooltip("What to draw when no prefab is assigned: a plain box or the parametric building.")]
        ContentShape m_ContentShape = ContentShape.Box;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How much of the total height is roof, in meters. Only used by the House shape.")]
        float m_RoofHeight = 1.5f;

        [SerializeField]
        [Tooltip("Ridge runs parallel to the facade. Turn off to put the triangular gable on the front.")]
        bool m_RidgeAlongWidth = true;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Show the on-screen calibration panel. Turn this off for a release build.")]
        bool m_ShowDebugTools = true;

        [SerializeField]
        [Tooltip("Show the short status banner at the top of the screen.")]
        bool m_ShowStatus = true;

        [SerializeField]
        [Tooltip("Reload the saved calibration for the active target on startup.")]
        bool m_LoadSavedCalibration = true;

        // Bumped to v2 when the content pivot moved from the box centre to its front face and base.
        // The stored offsets mean something different now, so reusing v1 values would silently put
        // the building half a depth off and leave no clue why.
        const string k_PrefsPrefix = "AncoRA.Calibration.v2.";

        ARTrackedImageManager m_TrackedImageManager;
        ARAnchorManager m_AnchorManager;
        Camera m_Camera;

        ARTrackedImage m_ActiveImage;
        ARAnchor m_Anchor;
        Transform m_BasisRoot;
        GameObject m_ContentInstance;
        Mesh m_GeneratedMesh;

        // Bounds of the content in its own local space, before any calibration scale. Measuring it
        // is what lets an arbitrary prefab - a model authored in centimetres, or one whose pivot sits
        // in a corner - be fitted and anchored by its facade like the generated shapes are.
        Bounds m_ContentBounds = new Bounds(Vector3.zero, Vector3.one);

        Vector3[] m_SamplePositions;
        Vector3[] m_SampleForwards;
        int m_SampleCount;
        int m_SampleCursor;
        int m_FramesWithoutTracking;

        GUIStyle m_StatusStyle;
        string m_AnchorStatus = "Sin ancla todavía.";

        /// <summary>Current stage of the placement flow.</summary>
        public ProbeState State { get; private set; } = ProbeState.Searching;

        /// <summary>Human-readable outcome of the last anchor attempt.</summary>
        public string AnchorStatus => m_AnchorStatus;

        /// <summary>Name of the reference image the probe is currently accepting, or empty for any.</summary>
        public string TargetImageName => m_TargetImageName;

        /// <summary>The reference image currently being tracked, or null.</summary>
        public ARTrackedImage ActiveImage => m_ActiveImage;

        /// <summary>Renderer of the placed box, so debug tools can restyle it.</summary>
        public Renderer ContentRenderer { get; private set; }

        /// <summary>Transform of the placed box, or null before it exists.</summary>
        public Transform ContentTransform => m_ContentInstance != null ? m_ContentInstance.transform : null;

        /// <summary>How many stability samples have been collected, out of <see cref="StabilityTarget"/>.</summary>
        public int StabilityProgress => m_SampleCount;

        /// <summary>How many stability samples are needed before the pose can lock.</summary>
        public int StabilityTarget => m_StabilitySamples;

        /// <summary>Meters to the right of the sign, seen by someone facing it.</summary>
        public float OffsetRight
        {
            get => m_OffsetRight;
            set { m_OffsetRight = value; ApplyCalibration(); }
        }

        /// <summary>Meters above the sign, along real gravity.</summary>
        public float OffsetUp
        {
            get => m_OffsetUp;
            set { m_OffsetUp = value; ApplyCalibration(); }
        }

        /// <summary>Meters in front of the sign, horizontally away from the wall.</summary>
        public float OffsetForward
        {
            get => m_OffsetForward;
            set { m_OffsetForward = value; ApplyCalibration(); }
        }

        /// <summary>Rotation around the vertical axis, in degrees.</summary>
        public float YawDegrees
        {
            get => m_YawDegrees;
            set { m_YawDegrees = value; ApplyCalibration(); }
        }

        /// <summary>
        /// Overall dimensions in meters, derived from the six face extents.
        /// </summary>
        public Vector3 SizeMeters => new(
            Mathf.Max(0.01f, m_ExtentLeft + m_ExtentRight),
            Mathf.Max(0.01f, m_ExtentDown + m_ExtentUp),
            Mathf.Max(0.01f, m_ExtentBack + m_ExtentFront));

        /// <summary>Meters the building extends to the right of the sign, facing it.</summary>
        public float ExtentRight
        {
            get => m_ExtentRight;
            set => SetExtent(ref m_ExtentRight, value);
        }

        /// <summary>Meters the building extends to the left of the sign, facing it.</summary>
        public float ExtentLeft
        {
            get => m_ExtentLeft;
            set => SetExtent(ref m_ExtentLeft, value);
        }

        /// <summary>Meters the building extends above the sign.</summary>
        public float ExtentUp
        {
            get => m_ExtentUp;
            set => SetExtent(ref m_ExtentUp, value);
        }

        /// <summary>Meters the building extends below the sign.</summary>
        public float ExtentDown
        {
            get => m_ExtentDown;
            set => SetExtent(ref m_ExtentDown, value);
        }

        /// <summary>Meters the building extends behind the sign, into the plot.</summary>
        public float ExtentBack
        {
            get => m_ExtentBack;
            set => SetExtent(ref m_ExtentBack, value);
        }

        /// <summary>Meters the building extends in front of the sign, towards the street.</summary>
        public float ExtentFront
        {
            get => m_ExtentFront;
            set => SetExtent(ref m_ExtentFront, value);
        }

        /// <summary>Sets all six extents at once, rebuilding the mesh only after the last one.</summary>
        public void SetExtents(float right, float left, float up, float down, float back, float front)
        {
            m_ExtentRight = Mathf.Max(0f, right);
            m_ExtentLeft = Mathf.Max(0f, left);
            m_ExtentUp = Mathf.Max(0f, up);
            m_ExtentDown = Mathf.Max(0f, down);
            m_ExtentBack = Mathf.Max(0f, back);
            m_ExtentFront = Mathf.Max(0f, front);

            RefreshGeneratedMesh();
            ApplyCalibration();
        }

        void SetExtent(ref float field, float value)
        {
            var clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(field, clamped))
                return;

            field = clamped;
            RefreshGeneratedMesh();
            ApplyCalibration();
        }

        /// <summary>What the probe draws when no prefab is assigned.</summary>
        public ContentShape Shape
        {
            get => m_ContentShape;
            set
            {
                if (m_ContentShape == value)
                    return;

                m_ContentShape = value;
                RebuildContent();
            }
        }

        /// <summary>Meters of <see cref="SizeMeters"/>.y taken by the roof, for the House shape.</summary>
        public float RoofHeight
        {
            get => m_RoofHeight;
            set
            {
                m_RoofHeight = Mathf.Max(0f, value);
                RefreshGeneratedMesh();
                ApplyCalibration();
            }
        }

        /// <summary>True when the ridge runs parallel to the facade instead of into the plot.</summary>
        public bool RidgeAlongWidth
        {
            get => m_RidgeAlongWidth;
            set
            {
                if (m_RidgeAlongWidth == value)
                    return;

                m_RidgeAlongWidth = value;
                RefreshGeneratedMesh();
                ApplyCalibration();
            }
        }

        /// <summary>Increments whenever the content mesh is rebuilt, so debug overlays can follow it.</summary>
        public int ContentVersion { get; private set; }

        /// <summary>Physical size declared for the tracked image, in meters, or zero if none is tracked.</summary>
        public Vector2 DeclaredImageSize =>
            m_ActiveImage != null ? m_ActiveImage.referenceImage.size : Vector2.zero;

        /// <summary>Distance from the camera to the tracked sign, or -1 when nothing is tracked.</summary>
        public float DistanceToSign =>
            m_ActiveImage != null && m_Camera != null
                ? Vector3.Distance(m_Camera.transform.position, m_ActiveImage.transform.position)
                : -1f;

        /// <summary>Distance from the camera to the placed box, or -1 when it does not exist yet.</summary>
        public float DistanceToBox =>
            m_ContentInstance != null && m_Camera != null
                ? Vector3.Distance(m_Camera.transform.position, m_ContentInstance.transform.position)
                : -1f;

        /// <summary>Gravity-aligned frame at the sign, used as the origin for every offset.</summary>
        public Transform BasisRoot => m_BasisRoot;

        void Awake()
        {
            m_TrackedImageManager = GetComponent<ARTrackedImageManager>();
            m_AnchorManager = GetComponent<ARAnchorManager>();

            if (m_AnchorManager == null)
            {
                // The manager carries [DefaultExecutionOrder] and [RequireComponent(XROrigin)], so adding
                // it here races the AR session startup and its subsystem may not be running when the first
                // anchor is requested. Say so rather than letting the fallback masquerade as an anchor.
                Debug.LogWarning(
                    "No hay ARAnchorManager en el XR Origin. Agrégalo en la escena: sin él, el cubo " +
                    "queda fijado en coordenadas de mundo pero sin corrección de deriva.",
                    this);
            }

            m_SamplePositions = new Vector3[m_StabilitySamples];
            m_SampleForwards = new Vector3[m_StabilitySamples];

            var origin = FindAnyObjectByType<XROrigin>();
            m_Camera = origin != null && origin.Camera != null ? origin.Camera : Camera.main;

            if (m_LoadSavedCalibration)
                LoadCalibration();

            // Detection range is capped by how many pixels ARCore gets on the sign, so the camera
            // configuration is part of placement, not a debug extra: it is added whether or not the
            // panel is on.
            if (GetComponent<CameraConfigurationTuner>() == null)
                gameObject.AddComponent<CameraConfigurationTuner>();

            if (m_ShowDebugTools && GetComponent<CalibrationDebugHud>() == null)
                gameObject.AddComponent<CalibrationDebugHud>();
        }

        void OnEnable()
        {
            m_TrackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
        }

        void OnDisable()
        {
            m_TrackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
        }

        void OnDestroy()
        {
            // Meshes created at runtime are not owned by any scene object, so they outlive the
            // component unless they are released explicitly.
            if (m_GeneratedMesh != null)
                Destroy(m_GeneratedMesh);
        }

        void Update()
        {
            // Once a native anchor holds the box, the platform owns its pose. Nothing to do.
            if (State == ProbeState.NativeAnchored || State == ProbeState.WorldLocked)
                return;

            if (m_ActiveImage == null)
                return;

            if (m_ActiveImage.trackingState != TrackingState.Tracking)
            {
                m_FramesWithoutTracking++;
                if (m_FramesWithoutTracking > m_MaxNonTrackingGap)
                    ReturnToSearching();
                return;
            }

            m_FramesWithoutTracking = 0;

            if (!TryComputeBasis(m_ActiveImage.transform, out var position, out var rotation))
                return;

            EnsureBasisRoot();
            m_BasisRoot.SetPositionAndRotation(position, rotation);
            EnsureContentInstance();
            ApplyCalibration();
            m_ContentInstance.SetActive(true);
            State = ProbeState.Stabilizing;

            PushSample(position, rotation * Vector3.forward);

            if (IsPoseStable(out var averagePosition, out var averageForward))
                LockPose(averagePosition, averageForward);
        }

        void OnGUI()
        {
            if (!m_ShowStatus)
                return;

            string message;
            Color background;

            switch (State)
            {
                case ProbeState.NativeAnchored:
                    message = "ANCLA NATIVA LISTA\nYA PUEDES ALEJARTE";
                    background = new Color(0.08f, 0.55f, 0.2f, 0.9f);
                    break;
                case ProbeState.WorldLocked:
                    message = "POSICIÓN FIJADA (SIN ANCLA NATIVA)\nPUEDE DERIVAR AL CAMINAR";
                    background = new Color(0.85f, 0.45f, 0.05f, 0.9f);
                    break;
                case ProbeState.Stabilizing:
                    var progress = Mathf.RoundToInt(100f * m_SampleCount / Mathf.Max(1, m_StabilitySamples));
                    message = $"MANTÉN EL TELÉFONO QUIETO\nMIDIENDO {progress}%";
                    background = new Color(0.95f, 0.6f, 0.05f, 0.9f);
                    break;
                default:
                    message = "APUNTA AL CARTEL\nACÉRCATE A 1-2 METROS";
                    background = new Color(0.75f, 0.12f, 0.12f, 0.9f);
                    break;
            }

            m_StatusStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            m_StatusStyle.fontSize = Mathf.Clamp(Screen.height / 40, 18, 34);

            var safeArea = Screen.safeArea;
            var width = Mathf.Min(Screen.width - 32f, 720f);
            var height = Mathf.Clamp(Screen.height * 0.09f, 74f, 120f);
            var topInset = Screen.height - safeArea.yMax;
            var rect = new Rect((Screen.width - width) * 0.5f, topInset + 12f, width, height);

            var previous = GUI.color;
            GUI.color = background;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, message, m_StatusStyle);
            GUI.color = previous;
        }

        void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            for (var i = 0; i < changes.added.Count; i++)
                ConsiderImage(changes.added[i]);

            for (var i = 0; i < changes.updated.Count; i++)
                ConsiderImage(changes.updated[i]);

            if (m_ActiveImage == null || State == ProbeState.NativeAnchored || State == ProbeState.WorldLocked)
                return;

            for (var i = 0; i < changes.removed.Count; i++)
            {
                if (changes.removed[i].Key != m_ActiveImage.trackableId)
                    continue;

                ReturnToSearching();
                break;
            }
        }

        void ConsiderImage(ARTrackedImage trackedImage)
        {
            if (State == ProbeState.NativeAnchored || State == ProbeState.WorldLocked)
                return;

            if (!MatchesTarget(trackedImage.referenceImage.name))
                return;

            if (m_ActiveImage == trackedImage)
                return;

            m_ActiveImage = trackedImage;
            ClearSamples();
        }

        bool MatchesTarget(string imageName)
        {
            return string.IsNullOrWhiteSpace(m_TargetImageName) ||
                string.Equals(imageName, m_TargetImageName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Builds a gravity-aligned frame at the sign: up is real gravity, forward is the sign's
        /// outward normal flattened onto the horizontal plane, right follows from those two.
        /// </summary>
        bool TryComputeBasis(Transform image, out Vector3 position, out Quaternion rotation)
        {
            position = image.position;
            rotation = Quaternion.identity;

            if (m_Camera == null)
                return false;

            var toCamera = m_Camera.transform.position - position;
            if (toCamera.sqrMagnitude < 1e-6f)
                return false;

            // The sign's outward normal is whichever local axis points most directly at the viewer.
            // Detecting it beats assuming a convention: AR Foundation documents the image normal as
            // its local +Y for XR Simulation, while ARCore documents +Y with +Z pointing down the
            // image, and the two disagree about where +X ends up.
            var normal = image.right;
            var best = float.NegativeInfinity;
            KeepBestAxis(image.right, toCamera, ref normal, ref best);
            KeepBestAxis(-image.right, toCamera, ref normal, ref best);
            KeepBestAxis(image.up, toCamera, ref normal, ref best);
            KeepBestAxis(-image.up, toCamera, ref normal, ref best);
            KeepBestAxis(image.forward, toCamera, ref normal, ref best);
            KeepBestAxis(-image.forward, toCamera, ref normal, ref best);

            var flat = Vector3.ProjectOnPlane(normal, Vector3.up);

            // A sign lying flat has no horizontal normal. Fall back to whichever of its other axes
            // still has a horizontal component so the basis stays defined.
            if (flat.sqrMagnitude < 1e-4f)
                flat = Vector3.ProjectOnPlane(image.up, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f)
                flat = Vector3.ProjectOnPlane(image.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f)
                return false;

            rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            return true;
        }

        static void KeepBestAxis(Vector3 axis, Vector3 toCamera, ref Vector3 normal, ref float best)
        {
            var score = Vector3.Dot(axis, toCamera);
            if (score <= best)
                return;

            best = score;
            normal = axis;
        }

        void PushSample(Vector3 position, Vector3 forward)
        {
            m_SamplePositions[m_SampleCursor] = position;
            m_SampleForwards[m_SampleCursor] = forward;
            m_SampleCursor = (m_SampleCursor + 1) % m_StabilitySamples;
            if (m_SampleCount < m_StabilitySamples)
                m_SampleCount++;
        }

        void ClearSamples()
        {
            m_SampleCount = 0;
            m_SampleCursor = 0;
            m_FramesWithoutTracking = 0;
        }

        /// <summary>
        /// Reports whether the collected poses agree closely enough to lock, and returns their average.
        /// Averaging is what makes the locked pose better than any single frame of it.
        /// </summary>
        bool IsPoseStable(out Vector3 averagePosition, out Vector3 averageForward)
        {
            averagePosition = Vector3.zero;
            averageForward = Vector3.forward;

            if (m_SampleCount < m_StabilitySamples)
                return false;

            var positionSum = Vector3.zero;
            var forwardSum = Vector3.zero;
            for (var i = 0; i < m_SampleCount; i++)
            {
                positionSum += m_SamplePositions[i];
                forwardSum += m_SampleForwards[i];
            }

            averagePosition = positionSum / m_SampleCount;
            if (forwardSum.sqrMagnitude < 1e-6f)
                return false;

            averageForward = forwardSum.normalized;

            for (var i = 0; i < m_SampleCount; i++)
            {
                if (Vector3.Distance(m_SamplePositions[i], averagePosition) > m_MaxPositionSpread)
                    return false;

                if (Vector3.Angle(m_SampleForwards[i], averageForward) > m_MaxAngleSpread)
                    return false;
            }

            return true;
        }

        void LockPose(Vector3 position, Vector3 forward)
        {
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            EnsureBasisRoot();
            m_BasisRoot.SetParent(null, true);
            m_BasisRoot.SetPositionAndRotation(position, rotation);

            EnsureContentInstance();
            ApplyCalibration();
            m_ContentInstance.SetActive(true);

            State = ProbeState.WorldLocked;
            m_AnchorStatus = "Fijado en mundo. Pidiendo ancla nativa...";
            RequestAnchor(new Pose(position, rotation));
        }

        async void RequestAnchor(Pose pose)
        {
            if (m_AnchorManager == null)
            {
                m_AnchorStatus = "SIN ANCLA NATIVA: falta ARAnchorManager en el XR Origin.";
                Debug.LogWarning(m_AnchorStatus, this);
                return;
            }

            if (m_AnchorManager.subsystem == null || !m_AnchorManager.subsystem.running)
            {
                m_AnchorStatus = "SIN ANCLA NATIVA: el subsistema de anclas no está corriendo.";
                Debug.LogWarning(m_AnchorStatus, this);
                return;
            }

            var result = await m_AnchorManager.TryAddAnchorAsync(pose);

            if (!isActiveAndEnabled || State != ProbeState.WorldLocked)
            {
                if (result.status.IsSuccess())
                    m_AnchorManager.TryRemoveAnchor(result.value);
                return;
            }

            if (!result.status.IsSuccess())
            {
                m_AnchorStatus = $"SIN ANCLA NATIVA: TryAddAnchorAsync devolvió {result.status}.";
                Debug.LogWarning(m_AnchorStatus, this);
                return;
            }

            m_Anchor = result.value;
            m_BasisRoot.SetParent(m_Anchor.transform, false);
            m_BasisRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ApplyCalibration();

            State = ProbeState.NativeAnchored;
            m_AnchorStatus = "Ancla nativa creada.";
            Debug.Log(m_AnchorStatus, this);
        }

        void ReturnToSearching()
        {
            m_ActiveImage = null;
            ClearSamples();
            State = ProbeState.Searching;

            if (m_ContentInstance != null)
                m_ContentInstance.SetActive(false);
        }

        void EnsureBasisRoot()
        {
            if (m_BasisRoot != null)
                return;

            m_BasisRoot = new GameObject("Sign Basis").transform;
        }

        /// <summary>True when the probe owns the mesh and may regenerate it from the size sliders.</summary>
        bool UsesGeneratedMesh => m_ContentPrefab == null && m_ContentShape == ContentShape.House;

        void EnsureContentInstance()
        {
            if (m_ContentInstance != null)
                return;

            m_ContentInstance = m_ContentPrefab != null
                ? Instantiate(m_ContentPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_ContentInstance.name = "Building Probe";

            // The box is a visual marker, not something to interact with. A collider on it would
            // swallow AR touch raycasts from the template's object placement UI.
            foreach (var collider in m_ContentInstance.GetComponentsInChildren<Collider>())
                Destroy(collider);

            ContentRenderer = m_ContentInstance.GetComponentInChildren<Renderer>();

            EnsureBasisRoot();
            m_ContentInstance.transform.SetParent(m_BasisRoot, false);

            if (UsesGeneratedMesh)
                RefreshGeneratedMesh();
            else
                MeasureContentBounds();

            ContentVersion++;
        }

        /// <summary>Throws away the current content so the next lock builds it from scratch.</summary>
        void RebuildContent()
        {
            if (m_ContentInstance == null)
                return;

            var wasActive = m_ContentInstance.activeSelf;

            // Destroy is deferred to the end of the frame, so the outgoing object would otherwise be
            // drawn on top of its replacement for one frame.
            m_ContentInstance.SetActive(false);
            Destroy(m_ContentInstance);
            m_ContentInstance = null;
            ContentRenderer = null;

            if (m_GeneratedMesh != null)
            {
                Destroy(m_GeneratedMesh);
                m_GeneratedMesh = null;
            }

            EnsureContentInstance();
            ApplyCalibration();
            m_ContentInstance.SetActive(wasActive);
        }

        /// <summary>
        /// Rebuilds the generated mesh at the current dimensions.
        ///
        /// The building is authored at its real size instead of being stretched from a unit cube,
        /// so that the roof pitch stays a property of the roof and does not follow the walls.
        /// </summary>
        void RefreshGeneratedMesh()
        {
            if (m_ContentInstance == null || !UsesGeneratedMesh)
                return;

            var filter = m_ContentInstance.GetComponent<MeshFilter>();
            if (filter == null)
                return;

            var replacement = HouseMeshBuilder.Build(SizeMeters, m_RoofHeight, m_RidgeAlongWidth);

            if (m_GeneratedMesh != null)
                Destroy(m_GeneratedMesh);

            m_GeneratedMesh = replacement;
            filter.sharedMesh = m_GeneratedMesh;

            MeasureContentBounds();
            ContentVersion++;
        }

        /// <summary>
        /// Records the content's bounds in its own local space, so the fit and the pivot come from
        /// real geometry instead of assuming a unit cube centred on its origin.
        /// </summary>
        void MeasureContentBounds()
        {
            m_ContentBounds = new Bounds(Vector3.zero, Vector3.one);

            if (m_ContentInstance == null)
                return;

            var toRoot = m_ContentInstance.transform.worldToLocalMatrix;
            var found = false;

            foreach (var filter in m_ContentInstance.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;

                var local = TransformBounds(toRoot * filter.transform.localToWorldMatrix, mesh.bounds);
                if (found)
                {
                    m_ContentBounds.Encapsulate(local);
                }
                else
                {
                    m_ContentBounds = local;
                    found = true;
                }
            }
        }

        /// <summary>Axis-aligned bounds of <paramref name="bounds"/> after <paramref name="matrix"/>.</summary>
        static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            var center = matrix.MultiplyPoint3x4(bounds.center);
            var extents = bounds.extents;
            var axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            var axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            var axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));

            var size = 2f * new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

            return new Bounds(center, size);
        }

        /// <summary>Writes the current offsets, yaw and size onto the placed content.</summary>
        [ContextMenu("Apply Calibration")]
        public void ApplyCalibration()
        {
            if (m_ContentInstance == null)
                return;

            var yaw = Quaternion.Euler(0f, m_YawDegrees, 0f);
            var size = SizeMeters;

            // Fit whatever content we have into the declared size. The generated shapes already come
            // out at that size, so this is a no-op for them, but it is what makes an imported model
            // authored at some arbitrary scale land at real-world metres.
            var measured = m_ContentBounds.size;
            var fit = new Vector3(
                measured.x > 1e-4f ? size.x / measured.x : 1f,
                measured.y > 1e-4f ? size.y / measured.y : 1f,
                measured.z > 1e-4f ? size.z / measured.z : 1f);

            // Place the box by its six faces rather than by a pivot. No single pivot is right for
            // every building: the sign sits wherever it was mounted - halfway up the wall here - so
            // any fixed anchor point forces the model to grow away from it in the wrong direction.
            // Sending the minimum corner to (-left, -down, -back) puts each face exactly where it was
            // measured, and the yaw then turns the whole building about the sign.
            var minimum = new Vector3(-m_ExtentLeft, -m_ExtentDown, -m_ExtentBack);

            var contentTransform = m_ContentInstance.transform;
            contentTransform.localScale = fit;
            contentTransform.localRotation = yaw;
            contentTransform.localPosition =
                new Vector3(m_OffsetRight, m_OffsetUp, m_OffsetForward) +
                yaw * (minimum - Vector3.Scale(fit, m_ContentBounds.min));
        }

        /// <summary>Drops the anchor and starts looking for the sign again.</summary>
        public void Recalibrate()
        {
            if (m_Anchor != null)
            {
                if (m_BasisRoot != null)
                    m_BasisRoot.SetParent(null, true);

                m_AnchorManager.TryRemoveAnchor(m_Anchor);
                m_Anchor = null;
            }

            m_AnchorStatus = "Sin ancla todavía.";
            ReturnToSearching();
        }

        /// <summary>Locks immediately at the current pose, skipping the stability gate.</summary>
        public void ForceLock()
        {
            if (State == ProbeState.NativeAnchored || State == ProbeState.WorldLocked)
                return;

            if (m_ActiveImage == null || !TryComputeBasis(m_ActiveImage.transform, out var position, out var rotation))
                return;

            LockPose(position, rotation * Vector3.forward);
        }

        /// <summary>Switches which reference image the probe accepts, and reloads its calibration.</summary>
        public void SetTargetImageName(string imageName)
        {
            m_TargetImageName = imageName ?? string.Empty;
            Recalibrate();

            if (m_LoadSavedCalibration)
                LoadCalibration();
        }

        string PrefsKey(string field) =>
            k_PrefsPrefix + (string.IsNullOrWhiteSpace(m_TargetImageName) ? "Any" : m_TargetImageName) + "." + field;

        /// <summary>Stores the calibration for the active target so it survives an app restart.</summary>
        public void SaveCalibration()
        {
            PlayerPrefs.SetFloat(PrefsKey("Right"), m_OffsetRight);
            PlayerPrefs.SetFloat(PrefsKey("Up"), m_OffsetUp);
            PlayerPrefs.SetFloat(PrefsKey("Forward"), m_OffsetForward);
            PlayerPrefs.SetFloat(PrefsKey("Yaw"), m_YawDegrees);
            PlayerPrefs.SetFloat(PrefsKey("Right2"), m_ExtentRight);
            PlayerPrefs.SetFloat(PrefsKey("Left2"), m_ExtentLeft);
            PlayerPrefs.SetFloat(PrefsKey("Up2"), m_ExtentUp);
            PlayerPrefs.SetFloat(PrefsKey("Down2"), m_ExtentDown);
            PlayerPrefs.SetFloat(PrefsKey("Back2"), m_ExtentBack);
            PlayerPrefs.SetFloat(PrefsKey("Front2"), m_ExtentFront);
            PlayerPrefs.SetInt(PrefsKey("Shape"), (int)m_ContentShape);
            PlayerPrefs.SetFloat(PrefsKey("Roof"), m_RoofHeight);
            PlayerPrefs.SetInt(PrefsKey("Ridge"), m_RidgeAlongWidth ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"Calibración guardada para '{m_TargetImageName}'.\n{DescribeCalibration()}", this);
        }

        /// <summary>Restores the stored calibration for the active target, if there is one.</summary>
        public void LoadCalibration()
        {
            if (!PlayerPrefs.HasKey(PrefsKey("Right")))
                return;

            m_OffsetRight = PlayerPrefs.GetFloat(PrefsKey("Right"), m_OffsetRight);
            m_OffsetUp = PlayerPrefs.GetFloat(PrefsKey("Up"), m_OffsetUp);
            m_OffsetForward = PlayerPrefs.GetFloat(PrefsKey("Forward"), m_OffsetForward);
            m_YawDegrees = PlayerPrefs.GetFloat(PrefsKey("Yaw"), m_YawDegrees);
            if (PlayerPrefs.HasKey(PrefsKey("Right2")))
            {
                m_ExtentRight = PlayerPrefs.GetFloat(PrefsKey("Right2"), m_ExtentRight);
                m_ExtentLeft = PlayerPrefs.GetFloat(PrefsKey("Left2"), m_ExtentLeft);
                m_ExtentUp = PlayerPrefs.GetFloat(PrefsKey("Up2"), m_ExtentUp);
                m_ExtentDown = PlayerPrefs.GetFloat(PrefsKey("Down2"), m_ExtentDown);
                m_ExtentBack = PlayerPrefs.GetFloat(PrefsKey("Back2"), m_ExtentBack);
                m_ExtentFront = PlayerPrefs.GetFloat(PrefsKey("Front2"), m_ExtentFront);
            }
            else if (PlayerPrefs.HasKey(PrefsKey("SizeX")))
            {
                // Calibrations saved before the faces became independent stored a size, which back
                // then was laid out from a facade-and-ground pivot. Rebuilding the extents from that
                // convention keeps the work already done on the phone instead of discarding it.
                var width = PlayerPrefs.GetFloat(PrefsKey("SizeX"), 1f);
                var height = PlayerPrefs.GetFloat(PrefsKey("SizeY"), 1f);
                var depth = PlayerPrefs.GetFloat(PrefsKey("SizeZ"), 1f);

                m_ExtentRight = width * 0.5f;
                m_ExtentLeft = width * 0.5f;
                m_ExtentUp = height;
                m_ExtentDown = 0f;
                m_ExtentBack = depth;
                m_ExtentFront = 0f;
            }
            m_ContentShape = (ContentShape)PlayerPrefs.GetInt(PrefsKey("Shape"), (int)m_ContentShape);
            m_RoofHeight = PlayerPrefs.GetFloat(PrefsKey("Roof"), m_RoofHeight);
            m_RidgeAlongWidth = PlayerPrefs.GetInt(PrefsKey("Ridge"), m_RidgeAlongWidth ? 1 : 0) != 0;

            // The stored shape may differ from the one on screen, and the offsets are applied against
            // the content's bounds, so the content has to be rebuilt before they mean anything. This
            // is a no-op at startup, when nothing has been placed yet.
            RebuildContent();
        }

        /// <summary>Forgets the stored calibration for the active target and zeroes the offsets.</summary>
        public void ResetCalibration()
        {
            PlayerPrefs.DeleteKey(PrefsKey("Right"));
            PlayerPrefs.DeleteKey(PrefsKey("Up"));
            PlayerPrefs.DeleteKey(PrefsKey("Forward"));
            PlayerPrefs.DeleteKey(PrefsKey("Yaw"));
            PlayerPrefs.DeleteKey(PrefsKey("SizeX"));
            PlayerPrefs.DeleteKey(PrefsKey("SizeY"));
            PlayerPrefs.DeleteKey(PrefsKey("SizeZ"));
            PlayerPrefs.DeleteKey(PrefsKey("Shape"));
            PlayerPrefs.DeleteKey(PrefsKey("Roof"));
            PlayerPrefs.DeleteKey(PrefsKey("Ridge"));
            PlayerPrefs.DeleteKey(PrefsKey("Right2"));
            PlayerPrefs.DeleteKey(PrefsKey("Left2"));
            PlayerPrefs.DeleteKey(PrefsKey("Up2"));
            PlayerPrefs.DeleteKey(PrefsKey("Down2"));
            PlayerPrefs.DeleteKey(PrefsKey("Back2"));
            PlayerPrefs.DeleteKey(PrefsKey("Front2"));
            PlayerPrefs.Save();

            m_OffsetRight = 0f;
            m_OffsetUp = 0f;
            m_OffsetForward = 0f;
            m_YawDegrees = 0f;
            m_ExtentRight = 0.5f;
            m_ExtentLeft = 0.5f;
            m_ExtentUp = 0.5f;
            m_ExtentDown = 0.5f;
            m_ExtentBack = 0.5f;
            m_ExtentFront = 0.5f;
            m_ContentShape = ContentShape.Box;
            m_RoofHeight = 1.5f;
            m_RidgeAlongWidth = true;
            RebuildContent();
        }

        /// <summary>The current calibration formatted for pasting back into the Inspector.</summary>
        public string DescribeCalibration()
        {
            return
                $"Offset Right: {m_OffsetRight:0.###}\n" +
                $"Offset Up: {m_OffsetUp:0.###}\n" +
                $"Offset Forward: {m_OffsetForward:0.###}\n" +
                $"Yaw Degrees: {m_YawDegrees:0.#}\n" +
                $"Extent Right: {m_ExtentRight:0.###}\n" +
                $"Extent Left: {m_ExtentLeft:0.###}\n" +
                $"Extent Up: {m_ExtentUp:0.###}\n" +
                $"Extent Down: {m_ExtentDown:0.###}\n" +
                $"Extent Back: {m_ExtentBack:0.###}\n" +
                $"Extent Front: {m_ExtentFront:0.###}\n" +
                $"Size Meters: ({SizeMeters.x:0.###}, {SizeMeters.y:0.###}, {SizeMeters.z:0.###})\n" +
                $"Content Shape: {m_ContentShape}\n" +
                $"Roof Height: {m_RoofHeight:0.###}\n" +
                $"Ridge Along Width: {m_RidgeAlongWidth}";
        }

        void OnValidate()
        {
            m_StabilitySamples = Mathf.Max(2, m_StabilitySamples);
            m_ExtentRight = Mathf.Max(0f, m_ExtentRight);
            m_ExtentLeft = Mathf.Max(0f, m_ExtentLeft);
            m_ExtentUp = Mathf.Max(0f, m_ExtentUp);
            m_ExtentDown = Mathf.Max(0f, m_ExtentDown);
            m_ExtentBack = Mathf.Max(0f, m_ExtentBack);
            m_ExtentFront = Mathf.Max(0f, m_ExtentFront);
            m_RoofHeight = Mathf.Max(0f, m_RoofHeight);
            ApplyCalibration();
        }
    }
}
