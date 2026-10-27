using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AncorRA.Tests
{
    /// <summary>
    /// Record mode inside PaseoLocalization.Localize. Whatever it does, the SDK's camera image is released exactly once
    /// (the 1.3.7 floor filter leaked it and ARCore stopped giving images) and the attempt still runs.
    /// </summary>
    public class PaseoRecordLocalizeTests
    {
        sealed class PinnedImage : ImageData
        {
            readonly byte[] bytes;
            GCHandle handle;
            public int Released;

            public PinnedImage(byte[] bytes)
            {
                this.bytes = bytes;
                handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            }

            public override IntPtr UnmanagedDataPointer => handle.AddrOfPinnedObject();
            public override byte[] ManagedBytes => bytes;

            public override void DisposeData()
            {
                Released++;
                if (handle.IsAllocated)
                    handle.Free();
            }
        }

        const int W = 32, H = 16;
        byte[] pixels;
        PinnedImage image;
        CameraData camera;
        GameObject go;
        PaseoLocalization localization;
        int backendCalls;
        IntPtr backendPixels;
        byte firstByteSeenByBackend;

        [SetUp]
        public void SetUp()
        {
            pixels = new byte[W * H];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (byte)(i * 7);
            image = new PinnedImage(pixels);
            camera = new CameraData(image)
            {
                Width = W, Height = H, Channels = 1,
                Intrinsics = new Vector4(500f, 480f, 16f, 8f),
                CameraPositionOnCapture = new Vector3(1f, 2f, 3f),
                CameraRotationOnCapture = Quaternion.Euler(0f, 40f, 0f),
                ScreenOrientation = Quaternion.Euler(0f, 0f, 90f),
                ImageOrientation = 90
            };
            go = new GameObject("PaseoRecordLocalizeTest");
            localization = go.AddComponent<PaseoLocalization>();
            backendCalls = 0;
            localization.Backend = (int n, int[] ids, int width, int height, ref Vector4 intrinsics, IntPtr px, int channels, int solver, ref Quaternion rotation) =>
            {
                backendCalls++;
                backendPixels = px;
                firstByteSeenByBackend = Marshal.ReadByte(px);
                return new LocalizeInfo { mapId = -1 };
            };
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(go);

        static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                throw task.Exception.GetBaseException();
        }

        [UnityTest]
        public IEnumerator WithoutRecordingTheAttemptRunsAndReleasesTheImageOnce()
        {
            var task = localization.Localize(camera, CancellationToken.None);
            yield return Wait(task);
            Assert.AreEqual(1, backendCalls);
            Assert.AreEqual(1, image.Released);
            Assert.IsFalse(task.Result.Success);
        }

        [UnityTest]
        public IEnumerator RecordingDeliversTheExactImageAndCameraAndStillReleasesTheImageOnce()
        {
            PaseoFrame got = null;
            PaseoAttempt gotAttempt = default;
            localization.RecordFrame = () => true;
            localization.FrameRecorded += (attempt, frame) =>
            {
                gotAttempt = attempt;
                got = frame;
            };
            var task = localization.Localize(camera, CancellationToken.None);
            yield return Wait(task);

            Assert.IsNotNull(got);
            Assert.AreEqual(1, gotAttempt.Number);
            Assert.AreEqual(W, got.Width);
            Assert.AreEqual(H, got.Height);
            Assert.AreEqual(1, got.Channels);
            CollectionAssert.AreEqual(pixels, got.Pixels);
            Assert.AreNotSame(pixels, got.Pixels, "a copy: the SDK's buffer is gone once the image is released");
            Assert.AreEqual(new Vector4(500f, 480f, 16f, 8f), got.Intrinsics);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), got.CameraPosition);
            Assert.AreEqual(90u, got.ImageOrientation);
            Assert.AreEqual(1, backendCalls);
            Assert.AreEqual(1, image.Released);
        }

        [UnityTest]
        public IEnumerator TheRecordedImageIsTheOneTheBackendReceives()
        {
            PaseoFrame got = null;
            localization.RecordFrame = () => true;
            localization.FrameRecorded += (_, frame) => got = frame;
            var task = localization.Localize(camera, CancellationToken.None);
            yield return Wait(task);
            Assert.AreEqual(pixels[0], firstByteSeenByBackend);
            Assert.AreEqual(pixels[0], got.Pixels[0]);
            Assert.AreEqual(W * H, got.Pixels.Length);
        }

        [UnityTest]
        public IEnumerator ARecordHookThatThrowsCostsOnlyTheRecording()
        {
            bool delivered = false;
            localization.RecordFrame = () => throw new InvalidOperationException("boom");
            localization.FrameRecorded += (_, _) => delivered = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No se pudo copiar la imagen"));
            var task = localization.Localize(camera, CancellationToken.None);
            yield return Wait(task);
            Assert.IsFalse(delivered);
            Assert.AreEqual(1, backendCalls, "the attempt still ran");
            Assert.AreEqual(1, image.Released);
        }

        [UnityTest]
        public IEnumerator AHandlerThatThrowsCostsOnlyTheRecording()
        {
            localization.RecordFrame = () => true;
            localization.FrameRecorded += (_, _) => throw new InvalidOperationException("disco");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No se pudo guardar la grabación"));
            var task = localization.Localize(camera, CancellationToken.None);
            yield return Wait(task);
            Assert.AreEqual(1, backendCalls);
            Assert.AreEqual(1, image.Released);
            Assert.IsFalse(task.Result.Success);
        }

        [UnityTest]
        public IEnumerator AFloorAttemptNeverRecordsAndStillReleasesTheImage()
        {
            bool asked = false;
            localization.RecordFrame = () => asked = true;
            camera.CameraRotationOnCapture = Quaternion.Euler(70f, 0f, 0f);
            var task = localization.Localize(camera, CancellationToken.None);
            yield return Wait(task);
            Assert.IsFalse(asked);
            Assert.AreEqual(0, backendCalls);
            Assert.AreEqual(1, image.Released);
        }

        [Test]
        public void AnImageWithoutAPointerIsNotCopied()
        {
            Assert.IsNull(PaseoFrame.Copy(IntPtr.Zero, camera));
        }
    }
}
