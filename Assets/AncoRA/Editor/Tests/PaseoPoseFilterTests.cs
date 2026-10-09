using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoPoseFilterTests
    {
        static Quaternion Yaw(float degrees) => Quaternion.Euler(0f, degrees, 0f);
        static Vector3 X(float x) => new(x, 0f, 0f);

        static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        static PaseoPoseFilter LockedAtOrigin()
        {
            var f = new PaseoPoseFilter();
            f.Add(Vector3.zero, Yaw(0f));
            Assert.AreEqual(PaseoPoseVerdict.Locked, f.Add(Vector3.zero, Yaw(0f)));
            return f;
        }

        [Test]
        public void OnePoseIsNotEnoughToShowTheBox()
        {
            var f = new PaseoPoseFilter();
            Assert.AreEqual(PaseoPoseVerdict.Waiting, f.Add(X(1f), Yaw(0f)));
            Assert.IsFalse(f.HasPose);
        }

        [Test]
        public void TwoAgreeingPosesLockAtTheirAverageWithoutSliding()
        {
            var f = new PaseoPoseFilter();
            f.Add(X(0f), Yaw(0f));
            Assert.AreEqual(PaseoPoseVerdict.Locked, f.Add(X(0.2f), Yaw(0f)));
            Assert.IsTrue(f.HasPose);
            AssertNear(X(0.1f), f.Position);
        }

        [Test]
        public void AWrongFirstPoseIsIgnoredOnceTwoOthersAgree()
        {
            var f = new PaseoPoseFilter();
            Assert.AreEqual(PaseoPoseVerdict.Waiting, f.Add(X(5f), Yaw(0f)));
            Assert.AreEqual(PaseoPoseVerdict.Waiting, f.Add(X(0f), Yaw(0f)));
            Assert.AreEqual(PaseoPoseVerdict.Locked, f.Add(X(0.1f), Yaw(0f)));
            AssertNear(X(0.05f), f.Position);
        }

        [Test]
        public void PosesThatDisagreeInRotationDoNotLock()
        {
            var f = new PaseoPoseFilter();
            f.Add(Vector3.zero, Yaw(0f));
            Assert.AreEqual(PaseoPoseVerdict.Waiting, f.Add(Vector3.zero, Yaw(10f)));
            Assert.IsFalse(f.HasPose);
        }

        [Test]
        public void LockedRotationIsTheAverage()
        {
            var f = new PaseoPoseFilter();
            f.Add(Vector3.zero, Yaw(0f));
            f.Add(Vector3.zero, Yaw(2f));
            Assert.AreEqual(1f, f.Rotation.eulerAngles.y, 1e-3f);
        }

        [Test]
        public void AnInlierMovesTheTargetToTheWindowAverageAndTheBoxSlidesThere()
        {
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Accepted, f.Add(X(0.6f), Yaw(0f)));
            AssertNear(X(0.2f), f.TargetPosition);
            AssertNear(X(0f), f.Position);

            f.Step(PaseoPoseFilter.SmoothSeconds / 2f);
            Assert.Greater(f.Position.x, 0f);
            Assert.Less(f.Position.x, 0.2f);

            f.Step(PaseoPoseFilter.SmoothSeconds / 2f);
            AssertNear(X(0.2f), f.Position);
        }

        [Test]
        public void AFarPoseIsRejectedAndNothingMoves()
        {
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, f.Add(X(2f), Yaw(0f)));
            f.Step(1f);
            AssertNear(X(0f), f.TargetPosition);
            AssertNear(X(0f), f.Position);
        }

        [Test]
        public void ATurnedPoseIsRejected()
        {
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, f.Add(Vector3.zero, Yaw(PaseoPoseFilter.InlierDegrees + 1f)));
        }

        [Test]
        public void ThreeAgreeingRejectionsRelockThereWithoutSliding()
        {
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, f.Add(X(3f), Yaw(0f)));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, f.Add(X(3.1f), Yaw(0f)));
            Assert.AreEqual(PaseoPoseVerdict.Relocked, f.Add(X(3.2f), Yaw(0f)));
            AssertNear(X(3.1f), f.TargetPosition);
            AssertNear(X(3.1f), f.Position);
        }

        [Test]
        public void ScatteredRejectionsDoNotRelock()
        {
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, f.Add(X(3f), Yaw(0f)));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, f.Add(X(-3f), Yaw(0f)));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, f.Add(new Vector3(0f, 0f, 3f), Yaw(0f)));
            AssertNear(X(0f), f.Position);
        }

        [Test]
        public void AnInlierBreaksARunOfRejections()
        {
            var f = LockedAtOrigin();
            f.Add(X(3f), Yaw(0f));
            f.Add(X(3.1f), Yaw(0f));
            Assert.AreEqual(PaseoPoseVerdict.Accepted, f.Add(X(0.1f), Yaw(0f)));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, f.Add(X(3.2f), Yaw(0f)));
        }

        [Test]
        public void TheAverageKeepsOnlyTheLastPoses()
        {
            var f = LockedAtOrigin();
            for (int i = 0; i < PaseoPoseFilter.Window; i++)
                f.Add(X(0.5f), Yaw(0f));
            AssertNear(X(0.5f), f.TargetPosition);
        }

        [Test]
        public void ResetForgetsThePose()
        {
            var f = LockedAtOrigin();
            f.Reset();
            Assert.IsFalse(f.HasPose);
            Assert.AreEqual(PaseoPoseVerdict.Waiting, f.Add(X(4f), Yaw(0f)));
        }
    }
}
