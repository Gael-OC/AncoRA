using System;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Rectangular loop on the XZ plane centred at the origin, walked by distance. Starts at (-hx, -hz) heading +X, then +Z,
    /// -X, -Z. Yaw is the travel direction in Unity terms (0 = +Z, 90 = +X). Corners are sharp.
    /// </summary>
    public sealed class PaseoParkPath
    {
        public const float MinHalfExtent = 0.5f;

        public float HalfX { get; }
        public float HalfZ { get; }
        public float Length => 4f * (HalfX + HalfZ);

        public PaseoParkPath(float halfX, float halfZ)
        {
            HalfX = Mathf.Max(MinHalfExtent, halfX);
            HalfZ = Mathf.Max(MinHalfExtent, halfZ);
        }

        /// <summary>Loop on the roof, inset from the edge of the box (x width, z depth).</summary>
        public static PaseoParkPath Roof(Vector3 boxSize, float marginMeters) =>
            new(boxSize.x / 2f - marginMeters, boxSize.z / 2f - marginMeters);

        /// <summary>Loop on the ground, outside the footprint of the box.</summary>
        public static PaseoParkPath Ground(Vector3 boxSize, float outsetMeters) =>
            new(boxSize.x / 2f + outsetMeters, boxSize.z / 2f + outsetMeters);

        /// <summary>Position and heading after walking «distance» metres; periodic in the loop length, any sign.</summary>
        public void Sample(float distance, out Vector2 xz, out float yawDegrees)
        {
            float length = Length;
            float s = distance % length;
            if (s < 0f)
                s += length;
            float w = 2f * HalfX, d = 2f * HalfZ;
            if (s < w)
            {
                xz = new Vector2(-HalfX + s, -HalfZ);
                yawDegrees = 90f;
                return;
            }
            s -= w;
            if (s < d)
            {
                xz = new Vector2(HalfX, -HalfZ + s);
                yawDegrees = 0f;
                return;
            }
            s -= d;
            if (s < w)
            {
                xz = new Vector2(HalfX - s, HalfZ);
                yawDegrees = 270f;
                return;
            }
            s -= w;
            xz = new Vector2(-HalfX, HalfZ - s);
            yawDegrees = 180f;
        }
    }
}
