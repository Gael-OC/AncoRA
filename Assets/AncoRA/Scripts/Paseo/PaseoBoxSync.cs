using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Keeps the boxes of one building's maps in step. Each map holds the same physical box in its own frame, so when the
    /// team edits it in one map, the other maps get the same change expressed in their frames: the edit as seen from the
    /// old box (a move along the box's own axes plus a turn around its centre) is applied to each other box. Without this,
    /// the edited map and the untouched one show the box in two places, and the view flips with whichever located last.
    /// </summary>
    public static class PaseoBoxSync
    {
        public static void Carry(Vector3 editedBefore, float editedYawBefore, Vector3 editedAfter, float editedYawAfter,
            Vector3 other, float otherYaw, out Vector3 otherAfter, out float otherYawAfter)
        {
            var moveInBox = Quaternion.Euler(0f, -editedYawBefore, 0f) * (editedAfter - editedBefore);
            otherAfter = other + Quaternion.Euler(0f, otherYaw, 0f) * moveInBox;
            otherYawAfter = otherYaw + (editedYawAfter - editedYawBefore);
        }
    }
}
