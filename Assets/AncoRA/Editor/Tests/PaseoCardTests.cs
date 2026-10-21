using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoCardTests
    {
        [Test]
        public void AShownBuildingGetsItsCard()
        {
            var card = PaseoCard.Choose(sdkReady: true, tracking: true, "Teología", "Facultad de Teología.");
            Assert.AreEqual(PaseoCardKind.Building, card.Kind);
            Assert.AreEqual("Teología", card.Title);
            Assert.AreEqual("Facultad de Teología.", card.Subtitle);
        }

        [Test]
        public void ABuildingCardSaysWhetherItsPlaceIsConfirmed()
        {
            Assert.IsTrue(PaseoCard.Choose(true, true, "Teología", "", confirmed: true).Confirmed);
            Assert.IsFalse(PaseoCard.Choose(true, true, "Teología", "", confirmed: false).Confirmed);
            Assert.AreNotEqual(PaseoCard.Building("X1", "", true), PaseoCard.Building("X1", "", false));
        }

        [Test]
        public void ABuildingWithoutDescriptionHasAnEmptySubtitle()
        {
            Assert.AreEqual("", PaseoCard.Choose(true, true, "X1", null).Subtitle);
        }

        [Test]
        public void WhileSearchingTheCardHintsWhatToDo()
        {
            var card = PaseoCard.Choose(sdkReady: true, tracking: true, null, null);
            Assert.AreEqual(PaseoCardKind.Hint, card.Kind);
            Assert.AreEqual(PaseoCard.HintTitle, card.Title);
        }

        [Test]
        public void BeforeTheSdkIsReadyOrWithoutTrackingThereIsNoCard()
        {
            Assert.AreEqual(PaseoCardKind.None, PaseoCard.Choose(sdkReady: false, tracking: true, null, null).Kind);
            Assert.AreEqual(PaseoCardKind.None, PaseoCard.Choose(sdkReady: true, tracking: false, null, null).Kind);
        }

        [Test]
        public void CardsWithTheSameContentAreEqual()
        {
            Assert.AreEqual(PaseoCard.Building("X1", "a"), PaseoCard.Building("X1", "a"));
            Assert.AreNotEqual(PaseoCard.Building("X1", "a"), PaseoCard.Building("EIC", "a"));
            Assert.AreEqual(PaseoCard.None, default(PaseoCard));
        }
    }
}
