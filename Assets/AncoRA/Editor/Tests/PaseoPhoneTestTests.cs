using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
    /// <summary>The phone test's plan against a fake phone: the times per size are scripted, no plugin is touched.</summary>
    public class PaseoPhoneTestTests
    {
        const int Low = PaseoResolution.Reduced;
        const int Mid = PaseoResolution.Medium;
        const int Top = PaseoResolution.Full;

        // Two images that locate (X1, CB) and two that do not, like the bundled ones.
        static List<PaseoPhoneTestImage> Images() => new()
        {
            new PaseoPhoneTestImage { Name = "prueba-1", Width = 4, Height = 2, MapIds = new[] { 152198, 152200 }, ExpectedMap = 152198, Pixels = new byte[8] },
            new PaseoPhoneTestImage { Name = "prueba-2", Width = 4, Height = 2, MapIds = new[] { 152192, 152196 }, ExpectedMap = 152192, Pixels = new byte[8] },
            new PaseoPhoneTestImage { Name = "prueba-3", Width = 4, Height = 2, MapIds = new[] { 152192, 152196 }, ExpectedMap = 0, Pixels = new byte[8] },
            new PaseoPhoneTestImage { Name = "prueba-4", Width = 4, Height = 2, MapIds = new[] { 152198, 152200 }, ExpectedMap = 0, Pixels = new byte[8] }
        };

        // A phone whose attempt takes the same seconds on every image at each size. The fake clock moves by what each attempt took.
        sealed class FakePhone
        {
            public readonly Dictionary<int, double> Seconds = new();
            public readonly ConcurrentQueue<(int Image, int MaxPixels)> Calls = new();
            // Overrides what an image finds: (image index, size) -> map id.
            public Func<int, int, int?> Finds = (_, _) => null;
            public List<PaseoPhoneTestImage> Images = PaseoPhoneTestTests.Images();
            public double Now;
            public readonly List<string> Log = new();
            public readonly List<string> Progress = new();

            public PaseoPhoneTestShot Shot(PaseoPhoneTestImage image, int maxPixels)
            {
                int index = Images.IndexOf(image);
                Calls.Enqueue((index, maxPixels));
                double seconds = Seconds[maxPixels];
                Now += seconds;
                return new PaseoPhoneTestShot(Finds(index, maxPixels) ?? image.ExpectedMap, seconds);
            }

            public PaseoPhoneTest Test(Func<int, bool> hasHandle = null, string model = "Fake 1") =>
                new(Images, Shot, hasHandle ?? (_ => true), () => Now, Log.Add, model);

            public int CallsAt(int maxPixels) => Calls.Count(c => c.MaxPixels == maxPixels);
        }

        static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                throw task.Exception.GetBaseException();
        }

        PaseoPhoneTestResult result;

        IEnumerator Run(FakePhone phone, CancellationToken token = default, PaseoPhoneTest test = null)
        {
            var task = (test ?? phone.Test()).RunAsync(phone.Progress.Add, token);
            yield return Wait(task);
            result = task.Result;
        }

        [UnityTest]
        public IEnumerator AFastPhoneMeasuresAllThreeSizesAndPicksTheFullImage()
        {
            var phone = new FakePhone { Seconds = { [Low] = 0.4, [Mid] = 0.8, [Top] = 1.7 } };
            yield return Run(phone);
            Assert.AreEqual(Top, result.Chosen);
            Assert.IsTrue(result.Steps.All(s => s.Measured));
            CollectionAssert.AreEqual(new[] { Low, Mid, Top }, result.Steps.Select(s => s.MaxPixels));
            Assert.AreEqual(4, phone.CallsAt(Low));
            Assert.AreEqual(4, phone.CallsAt(Mid));
            Assert.AreEqual(4, phone.CallsAt(Top));
            Assert.IsTrue(result.Completed);
        }

        [UnityTest]
        public IEnumerator AXiaomiFourteenLikePhoneMeasuresTwoSizesSkipsTheFullOneAndPicksTheMiddle()
        {
            // 2.08 s, 4.14 s and 9.10 s are what the Xiaomi 14 took on the bundled images.
            var phone = new FakePhone { Seconds = { [Low] = 2.08, [Mid] = 4.14, [Top] = 9.10 } };
            yield return Run(phone);
            Assert.AreEqual(Mid, result.Chosen);
            Assert.IsTrue(result.Steps[0].Measured);
            Assert.IsTrue(result.Steps[1].Measured);
            Assert.IsFalse(result.Steps[2].Measured);
            Assert.AreEqual(4.14 * PaseoPhoneTest.RatioFull, result.Steps[2].PredictedSeconds, 1e-6);
            Assert.AreEqual(0, phone.CallsAt(Top), "predicted over the budget: not measured");
            Assert.AreEqual(8, phone.Calls.Count);
        }

        [UnityTest]
        public IEnumerator ASlowPhoneSkipsTheMiddleAndTheFullSizesAndPicksTheSmallest()
        {
            // A 15T-like phone: 3.5 s at 960 x 720, so 1440 x 810 would be 7 s.
            var phone = new FakePhone { Seconds = { [Low] = 3.5, [Mid] = 7.0, [Top] = 15.4 } };
            yield return Run(phone);
            Assert.AreEqual(Low, result.Chosen);
            Assert.IsTrue(result.Steps[0].Measured);
            Assert.IsFalse(result.Steps[1].Measured);
            Assert.IsFalse(result.Steps[2].Measured);
            Assert.AreEqual(7.0, result.Steps[1].PredictedSeconds, 1e-6);
            Assert.AreEqual(7.0 * PaseoPhoneTest.RatioFull, result.Steps[2].PredictedSeconds, 1e-6, "chained from the middle one's prediction");
            Assert.AreEqual(4, phone.Calls.Count);
        }

        [UnityTest]
        public IEnumerator EvenIfTheSmallestIsOverTheBudgetItIsTheChoice()
        {
            var phone = new FakePhone { Seconds = { [Low] = 6.0 } };
            yield return Run(phone);
            Assert.AreEqual(Low, result.Chosen);
            Assert.IsTrue(result.Completed);
            Assert.AreEqual(4, phone.Calls.Count);
        }

        [UnityTest]
        public IEnumerator ASizeMeasuredOverTheBudgetIsNotChosenAndSkipsTheNext()
        {
            // 2.4 s predicts 4.8 s for the middle one, which turns out to take 5.2 s.
            var phone = new FakePhone { Seconds = { [Low] = 2.4, [Mid] = 5.2, [Top] = 11.0 } };
            yield return Run(phone);
            Assert.IsTrue(result.Steps[1].Measured);
            Assert.AreEqual(5.2, result.Steps[1].MeanSeconds, 1e-6);
            Assert.AreEqual(Low, result.Chosen);
            Assert.AreEqual(0, phone.CallsAt(Top));
        }

        [UnityTest]
        public IEnumerator TheBudgetItselfIsStillWithinTheBudget()
        {
            var phone = new FakePhone { Seconds = { [Low] = 2.5, [Mid] = 5.0, [Top] = 11.0 } };
            yield return Run(phone);
            Assert.AreEqual(Mid, result.Chosen, "exactly 5.0 s fits");
        }

        [UnityTest]
        public IEnumerator TheMeanIsOverTheFourImages()
        {
            var phone = new FakePhone { Seconds = { [Low] = 2.0, [Mid] = 4.0, [Top] = 9.0 } };
            yield return Run(phone, test: null);
            Assert.AreEqual(2.0, result.Steps[0].MeanSeconds, 1e-9);
            Assert.AreEqual(4, result.Steps[0].Images);
        }

        [UnityTest]
        public IEnumerator AnImageThatFindsSomethingElseIsReportedButTheChoiceFollowsTheTimes()
        {
            var phone = new FakePhone { Seconds = { [Low] = 2.08, [Mid] = 4.14, [Top] = 9.10 } };
            // At 960 x 720 the third image (it must not locate) finds a CB map, and the first one (it must) finds nothing.
            phone.Finds = (image, size) =>
            {
                if (size != Low)
                    return null;
                return image switch { 2 => 152192, 0 => 0, _ => (int?)null };
            };
            yield return Run(phone);
            Assert.AreEqual(Mid, result.Chosen);
            Assert.AreEqual(2, result.Warnings.Count);
            StringAssert.Contains("960×720", result.Warnings[0]);
            StringAssert.Contains("imagen 1", result.Warnings[0]);
            StringAssert.Contains("152198", result.Warnings[0]);
            StringAssert.Contains("imagen 3", result.Warnings[1]);
            StringAssert.Contains("152192", result.Warnings[1]);
            Assert.AreEqual(2, result.Steps[0].AsExpected, "images 2 and 4 gave what was expected");
            Assert.AreEqual(2, result.Steps[0].Located, "images 2 and 3 located in a map");
            Assert.AreEqual(4, result.Steps[1].AsExpected);
            CollectionAssert.IsSupersetOf(result.Lines(), result.Warnings);
        }

        [UnityTest]
        public IEnumerator WithoutAHandleForEveryMapItStopsWithTheWaitMessageAndCallsNothing()
        {
            var phone = new FakePhone { Seconds = { [Low] = 1.0, [Mid] = 2.0, [Top] = 4.0 } };
            yield return Run(phone, test: phone.Test(hasHandle: id => id != 152196));
            Assert.AreEqual("Espera a que carguen los mapas y vuelve a probar.", result.Failure);
            Assert.IsFalse(result.Completed);
            Assert.IsEmpty(phone.Calls);
            Assert.IsEmpty(result.Steps);
            Assert.IsEmpty(phone.Log);
        }

        [UnityTest]
        public IEnumerator WithoutTheBundledImagesItSaysSoInsteadOfRunning()
        {
            var phone = new FakePhone { Images = new List<PaseoPhoneTestImage>() };
            yield return Run(phone);
            Assert.AreEqual(PaseoPhoneTest.NoImages, result.Failure);
            Assert.IsEmpty(phone.Calls);
        }

        [UnityTest]
        public IEnumerator ProgressNamesTheSizeAndTheImage()
        {
            var phone = new FakePhone { Seconds = { [Low] = 3.0, [Mid] = 6.0 } };
            yield return Run(phone);
            CollectionAssert.AreEqual(new[]
            {
                "Probando 960×720: imagen 1 de 4…", "Probando 960×720: imagen 2 de 4…",
                "Probando 960×720: imagen 3 de 4…", "Probando 960×720: imagen 4 de 4…"
            }, phone.Progress);

            phone = new FakePhone { Seconds = { [Low] = 0.2, [Mid] = 0.4, [Top] = 0.9 } };
            yield return Run(phone);
            Assert.AreEqual("Probando completa: imagen 4 de 4…", phone.Progress.Last());
        }

        [UnityTest]
        public IEnumerator ItLogsEachMeasuredSizeAndTheVerdict()
        {
            var phone = new FakePhone { Seconds = { [Low] = 2.08, [Mid] = 4.14, [Top] = 9.10 } };
            yield return Run(phone);
            Assert.AreEqual(3, phone.Log.Count, "two measured sizes and the verdict; the skipped one has no line");
            Assert.AreEqual("[AncoRA Paseo] PRUEBA_TELEFONO res=1109x624 max_pixeles=691200 seg=2.08 imagenes=4 ubica=2/4 esperadas=4/4", phone.Log[0]);
            Assert.AreEqual("[AncoRA Paseo] PRUEBA_TELEFONO res=1440x810 max_pixeles=1166400 seg=4.14 imagenes=4 ubica=2/4 esperadas=4/4", phone.Log[1]);
            Assert.AreEqual("[AncoRA Paseo] PRUEBA_TELEFONO elegida=1440x810 total_seg=24.9 modelo=Fake_1", phone.Log[2]);
            Assert.AreEqual(24.88, result.TotalSeconds, 0.01, "four attempts at each of the two sizes");
        }

        [UnityTest]
        public IEnumerator TheLinesUnderTheButtonUseTheSpanishDecimalComma()
        {
            var phone = new FakePhone { Seconds = { [Low] = 2.08, [Mid] = 4.14, [Top] = 9.10 } };
            yield return Run(phone);
            CollectionAssert.AreEqual(new[]
            {
                "960×720: 2,1 s · ubica 2/4",
                "1440×810: 4,1 s · ubica 2/4",
                "Completa: no se probó (≈ 9,1 s)",
                "Elegida: 1440×810"
            }, result.Lines());
        }

        [Test]
        public void APredictionOfTenSecondsOrMoreHasNoDecimals()
        {
            var slow = new PaseoPhoneTestResult { Chosen = Low };
            slow.Steps.Add(new PaseoPhoneTestStep { MaxPixels = Low, Measured = true, MeanSeconds = 6.04, Images = 4, Located = 1 });
            slow.Steps.Add(new PaseoPhoneTestStep { MaxPixels = Mid, PredictedSeconds = 12.08 });
            slow.Steps.Add(new PaseoPhoneTestStep { MaxPixels = Top, PredictedSeconds = 26.576 });
            CollectionAssert.AreEqual(new[]
            {
                "960×720: 6,0 s · ubica 1/4", "1440×810: no se probó (≈ 12 s)", "Completa: no se probó (≈ 27 s)", "Elegida: 960×720"
            }, slow.Lines());
        }

        [UnityTest]
        public IEnumerator ACancelledRunStopsAtOnceAndChoosesNothing()
        {
            var phone = new FakePhone { Seconds = { [Low] = 1.0, [Mid] = 2.0, [Top] = 4.0 } };
            using var cancel = new CancellationTokenSource();
            var test = new PaseoPhoneTest(phone.Images, (image, size) =>
            {
                var taken = phone.Shot(image, size);
                cancel.Cancel();
                return taken;
            }, _ => true, () => phone.Now, phone.Log.Add);
            yield return Run(phone, cancel.Token, test);
            Assert.IsTrue(result.Cancelled);
            Assert.IsFalse(result.Completed);
            Assert.AreEqual(1, phone.Calls.Count, "no image after the cancellation");
            Assert.IsEmpty(phone.Log);
        }

        // ---- the live search is paused while it runs ----

        GameObject host;
        PaseoLocalization localization;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("PaseoPhoneTest");
            localization = host.AddComponent<PaseoLocalization>();
            localization.SetMaxPixels = _ => { };
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        // The fake backend never reads the pixels.
        sealed class BlankImage : ImageData
        {
            public override IntPtr UnmanagedDataPointer => IntPtr.Zero;
            public override byte[] ManagedBytes => null;
            public override void DisposeData() { }
        }

        static CameraData NewCameraData() => new(new BlankImage())
        {
            Width = 8, Height = 8, Channels = 1, Intrinsics = new Vector4(10f, 10f, 4f, 4f),
            CameraRotationOnCapture = Quaternion.identity, ScreenOrientation = Quaternion.identity
        };

        [UnityTest]
        public IEnumerator TheSearchIsPausedUntilTheAttemptInFlightEndsAndResumesAfterTheTest()
        {
            using var release = new ManualResetEventSlim(false);
            bool entered = false;
            localization.Backend = (int n, int[] ids, int width, int height, ref Vector4 intrinsics, IntPtr px, int channels, int solver, ref Quaternion rotation) =>
            {
                entered = true;
                release.Wait(5000);
                return new LocalizeInfo { mapId = -1 };
            };
            var attempt = localization.Localize(NewCameraData(), CancellationToken.None);
            while (!entered)
                yield return null;

            var phone = new FakePhone { Seconds = { [Low] = 0.1, [Mid] = 0.2, [Top] = 0.4 } };
            bool suspendedDuringTest = false;
            var test = new PaseoPhoneTest(phone.Images, (image, size) =>
            {
                suspendedDuringTest = localization.Suspended;
                return phone.Shot(image, size);
            }, _ => true, () => phone.Now, phone.Log.Add);
            var run = PaseoPhoneTest.RunWithoutSearchAsync(localization, test, phone.Progress.Add, CancellationToken.None);
            for (int i = 0; i < 5; i++)
                yield return null;
            Assert.IsTrue(localization.Suspended);
            Assert.IsEmpty(phone.Calls, "it waits for the attempt in flight");
            Assert.AreEqual("Esperando que termine el intento en curso…", phone.Progress.Last());

            release.Set();
            yield return Wait(attempt);
            yield return Wait(run);
            Assert.IsTrue(suspendedDuringTest);
            Assert.IsTrue(run.Result.Completed);
            Assert.AreEqual(12, phone.Calls.Count);
            Assert.IsFalse(localization.Suspended, "the search resumes");
        }

        [UnityTest]
        public IEnumerator TheSearchResumesEvenWhenTheTestFails()
        {
            var phone = new FakePhone { Seconds = { [Low] = 0.1 } };
            var test = new PaseoPhoneTest(phone.Images, (image, size) => throw new InvalidOperationException("plugin"), _ => true, () => 0, phone.Log.Add);
            var run = PaseoPhoneTest.RunWithoutSearchAsync(localization, test, null, CancellationToken.None);
            while (!run.IsCompleted)
                yield return null;
            Assert.IsTrue(run.IsFaulted);
            Assert.IsFalse(localization.Suspended);
        }

        [UnityTest]
        public IEnumerator AnAttemptThatNeverEndsMakesTheTestGiveUpAndResumeTheSearch()
        {
            using var release = new ManualResetEventSlim(false);
            bool entered = false;
            localization.Backend = (int n, int[] ids, int width, int height, ref Vector4 intrinsics, IntPtr px, int channels, int solver, ref Quaternion rotation) =>
            {
                entered = true;
                release.Wait(5000);
                return new LocalizeInfo { mapId = -1 };
            };
            var attempt = localization.Localize(NewCameraData(), CancellationToken.None);
            while (!entered)
                yield return null;

            var phone = new FakePhone { Seconds = { [Low] = 0.1 } };
            float clock = 0f;
            var run = PaseoPhoneTest.RunWithoutSearchAsync(localization, phone.Test(), null, CancellationToken.None, () => clock);
            yield return null;
            yield return null;
            Assert.IsFalse(run.IsCompleted);
            clock = PaseoPhoneTest.PauseTimeoutSeconds + 1f;
            yield return Wait(run);
            StringAssert.Contains("No se pudo pausar", run.Result.Failure);
            Assert.IsFalse(run.Result.Completed);
            Assert.IsEmpty(phone.Calls);
            Assert.IsFalse(localization.Suspended);

            release.Set();
            yield return Wait(attempt);
        }

        // ---- the bundled images ----

        [Test]
        public void TheBundledImagesAreFourFullHdBuffersTheSizeOfTheirImage()
        {
            var images = PaseoPhoneTest.LoadImages();
            Assert.AreEqual(4, images.Count, "Assets/AncoRA/Resources/PaseoPrueba");
            foreach (var image in images)
            {
                Assert.AreEqual(1920, image.Width, image.Name);
                Assert.AreEqual(1080, image.Height, image.Name);
                Assert.AreEqual(1920 * 1080, image.Pixels.Length, image.Name);
                Assert.Greater(image.Intrinsics.x, 1000f, image.Name);
                Assert.IsNotEmpty(image.MapIds, image.Name);
            }
            CollectionAssert.AreEqual(new[] { 152198, 152192, 0, 0 }, images.Select(i => i.ExpectedMap));
            CollectionAssert.AreEqual(new[] { "prueba-1", "prueba-2", "prueba-3", "prueba-4" }, images.Select(i => i.Name));
            Assert.Greater(images[0].Pixels.Distinct().Count(), 50, "a camera image, not blank");
        }

        [Test]
        public void AGzipThatHoldsExactlyTheExpectedBytesComesBackWhole()
        {
            var bytes = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();
            CollectionAssert.AreEqual(bytes, PaseoPhoneTest.Gunzip(Gzip(bytes), bytes.Length));
        }

        [Test]
        public void AGzipOfAnotherSizeIsRefused()
        {
            var bytes = new byte[100];
            Assert.Throws<InvalidDataException>(() => PaseoPhoneTest.Gunzip(Gzip(bytes), 99), "more than expected");
            Assert.Throws<InvalidDataException>(() => PaseoPhoneTest.Gunzip(Gzip(bytes), 101), "less than expected");
        }

        static byte[] Gzip(byte[] bytes)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                gzip.Write(bytes, 0, bytes.Length);
            return output.ToArray();
        }
    }
}
