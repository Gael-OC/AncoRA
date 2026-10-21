using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoSdkStartTests
    {
        const float Never = float.PositiveInfinity;

        // Defaults: SDK not ready, AR tracking (camera running), maps loaded, no retry running, long after the start.
        static bool Retry(bool sdkReady = false, bool arTracking = true, bool mapsSettled = true, bool retryRunning = false,
            float secondsSinceStart = 30f, float secondsSinceLastTry = Never, bool ownStartFinished = false) =>
            PaseoSdkStart.ShouldRetry(sdkReady, arTracking, mapsSettled, retryRunning, secondsSinceStart, secondsSinceLastTry, ownStartFinished);

        [Test]
        public void ASdkLeftNotReadyIsStartedAgainOnceTheCameraRuns()
        {
            // Field log 13-oct: the camera permission dialog kept the camera off while the SDK configured it once, and
            // the SDK never tried again.
            Assert.IsTrue(Retry());
        }

        [Test]
        public void AReadySdkIsLeftAlone()
        {
            Assert.IsFalse(Retry(sdkReady: true));
        }

        [Test]
        public void NothingIsRetriedBeforeTheCameraRuns()
        {
            // Configuring the camera before it runs fails the same way again.
            Assert.IsFalse(Retry(arTracking: false));
        }

        [Test]
        public void ARetryNeverOverlapsAnother()
        {
            Assert.IsFalse(Retry(retryRunning: true));
        }

        [Test]
        public void TheFirstRetryWaitsForTheSdksOwnStartToFinish()
        {
            // Two overlapping starts would configure the localizer twice and break the Immersal session.
            Assert.IsFalse(Retry(secondsSinceStart: PaseoSdkStart.FirstRetrySeconds - 1f));
            Assert.IsTrue(Retry(secondsSinceStart: PaseoSdkStart.FirstRetrySeconds));
        }

        [Test]
        public void OnceTheSdksOwnStartHasFinishedTheRetryNeedNotWait()
        {
            // The localizer is the last step of the SDK's start: once it configured the tour's localization method the
            // start is over, and nothing overlaps. Field log 14-oct: the fixed wait held the retry 2-3 s after «Comenzar».
            Assert.IsTrue(Retry(secondsSinceStart: 1f, ownStartFinished: true));
            Assert.IsFalse(Retry(secondsSinceStart: 1f, ownStartFinished: true, mapsSettled: false));
        }

        [Test]
        public void TheFirstRetryWaitsForTheMapsToLoad()
        {
            Assert.IsFalse(Retry(mapsSettled: false, secondsSinceStart: PaseoSdkStart.FirstRetrySeconds));
        }

        [Test]
        public void AMapThatNeverLoadsDoesNotBlockTheRetryForever()
        {
            Assert.IsTrue(Retry(mapsSettled: false, secondsSinceStart: PaseoSdkStart.MapsGiveUpSeconds));
        }

        [Test]
        public void RetriesAreSpaced()
        {
            Assert.IsFalse(Retry(secondsSinceLastTry: PaseoSdkStart.RetryEverySeconds - 1f));
            Assert.IsTrue(Retry(secondsSinceLastTry: PaseoSdkStart.RetryEverySeconds));
        }
    }
}
