using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Where a box that nobody placed yet appears the first time its map localizes: in front of the camera, on the
    /// ground, facing it, so the team can see it and move it with the panel. Not a measurement.
    /// </summary>
    public static class PaseoPlacement
    {
        public const float DistanceMeters = 12f;
        public const float BaseBelowCameraMeters = 1.5f;

        public static void InitialPose(Vector3 cameraPosition, Vector3 cameraForward, Vector3 sizeMeters,
            Vector3 spacePosition, Quaternion spaceRotation, out Vector3 localPosition, out float localYaw)
        {
            // Heading only: a building never tilts with the phone. Looking straight down has no heading; use world forward.
            var heading = new Vector3(cameraForward.x, 0f, cameraForward.z);
            heading = heading.sqrMagnitude < 1e-6f ? Vector3.forward : heading.normalized;

            // The box pivot is its centre: push it half a depth past the front face and lift it half a height.
            var center = cameraPosition + heading * (DistanceMeters + sizeMeters.z * 0.5f)
                         + Vector3.up * (sizeMeters.y * 0.5f - BaseBelowCameraMeters);
            // +Z is the front face (the one with the X); it must face the camera.
            var worldRotation = Quaternion.LookRotation(-heading, Vector3.up);

            var toLocal = Quaternion.Inverse(spaceRotation);
            localPosition = toLocal * (center - spacePosition);
            localYaw = (toLocal * worldRotation).eulerAngles.y;
        }
    }
}
