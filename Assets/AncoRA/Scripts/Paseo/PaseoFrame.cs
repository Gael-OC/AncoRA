using System;
using System.Runtime.InteropServices;
using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// A copy of what one attempt hands Core.LocalizeImage, taken before the call: the pixel buffer exactly as the SDK
    /// holds it (full resolution, one channel in the SDK's default; the plugin reduces it itself to LocalizationMaxPixels, PaseoResolution)
    /// and the camera values that go with it. The recorder (PaseoRecorder) saves it, so the same attempt can be replayed
    /// later on a computer.
    /// </summary>
    public sealed class PaseoFrame
    {
        // 4 bytes per pixel at 8000 × 6000 would still pass; anything larger is not a camera image.
        const long MaxBytes = 200L * 1024 * 1024;

        public byte[] Pixels;
        public int Width;
        public int Height;
        public int Channels;
        // As the SDK passes it: x, y = focal length, z, w = principal point (ARFoundationSupport.GetIntrinsics).
        public Vector4 Intrinsics;
        public Vector3 CameraPosition;
        // CameraRotationOnCapture; the rotation Core.LocalizeImage gets is this one times ScreenOrientation, handedness switched.
        public Quaternion CameraRotation;
        public Quaternion ScreenOrientation;
        public uint ImageOrientation;

        /// <summary>The image of <paramref name="cameraData"/> read from <paramref name="pixels"/>; null when it cannot be read.</summary>
        public static PaseoFrame Copy(IntPtr pixels, ICameraData cameraData)
        {
            int channels = cameraData.Channels == 0 ? 1 : cameraData.Channels;
            long size = (long)cameraData.Width * cameraData.Height * channels;
            if (pixels == IntPtr.Zero || cameraData.Width <= 0 || cameraData.Height <= 0 || size > MaxBytes)
                return null;
            var bytes = new byte[size];
            Marshal.Copy(pixels, bytes, 0, (int)size);
            return new PaseoFrame
            {
                Pixels = bytes,
                Width = cameraData.Width,
                Height = cameraData.Height,
                Channels = channels,
                Intrinsics = cameraData.Intrinsics,
                CameraPosition = cameraData.CameraPositionOnCapture,
                CameraRotation = cameraData.CameraRotationOnCapture,
                ScreenOrientation = cameraData.ScreenOrientation,
                ImageOrientation = cameraData.ImageOrientation
            };
        }
    }
}
