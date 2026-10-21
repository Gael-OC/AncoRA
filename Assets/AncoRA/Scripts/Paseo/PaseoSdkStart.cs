namespace AncorRA.AR
{
    /// <summary>
    /// When to start the Immersal SDK again. The SDK configures the camera once at startup (about 1 s of attempts); if
    /// the camera is not running yet, as while Android shows the camera permission dialog on the first launch, it stays
    /// not ready for the whole session and nothing ever localizes. Starting it again is safe once its own start has
    /// finished: maps and localizer are already done and only the camera is configured again. Two overlapping starts
    /// are not safe (the localizer would be configured twice), hence one retry at a time, and the first one only when
    /// the SDK's own start is known to be over: its last step configures the tour's localization method
    /// (PaseoLocalization), or failing that signal, after a fixed wait.
    /// </summary>
    public static class PaseoSdkStart
    {
        // On the Xiaomi 14 the SDK's own start ended 2.4 s after the scene started (maps loaded, then ~1 s of camera tries).
        public const float FirstRetrySeconds = 6f;
        // A map whose native load fails never reports; past this the retry stops waiting for it.
        public const float MapsGiveUpSeconds = 20f;
        public const float RetryEverySeconds = 3f;

        public static bool ShouldRetry(bool sdkReady, bool arTracking, bool mapsSettled, bool retryRunning,
            float secondsSinceStart, float secondsSinceLastTry, bool ownStartFinished)
        {
            if (sdkReady || !arTracking || retryRunning || secondsSinceLastTry < RetryEverySeconds)
                return false;
            return (ownStartFinished || secondsSinceStart >= FirstRetrySeconds) &&
                   (mapsSettled || secondsSinceStart >= MapsGiveUpSeconds);
        }
    }
}
