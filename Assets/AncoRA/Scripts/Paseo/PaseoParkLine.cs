using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>What a walking character follows: a position and a heading after walking «distance» metres.</summary>
    public interface IPaseoWalkPath
    {
        /// <summary>Metres of one full cycle; walkers start at a fraction of it.</summary>
        float Length { get; }

        void Sample(float distance, out Vector2 xz, out float yawDegrees);
    }

    /// <summary>
    /// A straight line on the XZ plane walked back and forth: one cycle is there and back. Yaw is the travel direction in Unity
    /// terms (0 = +Z, 90 = +X); at each end the character turns round at once.
    /// </summary>
    public sealed class PaseoParkLine : IPaseoWalkPath
    {
        public const float MinLengthMeters = 0.5f;

        readonly Vector2 from;
        readonly Vector2 to;
        readonly float lineLength;
        readonly float yawThere;

        public PaseoParkLine(Vector2 from, Vector2 to)
        {
            this.from = from;
            this.to = to;
            lineLength = Mathf.Max(MinLengthMeters, Vector2.Distance(from, to));
            var direction = to - from;
            yawThere = direction.sqrMagnitude > 1e-6f ? Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg : 0f;
        }

        public float Length => 2f * lineLength;

        public void Sample(float distance, out Vector2 xz, out float yawDegrees)
        {
            float s = Mathf.Repeat(distance, Length);
            if (s <= lineLength)
            {
                xz = Vector2.Lerp(from, to, s / lineLength);
                yawDegrees = Mathf.Repeat(yawThere, 360f);
                return;
            }
            xz = Vector2.Lerp(to, from, (s - lineLength) / lineLength);
            yawDegrees = Mathf.Repeat(yawThere + 180f, 360f);
        }
    }
}
