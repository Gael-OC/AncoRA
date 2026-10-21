using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>Where the parts of a building's replacement go, in the box frame (pivot at the box centre).</summary>
    public static class PaseoReplacementLayout
    {
        /// <summary>The painted-over volume: the box grown by the margin on every side, top and bottom included.</summary>
        public static Vector3 MaskSize(Vector3 boxSize, float marginMeters) => boxSize + Vector3.one * (2f * Mathf.Max(0f, marginMeters));

        /// <summary>The model's pivot: the offset measured from the centre of the box base, which stands on the ground.</summary>
        public static Vector3 ModelPosition(Vector3 boxSize, Vector3 offset) => new(offset.x, -boxSize.y * 0.5f + offset.y, offset.z);

        public static float SpinYaw(float yaw, float degreesPerSecond, float seconds) => Mathf.Repeat(yaw + degreesPerSecond * seconds, 360f);

        /// <summary>How far above its base a Unity primitive's centre sits: a model stands on its pivot, a primitive does not.</summary>
        public static float ShapeBaseHeight(PrimitiveType type) => type is PrimitiveType.Cylinder or PrimitiveType.Capsule ? 1f : 0.5f;
    }
}
