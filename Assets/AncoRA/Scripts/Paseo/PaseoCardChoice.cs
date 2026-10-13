using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Which shown building gets the card: the one whose box centre is nearest the middle of the screen. Points come from
    /// Camera.WorldToViewportPoint: x and y run 0–1 across the screen and z &lt;= 0 means behind the camera.
    /// </summary>
    public static class PaseoCardChoice
    {
        // Up close a building fills the view while its centre sits a little off screen.
        public const float Margin = 0.25f;

        public static int Pick(IReadOnlyList<Vector3> viewportPoints)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < viewportPoints.Count; i++)
            {
                var p = viewportPoints[i];
                if (p.z <= 0f || p.x < -Margin || p.x > 1f + Margin || p.y < -Margin || p.y > 1f + Margin)
                    continue;
                float distance = (new Vector2(p.x, p.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }
    }
}
