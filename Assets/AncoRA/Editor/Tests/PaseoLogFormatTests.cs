using System;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoLogFormatTests
    {
        static readonly DateTime At = new(2026, 10, 14, 12, 40, 5, 123);

        const string ImmersalReady =
            "[.\\Library\\PackageCache\\com.immersal.core@0f1d5db1dc1b\\Runtime\\Scripts\\ImmersalSDK] Immersal SDK ready!";

        [Test]
        public void TheTourAndImmersalLinesAreKept()
        {
            Assert.IsTrue(PaseoLogFormat.Keep("[AncoRA Paseo] UBICACION_PASEO mapa=152259 veredicto=aceptada", LogType.Log));
            Assert.IsTrue(PaseoLogFormat.Keep("[AncoRA UI] Esquinas redondeadas: arriba 134 px", LogType.Log));
            Assert.IsTrue(PaseoLogFormat.Keep(ImmersalReady, LogType.Log));
        }

        [Test]
        public void EveryWarningAndErrorIsKept()
        {
            Assert.IsTrue(PaseoLogFormat.Keep("Shader 'X' is not supported", LogType.Warning));
            Assert.IsTrue(PaseoLogFormat.Keep("NullReferenceException", LogType.Exception));
            Assert.IsTrue(PaseoLogFormat.Keep("Algo falló", LogType.Error));
        }

        [Test]
        public void OtherInformationIsLeftOut()
        {
            Assert.IsFalse(PaseoLogFormat.Keep("Shader 'Hidden/Universal Render Pipeline/CameraMotionBlur' is not supported", LogType.Log));
            Assert.IsFalse(PaseoLogFormat.Keep(null, LogType.Log));
        }

        [Test]
        public void ALineHasTheTimeTheLevelAndTheFirstLineOfTheMessage()
        {
            Assert.AreEqual("12:40:05.123 I [AncoRA Paseo] hola", PaseoLogFormat.Line(At, LogType.Log, "[AncoRA Paseo] hola\nsegunda", ""));
            Assert.AreEqual("12:40:05.123 W cuidado", PaseoLogFormat.Line(At, LogType.Warning, "cuidado", ""));
        }

        [Test]
        public void ImmersalsLongCallerPathIsShortened()
        {
            Assert.AreEqual("12:40:05.123 I [Immersal ImmersalSDK] Immersal SDK ready!", PaseoLogFormat.Line(At, LogType.Log, ImmersalReady, ""));
        }

        [Test]
        public void ErrorsCarryTheirFirstStackFrame()
        {
            Assert.AreEqual("12:40:05.123 E boom @ AncorRA.AR.PaseoTour:Update()",
                PaseoLogFormat.Line(At, LogType.Error, "boom", "AncorRA.AR.PaseoTour:Update()\nUnityEngine.Something()"));
            Assert.AreEqual("12:40:05.123 X NullReferenceException", PaseoLogFormat.Line(At, LogType.Exception, "NullReferenceException", ""));
        }

        [Test]
        public void OnlyTheNewestFilesAreKept()
        {
            // Names carry the start time, so their order is the order of the sessions.
            var old = PaseoLogFormat.FilesToDelete(new[]
            {
                "paseo-20261014-123000.log", "paseo-20261012-090000.log", "paseo-20261013-110000.log"
            }, keep: 2);
            CollectionAssert.AreEqual(new[] { "paseo-20261012-090000.log" }, old);
            Assert.IsEmpty(PaseoLogFormat.FilesToDelete(new[] { "paseo-20261014-123000.log" }, keep: 2));
        }

        [Test]
        public void EachAttemptSaysWhatItSearchedHowLongItTookAndWhatItFound()
        {
            var search = new PaseoSearch("Teologia", "Teología", new[] { 152259 }, PaseoSearchReason.Gps, 18.4);
            var attempt = new PaseoAttempt(12, search, 1.837f, 152259, 14, 0f);
            Assert.AreEqual("[AncoRA Paseo] INTENTO_PASEO n=12 busca=Teologia mapas=1 motivo=gps dist_gps=18 seg=1.84 resultado=152259 conf=14 rumbo=- rumbo_dif=- contraste=0 solver=default prior=0 realce_ms=- rmse=-",
                PaseoLogFormat.Attempt(attempt));
        }

        [Test]
        public void AnAttemptChosenByTheCompassSaysTheHeadingAndHowFarTheBuildingLiesFromIt()
        {
            var search = new PaseoSearch("X1", "X1", new[] { 152198, 152200 }, PaseoSearchReason.Compass, 26.02, 312.4, 0.37);
            var attempt = new PaseoAttempt(7, search, 2.1f, 0, 0, 0f);
            Assert.AreEqual("[AncoRA Paseo] INTENTO_PASEO n=7 busca=X1 mapas=2 motivo=brujula dist_gps=26 seg=2.10 resultado=ninguno conf=0 rumbo=312 rumbo_dif=0 contraste=0 solver=default prior=0 realce_ms=- rmse=-",
                PaseoLogFormat.Attempt(attempt));
        }

        [Test]
        public void TheHeadingOfAnAttemptWrapsAndNeverReadsMinusZero()
        {
            var search = new PaseoSearch("Teologia", "Teología", new[] { 152259 }, PaseoSearchReason.Gps, 10.0, 359.7, 47.9);
            StringAssert.Contains(" rumbo=0 rumbo_dif=48" + " contraste=0 solver=default prior=0 realce_ms=- rmse=-", PaseoLogFormat.Attempt(new PaseoAttempt(1, search, 1f, 0, 0, 0f)));
            search = new PaseoSearch("Teologia", "Teología", new[] { 152259 }, PaseoSearchReason.Gps, 10.0, 0.2, -0.3);
            StringAssert.Contains(" rumbo=0 rumbo_dif=0" + " contraste=0 solver=default prior=0 realce_ms=- rmse=-", PaseoLogFormat.Attempt(new PaseoAttempt(1, search, 1f, 0, 0, 0f)));
        }

        [Test]
        public void AnAttemptOverEveryMapKeepsTheHeadingButNoDifference()
        {
            var search = new PaseoSearch(null, null, new[] { 1, 2 }, PaseoSearchReason.All, double.NaN, 90.0);
            StringAssert.Contains(" conf=0 rumbo=90 rumbo_dif=-" + " contraste=0 solver=default prior=0 realce_ms=- rmse=-", PaseoLogFormat.Attempt(new PaseoAttempt(2, search, 9f, 0, 0, 0f)));
        }

        [Test]
        public void AnAttemptOverEveryMapWithoutAMatch()
        {
            var search = new PaseoSearch(null, null, new[] { 1, 2, 3, 4, 5, 6, 7 }, PaseoSearchReason.All, double.NaN);
            var attempt = new PaseoAttempt(3, search, 9.5f, 0, 0, 0f);
            Assert.AreEqual("[AncoRA Paseo] INTENTO_PASEO n=3 busca=todos mapas=7 motivo=todos dist_gps=- seg=9.50 resultado=ninguno conf=0 rumbo=- rumbo_dif=- contraste=0 solver=default prior=0 realce_ms=- rmse=-",
                PaseoLogFormat.Attempt(attempt));
        }

        [Test]
        public void AnAttemptSaysWhichTestsItRanAndWhatTheyCost()
        {
            var search = new PaseoSearch("Teologia", "Teología", new[] { 152259 }, PaseoSearchReason.Gps, 18.4);
            var attempt = new PaseoAttempt(12, search, 1.837f, 152259, 14, 0f, contrast: PaseoContrastUse.Enhanced, gravity: true,
                enhanceMilliseconds: 23.456f, rmse: 0.4567);
            Assert.AreEqual("[AncoRA Paseo] INTENTO_PASEO n=12 busca=Teologia mapas=1 motivo=gps dist_gps=18 seg=1.84 resultado=152259 conf=14 rumbo=- rumbo_dif=- contraste=1 solver=lean prior=0 realce_ms=23.5 rmse=0.457",
                PaseoLogFormat.Attempt(attempt));
        }

        [Test]
        public void AnAttemptWithAPriorPositionHasNoSolverChoice()
        {
            var search = new PaseoSearch("X1", "X1", new[] { 152198 }, PaseoSearchReason.InView, 20.0);
            // The team asked for the gravity solver too, but the search with a prior position does not use it.
            var attempt = new PaseoAttempt(5, search, 0.9f, 152198, 40, 0f, gravity: true, prior: true, rmse: 0.2);
            StringAssert.EndsWith(" contraste=0 solver=prior prior=1 realce_ms=- rmse=0.200", PaseoLogFormat.Attempt(attempt));
        }

        [Test]
        public void AnImageThatCouldNotBeEnhancedSaysSo()
        {
            var search = new PaseoSearch(null, null, new[] { 1, 2 }, PaseoSearchReason.All, double.NaN);
            var attempt = new PaseoAttempt(3, search, 4f, 0, 0, 0f, contrast: PaseoContrastUse.NotApplicable);
            StringAssert.EndsWith(" contraste=na solver=default prior=0 realce_ms=- rmse=-", PaseoLogFormat.Attempt(attempt));
        }

        [Test]
        public void ATestNumberThatIsNotFiniteReadsAsADash()
        {
            var search = new PaseoSearch(null, null, new[] { 1 }, PaseoSearchReason.All, double.NaN);
            var attempt = new PaseoAttempt(4, search, 1f, 1, 10, 0f, contrast: PaseoContrastUse.Enhanced,
                enhanceMilliseconds: float.PositiveInfinity, rmse: double.NaN);
            StringAssert.EndsWith(" contraste=1 solver=default prior=0 realce_ms=- rmse=-", PaseoLogFormat.Attempt(attempt));
        }

        [Test]
        public void TheTestFieldsComeAfterTheOnesOldToolingReads()
        {
            var search = new PaseoSearch("X1", "X1", new[] { 152198, 152200 }, PaseoSearchReason.Compass, 26.0, 312.0, 0.4);
            string line = PaseoLogFormat.Attempt(new PaseoAttempt(7, search, 2.1f, 0, 0, 0f, contrast: PaseoContrastUse.Enhanced));
            StringAssert.Contains("conf=0 rumbo=312 rumbo_dif=0 contraste=1 ", line);
            Assert.Less(line.IndexOf("rumbo_dif=", System.StringComparison.Ordinal), line.IndexOf("contraste=", System.StringComparison.Ordinal));
        }

        [Test]
        public void TheStatusLineCountsAttemptsAndFailuresAsNumbers()
        {
            // 1.2.2 wrote «intentos=8_intentos,_6_sin_coincidencia».
            Assert.AreEqual("comenzado=1 sdk=1 visibles=1 ritmo=0.5 intentos=8 fallos=6 gps=7",
                PaseoLogFormat.TourState(true, true, 1, 0.5f, 8, 6, 7.2f));
            Assert.AreEqual("comenzado=0 sdk=0 visibles=0 ritmo=2.0 intentos=0 fallos=0 gps=-",
                PaseoLogFormat.TourState(false, false, 0, 2f, 0, 0, float.NaN));
        }

        [Test]
        public void TheDebugLineOfAnAttemptSaysWhereThePhoneWasAndWhatItSaw()
        {
            var search = new PaseoSearch("Teologia", "Teología", new[] { 152259 }, PaseoSearchReason.Gps, 18.4);
            var attempt = new PaseoAttempt(12, search, 1.837f, 152259, 14, 0f, 1104, 621, new Vector3(-3.214f, 1.48f, 7.9f), 123.4f);
            var context = new PaseoDebugContext
            {
                Tracking = "SessionTracking", InView = "Teologia", Interval = 0.5f,
                GpsRunning = true, GpsUsable = true, Latitude = -29.9640541, Longitude = -71.3491443, GpsAccuracy = 6.2f, GpsAge = 1.24
            };
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_INTENTO n=12 img=1104x621 cam=-3.21,1.48,7.90 rumbo=123 tracking=SessionTracking " +
                            "vista=Teologia ritmo=0.5 gps=-29.964054,-71.349144 prec=6 edad=1.2 gps_util=1",
                PaseoLogFormat.AttemptDebug(attempt, context));
        }

        [Test]
        public void WithoutGpsNorABuildingInViewTheDebugLineSaysSo()
        {
            var search = new PaseoSearch(null, null, new int[0], PaseoSearchReason.All, double.NaN);
            var attempt = new PaseoAttempt(3, search, 9.5f, 0, 0, 0f, 1104, 621, Vector3.zero, 0f);
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_INTENTO n=3 img=1104x621 cam=0.00,0.00,0.00 rumbo=0 tracking=SessionInitializing " +
                            "vista=- ritmo=0.0 gps=- prec=- edad=- gps_util=0",
                PaseoLogFormat.AttemptDebug(attempt, new PaseoDebugContext { Tracking = "SessionInitializing" }));
        }

        [Test]
        public void ATrackingChangeSaysTheStateAndWhy()
        {
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_TRACKING estado=SessionInitializing motivo=InsufficientLight t=12.3",
                PaseoLogFormat.TrackingChange("SessionInitializing", "InsufficientLight", 12.34f));
        }

        [Test]
        public void TheCameraLineSaysItsConfigurationAndWhetherTheImageIsReduced()
        {
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_CAMARA config=1920x1080@30 reducida=1", PaseoLogFormat.CameraConfig(1920, 1080, 30, true));
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_CAMARA config=- reducida=0", PaseoLogFormat.CameraConfig(0, 0, 0, false));
        }

        [Test]
        public void TheViewLineIsAnInterfaceEventWithTheModeInLowerCase()
        {
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_UI vista=caja t=12.3", PaseoLogFormat.ViewMode(PaseoViewMode.Box, 12.34f));
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_UI vista=fondo t=0.0", PaseoLogFormat.ViewMode(PaseoViewMode.Background, 0f));
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_UI vista=objeto t=100.0", PaseoLogFormat.ViewMode(PaseoViewMode.Object, 100f));
        }

        [Test]
        public void TheOcclusionLineSaysIfThePhoneHasDepthAndWhatTheSubsystemRunsIn()
        {
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_OCLUSION soportada=1 modo=Fastest suavizado=1 fondo=BeforeOpaques t=12.3",
                PaseoLogFormat.Occlusion(true, "Fastest", true, "BeforeOpaques", 12.34f));
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_OCLUSION soportada=0 modo=Disabled suavizado=0 fondo=- t=0.0",
                PaseoLogFormat.Occlusion(false, "Disabled", false, null, 0f));
        }
    }
}
