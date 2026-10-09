using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoLocalizationSettleTests
    {
        const float Short = PaseoLocalizationSettle.WithoutMoveSeconds;

        [Test]
        public void AMovedSpaceWithAFilteredPoseIsSettled()
        {
            Assert.IsTrue(PaseoLocalizationSettle.IsSettled(spaceMoved: true, spaceEverMoved: true, secondsWaiting: 0f, filterHasPose: true));
        }

        [Test]
        public void ALocalizationThatLeavesTheSpaceInPlaceSettlesAfterAShortWait()
        {
            Assert.IsFalse(PaseoLocalizationSettle.IsSettled(false, true, Short * 0.5f, true));
            Assert.IsTrue(PaseoLocalizationSettle.IsSettled(false, true, Short, true));
        }

        [Test]
        public void NothingSettlesBeforeTheSpaceFirstMoves()
        {
            Assert.IsFalse(PaseoLocalizationSettle.IsSettled(false, false, 10f, true));
        }

        [Test]
        public void NothingSettlesWhileTheFilterHasNoPose()
        {
            // After tracking was lost the filter resets: the space still sits at its old pose, which must not show.
            Assert.IsFalse(PaseoLocalizationSettle.IsSettled(false, true, 10f, false));
            Assert.IsFalse(PaseoLocalizationSettle.IsSettled(true, true, 0f, false));
        }
    }
}
