using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoPoseFilterTests
    {
        // A 20 x 5 x 10 m box whose centre sits 10 m in front of the map origin: its far corners are 18 m from the origin.
        static readonly PaseoBoxGeometry NearBox = new(new Vector3(0f, 0f, 10f), 0f, new Vector3(20f, 5f, 10f));

        // A small 4 x 3 x 4 m box 30 m from the map origin, like Teología or EIC seen from their maps.
        static readonly PaseoBoxGeometry FarBox = new(new Vector3(0f, 0f, 30f), 0f, new Vector3(4f, 3f, 4f));

        static Quaternion Yaw(float degrees) => Quaternion.Euler(0f, degrees, 0f);
        static Vector3 X(float x) => new(x, 0f, 0f);

        static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-4f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, tolerance, "y");
            Assert.AreEqual(expected.z, actual.z, tolerance, "z");
        }

        static PaseoPoseFilter NewFilter(PaseoBoxGeometry box) => new() { Box = box };

        // The camera stands at the space origin: with a level rotation it does not matter where it is.
        static PaseoPoseVerdict Add(PaseoPoseFilter f, Vector3 position, float yaw, float confidence = PaseoPoseFilter.FullConfidence) =>
            f.Add(position, Yaw(yaw), position, confidence).Verdict;

        // Space pose that turns the map by `degrees` around the centre of the box: the box itself stays in place.
        static void TurnAroundBox(PaseoBoxGeometry box, float degrees, out Vector3 position, out Quaternion rotation)
        {
            rotation = Yaw(degrees);
            position = box.Center - rotation * box.Center;
        }

        static PaseoPoseFilter LockedAtOrigin()
        {
            var f = NewFilter(NearBox);
            Add(f, Vector3.zero, 0f);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, Vector3.zero, 0f));
            return f;
        }

        [Test]
        public void OnePoseIsNotEnoughToShowTheBox()
        {
            var f = NewFilter(NearBox);
            Assert.AreEqual(PaseoPoseVerdict.Waiting, Add(f, X(1f), 0f));
            Assert.IsFalse(f.HasPose);
        }

        [Test]
        public void TwoAgreeingPosesLockAtTheirAverageWithoutSliding()
        {
            var f = NewFilter(NearBox);
            Add(f, X(0f), 0f);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(0.2f), 0f));
            Assert.IsTrue(f.HasPose);
            AssertNear(X(0.1f), f.Position);
        }

        [Test]
        public void AWrongFirstPoseIsIgnoredOnceTwoOthersAgree()
        {
            var f = NewFilter(NearBox);
            Assert.AreEqual(PaseoPoseVerdict.Waiting, Add(f, X(5f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Waiting, Add(f, X(0f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(0.1f), 0f));
            AssertNear(X(0.05f), f.Position);
        }

        [Test]
        public void ATurnThatMovesTheFarCornersTooMuchDoesNotLock()
        {
            // 3 degrees around the map origin move the corners 18 m away by 0.94 m, more than the 0.6 m agreement.
            var f = NewFilter(NearBox);
            Add(f, Vector3.zero, 0f);
            Assert.AreEqual(PaseoPoseVerdict.Waiting, Add(f, Vector3.zero, 3f));
            Assert.IsFalse(f.HasPose);
        }

        [Test]
        public void LockedRotationIsTheAverage()
        {
            var f = NewFilter(NearBox);
            Add(f, Vector3.zero, 0f);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, Vector3.zero, 1f));
            Assert.AreEqual(0.5f, f.Rotation.eulerAngles.y, 1e-3f);
        }

        [Test]
        public void AveragingTurnsAroundTheBoxSoTheBoxStaysInPlace()
        {
            // Both estimates hold the far box at the same place, turned 4 degrees apart: their average must too.
            var f = NewFilter(FarBox);
            TurnAroundBox(FarBox, 4f, out var turnedPosition, out var turnedRotation);
            f.Add(Vector3.zero, Quaternion.identity, Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Locked, f.Add(turnedPosition, turnedRotation, Vector3.zero, PaseoPoseFilter.FullConfidence).Verdict);
            AssertNear(new Vector3(0f, 0f, 30f), f.Position + f.Rotation * FarBox.Center, 1e-3f);
            Assert.AreEqual(2f, f.Rotation.eulerAngles.y, 1e-3f);
        }

        [Test]
        public void AnInlierIsJudgedWhereTheBoxIsNotAtTheMapOrigin()
        {
            // Turning 4 degrees around the far box moves the map origin 2.1 m but the box corners only 0.2 m.
            var f = NewFilter(FarBox);
            Add(f, Vector3.zero, 0f);
            Add(f, Vector3.zero, 0f);
            TurnAroundBox(FarBox, 4f, out var position, out var rotation);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, f.Add(position, rotation, Vector3.zero, PaseoPoseFilter.FullConfidence).Verdict);
        }

        [Test]
        public void AnInlierMovesTheTargetToTheWindowAverageAndTheBoxSlidesThere()
        {
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Accepted, Add(f, X(0.6f), 0f));
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
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(2f), 0f));
            f.Step(1f);
            AssertNear(X(0f), f.TargetPosition);
            AssertNear(X(0f), f.Position);
        }

        [Test]
        public void ATurnThatWouldMoveTheBoxCornersTooFarIsRejected()
        {
            // 4 degrees around the map origin move the far corners 1.26 m, more than the 1 m inlier gate.
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, Vector3.zero, 4f));
        }

        [Test]
        public void ThreeAgreeingRejectionsRelockThereWithoutSliding()
        {
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(3f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(3.1f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Relocked, Add(f, X(3.2f), 0f));
            AssertNear(X(3.1f), f.TargetPosition);
            AssertNear(X(3.1f), f.Position);
        }

        [Test]
        public void ScatteredRejectionsDoNotRelock()
        {
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(3f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(-3f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, new Vector3(0f, 0f, 3f), 0f));
            AssertNear(X(0f), f.Position);
        }

        [Test]
        public void AnInlierBreaksARunOfRejections()
        {
            var f = LockedAtOrigin();
            Add(f, X(3f), 0f);
            Add(f, X(3.1f), 0f);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, Add(f, X(0.1f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(3.2f), 0f));
        }

        [Test]
        public void TheAverageKeepsOnlyTheLastPoses()
        {
            var f = LockedAtOrigin();
            for (int i = 0; i < PaseoPoseFilter.Window; i++)
                Add(f, X(0.5f), 0f);
            AssertNear(X(0.5f), f.TargetPosition);
        }

        [Test]
        public void ConfidenceWeighsTheAverage()
        {
            // Weights 1 (confidence 50) and 0.2 (confidence 10): 0.3 * 0.2 / 1.2 = 0.05.
            var f = NewFilter(NearBox);
            Add(f, X(0f), 0f, confidence: 50f);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(0.3f), 0f, confidence: 10f));
            AssertNear(X(0.05f), f.Position);
        }

        [Test]
        public void ASmallTiltIsLevelledAroundTheCamera()
        {
            // The map and the AR world both stand on gravity: a 2 degree pitch is noise. The level pose keeps the
            // camera where both frames put it and turns only around the vertical.
            var f = NewFilter(NearBox);
            var tilted = Quaternion.Euler(2f, 0f, 0f);
            var camera = new Vector3(0f, 1.5f, 20f);
            var cameraInMap = Quaternion.Inverse(tilted) * camera;
            f.Add(Vector3.zero, tilted, camera, PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Locked, f.Add(Vector3.zero, tilted, camera, PaseoPoseFilter.FullConfidence).Verdict);
            Assert.AreEqual(0f, Vector3.Angle(f.Rotation * Vector3.up, Vector3.up), 1e-3f, "tilt left");
            AssertNear(camera, f.Position + f.Rotation * cameraInMap, 1e-3f);
        }

        [Test]
        public void ATiltedLocalizationIsIgnored()
        {
            var f = LockedAtOrigin();
            var report = f.Add(X(0.5f), Quaternion.Euler(PaseoPoseFilter.MaxTiltDegrees + 1f, 0f, 0f), Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Tilted, report.Verdict);
            AssertNear(X(0f), f.TargetPosition);
        }

        [Test]
        public void ATiltedLocalizationCannotHelpToLock()
        {
            var f = NewFilter(NearBox);
            f.Add(Vector3.zero, Quaternion.Euler(PaseoPoseFilter.MaxTiltDegrees + 1f, 0f, 0f), Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Waiting, Add(f, Vector3.zero, 0f));
        }

        [Test]
        public void TheReportSaysHowFarTheBoxWouldMoveAndHowFarTheCameraIs()
        {
            var f = LockedAtOrigin();
            var report = f.Add(X(0.5f), Quaternion.Euler(2f, 0f, 0f), new Vector3(0.5f, 0f, 0f), 34f);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, report.Verdict);
            Assert.IsTrue(report.Used);
            Assert.AreEqual(34f, report.Confidence, 1e-4f);
            Assert.AreEqual(2f, report.TiltDegrees, 1e-3f);
            Assert.AreEqual(0.5f, report.BoxMoveMeters, 1e-3f);
            Assert.AreEqual(10f, report.CameraToBoxMeters, 1e-3f);
        }

        [Test]
        public void BeforeTheFirstLockThereIsNothingToMove()
        {
            var report = NewFilter(NearBox).Add(Vector3.zero, Quaternion.identity, Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.IsFalse(report.Used);
            Assert.IsTrue(float.IsNaN(report.BoxMoveMeters));
        }

        [Test]
        public void ResetForgetsThePose()
        {
            var f = LockedAtOrigin();
            f.Reset();
            Assert.IsFalse(f.HasPose);
            Assert.AreEqual(PaseoPoseVerdict.Waiting, Add(f, X(4f), 0f));
        }
    }
}
