using System;
using System.Reflection;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AncorRA.Tests
{
    public class PaseoTourBeginTests
    {
        GameObject host;
        PaseoTour tour;
        UnityEvent<int> previousMapLoaded;

        [SetUp]
        public void SetUp()
        {
            previousMapLoaded = MapManager.MapRegisteredAndLoaded;
            host = new GameObject("Paseo de prueba");
            tour = host.AddComponent<PaseoTour>();
            var localizerHost = new GameObject("Localizer de prueba");
            localizerHost.transform.SetParent(host.transform);
            var localizer = localizerHost.AddComponent<Localizer>();
            localizer.OnLocalizationResult = new UnityEvent<ILocalizationResults>();
            var sdkHost = new GameObject("SDK de prueba");
            sdkHost.transform.SetParent(host.transform);
            sdkHost.SetActive(false);
            var sdk = sdkHost.AddComponent<ImmersalSDK>();
            tour.Configure(sdk, localizer, Array.Empty<PaseoMapContent>());
            Run("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            MapManager.MapRegisteredAndLoaded = previousMapLoaded;
        }

        void Run(string method) => typeof(PaseoTour).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tour, null);

        void StartedSecondsAgo(float seconds) => typeof(PaseoTour)
            .GetField("sceneStart", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(tour, Time.realtimeSinceStartup - seconds);

        [Test]
        public void WhileTheWelcomeIsOpenTheSdkTimeoutDoesNotFire()
        {
            tour.WaitForBegin();
            StartedSecondsAgo(20f);
            Run("CheckSdkStart");
            Assert.IsEmpty(tour.Error);
            Assert.IsFalse(tour.Begun);
        }

        [Test]
        public void BeginRestartsTheClockForTheSdkTimeout()
        {
            tour.WaitForBegin();
            StartedSecondsAgo(20f);
            tour.Begin();
            Assert.Less(tour.SecondsSinceStart, 1f);
            Run("CheckSdkStart");
            Assert.IsEmpty(tour.Error, "the minutes spent on the welcome do not count");

            StartedSecondsAgo(20f);
            LogAssert.Expect(LogType.Error, "[AncoRA Paseo] El SDK no quedó listo en 15 s; revisar el log de inicio.");
            Run("CheckSdkStart");
            Assert.IsNotEmpty(tour.Error);
        }

        void LoadedSecondsAgo(float seconds) => typeof(PaseoTour)
            .GetField("loadedAt", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(tour, Time.realtimeSinceStartup - seconds);

        int SdkRetries => (int)typeof(PaseoTour).GetField("sdkRetries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tour);

        void RetrySdkStart(bool tracking) => typeof(PaseoTour)
            .GetMethod("RetrySdkStart", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(tour, new object[] { Time.realtimeSinceStartup, tracking });

        [Test]
        public void AfterTheWelcomeTheSdkIsStartedAgainAtOnce()
        {
            // With the ARSession off behind the welcome, the SDK's own start (at scene load) never finds the camera. Once
            // the welcome closes and AR tracks, the retry must not wait its first-retry delay counted from Begin.
            tour.WaitForBegin();
            LoadedSecondsAgo(PaseoSdkStart.FirstRetrySeconds + 10f);
            tour.Begin();
            RetrySdkStart(tracking: true);
            Assert.AreEqual(1, SdkRetries);
        }

        [Test]
        public void TheFirstRetryStillWaitsForTheSdksOwnStart()
        {
            tour.WaitForBegin();
            LoadedSecondsAgo(1f);
            tour.Begin();
            RetrySdkStart(tracking: true);
            Assert.AreEqual(0, SdkRetries);
        }

        [Test]
        public void ATourWithoutAWelcomeHasBegun()
        {
            Assert.IsTrue(tour.Begun);
        }
    }
}
