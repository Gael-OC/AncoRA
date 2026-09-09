using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AncorRA.AR
{
    /// <summary>
    /// Places one calibrated object relative to a recognized real-world image.
    /// Swap the image in the reference library to reuse the same flow at another location.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARTrackedImageManager))]
    [RequireComponent(typeof(ARAnchorManager))]
    public sealed class ImageAnchorBuildingProbe : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField]
        [Tooltip("Reference Image Library entry name. Leave empty to accept the first detected image.")]
        string m_TargetImageName = "HouseTarget";

        [SerializeField]
        [Tooltip("Keep the content at its last known pose while image tracking is limited.")]
        bool m_KeepVisibleWhenTrackingIsLimited = true;

        [SerializeField]
        [Min(1)]
        [Tooltip("Consecutive tracking frames required before locking the pose to an AR anchor.")]
        int m_TrackingFramesBeforeAnchor = 15;

        [SerializeField]
        [Tooltip("Show on-device instructions while the building pose is being acquired.")]
        bool m_ShowStatus = true;

        [Header("Content")]
        [SerializeField]
        [Tooltip("Optional prefab. When empty, the component creates a plain Unity cube.")]
        GameObject m_ContentPrefab;

        [SerializeField]
        [Tooltip("Position in meters relative to the detected image.")]
        Vector3 m_LocalPosition;

        [SerializeField]
        [Tooltip("Rotation in degrees relative to the detected image.")]
        Vector3 m_LocalEulerAngles;

        [SerializeField]
        [Tooltip("Final dimensions in meters. For the test cube this should approximate the building.")]
        Vector3 m_SizeMeters = new Vector3(8f, 5f, 8f);

        ARTrackedImageManager m_TrackedImageManager;
        ARAnchorManager m_AnchorManager;
        ARTrackedImage m_ActiveImage;
        ARAnchor m_Anchor;
        Transform m_PoseLock;
        GameObject m_ContentInstance;
        int m_ConsecutiveTrackingFrames;
        GUIStyle m_StatusStyle;

        void Awake()
        {
            m_TrackedImageManager = GetComponent<ARTrackedImageManager>();
            m_AnchorManager = GetComponent<ARAnchorManager>();

            // RequireComponent is not applied retroactively when this script is updated.
            if (m_AnchorManager == null)
                m_AnchorManager = gameObject.AddComponent<ARAnchorManager>();
        }

        void OnEnable()
        {
            m_TrackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);

            if ((m_Anchor != null || m_PoseLock != null) && m_ContentInstance != null)
                m_ContentInstance.SetActive(true);
        }

        void Update()
        {
            if (m_Anchor != null || m_PoseLock != null || m_ActiveImage == null)
                return;

            if (m_ActiveImage.trackingState != TrackingState.Tracking)
            {
                m_ConsecutiveTrackingFrames = 0;
                return;
            }

            m_ConsecutiveTrackingFrames++;
            if (m_ConsecutiveTrackingFrames >= m_TrackingFramesBeforeAnchor)
                LockPose(m_ActiveImage.transform);
        }

        void OnDisable()
        {
            m_TrackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);

            if (m_ContentInstance != null)
                m_ContentInstance.SetActive(false);
        }

        void OnGUI()
        {
            if (!m_ShowStatus)
                return;

            string message;
            Color backgroundColor;

            if (m_Anchor != null || m_PoseLock != null)
            {
                message = "ANCLA LISTA\nYA PUEDES ALEJARTE";
                backgroundColor = new Color(0.08f, 0.55f, 0.2f, 0.9f);
            }
            else if (m_ActiveImage != null && m_ActiveImage.trackingState == TrackingState.Tracking)
            {
                var progress = Mathf.Clamp(
                    Mathf.RoundToInt(100f * m_ConsecutiveTrackingFrames / m_TrackingFramesBeforeAnchor),
                    0,
                    100);
                message = $"MANTÉN EL TELÉFONO QUIETO\nFIJANDO ANCLA {progress}%";
                backgroundColor = new Color(0.95f, 0.6f, 0.05f, 0.9f);
            }
            else
            {
                message = "APUNTA A LA PLACA\nACÉRCATE A 1 METRO";
                backgroundColor = new Color(0.75f, 0.12f, 0.12f, 0.9f);
            }

            m_StatusStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            m_StatusStyle.fontSize = Mathf.Clamp(Screen.height / 32, 20, 42);

            var safeArea = Screen.safeArea;
            var width = Mathf.Min(Screen.width - 32f, 720f);
            var height = Mathf.Clamp(Screen.height * 0.11f, 90f, 150f);
            var topInset = Screen.height - safeArea.yMax;
            var rect = new Rect((Screen.width - width) * 0.5f, topInset + 16f, width, height);

            var previousColor = GUI.color;
            GUI.color = backgroundColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, message, m_StatusStyle);
            GUI.color = previousColor;
        }

        void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            for (var i = 0; i < changes.added.Count; i++)
                UpdateTrackedImage(changes.added[i]);

            for (var i = 0; i < changes.updated.Count; i++)
                UpdateTrackedImage(changes.updated[i]);

            if (m_ActiveImage == null || m_Anchor != null || m_PoseLock != null)
                return;

            for (var i = 0; i < changes.removed.Count; i++)
            {
                if (changes.removed[i].Key != m_ActiveImage.trackableId)
                    continue;

                m_ActiveImage = null;
                if (m_ContentInstance != null)
                    m_ContentInstance.SetActive(false);
                break;
            }
        }

        void UpdateTrackedImage(ARTrackedImage trackedImage)
        {
            if (!MatchesTarget(trackedImage.referenceImage.name))
                return;

            if (m_Anchor != null || m_PoseLock != null)
                return;

            if (m_ActiveImage != trackedImage)
            {
                m_ActiveImage = trackedImage;
                m_ConsecutiveTrackingFrames = 0;
                EnsureContentInstance();
                m_ContentInstance.transform.SetParent(trackedImage.transform, false);
                ApplyCalibration();
            }

            var isTracking = trackedImage.trackingState == TrackingState.Tracking;
            var shouldBeVisible = isTracking ||
                (m_KeepVisibleWhenTrackingIsLimited && trackedImage.trackingState == TrackingState.Limited);
            m_ContentInstance.SetActive(shouldBeVisible);

            if (!isTracking)
                m_ConsecutiveTrackingFrames = 0;
        }

        void LockPose(Transform imageTransform)
        {
            var pose = new Pose(imageTransform.position, imageTransform.rotation);
            m_PoseLock = new GameObject("Building Pose Lock").transform;
            m_PoseLock.SetPositionAndRotation(pose.position, pose.rotation);

            EnsureContentInstance();
            m_ContentInstance.transform.SetParent(m_PoseLock, false);
            ApplyCalibration();
            m_ContentInstance.SetActive(true);

            Debug.Log("Building pose locked independently from the tracked image.", this);
            CreateAnchor(pose);
        }

        async void CreateAnchor(Pose pose)
        {
            var result = await m_AnchorManager.TryAddAnchorAsync(pose);

            if (!isActiveAndEnabled)
            {
                if (result.status.IsSuccess())
                    m_AnchorManager.TryRemoveAnchor(result.value);
                return;
            }

            if (!result.status.IsSuccess())
            {
                Debug.LogWarning(
                    $"Could not create the native building AR anchor ({result.status}). Using the independent world pose.",
                    this);
                return;
            }

            m_Anchor = result.value;
            EnsureContentInstance();
            m_ContentInstance.transform.SetParent(m_Anchor.transform, false);
            ApplyCalibration();
            m_ContentInstance.SetActive(true);

            if (m_PoseLock != null)
            {
                Destroy(m_PoseLock.gameObject);
                m_PoseLock = null;
            }

            Debug.Log("Native building AR anchor created.", this);
        }

        bool MatchesTarget(string imageName)
        {
            return string.IsNullOrWhiteSpace(m_TargetImageName) ||
                string.Equals(imageName, m_TargetImageName, StringComparison.Ordinal);
        }

        void EnsureContentInstance()
        {
            if (m_ContentInstance != null)
                return;

            m_ContentInstance = m_ContentPrefab != null
                ? Instantiate(m_ContentPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_ContentInstance.name = "Building Probe";
        }

        [ContextMenu("Apply Calibration")]
        public void ApplyCalibration()
        {
            if (m_ContentInstance == null)
                return;

            var contentTransform = m_ContentInstance.transform;
            contentTransform.localPosition = m_LocalPosition;
            contentTransform.localRotation = Quaternion.Euler(m_LocalEulerAngles);
            contentTransform.localScale = m_SizeMeters;
        }

        void OnValidate()
        {
            m_TrackingFramesBeforeAnchor = Mathf.Max(1, m_TrackingFramesBeforeAnchor);
            m_SizeMeters.x = Mathf.Max(0.01f, m_SizeMeters.x);
            m_SizeMeters.y = Mathf.Max(0.01f, m_SizeMeters.y);
            m_SizeMeters.z = Mathf.Max(0.01f, m_SizeMeters.z);
            ApplyCalibration();
        }
    }
}
