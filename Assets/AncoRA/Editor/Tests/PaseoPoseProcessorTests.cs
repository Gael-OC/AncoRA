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

        static SceneUpdateData Localization(float x) =>
            new() { Pose = Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.identity, Vector3.one) };

        SceneUpdateData NewData(float x) => processor.ProcessData(Localization(x), DataProcessorTrigger.NewData).Result;

        [Test]
        public void TheFirstLocalizationDoesNotMoveTheSpace()
        {
            Assert.IsTrue(NewData(1f).Ignore);
            Assert.IsFalse(processor.HasPose);
        }

        [Test]
        public void TwoAgreeingLocalizationsMoveTheSpaceToTheirAverage()
        {
            NewData(1f);
            var output = NewData(1.2f);
            Assert.IsFalse(output.Ignore);
            Assert.IsTrue(processor.HasPose);
            Assert.AreEqual(1.1f, output.Pose.GetPosition().x, 1e-4f);
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
        public void ResetMakesTheSpaceWaitForANewAgreement()
        {
            NewData(1f);
            NewData(1f);
            processor.ResetProcessor().Wait();
            Assert.IsFalse(processor.HasPose);
            Assert.IsTrue(processor.ProcessData(Localization(1f), DataProcessorTrigger.Update).Result.Ignore);
        }
    }
}
