using System.Text.RegularExpressions;
using AncorRA.AR;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AncorRA.Tests
{
    public class PaseoPoseProcessorTests
    {
        GameObject host;
        PaseoPoseProcessor processor;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("XR Space de prueba");
            processor = host.AddComponent<PaseoPoseProcessor>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        static SceneUpdateData Localization(Vector3 position, Quaternion rotation) =>
            new() { Pose = Matrix4x4.TRS(position, rotation, Vector3.one) };

        static SceneUpdateData Localization(float x) => Localization(new Vector3(x, 0f, 0f), Quaternion.identity);

        SceneUpdateData NewData(SceneUpdateData data) => processor.ProcessData(data, DataProcessorTrigger.NewData).Result;

        SceneUpdateData NewData(float x) => NewData(Localization(x));

        // A small box 30 m in front of the space origin, as the child the tour puts in each XR Space.
        void AddFarBox()
        {
            var boxObject = new GameObject("Caja");
            boxObject.transform.SetParent(host.transform, false);
            var frame = boxObject.AddComponent<EdificioFacadeFrame>();
            frame.Configure(4f, 3f, 4f, null, null, null, false);
            var content = boxObject.AddComponent<PaseoMapContent>();
            content.Configure(new[] { 1 }, 1, "Prueba", "Prueba", "", frame);
            content.SetPose(new Vector3(0f, 0f, 30f), 0f, placed: true);
        }

        [Test]
        public void TheFirstLocalizationMovesTheSpaceAtOnce()
        {
            var output = NewData(1f);
            Assert.IsFalse(output.Ignore);
            Assert.IsTrue(processor.HasPose);
            Assert.IsFalse(processor.Confirmed);
            Assert.AreEqual(1f, output.Pose.GetPosition().x, 1e-4f);
        }

        [Test]
        public void ASecondAgreeingLocalizationConfirmsWithoutAJump()
        {
            NewData(1f);
            var output = NewData(1.2f);
            Assert.IsTrue(processor.Confirmed);
            Assert.AreEqual(1f, output.Pose.GetPosition().x, 1e-4f, "it slides to the average on the next frames");
        }

        [Test]
        public void ItRemembersWhenItJudgedTheLastLocalization()
        {
            float before = Time.realtimeSinceStartup;
            NewData(1f);
            Assert.GreaterOrEqual(processor.LastReportAt, before);
        }

        [Test]
        public void ARejectedLocalizationKeepsTheFilteredPose()
        {
            NewData(1f);
            NewData(1f);
            var output = NewData(5f);
            Assert.IsFalse(output.Ignore);
            Assert.AreEqual(1f, output.Pose.GetPosition().x, 1e-4f);
        }

        [Test]
        public void ItCountsTheLocalizationsItSawAndTheOnesItUsed()
        {
            NewData(1f);
            Assert.AreEqual((1, 1), (processor.Results, processor.UsedResults), "shown at the first one");
            NewData(1f);
            Assert.AreEqual((2, 2), (processor.Results, processor.UsedResults), "confirmed");
            NewData(5f);
            Assert.AreEqual((3, 2), (processor.Results, processor.UsedResults), "rejected");
            Assert.AreEqual(PaseoPoseVerdict.Rejected, processor.LastReport?.Verdict);
        }

        static SceneUpdateData FromMap(int mapId, float x) => new()
        {
            Pose = Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.identity, Vector3.one),
            MapEntry = new MapEntry { Map = MapOf(mapId) }
        };

        static XRMap MapOf(int mapId)
        {
            var go = new GameObject($"XR Map {mapId}");
            var map = go.AddComponent<XRMap>();
            map.SetIdAndName(mapId, "prueba");
            return map;
        }

        [Test]
        public void LocalizationsFromAnyMapOfTheBuildingFeedTheSameFilter()
        {
            var first = FromMap(11, 1f);
            var second = FromMap(22, 1.1f);
            try
            {
                NewData(first);
                NewData(second);
                Assert.IsTrue(processor.Confirmed, "two maps that agree confirm the one box");
                Assert.AreEqual((2, 2), (processor.Results, processor.UsedResults));
                Assert.AreEqual(22, processor.LastMapId);
                Assert.IsTrue(processor.TryGetMapStats(11, out var a));
                Assert.IsTrue(processor.TryGetMapStats(22, out var b));
                Assert.AreEqual((1, 1), (a.Seen, a.Used));
                Assert.AreEqual((1, 1), (b.Seen, b.Used));
                Assert.IsFalse(processor.TryGetMapStats(33, out _));
            }
            finally
            {
                Object.DestroyImmediate(first.MapEntry.Map.gameObject);
                Object.DestroyImmediate(second.MapEntry.Map.gameObject);
            }
        }

        [Test]
        public void EachMapKeepsItsOwnLastVerdict()
        {
            var good = FromMap(11, 1f);
            var far = FromMap(22, 9f);
            try
            {
                NewData(good);
                NewData(good);
                NewData(far);
                processor.TryGetMapStats(11, out var a);
                processor.TryGetMapStats(22, out var b);
                Assert.AreEqual((2, 2), (a.Seen, a.Used));
                Assert.AreEqual((1, 0), (b.Seen, b.Used));
                Assert.AreEqual(PaseoPoseVerdict.Rejected, b.Last?.Verdict);
                Assert.AreNotEqual(PaseoPoseVerdict.Rejected, a.Last?.Verdict);
            }
            finally
            {
                Object.DestroyImmediate(good.MapEntry.Map.gameObject);
                Object.DestroyImmediate(far.MapEntry.Map.gameObject);
            }
        }

        [Test]
        public void ItJudgesLocalizationsAtItsOwnBox()
        {
            // Turning 4 degrees around the far box leaves the box in place but moves the space origin 2.1 m: measured at
            // the box, the two localizations agree.
            AddFarBox();
            var rotation = Quaternion.Euler(0f, 4f, 0f);
            var centre = new Vector3(0f, 0f, 30f);
            NewData(Localization(Vector3.zero, Quaternion.identity));
            NewData(Localization(centre - rotation * centre, rotation));
            Assert.IsTrue(processor.Confirmed);
        }

        [Test]
        public void ItReportsTheGateOnlyOnceTheBoxIsLocked()
        {
            NewData(1f);
            Assert.IsTrue(float.IsNaN(processor.LastReport.Value.GateMeters));
            Assert.IsTrue(float.IsNaN(processor.LastReport.Value.TravelMeters));
            NewData(1f);
            Assert.AreEqual(PaseoPoseFilter.InlierMeters, processor.LastReport.Value.GateMeters, 1e-4f);
            Assert.AreEqual(0f, processor.LastReport.Value.TravelMeters, 1e-4f);
        }

        [Test]
        public void TheLogLineEndsWithTheGateAndTheTravel()
        {
            var locked = new PaseoPoseReport(PaseoPoseVerdict.Locked, 12f, 0.5f, float.NaN, 18.4f);
            StringAssert.EndsWith(" confirmada=0 umbral=- recorrido=-", PaseoPoseProcessor.LogLine(152198, locked));
            var accepted = new PaseoPoseReport(PaseoPoseVerdict.Accepted, 34f, 0.62f, 0.23f, 18.4f, true, 1.9f, 30f);
            string line = PaseoPoseProcessor.LogLine(152198, accepted);
            StringAssert.Contains("UBICACION_PASEO mapa=152198 veredicto=aceptada conf=34 incl=0.62 mueve=0.23 dist=18.4 confirmada=1", line);
            StringAssert.EndsWith(" umbral=1.90 recorrido=30.0", line);
        }

        [Test]
        public void EveryLocalizationLogsItsGateAndTravel()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"UBICACION_PASEO mapa=0 veredicto=fijada .* umbral=- recorrido=-$"));
            NewData(1f);
            LogAssert.Expect(LogType.Log, new Regex(@"UBICACION_PASEO mapa=0 veredicto=aceptada .* umbral=1\.00 recorrido=0\.0$"));
            NewData(1f);
        }

        [Test]
        public void ResetHidesTheSpaceUntilTheNextLocalization()
        {
            NewData(1f);
            NewData(1f);
            processor.ResetProcessor().Wait();
            Assert.IsFalse(processor.HasPose);
            Assert.IsTrue(processor.ProcessData(Localization(1f), DataProcessorTrigger.Update).Result.Ignore);
        }
    }
}
