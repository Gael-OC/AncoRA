using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoLocalizationSettleTests
    {
        const float Short = PaseoLocalizationSettle.WithoutMoveSeconds;

        static PaseoSettle Used(bool spaceMoved, bool spaceEverMoved, float secondsWaiting, bool filterHasPose = true) =>
            PaseoLocalizationSettle.Decide(filterSawResult: true, filterUsedResult: true, spaceMoved, spaceEverMoved, secondsWaiting, filterHasPose);

        [Test]
        public void AMovedSpaceWithAFilteredPoseIsLocated()
        {
            Assert.AreEqual(PaseoSettle.Located, Used(spaceMoved: true, spaceEverMoved: true, secondsWaiting: 0f));
        }

        [Test]
        public void ALocalizationThatLeavesTheSpaceInPlaceIsLocatedAfterAShortWait()
        {
            Assert.AreEqual(PaseoSettle.Wait, Used(false, true, Short * 0.5f));
            Assert.AreEqual(PaseoSettle.Located, Used(false, true, Short));
        }

        [Test]
        public void NothingIsLocatedBeforeTheSpaceFirstMoves()
        {
            Assert.AreEqual(PaseoSettle.Wait, Used(false, false, 10f));
        }

        [Test]
        public void NothingIsLocatedWhileTheFilterHasNoPose()
        {
            // After tracking was lost the filter resets: the space still sits at its old pose, which must not show.
            Assert.AreEqual(PaseoSettle.Wait, Used(false, true, 10f, filterHasPose: false));
            Assert.AreEqual(PaseoSettle.Wait, Used(true, true, 0f, filterHasPose: false));
        }

        [Test]
        public void NothingIsDecidedBeforeTheFilterSawTheLocalization()
        {
            // The result event fires before the SDK hands the localization to the XR Space and its filter.
            Assert.AreEqual(PaseoSettle.Wait, PaseoLocalizationSettle.Decide(false, false, false, true, 10f, true));
        }

        [Test]
        public void ALocalizationTheFilterDidNotUseIsDroppedEvenWhenTheBoxIsShown()
        {
            // A rejected outlier leaves the space in place; it must not make its map the located (and visible) one.
            Assert.AreEqual(PaseoSettle.Dropped, PaseoLocalizationSettle.Decide(true, false, false, true, 10f, true));
        }
    }
}
