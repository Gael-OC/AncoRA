using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Moves a box along its own axes as seen by someone standing in front of it (facing the face with the X). The box's
    /// +Z looks at that person, so their right is the box's -X. The map frame's axes point anywhere on site, which made the
    /// old X/Y/Z steppers a guessing game.
    /// </summary>
    public static class PaseoBoxNudge
    {
        public static Vector3 Move(Vector3 localPosition, float yaw, float right, float closer, float up) =>
            localPosition + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-right, up, closer);
    }
}
