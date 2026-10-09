using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBoxSyncTests
    {
        static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        [Test]
        public void WithoutAnEditTheOtherBoxStays()
        {
            PaseoBoxSync.Carry(new Vector3(1f, 2f, 3f), 30f, new Vector3(1f, 2f, 3f), 30f,
                new Vector3(-5f, 0f, 7f), 120f, out var position, out float yaw);
            AssertNear(new Vector3(-5f, 0f, 7f), position);
            Assert.AreEqual(120f, yaw, 1e-4f);
        }

        [Test]
        public void AMoveAlongTheBoxIsTheSameMoveAlongTheBoxInTheOtherMap()
        {
            // Edited map: box faces +Z (yaw 0) and moves 1 m along its own X. Other map: the same box sits at yaw 90,
            // so its own X points to -Z in that map's frame.
            PaseoBoxSync.Carry(Vector3.zero, 0f, new Vector3(1f, 0f, 0f), 0f,
                new Vector3(10f, 0f, 10f), 90f, out var position, out float yaw);
            AssertNear(new Vector3(10f, 0f, 9f), position);
            Assert.AreEqual(90f, yaw, 1e-4f);
        }

        [Test]
        public void ATurnOfTheBoxTurnsTheOtherBoxByTheSameAngleAroundItsCentre()
        {
            PaseoBoxSync.Carry(new Vector3(3f, 0f, 3f), 10f, new Vector3(3f, 0f, 3f), 12f,
                new Vector3(-4f, 1f, 2f), 200f, out var position, out float yaw);
            AssertNear(new Vector3(-4f, 1f, 2f), position);
            Assert.AreEqual(202f, yaw, 1e-4f);
        }

        [Test]
        public void HeightChangesCarryOverUnchanged()
        {
            PaseoBoxSync.Carry(Vector3.zero, 45f, new Vector3(0f, -0.5f, 0f), 45f,
                new Vector3(2f, 1f, 2f), -60f, out var position, out _);
            AssertNear(new Vector3(2f, 0.5f, 2f), position);
        }
    }
}
