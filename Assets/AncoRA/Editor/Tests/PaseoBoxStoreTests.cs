using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBoxStoreTests
    {
        const string Prefix = "AncoRA.Test.Paseo.";
        static readonly string BakedPose = PaseoBoxStore.Fingerprint(new Vector3(0f, 0f, 12f), 0f, false);
        static readonly string BakedSize = PaseoBoxStore.Fingerprint(new Vector3(20f, 8f, 12f), false);
        PaseoBoxStore store;

        [SetUp]
        public void SetUp() => store = new PaseoBoxStore(Prefix);

        [TearDown]
        public void TearDown() => store.Clear(new[] { "A", "B" });

        [Test]
        public void TheDefaultPrefixIsVersionTwo()
        {
            // v1 held one pose per map, in the frame of that map; v2 holds one per building, in the building frame.
            StringAssert.EndsWith(".v2.", PaseoBoxStore.DefaultPrefix);
        }

        [Test]
        public void PoseRoundTrip()
        {
            Assert.IsFalse(store.TryLoadPose("A", BakedPose, out _, out _));
            store.SavePose("A", new Vector3(1f, 2f, 3f), 45f, BakedPose);
            Assert.IsTrue(store.TryLoadPose("A", BakedPose, out var position, out float yaw));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), position);
            Assert.AreEqual(45f, yaw);
            Assert.IsFalse(store.TryLoadPose("B", BakedPose, out _, out _), "poses are per building");
        }

        [Test]
        public void SizeRoundTrip()
        {
            Assert.IsFalse(store.TryLoadSize("A", BakedSize, out _, out _));
            store.SaveSize("A", new Vector3(30f, 10f, 15f), true, BakedSize);
            Assert.IsTrue(store.TryLoadSize("A", BakedSize, out var size, out bool solid));
            Assert.AreEqual(new Vector3(30f, 10f, 15f), size);
            Assert.IsTrue(solid);
        }

        [Test]
        public void PoseAndSizeOfABuildingDoNotClash()
        {
            store.SavePose("A", new Vector3(1f, 2f, 3f), 45f, BakedPose);
            Assert.IsFalse(store.HasSize("A"));
            store.SaveSize("A", new Vector3(30f, 10f, 15f), true, BakedSize);
            Assert.IsTrue(store.TryLoadPose("A", BakedPose, out var position, out _));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), position);
        }

        [Test]
        public void PoseMadeOnAnOlderSceneIsDiscarded()
        {
            store.SavePose("A", new Vector3(1f, 2f, 3f), 45f, BakedPose);
            string rebaked = PaseoBoxStore.Fingerprint(new Vector3(5f, 0f, 20f), 90f, true);
            Assert.IsTrue(store.HasPose("A"));
            Assert.IsFalse(store.TryLoadPose("A", rebaked, out _, out _), "the scene was baked again after this edit");
            Assert.IsFalse(store.HasPose("A"), "the stale phone pose is forgotten");
        }

        [Test]
        public void SizeMadeOnAnOlderSceneIsDiscarded()
        {
            store.SaveSize("A", new Vector3(30f, 10f, 15f), true, BakedSize);
            string measured = PaseoBoxStore.Fingerprint(new Vector3(21.3f, 7.4f, 9.6f), false);
            Assert.IsFalse(store.TryLoadSize("A", measured, out _, out _));
            Assert.IsFalse(store.HasSize("A"));
        }

        [Test]
        public void ClearForgetsEverything()
        {
            store.SavePose("A", Vector3.one, 1f, BakedPose);
            store.SaveSize("A", Vector3.one, false, BakedSize);
            store.Clear(new[] { "A" });
            Assert.IsFalse(store.HasPose("A"));
            Assert.IsFalse(store.HasSize("A"));
        }
    }
}
