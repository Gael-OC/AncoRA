using AncorRA.AR;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;

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
            content.Configure(1, "Prueba", "Prueba", "", frame);
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
