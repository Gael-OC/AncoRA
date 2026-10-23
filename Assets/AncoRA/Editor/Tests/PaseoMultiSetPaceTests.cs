using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoMultiSetPaceTests
    {
        PaseoMultiSetPace pace;

        [SetUp]
        public void SetUp() => pace = new PaseoMultiSetPace(maxQueries: 5, secondsAfterLocated: 10f);

        [Test]
        public void TheFirstQueryGoesAtOnce() => Assert.IsTrue(pace.Due(0f));

        [Test]
        public void OnlyOneQueryIsInFlight()
        {
            pace.Begin(0f);
            Assert.IsTrue(pace.InFlight);
            Assert.IsFalse(pace.Due(5f), "the first is still waiting for its answer");
            pace.End(5f, accepted: false);
            Assert.IsFalse(pace.InFlight);
        }

        [Test]
        public void UntilTheFirstLocationTheQueriesGoBackToBackWithAHalfSecondGap()
        {
            pace.Begin(0f);
            pace.End(2f, accepted: false);
            Assert.IsFalse(pace.Due(2.2f));
            Assert.IsTrue(pace.Due(2.5f));
            Assert.AreEqual(PaseoMultiSetPace.MinGapSeconds, pace.CurrentGapSeconds);
        }

        [Test]
        public void AfterALocationTheyGoEveryConfiguredSeconds()
        {
            pace.Begin(0f);
            pace.End(2f, accepted: true);
            Assert.IsTrue(pace.Located);
            Assert.IsFalse(pace.Due(9.9f));
            Assert.IsTrue(pace.Due(12f));
            Assert.AreEqual(10f, pace.CurrentGapSeconds);
        }

        [Test]
        public void ALocationIsKeptAcrossFailedQueries()
        {
            pace.Begin(0f);
            pace.End(1f, accepted: true);
            pace.Begin(11f);
            pace.End(12f, accepted: false);
            Assert.IsTrue(pace.Located, "one miss does not undo the location; losing tracking does");
        }

        [Test]
        public void LosingTrackingBringsTheFastRhythmBack()
        {
            pace.Begin(0f);
            pace.End(1f, accepted: true);
            Assert.IsFalse(pace.Due(3f));
            pace.LoseTracking();
            Assert.IsFalse(pace.Located);
            Assert.IsTrue(pace.Due(3f));
        }

        [Test]
        public void TheSessionStopsAtTheCap()
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(pace.CapReached);
                Assert.IsTrue(pace.Due(i * 10f));
                pace.Begin(i * 10f);
                pace.End(i * 10f + 1f, accepted: false);
            }
            Assert.AreEqual(5, pace.Queries);
            Assert.IsTrue(pace.CapReached);
            Assert.IsFalse(pace.Due(1000f));
        }

        [Test]
        public void AQueryCountsAsSoonAsItGoesOut()
        {
            pace.Begin(0f);
            Assert.AreEqual(1, pace.Queries, "the plan counts it whether or not it answers");
        }

        [Test]
        public void AQueryThatNeverAnswersIsGivenUpAfterThirtySeconds()
        {
            pace.Begin(0f);
            Assert.IsFalse(pace.TimedOut(PaseoMultiSetPace.TimeoutSeconds - 1f));
            Assert.IsTrue(pace.TimedOut(PaseoMultiSetPace.TimeoutSeconds));
            pace.End(PaseoMultiSetPace.TimeoutSeconds, accepted: false);
            Assert.IsFalse(pace.TimedOut(1000f), "nothing in flight");
        }

        [Test]
        public void AResultIsALocationFromThirtyPercentWhenItNamesAMap()
        {
            Assert.IsTrue(PaseoMultiSetPace.Accepts("MAP_X", 0.3f));
            Assert.IsTrue(PaseoMultiSetPace.Accepts("MAP_X", 0.95f));
            Assert.IsFalse(PaseoMultiSetPace.Accepts("MAP_X", 0.29f));
            Assert.IsFalse(PaseoMultiSetPace.Accepts(null, 0.9f));
            Assert.IsFalse(PaseoMultiSetPace.Accepts("", 0.9f));
        }
    }
}
