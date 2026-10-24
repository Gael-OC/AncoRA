using System;
using System.Reflection;
using System.Text.RegularExpressions;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
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
            StringAssert.Contains("3 intentos, 2 sin coincidencia", tour.DescribeMaps());
        }

        // On an inactive object so the manager's OnEnable (which looks for the AR subsystem) does not run in EditMode.
        AROcclusionManager NewOcclusionManager()
        {
            var cameraHost = new GameObject("Cámara de prueba");
            cameraHost.transform.SetParent(host.transform);
            cameraHost.SetActive(false);
            var manager = cameraHost.AddComponent<AROcclusionManager>();
            manager.requestedEnvironmentDepthMode = PaseoTour.OcclusionDepthMode;
            manager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
            tour.SetOcclusion(manager);
            return manager;
        }

        [Test]
        public void ToggleOcclusionSwitchesTheManagerBetweenItsDepthModeAndDisabled()
        {
            var manager = NewOcclusionManager();
            Assert.IsTrue(tour.OcclusionOn);
            Assert.IsFalse(tour.OcclusionUnsupported, "unknown until the AR session runs is not unsupported");

            tour.ToggleOcclusion();
            Assert.IsFalse(tour.OcclusionOn);
            Assert.AreEqual(EnvironmentDepthMode.Disabled, manager.requestedEnvironmentDepthMode);
            Assert.AreEqual(OcclusionPreferenceMode.NoOcclusion, manager.requestedOcclusionPreferenceMode,
                "the camera background drops the depth at the next frame, without waiting for ARCore");

            tour.ToggleOcclusion();
            Assert.IsTrue(tour.OcclusionOn);
            Assert.AreEqual(PaseoTour.OcclusionDepthMode, manager.requestedEnvironmentDepthMode);
            Assert.AreEqual(OcclusionPreferenceMode.PreferEnvironmentOcclusion, manager.requestedOcclusionPreferenceMode);
        }

        [Test]
        public void WithoutAnOcclusionManagerTheToggleDoesNothing()
        {
            tour.SetOcclusion(null);
            Assert.IsFalse(tour.OcclusionOn);
            Assert.DoesNotThrow(() => tour.ToggleOcclusion());
            Assert.IsFalse(tour.OcclusionOn);
        }

        [Test]
        public void WithoutALocalizationMethodTheDetectionTestsOf136DoNothing()
        {
            // This tour's localizer has no PaseoLocalization: the buttons must not throw and the tests stay normal.
            Assert.DoesNotThrow(() =>
            {
                tour.CycleContrastTest();
                tour.CycleSolverTest();
                tour.TogglePriorTest();
            });
            Assert.AreEqual(PaseoAbMode.Normal, tour.ContrastTest);
            Assert.AreEqual(PaseoAbMode.Normal, tour.SolverTest);
            Assert.IsFalse(tour.PriorTest);
        }

        [Test]
        public void TheTourPassesTheAdjustingStateToEveryBuilding()
        {
            var boxObject = new GameObject("Caja");
            boxObject.transform.SetParent(host.transform);
            var frame = boxObject.AddComponent<EdificioFacadeFrame>();
            frame.Configure(4f, 3f, 4f, null, null, null, false);
            var content = boxObject.AddComponent<PaseoMapContent>();
            content.Configure(1, "Prueba", "Prueba", "", frame);
            tour.Configure(sdk, localizer, new[] { content });

            tour.SetAdjusting(true);
            Assert.IsTrue(tour.Adjusting);
            Assert.IsTrue(content.IsAdjusting);
            tour.SetAdjusting(true);
            tour.SetAdjusting(false);
            Assert.IsFalse(tour.Adjusting);
            Assert.IsFalse(content.IsAdjusting);
        }

        [Test]
        public void TheTourStartsOnTheBoxAndCyclesTheViewOfEveryBuilding()
        {
            var contents = new PaseoMapContent[2];
            for (int i = 0; i < contents.Length; i++)
            {
                var boxObject = new GameObject("Caja " + i);
                boxObject.transform.SetParent(host.transform);
                var frame = boxObject.AddComponent<EdificioFacadeFrame>();
                frame.Configure(4f, 3f, 4f, null, null, null, false);
                contents[i] = boxObject.AddComponent<PaseoMapContent>();
                contents[i].Configure(i + 1, "Prueba" + i, "Prueba" + i, "", frame);
            }
            tour.Configure(sdk, localizer, contents);
            Assert.AreEqual(PaseoViewMode.Box, tour.ViewMode, "the app starts on the box, for now");

            foreach (var (expected, name) in new[]
                     {
                         (PaseoViewMode.Background, "fondo"), (PaseoViewMode.Object, "objeto"), (PaseoViewMode.Box, "caja")
                     })
            {
                LogAssert.Expect(LogType.Log, new Regex(@"^\[AncoRA Paseo\] DEPURACION_UI vista=" + name + @" t=\d+\.\d$"));
                tour.CycleViewMode();
                Assert.AreEqual(expected, tour.ViewMode);
                foreach (var content in contents)
                    Assert.AreEqual(expected, content.ViewMode, "every building follows the tour: " + name);
            }

            tour.SetViewMode(PaseoViewMode.Box);
            Assert.AreEqual(PaseoViewMode.Box, tour.ViewMode);
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
