using System;
using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoDebugTests
    {
        [Test]
        public void ThePoseLineSaysWhereTheCameraIsAndWhichBoxesItSees()
        {
            var sample = new PaseoPoseSample
            {
                Seconds = 12.34f, Camera = new Vector3(1.2f, 1.5f, -3.4f), Yaw = 270.4f, Pitch = -5.2f, Speed = 0.84f, TurnSpeed = 12.3f,
                Tracking = "SessionTracking",
                Boxes = new List<PaseoBoxSighting> { new("Teologia", 18.44f, -12.2f), new("X1", 30.06f, 40.4f) }
            };
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_POSE t=12.3 cam=1.20,1.50,-3.40 rumbo=270 incl=-5 vel=0.8 giro=12 " +
                            "tracking=SessionTracking norte=- norte_estado=- brujula=- brujula_prec=- campo_ut=- cajas=Teologia:18.4:-12,X1:30.1:40",
                PaseoLogFormat.Pose(sample));
            sample.Boxes = new List<PaseoBoxSighting>();
            StringAssert.EndsWith("cajas=-", PaseoLogFormat.Pose(sample));
        }

        [Test]
        public void ThePoseLineCarriesTheComputedCompassNextToWhatUnitySays()
        {
            // norte is the heading of the back camera; brujula is the one Unity reports, for the top edge of the device (pointing at the sky when filming upright).
            var sample = new PaseoPoseSample
            {
                Seconds = 12.34f, Camera = Vector3.zero, Yaw = 270.4f, Pitch = -5.2f, Tracking = "SessionTracking",
                Boxes = new List<PaseoBoxSighting> { new("X1", 30.06f, 40.4f) },
                Compass = new PaseoCompassSighting(PaseoHeadingState.Valid, 312.4f, 187.6f, 25f, 23.84f)
            };
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_POSE t=12.3 cam=0.00,0.00,0.00 rumbo=270 incl=-5 vel=0.0 giro=0 " +
                            "tracking=SessionTracking norte=312 norte_estado=valido brujula=188 brujula_prec=25 campo_ut=23.8 cajas=X1:30.1:40",
                PaseoLogFormat.Pose(sample));
        }

        [Test]
        public void WhenTheHeadingIsNotUsableThePoseLineStillShowsTheFieldAndTheReason()
        {
            var sample = new PaseoPoseSample
            {
                Seconds = 1f, Tracking = "SessionTracking", Boxes = new List<PaseoBoxSighting>(),
                Compass = new PaseoCompassSighting(PaseoHeadingState.FieldStrength, 0f, 0f, -1f, 112.5f)
            };
            StringAssert.Contains("norte=- norte_estado=campo-fuera-de-rango brujula=0 brujula_prec=-1 campo_ut=112.5 cajas=-", PaseoLogFormat.Pose(sample));
            sample.Compass = new PaseoCompassSighting(PaseoHeadingState.NoData, 0f, 0f, 0f, float.NaN);
            StringAssert.Contains("norte=- norte_estado=sin-datos brujula=0 brujula_prec=0 campo_ut=- cajas=-", PaseoLogFormat.Pose(sample));
        }

        [Test]
        public void ThePerformanceLineSaysFramesWorstFrameAndMemory()
        {
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_RENDIMIENTO fps=29.7 peor_ms=48 mem_mb=180 gc_mb=35",
                PaseoLogFormat.Performance(29.66f, 47.9f, 180.2f, 35.4f));
        }

        [Test]
        public void TheBoxLineComparesThisLocalizationWithTheFilteredBox()
        {
            Assert.AreEqual("[AncoRA Paseo] DEPURACION_CAJA mapa=152259 estimada=1.00,2.00,3.00 filtrada=1.10,2.00,3.05 giro=66.7 confirmada=1",
                PaseoLogFormat.BoxAfterLocalization(152259, new Vector3(1f, 2f, 3f), new Vector3(1.1f, 2f, 3.05f), 66.66f, true));
        }

        [Test]
        public void ABoxAheadIsAtBearingZeroAndOneToTheRightIsPositive()
        {
            var camera = Vector3.zero;
            Assert.AreEqual(0f, PaseoDebugGeometry.Bearing(camera, Vector3.forward, new Vector3(0f, 5f, 10f)), 1e-3f);
            Assert.AreEqual(45f, PaseoDebugGeometry.Bearing(camera, Vector3.forward, new Vector3(10f, 0f, 10f)), 1e-3f);
            Assert.AreEqual(-90f, PaseoDebugGeometry.Bearing(camera, Vector3.forward, new Vector3(-3f, 0f, 0f)), 1e-3f);
            // Looking down does not change the bearing: only the horizontal direction counts.
            Assert.AreEqual(45f, PaseoDebugGeometry.Bearing(camera, new Vector3(0f, -1f, 1f), new Vector3(10f, 0f, 10f)), 1e-3f);
        }

        [Test]
        public void PhotosAreTakenEveryFewSecondsUpToALimit()
        {
            Assert.IsTrue(PaseoDebugImages.ShouldCapture(10f, float.NegativeInfinity, 0));
            Assert.IsFalse(PaseoDebugImages.ShouldCapture(10f, 10f - PaseoDebugImages.EverySeconds + 0.5f, 3));
            Assert.IsTrue(PaseoDebugImages.ShouldCapture(10f, 10f - PaseoDebugImages.EverySeconds, 3));
            Assert.IsFalse(PaseoDebugImages.ShouldCapture(100f, 0f, PaseoDebugImages.MaxPerSession));
        }

        [Test]
        public void APhotoIsNamedAfterItsAttemptAndWhatItFound()
        {
            Assert.AreEqual("0012-152259.jpg", PaseoDebugImages.FileName(12, 152259));
            Assert.AreEqual("0013-ninguno.jpg", PaseoDebugImages.FileName(13, 0));
        }

        [Test]
        public void AGrayImageBecomesRgbRotatedUprightWithRowsFromTheBottom()
        {
            // 2 × 1 grey image [a b]: turned a quarter clockwise it stands as a column, a on top. Unity's encoders read rows
            // from the bottom, so the bottom pixel (b) comes first.
            var rgb = PaseoDebugImages.ToRgb(new byte[] { 10, 20 }, 2, 1, 1, 480, rotateClockwise: true, out int w, out int h);
            Assert.AreEqual(1, w);
            Assert.AreEqual(2, h);
            CollectionAssert.AreEqual(new byte[] { 20, 20, 20, 10, 10, 10 }, rgb);
        }

        [Test]
        public void ALargeImageIsShrunkToTheMaximumWidth()
        {
            var gray = new byte[1920 * 1080];
            PaseoDebugImages.ToRgb(gray, 1920, 1080, 1, 480, rotateClockwise: false, out int w, out int h);
            Assert.AreEqual(480, w);
            Assert.AreEqual(270, h);
        }

        [Test]
        public void AColourImageKeepsItsFirstChannel()
        {
            var rgb = PaseoDebugImages.ToRgb(new byte[] { 7, 8, 9, 255 }, 1, 1, 4, 480, rotateClockwise: false, out _, out _);
            CollectionAssert.AreEqual(new byte[] { 7, 7, 7 }, rgb);
        }

        [Test]
        public void TheSharedZipSaysWhichPhoneAndWhen()
        {
            Assert.AreEqual("AncoRA-registro-23127PN0CC-20261021-170512.zip",
                PaseoShare.ZipName("23127PN0CC", new DateTime(2026, 10, 21, 17, 5, 12)));
            Assert.AreEqual("AncoRA-registro-samsung-SM-S931B-20261021-170512.zip",
                PaseoShare.ZipName("samsung SM-S931B", new DateTime(2026, 10, 21, 17, 5, 12)));
            Assert.AreEqual("AncoRA-registro-telefono-20261021-170512.zip", PaseoShare.ZipName(" ¿? ", new DateTime(2026, 10, 21, 17, 5, 12)));
        }

        [Test]
        public void TheNewestSessionsAndTheirPhotosAreShared()
        {
            var logs = new[] { "paseo-20261021-090000.log", "paseo-20261021-170512.log", "paseo-20261020-120000.log", "paseo-20261021-120000.log" };
            CollectionAssert.AreEqual(new[] { "paseo-20261021-120000.log", "paseo-20261021-170512.log" }, PaseoShare.NewestSessions(logs, 2));
            Assert.AreEqual("20261021-170512", PaseoShare.SessionStamp("paseo-20261021-170512.log"));
        }
    }
}
