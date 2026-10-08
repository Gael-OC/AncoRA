using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoPlacementTests
    {
        static readonly Vector3 Size = new(20f, 8f, 12f);

        static void AssertClose(Vector3 expected, Vector3 actual) =>
            Assert.Less(Vector3.Distance(expected, actual), 1e-3f, $"esperado {expected}, obtenido {actual}");

        [Test]
        public void SpaceAtIdentity_BoxAheadWithFrontFacingCamera()
        {
            PaseoPlacement.InitialPose(new Vector3(0f, 1.6f, 0f), Vector3.forward, Size, Vector3.zero, Quaternion.identity,
                out var position, out float yaw);
            // Front face at 12 m, so the centre is 12 + 6 ahead; base 1.5 m below the camera, so the centre is 4 m above it.
            AssertClose(new Vector3(0f, 4.1f, 18f), position);
            Assert.AreEqual(180f, yaw, 1e-3f);
        }

        [Test]
        public void RotatedAndMovedSpace_ResultIsLocalToTheSpace()
        {
            PaseoPlacement.InitialPose(new Vector3(0f, 1.6f, 0f), Vector3.forward, Size,
                new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), out var position, out float yaw);
            AssertClose(new Vector3(-18f, 4.1f, -10f), position);
            Assert.AreEqual(90f, yaw, 1e-3f);
        }

        [Test]
        public void PitchedCameraUsesItsHeadingOnly()
        {
            var pitchedDown = Quaternion.Euler(40f, 0f, 0f) * Vector3.forward;
            PaseoPlacement.InitialPose(new Vector3(0f, 1.6f, 0f), pitchedDown, Size, Vector3.zero, Quaternion.identity,
                out var position, out _);
            AssertClose(new Vector3(0f, 4.1f, 18f), position);
        }

        [Test]
        public void CameraLookingStraightDown_GivesAFinitePose()
        {
            PaseoPlacement.InitialPose(new Vector3(0f, 1.6f, 0f), Vector3.down, Size, Vector3.zero, Quaternion.identity,
                out var position, out float yaw);
            Assert.IsFalse(float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) || float.IsNaN(yaw));
            Assert.Greater(position.magnitude, 10f);
        }
    }
}
