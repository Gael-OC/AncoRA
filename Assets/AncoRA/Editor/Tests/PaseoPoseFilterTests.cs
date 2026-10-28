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

        // The camera stands at the space origin unless told otherwise: with a level rotation it does not change the pose,
        // and a fixed camera means no travel, so the gate and the average behave as without the travel rule.
        static PaseoPoseVerdict Add(PaseoPoseFilter f, Vector3 position, float yaw, float confidence = PaseoPoseFilter.FullConfidence,
            Vector3? camera = null) =>
            f.Add(position, Yaw(yaw), camera ?? Vector3.zero, confidence).Verdict;

        // Same, from a camera that stands `ahead` metres from the origin.
        static PaseoPoseVerdict AddFrom(PaseoPoseFilter f, Vector3 position, float ahead, float confidence = PaseoPoseFilter.FullConfidence) =>
            Add(f, position, 0f, confidence, X(ahead));

        // Space pose that turns the map by `degrees` around the centre of the box: the box itself stays in place.
        static void TurnAroundBox(PaseoBoxGeometry box, float degrees, out Vector3 position, out Quaternion rotation)
        {
            rotation = Yaw(degrees);
            position = box.Center - rotation * box.Center;
        }

        // Shown at the first pose and confirmed by a second one in the same place.
        static PaseoPoseFilter LockedAtOrigin()
        {
            var f = NewFilter(NearBox);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, Vector3.zero, 0f));
            Assert.AreEqual(PaseoPoseVerdict.Accepted, Add(f, Vector3.zero, 0f));
            Assert.IsTrue(f.Confirmed);
            return f;
        }

        [Test]
        public void TheFirstPoseShowsTheBoxAtOnceButUnconfirmed()
        {
            // Field test 14-oct: waiting for a second agreeing localization kept the box hidden for tens of seconds.
            var f = NewFilter(NearBox);
            var report = f.Add(X(1f), Quaternion.identity, X(1f), 12f);
            Assert.AreEqual(PaseoPoseVerdict.Locked, report.Verdict);
            Assert.IsTrue(report.Used);
            Assert.IsFalse(report.Confirmed);
            Assert.IsTrue(f.HasPose);
            Assert.IsFalse(f.Confirmed);
            AssertNear(X(1f), f.Position);
        }

        [Test]
        public void ASecondAgreeingPoseConfirmsTheBoxAndJoinsTheAverage()
        {
            var f = NewFilter(NearBox);
            Add(f, X(0f), 0f);
            var report = f.Add(X(0.2f), Quaternion.identity, Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, report.Verdict);
            Assert.IsTrue(report.Confirmed);
            Assert.IsTrue(f.Confirmed);
            AssertNear(X(0.1f), f.TargetPosition);
        }

        [Test]
        public void OneDisagreeingPoseDoesNotMoveAnUnconfirmedBox()
        {
            var f = NewFilter(NearBox);
            Add(f, X(0f), 0f);
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(5f), 0f));
            f.Step(1f);
            AssertNear(X(0f), f.Position);
            Assert.IsFalse(f.Confirmed);
        }

        [Test]
        public void AWrongFirstPoseIsReplacedOnceTwoOthersAgree()
        {
            var f = NewFilter(NearBox);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(5f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(0f), 0f));
            Assert.AreEqual(PaseoPoseVerdict.Relocked, Add(f, X(0.1f), 0f));
            AssertNear(X(0.05f), f.Position);
            Assert.IsTrue(f.Confirmed);
        }

        [Test]
        public void TwoRejectionsReplaceAnUnconfirmedBoxOnlyIfTheyAgreeAtTheBox()
        {
            // 3 degrees around the map origin move the corners 18 m away by 0.94 m, more than the 0.6 m agreement.
            var f = NewFilter(NearBox);
            Add(f, X(5f), 0f);
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, Vector3.zero, 0f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, Vector3.zero, 3f));
            AssertNear(X(5f), f.Position);
        }

        [Test]
        public void LockedRotationIsTheAverage()
        {
            var f = NewFilter(NearBox);
            Add(f, Vector3.zero, 0f);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, Add(f, Vector3.zero, 1f));
            Assert.AreEqual(0.5f, f.TargetRotation.eulerAngles.y, 1e-3f);
        }

        [Test]
        public void AveragingTurnsAroundTheBoxSoTheBoxStaysInPlace()
        {
            // Both estimates hold the far box at the same place, turned 4 degrees apart: their average must too.
            var f = NewFilter(FarBox);
            TurnAroundBox(FarBox, 4f, out var turnedPosition, out var turnedRotation);
            f.Add(Vector3.zero, Quaternion.identity, Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, f.Add(turnedPosition, turnedRotation, Vector3.zero, PaseoPoseFilter.FullConfidence).Verdict);
            AssertNear(new Vector3(0f, 0f, 30f), f.TargetPosition + f.TargetRotation * FarBox.Center, 1e-3f);
            Assert.AreEqual(2f, f.TargetRotation.eulerAngles.y, 1e-3f);
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
            Assert.AreEqual(PaseoPoseVerdict.Accepted, Add(f, X(0.3f), 0f, confidence: 10f));
            AssertNear(X(0.05f), f.TargetPosition);
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
        public void ATiltedLocalizationDoesNotShowTheBox()
        {
            var f = NewFilter(NearBox);
            var report = f.Add(Vector3.zero, Quaternion.Euler(PaseoPoseFilter.MaxTiltDegrees + 1f, 0f, 0f), Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Tilted, report.Verdict);
            Assert.IsFalse(f.HasPose);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, Vector3.zero, 0f));
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
        public void TheFirstPoseHasNothingToMoveFrom()
        {
            var report = NewFilter(NearBox).Add(Vector3.zero, Quaternion.identity, Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.IsTrue(float.IsNaN(report.BoxMoveMeters));
        }

        [Test]
        public void TheGateWidensAfterTheCameraWalked()
        {
            // A 1.6 m correction is an outlier right after the lock, but ARCore drifts with the distance walked:
            // 30 m later (gate 1 + 0.03 * 30 = 1.9 m) it is the right one.
            var near = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(near, X(1.6f), 0f));

            var walked = LockedAtOrigin();
            var report = walked.Add(X(1.6f), Quaternion.identity, X(30f), PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, report.Verdict);
            Assert.AreEqual(30f, report.TravelMeters, 1e-3f);
            Assert.AreEqual(1.9f, report.GateMeters, 1e-3f);
        }

        [Test]
        public void TheGateIsCapped()
        {
            var f = LockedAtOrigin();
            var report = f.Add(X(2.6f), Quaternion.identity, X(200f), PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Rejected, report.Verdict);
            Assert.AreEqual(PaseoPoseFilter.MaxInlierMeters, report.GateMeters, 1e-4f);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, AddFrom(LockedAtOrigin(), X(2.4f), 200f));
        }

        [Test]
        public void TheTravelIsHorizontalAndMeasuredFromTheLastUsedEstimate()
        {
            var f = LockedAtOrigin();
            // The camera climbed 40 m: that is not walking.
            var climbed = f.Add(X(1.6f), Quaternion.identity, new Vector3(0f, 40f, 0f), PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(0f, climbed.TravelMeters, 1e-4f);
            Assert.AreEqual(PaseoPoseVerdict.Rejected, climbed.Verdict);

            // Accepted at 30 m: the next estimate from the same spot has not walked at all, so it faces the plain 1 m gate.
            Assert.AreEqual(PaseoPoseVerdict.Accepted, AddFrom(f, X(1.6f), 30f));
            AssertNear(X(1.6f), f.TargetPosition);
            var next = f.Add(X(3f), Quaternion.identity, X(30f), PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(0f, next.TravelMeters, 1e-4f);
            Assert.AreEqual(PaseoPoseFilter.InlierMeters, next.GateMeters, 1e-4f);
            Assert.AreEqual(PaseoPoseVerdict.Rejected, next.Verdict);
        }

        [Test]
        public void WalkingWithDriftKeepsTheBoxFollowingTheNewestEstimates()
        {
            // 5 % drift while walking 40 m in 10 m steps: the true correction reaches 2 m. With a fixed 1 m gate the later
            // estimates would be rejected against the old average; with the travel rule each one is accepted.
            var f = NewFilter(NearBox);
            Assert.AreEqual(PaseoPoseVerdict.Locked, AddFrom(f, X(0f), 0f));
            for (int step = 1; step <= 4; step++)
                Assert.AreEqual(PaseoPoseVerdict.Accepted, AddFrom(f, X(0.05f * 10f * step), 10f * step), $"step {step}");
            Assert.AreEqual(2f, f.TargetPosition.x, 0.3f);
        }

        [Test]
        public void AnEstimateTakenFarFromTheNewestWeighsLessInTheAverage()
        {
            // Both cases average X(0) with X(0.6) (full confidence); only where the older camera stood changes.
            var together = NewFilter(NearBox);
            AddFrom(together, X(0f), 10f);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, AddFrom(together, X(0.6f), 10f));
            AssertNear(X(0.3f), together.TargetPosition);

            var apart = NewFilter(NearBox);
            AddFrom(apart, X(0f), 0f);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, AddFrom(apart, X(0.6f), 10f));
            float older = Mathf.Exp(-10f / PaseoPoseFilter.TravelDecayMeters);
            AssertNear(X(0.6f / (1f + older)), apart.TargetPosition);
            Assert.Greater(apart.TargetPosition.x, together.TargetPosition.x);
        }

        [Test]
        public void EstimatesBeyondTheWindowRadiusLeaveTheAverage()
        {
            // The newest estimate stood 30 m (> 25 m) from the two locked ones: it is the whole window.
            var gone = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Accepted, AddFrom(gone, X(0.5f), PaseoPoseFilter.WindowRadiusMeters + 5f));
            AssertNear(X(0.5f), gone.TargetPosition);

            // At 20 m they stay, faded by exp(-20 / 8) each.
            var kept = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Accepted, AddFrom(kept, X(0.5f), 20f));
            float faded = Mathf.Exp(-20f / PaseoPoseFilter.TravelDecayMeters);
            AssertNear(X(0.5f / (1f + 2f * faded)), kept.TargetPosition);
        }

        [Test]
        public void RelockingStillNeedsAgreeingRejectionsAfterWalking()
        {
            // Far beyond the gate and 30 m away: three that agree among themselves relock, near their own average.
            var f = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, AddFrom(f, X(4f), 30f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, AddFrom(f, X(4.1f), 31f));
            Assert.AreEqual(PaseoPoseVerdict.Relocked, AddFrom(f, X(4.2f), 32f));
            Assert.AreEqual(4.1f, f.Position.x, 0.05f);
            Assert.IsTrue(f.Confirmed);

            // Scattered ones do not, however far the camera walks.
            var scattered = LockedAtOrigin();
            Assert.AreEqual(PaseoPoseVerdict.Rejected, AddFrom(scattered, X(4f), 30f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, AddFrom(scattered, X(-4f), 31f));
            Assert.AreEqual(PaseoPoseVerdict.Rejected, AddFrom(scattered, new Vector3(0f, 0f, 4f), 32f));
            AssertNear(X(0f), scattered.Position);
        }

        [Test]
        public void TheReportCarriesNoGateNorTravelBeforeTheFirstLock()
        {
            var f = NewFilter(NearBox);
            var tilted = f.Add(Vector3.zero, Quaternion.Euler(PaseoPoseFilter.MaxTiltDegrees + 1f, 0f, 0f), Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.IsTrue(float.IsNaN(tilted.GateMeters));
            Assert.IsTrue(float.IsNaN(tilted.TravelMeters));

            var first = f.Add(Vector3.zero, Quaternion.identity, Vector3.zero, PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Locked, first.Verdict);
            Assert.IsTrue(float.IsNaN(first.GateMeters));
            Assert.IsTrue(float.IsNaN(first.TravelMeters));

            var second = f.Add(Vector3.zero, Quaternion.identity, X(10f), PaseoPoseFilter.FullConfidence);
            Assert.AreEqual(10f, second.TravelMeters, 1e-3f);
            Assert.AreEqual(1.3f, second.GateMeters, 1e-3f);
        }

        // Campus mode: one pose places every box, so a weak first estimate must not lock the filter alone.
        static PaseoPoseFilter CampusFilter() => new() { Box = NearBox, SingleLockConfidence = 20f };

        [Test]
        public void WithoutALimitAnyFirstEstimateLocksAlone()
        {
            var f = NewFilter(NearBox);
            Assert.AreEqual(0f, f.SingleLockConfidence);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(0f), 0f, confidence: 1f));
            Assert.IsTrue(f.HasPose);
            Assert.IsFalse(f.Confirmed);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(NewFilter(NearBox), X(0f), 0f, confidence: 0f));
        }

        [Test]
        public void AWeakFirstEstimateIsHeldPendingWhenALimitIsSet()
        {
            var f = CampusFilter();
            var report = f.Add(X(1f), Quaternion.identity, X(1f), 12f);
            Assert.AreEqual(PaseoPoseVerdict.Pending, report.Verdict);
            Assert.IsFalse(report.Used, "a held estimate moves nothing");
            Assert.IsFalse(report.Confirmed);
            Assert.IsTrue(float.IsNaN(report.BoxMoveMeters));
            Assert.IsFalse(f.HasPose);
            Assert.IsFalse(f.Confirmed);
        }

        [Test]
        public void TwoAgreeingWeakEstimatesLockTogetherConfirmed()
        {
            var f = CampusFilter();
            Assert.AreEqual(PaseoPoseVerdict.Pending, Add(f, X(0f), 0f, confidence: 12f));
            var report = f.Add(X(0.2f), Quaternion.identity, Vector3.zero, 15f);
            Assert.AreEqual(PaseoPoseVerdict.Locked, report.Verdict);
            Assert.IsTrue(report.Used);
            Assert.IsTrue(report.Confirmed);
            Assert.IsTrue(f.HasPose);
            Assert.IsTrue(f.Confirmed);
            // Weights 12/50 and 15/50: the average leans to the second, at 0.2 * 0.3 / 0.54.
            AssertNear(X(0.2f * 0.3f / 0.54f), f.TargetPosition, 1e-3f);
            AssertNear(f.TargetPosition, f.Position);
        }

        [Test]
        public void WeakEstimatesThatDisagreeStayPendingUntilTwoAgree()
        {
            var f = CampusFilter();
            Assert.AreEqual(PaseoPoseVerdict.Pending, Add(f, X(0f), 0f, confidence: 12f));
            Assert.AreEqual(PaseoPoseVerdict.Pending, Add(f, X(5f), 0f, confidence: 12f));
            Assert.IsFalse(f.HasPose);
            // The third agrees with the first one held, not with the last.
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(0.1f), 0f, confidence: 12f));
            Assert.IsTrue(f.Confirmed);
            AssertNear(X(0.05f), f.TargetPosition, 1e-3f);
        }

        [Test]
        public void AnEstimateAtTheLimitLocksAlone()
        {
            var f = CampusFilter();
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(1f), 0f, confidence: 20f));
            Assert.IsTrue(f.HasPose);
            Assert.IsFalse(f.Confirmed, "like any first estimate, it is confirmed by the next one");
            AssertNear(X(1f), f.Position);
        }

        [Test]
        public void AStrongEstimateLocksAloneEvenWithAWeakOneHeld()
        {
            var f = CampusFilter();
            Assert.AreEqual(PaseoPoseVerdict.Pending, Add(f, X(0f), 0f, confidence: 12f));
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(4f), 0f, confidence: 30f));
            AssertNear(X(4f), f.Position);
            Assert.IsFalse(f.Confirmed);
            // The held one is gone: it does not come back to confirm or contest the lock.
            Assert.AreEqual(PaseoPoseVerdict.Rejected, Add(f, X(0f), 0f, confidence: 12f));
        }

        [Test]
        public void TheLimitOnlyHoldsEstimatesBeforeTheFirstLock()
        {
            var f = CampusFilter();
            Add(f, X(0f), 0f, confidence: 30f);
            Assert.AreEqual(PaseoPoseVerdict.Accepted, Add(f, X(0.2f), 0f, confidence: 5f));
            Assert.IsTrue(f.Confirmed);
        }

        [Test]
        public void ATiltedEstimateIsNeverHeld()
        {
            var f = CampusFilter();
            var tilted = f.Add(Vector3.zero, Quaternion.Euler(PaseoPoseFilter.MaxTiltDegrees + 1f, 0f, 0f), Vector3.zero, 12f);
            Assert.AreEqual(PaseoPoseVerdict.Tilted, tilted.Verdict);
            Assert.AreEqual(PaseoPoseVerdict.Pending, Add(f, X(0f), 0f, confidence: 12f), "the tilted one did not wait for company");
        }

        [Test]
        public void ResetForgetsTheHeldEstimate()
        {
            var f = CampusFilter();
            Assert.AreEqual(PaseoPoseVerdict.Pending, Add(f, X(0f), 0f, confidence: 12f));
            f.Reset();
            Assert.AreEqual(PaseoPoseVerdict.Pending, Add(f, X(0f), 0f, confidence: 12f), "the held one did not survive the reset");
            Assert.IsFalse(f.HasPose);
        }

        [Test]
        public void ResetForgetsThePose()
        {
            var f = LockedAtOrigin();
            f.Reset();
            Assert.IsFalse(f.HasPose);
            Assert.IsFalse(f.Confirmed);
            Assert.AreEqual(PaseoPoseVerdict.Locked, Add(f, X(4f), 0f));
            Assert.IsFalse(f.Confirmed);
        }
    }
}
