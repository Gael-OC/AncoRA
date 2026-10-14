using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoPaceTests
    {
        [Test]
        public void WhileSearchingItTriesFast()
        {
            Assert.AreEqual(PaseoPace.FastSeconds, new PaseoPace().Interval(0f, locatedBoxInView: false));
        }

        [Test]
        public void ALocatedBuildingInViewSlowsDownOnlyAfterItSettles()
        {
            // The first seconds after a lock still feed the filter's average at full speed.
            var pace = new PaseoPace();
            Assert.AreEqual(PaseoPace.FastSeconds, pace.Interval(0f, true));
            Assert.AreEqual(PaseoPace.FastSeconds, pace.Interval(PaseoPace.SettleSeconds - 0.1f, true));
            Assert.AreEqual(PaseoPace.SlowSeconds, pace.Interval(PaseoPace.SettleSeconds, true));
        }

        [Test]
        public void LookingAwayFromItGoesBackToFastAtOnce()
        {
            var pace = new PaseoPace();
            pace.Interval(0f, true);
            Assert.AreEqual(PaseoPace.SlowSeconds, pace.Interval(10f, true));
            Assert.AreEqual(PaseoPace.FastSeconds, pace.Interval(10.1f, false));
        }

        [Test]
        public void ComingBackToABuildingSettlesAgain()
        {
            var pace = new PaseoPace();
            pace.Interval(0f, true);
            pace.Interval(10f, true);
            pace.Interval(11f, false);
            Assert.AreEqual(PaseoPace.FastSeconds, pace.Interval(12f, true));
            Assert.AreEqual(PaseoPace.SlowSeconds, pace.Interval(12f + PaseoPace.SettleSeconds, true));
        }
    }
}
