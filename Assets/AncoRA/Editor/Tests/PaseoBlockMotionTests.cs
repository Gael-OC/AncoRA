using System;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBlockMotionTests
    {
        static readonly PaseoMove[] All = (PaseoMove[])Enum.GetValues(typeof(PaseoMove));

        static void AssertPoseEqual(PaseoBlockPose a, PaseoBlockPose b)
        {
            Assert.AreEqual(a.leftShoulder, b.leftShoulder, 1e-2f);
            Assert.AreEqual(a.rightShoulder, b.rightShoulder, 1e-2f);
            Assert.AreEqual(a.leftHip, b.leftHip, 1e-2f);
            Assert.AreEqual(a.rightHip, b.rightHip, 1e-2f);
            Assert.AreEqual(a.leftShoulderRoll, b.leftShoulderRoll, 1e-2f);
            Assert.AreEqual(a.rightShoulderRoll, b.rightShoulderRoll, 1e-2f);
            Assert.AreEqual(a.headYaw, b.headYaw, 1e-2f);
            Assert.AreEqual(a.bodyLift, b.bodyLift, 1e-3f);
            Assert.AreEqual(a.bodyYaw, b.bodyYaw, 1e-2f);
            Assert.AreEqual(a.bodySway, b.bodySway, 1e-2f);
        }

        [Test]
        public void EveryMoveIsPeriodic()
        {
            foreach (var move in All)
            {
                float period = PaseoBlockMotion.Period(move);
                Assert.Greater(period, 0f, move.ToString());
                foreach (float t in new[] { 0f, 0.13f, 0.77f, 3.1f })
                    AssertPoseEqual(PaseoBlockMotion.Evaluate(move, t, 0.2f), PaseoBlockMotion.Evaluate(move, t + period, 0.2f));
            }
        }

        [Test]
        public void PhaseShiftsInTime()
        {
            float period = PaseoBlockMotion.Period(PaseoMove.Caminar);
            AssertPoseEqual(PaseoBlockMotion.Evaluate(PaseoMove.Caminar, 0.3f, 0.25f),
                PaseoBlockMotion.Evaluate(PaseoMove.Caminar, 0.3f + 0.25f * period, 0f));
        }

        [Test]
        public void JumpNeverGoesBelowStandingAndReachesJumpHeight()
        {
            float max = 0f;
            for (float t = 0f; t < 3f; t += 0.005f)
            {
                float lift = PaseoBlockMotion.Evaluate(PaseoMove.Saltar, t, 0f).bodyLift;
                Assert.GreaterOrEqual(lift, 0f);
                max = Mathf.Max(max, lift);
            }
            Assert.AreEqual(PaseoBlockMotion.JumpHeight, max, 0.01f);
            Assert.AreEqual(0.6f, PaseoBlockMotion.JumpHeight, 0.1f);
        }

        [Test]
        public void WaveKeepsRightArmUpAndOscillating()
        {
            float min = float.MaxValue, max = float.MinValue;
            for (float t = 0f; t < 2f; t += 0.01f)
            {
                float roll = PaseoBlockMotion.Evaluate(PaseoMove.Saludar, t, 0f).rightShoulderRoll;
                Assert.GreaterOrEqual(roll, 120f);
                min = Mathf.Min(min, roll);
                max = Mathf.Max(max, roll);
            }
            Assert.Greater(max - min, 20f);
        }

        [TestCase(PaseoMove.Caminar)]
        [TestCase(PaseoMove.Trotar)]
        public void WalkingLegsAreOppositeAndArmsOpposeLegs(PaseoMove move)
        {
            bool moved = false;
            for (float t = 0f; t < 2f; t += 0.03f)
            {
                var p = PaseoBlockMotion.Evaluate(move, t, 0.1f);
                Assert.AreEqual(-p.rightHip, p.leftHip, 1e-3f);
                Assert.AreEqual(-p.rightShoulder, p.leftShoulder, 1e-3f);
                Assert.LessOrEqual(p.leftHip * p.leftShoulder, 1e-3f); // left arm opposite its leg
                Assert.LessOrEqual(p.rightHip * p.rightShoulder, 1e-3f);
                moved |= Mathf.Abs(p.leftHip) > 10f;
            }
            Assert.IsTrue(moved);
        }

        [Test]
        public void DanceSwaysAndSpins()
        {
            float maxSway = 0f, maxYaw = 0f;
            for (float t = 0f; t < PaseoBlockMotion.Period(PaseoMove.Bailar); t += 0.01f)
            {
                var p = PaseoBlockMotion.Evaluate(PaseoMove.Bailar, t, 0f);
                maxSway = Mathf.Max(maxSway, Mathf.Abs(p.bodySway));
                maxYaw = Mathf.Max(maxYaw, Mathf.Abs(p.bodyYaw));
            }
            Assert.Greater(maxSway, 5f);
            Assert.Greater(maxYaw, 10f);
        }

        [Test]
        public void SpeedOnlyForWalkingMoves()
        {
            Assert.AreEqual(1.4f, PaseoBlockMotion.Speed(PaseoMove.Caminar));
            Assert.AreEqual(2.6f, PaseoBlockMotion.Speed(PaseoMove.Trotar));
            Assert.AreEqual(0f, PaseoBlockMotion.Speed(PaseoMove.Saltar));
            Assert.AreEqual(0f, PaseoBlockMotion.Speed(PaseoMove.Saludar));
            Assert.AreEqual(0f, PaseoBlockMotion.Speed(PaseoMove.Bailar));
        }
    }
}
