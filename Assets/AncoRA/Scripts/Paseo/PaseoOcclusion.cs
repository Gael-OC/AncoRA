using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AncorRA.AR
{
    public enum PaseoOcclusionMode { Off, Near, Normal }

    /// <summary>Optional demo occlusion. Raw depth is never filled across missing or uncertain pixels.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARCameraManager), typeof(AROcclusionManager))]
    public sealed class PaseoOcclusion : MonoBehaviour
    {
        const string Key = PaseoBoxStore.DefaultPrefix + "Oclusion";
        static readonly int OnId = Shader.PropertyToID("_PaseoOcclusionOn");
        static readonly int DepthId = Shader.PropertyToID("_PaseoOcclusionDepth");
        static readonly int DisplayId = Shader.PropertyToID("_PaseoOcclusionDisplay");
        ARCameraManager cameraManager;
        AROcclusionManager manager;
        Texture2D filteredDepth;
        float[] pixels;
        float lastCameraFrame = float.NegativeInfinity;
        double cameraTimestamp;
        bool hasDisplay;

        public PaseoOcclusionMode Mode { get; private set; }
        public float RangeMeters { get; private set; } = 5f;
        public string Status { get; private set; } = "Sin oclusión";

        void OnEnable()
        {
            cameraManager = GetComponent<ARCameraManager>();
            manager = GetComponent<AROcclusionManager>();
            cameraManager.frameReceived += CameraFrame;
            int saved = PlayerPrefs.GetInt(Key, 0);
            Mode = saved >= 0 && saved <= 2 ? (PaseoOcclusionMode)saved : PaseoOcclusionMode.Off;
            float range = PlayerPrefs.GetFloat(Key + ".Distancia", 5f);
            RangeMeters = range == 3f || range == 5f || range == 8f ? range : 5f;
            ApplyMode();
        }

        public void SetMode(PaseoOcclusionMode mode)
        {
            if (mode < PaseoOcclusionMode.Off || mode > PaseoOcclusionMode.Normal)
                return;
            Mode = mode;
            PlayerPrefs.SetInt(Key, (int)mode);
            PlayerPrefs.Save();
            ApplyMode();
        }

        public void SetRange(float meters)
        {
            if (meters != 3f && meters != 5f && meters != 8f)
                return;
            RangeMeters = meters;
            PlayerPrefs.SetFloat(Key + ".Distancia", meters);
            PlayerPrefs.Save();
            Shader.SetGlobalFloat(OnId, 0f);
            Debug.Log($"[AncoRA Paseo] OCLUSION distancia={meters:F0}m");
        }

        void ApplyMode()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            hasDisplay = false;
            manager.requestedHumanStencilMode = HumanSegmentationStencilMode.Disabled;
            manager.requestedHumanDepthMode = HumanSegmentationDepthMode.Disabled;
            manager.requestedEnvironmentDepthMode = Mode == PaseoOcclusionMode.Off ? EnvironmentDepthMode.Disabled : EnvironmentDepthMode.Fastest;
            manager.environmentDepthTemporalSmoothingRequested = Mode == PaseoOcclusionMode.Normal;
            // Keep the camera background from writing unfiltered real depth in Near mode.
            manager.requestedOcclusionPreferenceMode = Mode == PaseoOcclusionMode.Normal
                ? OcclusionPreferenceMode.PreferEnvironmentOcclusion : OcclusionPreferenceMode.NoOcclusion;
            manager.enabled = Mode != PaseoOcclusionMode.Off;
            SetStatus(Mode == PaseoOcclusionMode.Off ? "Sin oclusión" : "Esperando profundidad…");
            Debug.Log($"[AncoRA Paseo] OCLUSION modo={Mode} distancia={RangeMeters:F0}m confianza_min=128/255");
        }

        void CameraFrame(ARCameraFrameEventArgs args)
        {
            if (!args.displayMatrix.HasValue || !args.timestampNs.HasValue)
                return;
            Shader.SetGlobalMatrix(DisplayId, args.displayMatrix.Value);
            lastCameraFrame = Time.realtimeSinceStartup;
            cameraTimestamp = args.timestampNs.Value * 1e-9;
            hasDisplay = true;
        }

        void LateUpdate()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            if (Mode == PaseoOcclusionMode.Off)
                return;
            if (ARSession.state != ARSessionState.SessionTracking || !hasDisplay || Time.realtimeSinceStartup - lastCameraFrame > 0.5f)
            {
                SetStatus("Esperando cámara y tracking…");
                return;
            }
            if (manager.descriptor?.environmentDepthImageSupported == Supported.Unsupported)
            {
                SetStatus("Este teléfono no ofrece profundidad; se muestra sin oclusión.");
                return;
            }
            if (Mode == PaseoOcclusionMode.Normal)
            {
                SetStatus(manager.currentEnvironmentDepthMode == EnvironmentDepthMode.Disabled ? "Esperando profundidad…" : "Normal activa");
                return;
            }
            SetStatus("Cercana: esperando profundidad y confianza…");
            if (!manager.TryAcquireRawEnvironmentDepthCpuImage(out var depth))
                return;
            using (depth)
            {
                if (!manager.TryAcquireEnvironmentDepthConfidenceCpuImage(out var confidence))
                    return;
                using (confidence)
                {
                    // Paired raw images only. Never retain a screen mask across missing frames or tracking loss.
                    if (depth.format != XRCpuImage.Format.DepthUint16 || confidence.format != XRCpuImage.Format.OneComponent8 ||
                        depth.width != confidence.width || depth.height != confidence.height ||
                        Math.Abs(depth.timestamp - confidence.timestamp) > 0.05 || Math.Abs(cameraTimestamp - depth.timestamp) > 0.5)
                        return;
                    if (!SystemInfo.SupportsTextureFormat(TextureFormat.RFloat))
                    {
                        SetStatus("Profundidad filtrada no compatible; se muestra sin oclusión.");
                        return;
                    }
                    if (filteredDepth == null || filteredDepth.width != depth.width || filteredDepth.height != depth.height)
                    {
                        ReleaseTexture();
                        filteredDepth = new Texture2D(depth.width, depth.height, TextureFormat.RFloat, false, true)
                            { name = "Oclusión cercana", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                        pixels = new float[depth.width * depth.height];
                    }
                    var d = depth.GetPlane(0);
                    var c = confidence.GetPlane(0);
                    Filter(d.data, d.rowStride, d.pixelStride, c.data, c.rowStride, c.pixelStride,
                        depth.width, depth.height, RangeMeters, pixels);
                    filteredDepth.SetPixelData(pixels, 0);
                    filteredDepth.Apply(false, false);
                    Shader.SetGlobalTexture(DepthId, filteredDepth);
                    Shader.SetGlobalFloat(OnId, 1f);
                    SetStatus($"Cercana activa · hasta {RangeMeters:F0} m");
                }
            }
        }

        // ARCore raw depth is little-endian uint16 millimetres; confidence is uint8. Respect padded rows/pixels.
        public static void Filter(NativeArray<byte> depth, int depthRow, int depthPixel, NativeArray<byte> confidence,
            int confidenceRow, int confidencePixel, int width, int height, float range, float[] output)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * depthRow + x * depthPixel;
                    float meters = (depth[i] | depth[i + 1] << 8) * 0.001f;
                    output[y * width + x] = confidence[y * confidenceRow + x * confidencePixel] >= 128 &&
                        meters >= 0.2f && meters <= range ? meters : 0f;
                }
        }

        void SetStatus(string value)
        {
            Status = value;
        }

        void ReleaseTexture()
        {
            if (filteredDepth != null)
            {
                if (Application.isPlaying) Destroy(filteredDepth);
                else DestroyImmediate(filteredDepth);
            }
            filteredDepth = null;
            pixels = null;
        }

        void OnDisable()
        {
            if (cameraManager != null) cameraManager.frameReceived -= CameraFrame;
            Shader.SetGlobalFloat(OnId, 0f);
            hasDisplay = false;
            if (manager != null)
            {
                manager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
                manager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;
                manager.enabled = false;
            }
            ReleaseTexture();
        }
    }
}
