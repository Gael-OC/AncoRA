using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AncorRA.Tests
{
    /// <summary>The tour's side of the image size: reading the saved one, applying a new one, and running the phone test with the search paused.</summary>
    public class PaseoResolutionTourTests
    {
        const string Prefix = "AncoRA.Test.TourResolucion.";
        GameObject host;
        PaseoTour tour;
        PaseoLocalization localization;
        Localizer localizer;
        PaseoResolutionStore store;
        UnityEvent<int> previousMapLoaded;

        [SetUp]
        public void SetUp()
        {
            previousMapLoaded = MapManager.MapRegisteredAndLoaded;
            store = new PaseoResolutionStore(Prefix);
            store.Clear();
            host = new GameObject("Paseo de prueba");
            tour = host.AddComponent<PaseoTour>();
            var localizerHost = new GameObject("Localizer de prueba");
            localizerHost.transform.SetParent(host.transform);
            localizer = localizerHost.AddComponent<Localizer>();
            localizer.OnLocalizationResult = new UnityEvent<ILocalizationResults>();
            localization = localizerHost.AddComponent<PaseoLocalization>();
            localization.SetMaxPixels = _ => { };
            var methods = new SerializedObject(localizer);
            var list = methods.FindProperty("m_LocalizationMethodObjects");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = localization;
            methods.ApplyModifiedPropertiesWithoutUndo();
            var sdkHost = new GameObject("SDK de prueba");
            sdkHost.transform.SetParent(host.transform);
            sdkHost.SetActive(false);
            tour.Configure(sdkHost.AddComponent<ImmersalSDK>(), localizer, Array.Empty<PaseoMapContent>());
            tour.ResolutionStore = store;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            MapManager.MapRegisteredAndLoaded = previousMapLoaded;
            store.Clear();
        }

        // The tests choose what the phone has saved before the tour wakes up.
        void Awake() => typeof(PaseoTour).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tour, null);

        [Test]
        public void WithNothingSavedTheTourStartsAtTheDefaultSize()
        {
            LogAssert.Expect(LogType.Log, "[AncoRA Paseo] RESOLUCION_PASEO max_pixeles=1166400 imagen=1440x810 origen=por-defecto");
            Awake();
            Assert.AreEqual(PaseoResolution.Default, localization.MaxPixels);
            Assert.AreEqual(PaseoResolution.Default, tour.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginDefault, tour.ResolutionChoice.Origin);
        }

        [Test]
        public void TheTourStartsAtTheSizeThePhoneSavedAndSaysSoOnce()
        {
            store.Save(PaseoResolution.Full, PaseoResolution.OriginTest, new DateTime(2026, 10, 29));
            LogAssert.Expect(LogType.Log, "[AncoRA Paseo] RESOLUCION_PASEO max_pixeles=0 imagen=1920x1080 origen=prueba");
            Awake();
            Assert.AreEqual(0, localization.MaxPixels);
            Assert.AreEqual(0, tour.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginTest, tour.ResolutionChoice.Origin);
            Assert.AreEqual(new DateTime(2026, 10, 29), tour.ResolutionChoice.Date);
        }

        [Test]
        public void ChoosingASizeAppliesItSavesItAndLogsIt()
        {
            Awake();
            LogAssert.Expect(LogType.Log, "[AncoRA Paseo] RESOLUCION_PASEO max_pixeles=691200 imagen=1109x624 origen=elegida");
            tour.SetResolution(PaseoResolution.Reduced, PaseoResolution.OriginChosen);
            Assert.AreEqual(PaseoResolution.Reduced, localization.MaxPixels);
            Assert.AreEqual(PaseoResolution.Reduced, tour.MaxPixels);
            var saved = store.Load();
            Assert.AreEqual(PaseoResolution.Reduced, saved.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginChosen, saved.Origin);
            Assert.AreEqual(DateTime.Now.Date, saved.Date);
            Assert.AreEqual(saved.MaxPixels, tour.ResolutionChoice.MaxPixels);
        }

        [Test]
        public void ASizeTheVersionDoesNotOfferIsIgnored()
        {
            Awake();
            tour.SetResolution(921600, PaseoResolution.OriginChosen);
            Assert.AreEqual(PaseoResolution.Default, localization.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginDefault, store.Load().Origin);
        }

        [Test]
        public void ThePausedSearchDoesNotCountItsEmptyResultsAsFailedAttempts()
        {
            Awake();
            var empty = new LocalizationResults { Results = new ILocalizationResult[] { new LocalizationResult { Success = false, MapId = 0 } } };
            localization.Suspended = true;
            localizer.OnLocalizationResult.Invoke(empty);
            Assert.AreEqual(0, tour.LocalizationAttempts);
            localization.Suspended = false;
            localizer.OnLocalizationResult.Invoke(empty);
            Assert.AreEqual(1, tour.LocalizationAttempts);
            Assert.AreEqual(1, tour.LocalizationFailures);
        }

        // ---- the phone test ----

        static System.Collections.Generic.List<PaseoPhoneTestImage> Images() => new()
        {
            new PaseoPhoneTestImage { Name = "a", Width = 4, Height = 2, MapIds = new[] { 152198 }, ExpectedMap = 152198, Pixels = new byte[8] },
            new PaseoPhoneTestImage { Name = "b", Width = 4, Height = 2, MapIds = new[] { 152192 }, ExpectedMap = 0, Pixels = new byte[8] }
        };

        static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                throw task.Exception.GetBaseException();
        }

        [UnityTest]
        public IEnumerator ThePhoneTestPausesTheSearchUsesWhatItChoosesAndIgnoresASecondRequest()
        {
            Awake();
            using var release = new ManualResetEventSlim(false);
            var test = new PaseoPhoneTest(Images(), (image, size) =>
            {
                release.Wait(5000);
                // Fast phone: every size is within the budget, so the full image wins.
                return new PaseoPhoneTestShot(image.ExpectedMap, size == PaseoResolution.Reduced ? 0.3 : size == PaseoResolution.Medium ? 0.6 : 1.3);
            }, _ => true, null, _ => { }, "Fake");
            tour.PhoneTestFactory = () => test;

            var first = tour.RunPhoneTest();
            Assert.IsTrue(tour.PhoneTestRunning);
            Assert.IsTrue(localization.Suspended, "the live search is paused");
            Assert.IsNotEmpty(tour.PhoneTestProgress);

            var second = tour.RunPhoneTest();
            Assert.IsTrue(second.IsCompleted);
            Assert.IsNull(second.Result, "a second request while one runs is ignored");

            release.Set();
            yield return Wait(first);
            Assert.IsTrue(first.Result.Completed);
            Assert.AreEqual(PaseoResolution.Full, first.Result.Chosen);
            Assert.AreEqual(0, localization.MaxPixels, "applied to the localization");
            Assert.IsFalse(localization.Suspended);
            Assert.IsFalse(tour.PhoneTestRunning);
            var saved = store.Load();
            Assert.AreEqual(0, saved.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginTest, saved.Origin, "saved as chosen by the test");
            Assert.AreEqual(PaseoResolution.OriginTest, tour.ResolutionChoice.Origin);
        }

        [UnityTest]
        public IEnumerator ATestThatCannotRunChangesNothingAndResumesTheSearch()
        {
            Awake();
            tour.PhoneTestFactory = () => new PaseoPhoneTest(Images(), (image, size) => default, id => false, null, _ => { }, "Fake");
            LogAssert.Expect(LogType.Warning, new Regex("PRUEBA_TELEFONO no se hizo: Espera a que carguen los mapas"));
            var run = tour.RunPhoneTest();
            yield return Wait(run);
            Assert.AreEqual(PaseoPhoneTest.WaitForMaps, run.Result.Failure);
            Assert.AreEqual(PaseoResolution.Default, localization.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginDefault, store.Load().Origin, "nothing was saved");
            Assert.IsFalse(localization.Suspended);
            Assert.IsFalse(tour.PhoneTestRunning);
        }

        [UnityTest]
        public IEnumerator ATestThatThrowsIsReportedAndLeavesTheTourUsable()
        {
            Awake();
            tour.PhoneTestFactory = () => throw new InvalidOperationException("sin imágenes");
            LogAssert.Expect(LogType.Warning, new Regex("PRUEBA_TELEFONO falló: sin imágenes"));
            var run = tour.RunPhoneTest();
            yield return Wait(run);
            StringAssert.Contains("sin imágenes", run.Result.Failure);
            Assert.IsFalse(localization.Suspended);
            Assert.IsFalse(tour.PhoneTestRunning);

            // And it can run again.
            tour.PhoneTestFactory = () => new PaseoPhoneTest(Images(), (image, size) => new PaseoPhoneTestShot(image.ExpectedMap, 9.0), _ => true, null, _ => { }, "Fake");
            var again = tour.RunPhoneTest();
            yield return Wait(again);
            Assert.IsTrue(again.Result.Completed);
            Assert.AreEqual(PaseoResolution.Reduced, again.Result.Chosen, "a very slow phone keeps the smallest size");
        }
    }
}
