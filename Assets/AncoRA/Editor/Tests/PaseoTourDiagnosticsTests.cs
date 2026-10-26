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
    public class PaseoTourDiagnosticsTests
    {
        GameObject host;
        PaseoTour tour;
        Localizer localizer;
        ImmersalSDK sdk;
        UnityEvent<int> previousMapLoaded;

        [SetUp]
        public void SetUp()
        {
            previousMapLoaded = MapManager.MapRegisteredAndLoaded;
            host = new GameObject("Paseo de prueba");
            tour = host.AddComponent<PaseoTour>();
            var localizerHost = new GameObject("Localizer de prueba");
            localizerHost.transform.SetParent(host.transform);
            localizer = localizerHost.AddComponent<Localizer>();
            localizer.OnLocalizationResult = new UnityEvent<ILocalizationResults>();
            var sdkHost = new GameObject("SDK de prueba");
            sdkHost.transform.SetParent(host.transform);
            sdkHost.SetActive(false);
            sdk = sdkHost.AddComponent<ImmersalSDK>();
            tour.Configure(sdk, localizer, Array.Empty<PaseoMapContent>());
            RunTourMethod("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            MapManager.MapRegisteredAndLoaded = previousMapLoaded;
        }

        // SendMessage enforces Play Mode lifecycle checks in Unity 6; invoke these callbacks directly in EditMode.
        void RunTourMethod(string name) => typeof(PaseoTour)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tour, null);

        [Test]
        public void FailedLocalizationsWithoutMapIdAppearInTheDiagnostics()
        {
            localizer.OnLocalizationResult.Invoke(new LocalizationResults
            {
                Results = new ILocalizationResult[]
                {
                    new LocalizationResult { Success = false, MapId = 0 },
                    new LocalizationResult { Success = true, MapId = 152257 },
                    new LocalizationResult { Success = false, MapId = 0 }
                }
            });
            StringAssert.Contains("3 intentos, 2 sin coincidencia", tour.LocalizationSummary);
        }

        [Test]
        public void LateSdkInitializationClearsTheErrorBanner()
        {
            // Simulate a slow camera/SDK startup without waiting or initializing native AR in EditMode.
            typeof(PaseoTour).GetField("sceneStart", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(tour, Time.realtimeSinceStartup - 20f);
            LogAssert.Expect(LogType.Error, "[AncoRA Paseo] El SDK no quedó listo en 15 s; revisar el log de inicio.");
            RunTourMethod("CheckSdkStart");
            Assert.IsNotEmpty(tour.Error);

            typeof(ImmersalSDK).GetField("m_IsReady", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sdk, true);
            RunTourMethod("CheckSdkStart");
            Assert.IsEmpty(tour.Error);
            PaseoStatusText.Describe(new PaseoStatusInputs { SdkReady = tour.SdkReady, Error = tour.Error }, out var level);
            Assert.AreNotEqual(PaseoStatusLevel.Error, level);
        }
    }
}
