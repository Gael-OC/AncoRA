using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBoxNudgeTests
    {
        static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        [Test]
        public void RightIsTheViewersRightWhichIsTheBoxsMinusX()
        {
            AssertNear(new Vector3(-1f, 0f, 0f), PaseoBoxNudge.Move(Vector3.zero, 0f, right: 1f, closer: 0f, up: 0f));
        }

        [Test]
        public void CloserMovesTowardsTheViewerAlongTheFront()
        {
            AssertNear(new Vector3(0f, 0f, 1f), PaseoBoxNudge.Move(Vector3.zero, 0f, right: 0f, closer: 1f, up: 0f));
        }

        [Test]
        public void UpIsUpWhateverTheTurn()
        {
            AssertNear(new Vector3(2f, 1.5f, 3f), PaseoBoxNudge.Move(new Vector3(2f, 1f, 3f), 37f, right: 0f, closer: 0f, up: 0.5f));
        }

        [Test]
        public void TheMoveTurnsWithTheBox()
        {
            // Turned 90°, the front looks along map +X, so the viewer's right is map +Z.
            AssertNear(new Vector3(0f, 0f, 1f), PaseoBoxNudge.Move(Vector3.zero, 90f, right: 1f, closer: 0f, up: 0f));
            AssertNear(new Vector3(1f, 0f, 0f), PaseoBoxNudge.Move(Vector3.zero, 90f, right: 0f, closer: 1f, up: 0f));
        }
    }
}
