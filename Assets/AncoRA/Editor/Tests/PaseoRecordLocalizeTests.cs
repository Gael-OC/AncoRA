using System;
using System.Collections;
using System.Collections.Concurrent;
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
        // What the worker thread did, in order: px=<n> for the plugin's setting, backend for the native call.
        readonly ConcurrentQueue<string> calls = new();

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
            calls.Clear();
            // No test reaches the plugin: the setting goes to the list instead of Core.SetInteger.
            localization.SetMaxPixels = value => calls.Enqueue("px=" + value);
            localization.Backend = (int n, int[] ids, int width, int height, ref Vector4 intrinsics, IntPtr px, int channels, int solver, ref Quaternion rotation) =>
            {
                backendCalls++;
                backendPixels = px;
                firstByteSeenByBackend = Marshal.ReadByte(px);
                calls.Enqueue("backend");
                return new LocalizeInfo { mapId = -1 };
            };
        }

        // A fresh camera image: Localize releases the one it is given, so a second attempt cannot reuse it.
        CameraData NewCamera(out PinnedImage fresh)
        {
            fresh = new PinnedImage((byte[])pixels.Clone());
            return new CameraData(fresh)
            {
                Width = W, Height = H, Channels = 1,
                Intrinsics = new Vector4(500f, 480f, 16f, 8f),
                CameraRotationOnCapture = Quaternion.Euler(0f, 40f, 0f),
                ScreenOrientation = Quaternion.Euler(0f, 0f, 90f)
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

        // ---- the image size (PaseoResolution) ----

        [Test]
        public void TheDefaultSizeIsTheMiddleOne()
        {
            Assert.AreEqual(1440 * 810, localization.MaxPixels);
        }

        [UnityTest]
        public IEnumerator TheSizeIsAppliedOnTheWorkerRightBeforeTheNativeCall()
        {
            localization.MaxPixels = PaseoResolution.Medium;
            PaseoAttempt attempt = default;
            localization.AttemptFinished += a => attempt = a;
            yield return Wait(localization.Localize(camera, CancellationToken.None));
            CollectionAssert.AreEqual(new[] { "px=1166400", "backend" }, calls);
            Assert.AreEqual(PaseoResolution.Medium, attempt.MaxPixels, "the attempt remembers the size it ran at");
        }

        [UnityTest]
        public IEnumerator AChangeOfSizeAppliesOnTheNextAttempt()
        {
            var attempts = new System.Collections.Generic.List<int>();
            localization.AttemptFinished += a => attempts.Add(a.MaxPixels);
            localization.MaxPixels = PaseoResolution.Reduced;
            yield return Wait(localization.Localize(camera, CancellationToken.None));
            localization.MaxPixels = PaseoResolution.Full;
            var second = NewCamera(out _);
            yield return Wait(localization.Localize(second, CancellationToken.None));
            CollectionAssert.AreEqual(new[] { "px=691200", "backend", "px=0", "backend" }, calls);
            CollectionAssert.AreEqual(new[] { PaseoResolution.Reduced, PaseoResolution.Full }, attempts);
        }

        [UnityTest]
        public IEnumerator ASuspendedLocalizationSkipsTheAttemptButStillReleasesTheImage()
        {
            int asked = 0, started = 0, finished = 0;
            localization.NextSearch = () =>
            {
                asked++;
                return new PaseoSearch(null, null, new int[0], PaseoSearchReason.All, double.NaN);
            };
            localization.AttemptStarted += _ => started++;
            localization.AttemptFinished += _ => finished++;
            localization.Suspended = true;
            var task = localization.Localize(camera, CancellationToken.None);
            yield return Wait(task);
            Assert.IsFalse(task.Result.Success);
            Assert.AreEqual(0, backendCalls);
            Assert.IsEmpty(calls, "not even the plugin's setting is touched");
            Assert.AreEqual(1, image.Released, "the SDK's camera image is still released");
            Assert.AreEqual(0, asked, "the search plan does not advance");
            Assert.AreEqual(0, started + finished);

            // Resumed: the first real attempt is the number 1 (a skipped one is not an attempt).
            localization.Suspended = false;
            PaseoAttempt attempt = default;
            localization.AttemptFinished += a => attempt = a;
            yield return Wait(localization.Localize(NewCamera(out _), CancellationToken.None));
            Assert.AreEqual(1, attempt.Number);
            Assert.AreEqual(1, asked);
        }

        [UnityTest]
        public IEnumerator BusyIsTrueWhileTheNativeCallRuns()
        {
            using var release = new ManualResetEventSlim(false);
            bool entered = false;
            localization.Backend = (int n, int[] ids, int width, int height, ref Vector4 intrinsics, IntPtr px, int channels, int solver, ref Quaternion rotation) =>
            {
                entered = true;
                release.Wait(5000);
                return new LocalizeInfo { mapId = -1 };
            };
            Assert.IsFalse(localization.Busy);
            var task = localization.Localize(camera, CancellationToken.None);
            while (!entered)
                yield return null;
            Assert.IsTrue(localization.Busy);
            release.Set();
            yield return Wait(task);
            Assert.IsFalse(localization.Busy);
        }

        [UnityTest]
        public IEnumerator BusyClearsWhenTheNativeCallThrows()
        {
            localization.Backend = (int n, int[] ids, int width, int height, ref Vector4 intrinsics, IntPtr px, int channels, int solver, ref Quaternion rotation) =>
                throw new InvalidOperationException("plugin");
            var task = localization.Localize(camera, CancellationToken.None);
            while (!task.IsCompleted)
                yield return null;
            Assert.IsTrue(task.IsFaulted);
            Assert.IsFalse(localization.Busy);
            Assert.AreEqual(1, image.Released);
        }

        [Test]
        public void AnImageWithoutAPointerIsNotCopied()
        {
            Assert.IsNull(PaseoFrame.Copy(IntPtr.Zero, camera));
        }
    }
}
