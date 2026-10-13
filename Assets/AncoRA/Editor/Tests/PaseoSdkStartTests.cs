using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoSdkStartTests
    {
        const float Never = float.PositiveInfinity;

        // Defaults: SDK not ready, AR tracking (camera running), maps loaded, no retry running, long after the start.
        static bool Retry(bool sdkReady = false, bool arTracking = true, bool mapsSettled = true, bool retryRunning = false,
            float secondsSinceStart = 30f, float secondsSinceLastTry = Never) =>
            PaseoSdkStart.ShouldRetry(sdkReady, arTracking, mapsSettled, retryRunning, secondsSinceStart, secondsSinceLastTry);

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
