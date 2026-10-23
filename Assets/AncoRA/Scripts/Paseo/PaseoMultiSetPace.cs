namespace AncorRA.AR
{
    /// <summary>
    /// When the MultiSet probe asks the cloud to localize the camera. One query in flight at a time. Until the first accepted
    /// location the queries go back to back (at least <see cref="MinGapSeconds"/> between one ending and the next starting);
    /// afterwards one every <c>secondsAfterLocated</c> s, and back to fast when AR tracking is lost. The free plan has 1000
    /// queries in all, so a session stops at <c>maxQueries</c>. Pure logic: the probe feeds it the clock.
    /// </summary>
    public sealed class PaseoMultiSetPace
    {
        public const float MinGapSeconds = 0.5f;
        /// <summary>The SDK's own default confidence threshold: from here a result counts as a location (every result is logged).</summary>
        public const float AcceptConfidence = 0.3f;
        /// <summary>A query that never answered is given up after this long, so the next one can go.</summary>
        public const float TimeoutSeconds = 30f;

        readonly int maxQueries;
        readonly float secondsAfterLocated;
        float lastFinished = float.NegativeInfinity;
        float startedAt;

        public PaseoMultiSetPace(int maxQueries, float secondsAfterLocated)
        {
            this.maxQueries = maxQueries;
            this.secondsAfterLocated = secondsAfterLocated;
        }

        public int Queries { get; private set; }
        public bool InFlight { get; private set; }
        /// <summary>A location was accepted and AR tracking has not been lost since.</summary>
        public bool Located { get; private set; }
        public bool CapReached => Queries >= maxQueries;
        public float CurrentGapSeconds => Located ? secondsAfterLocated : MinGapSeconds;

        /// <summary>A result is a location when it names a map and its confidence reaches <see cref="AcceptConfidence"/>.</summary>
        public static bool Accepts(string mapCode, float confidence) => !string.IsNullOrEmpty(mapCode) && confidence >= AcceptConfidence;

        /// <summary>Whether to send a query now.</summary>
        public bool Due(float now) => !InFlight && !CapReached && now - lastFinished >= CurrentGapSeconds;

        /// <summary>The query in flight has had no answer for <see cref="TimeoutSeconds"/>.</summary>
        public bool TimedOut(float now) => InFlight && now - startedAt >= TimeoutSeconds;

        /// <summary>A query went out: it counts against the cap now, answered or not.</summary>
        public void Begin(float now)
        {
            InFlight = true;
            startedAt = now;
            Queries++;
        }

        public void End(float now, bool accepted)
        {
            InFlight = false;
            lastFinished = now;
            if (accepted)
                Located = true;
        }

        /// <summary>AR tracking was lost: the next location has to be found again, so the fast rhythm returns.</summary>
        public void LoseTracking() => Located = false;
    }
}
