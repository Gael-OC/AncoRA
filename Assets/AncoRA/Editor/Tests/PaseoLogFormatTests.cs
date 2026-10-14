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
    }
}
