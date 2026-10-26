namespace AncorRA.AR
{
    public enum PaseoSettle
    {
        Wait,
        Located,
        Dropped
    }

    /// <summary>
    /// When a successful localization counts as "located": the result event fires before the SDK hands it to the XR Space
    /// and its pose filter, so nothing is decided until the filter saw it. One the filter did not use (waiting for a second
    /// one, an outlier, a tilted one) is dropped: the space did not move for it, and counting it would show a building the
    /// filter has no pose for. A used one counts once the space moved, or, if it was already in place, after a short
    /// wait; and the filter must hold a pose, which it drops after tracking is lost.
    /// </summary>
    public static class PaseoLocalizationSettle
    {
        // A re-localization that lands on (almost) the same pose does not move the space measurably.
        public const float WithoutMoveSeconds = 0.5f;

        public static PaseoSettle Decide(bool filterSawResult, bool filterUsedResult, bool spaceMoved, bool spaceEverMoved,
            float secondsWaiting, bool filterHasPose)
        {
            if (!filterSawResult)
                return PaseoSettle.Wait;
            if (!filterUsedResult)
                return PaseoSettle.Dropped;
            return filterHasPose && (spaceMoved || (spaceEverMoved && secondsWaiting >= WithoutMoveSeconds))
                ? PaseoSettle.Located
                : PaseoSettle.Wait;
        }
    }
}
