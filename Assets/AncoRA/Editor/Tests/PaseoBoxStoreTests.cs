using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBoxStoreTests
    {
        const string Prefix = "AncoRA.Test.Paseo.";
        PaseoBoxStore store;

        [SetUp]
        public void SetUp() => store = new PaseoBoxStore(Prefix);

        [TearDown]
        public void TearDown() => store.Clear(new[] { 1, 2 }, new[] { "A" });

        [Test]
        public void PoseRoundTrip()
        {
            Assert.IsFalse(store.TryLoadPose(1, out _, out _));
            store.SavePose(1, new Vector3(1f, 2f, 3f), 45f);
            Assert.IsTrue(store.TryLoadPose(1, out var position, out float yaw));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), position);
            Assert.AreEqual(45f, yaw);
            Assert.IsFalse(store.TryLoadPose(2, out _, out _), "poses are per map");
        }

        [Test]
        public void SizeRoundTrip()
        {
            Assert.IsFalse(store.TryLoadSize("A", out _, out _));
            store.SaveSize("A", new Vector3(30f, 10f, 15f), true);
            Assert.IsTrue(store.TryLoadSize("A", out var size, out bool solid));
            Assert.AreEqual(new Vector3(30f, 10f, 15f), size);
            Assert.IsTrue(solid);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            store.SavePose(1, Vector3.one, 1f);
            store.SaveSize("A", Vector3.one, false);
            store.Clear(new[] { 1 }, new[] { "A" });
            Assert.IsFalse(store.TryLoadPose(1, out _, out _));
            Assert.IsFalse(store.TryLoadSize("A", out _, out _));
        }
    }
}
