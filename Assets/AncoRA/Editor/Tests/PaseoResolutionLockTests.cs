using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    /// <summary>The bar for a lone first localization in campus mode follows the image size: a bigger image gives wrong matches a higher confidence.</summary>
    public class PaseoResolutionLockTests
    {
        [Test]
        public void TheBarRisesWithTheImageSize()
        {
            Assert.AreEqual(PaseoPoseProcessor.CampusSingleLockConfidence, PaseoResolution.SingleLockConfidence(PaseoResolution.Reduced));
            Assert.AreEqual(30f, PaseoResolution.SingleLockConfidence(PaseoResolution.Medium));
            Assert.AreEqual(45f, PaseoResolution.SingleLockConfidence(PaseoResolution.Full));
            float previous = 0f;
            foreach (int option in PaseoResolution.Options)
            {
                Assert.Greater(PaseoResolution.SingleLockConfidence(option), previous - 0.001f, "options run from fewest pixels to most");
                previous = PaseoResolution.SingleLockConfidence(option);
            }
        }

        [Test]
        public void AnUnknownSizeFallsBackBySizeNotToTheLowestBar()
        {
            Assert.AreEqual(PaseoPoseProcessor.CampusSingleLockConfidence, PaseoResolution.SingleLockConfidence(500 * 400));
            Assert.AreEqual(30f, PaseoResolution.SingleLockConfidence(1280 * 960));
            Assert.AreEqual(45f, PaseoResolution.SingleLockConfidence(-1));
        }
    }
}
