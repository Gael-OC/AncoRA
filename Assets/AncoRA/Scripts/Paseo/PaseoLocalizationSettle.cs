namespace AncorRA.AR
{
    /// <summary>
    /// When a successful localization counts as "located": the result event fires before the XR Space moves, and showing
    /// the box earlier would flash it at the old pose (or the world origin). The space has to move, or, if it was already
    /// in place, a short wait has to pass; and the pose filter must hold a pose, which it drops after tracking is lost.
    /// </summary>
    public static class PaseoLocalizationSettle
    {
        // A re-localization that lands on (almost) the same pose does not move the space measurably.
        public const float WithoutMoveSeconds = 0.5f;

        public static bool IsSettled(bool spaceMoved, bool spaceEverMoved, float secondsWaiting, bool filterHasPose) =>
            filterHasPose && (spaceMoved || (spaceEverMoved && secondsWaiting >= WithoutMoveSeconds));
    }
}
