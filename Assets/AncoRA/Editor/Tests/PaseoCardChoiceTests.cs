using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoCardChoiceTests
    {
        [Test]
        public void TheBoxNearestTheMiddleWins()
        {
            Assert.AreEqual(1, PaseoCardChoice.Pick(new[] { new Vector3(0.1f, 0.5f, 10f), new Vector3(0.55f, 0.45f, 20f) }));
        }

        [Test]
        public void ABoxBehindTheCameraIsNeverChosen()
        {
            // WorldToViewportPoint mirrors points behind the camera into the screen; z < 0 gives them away.
            Assert.AreEqual(1, PaseoCardChoice.Pick(new[] { new Vector3(0.5f, 0.5f, -5f), new Vector3(0.9f, 0.9f, 5f) }));
        }

        [Test]
        public void ABoxFarOutsideTheScreenIsNotChosen()
        {
            Assert.AreEqual(-1, PaseoCardChoice.Pick(new[] { new Vector3(1f + PaseoCardChoice.Margin + 0.1f, 0.5f, 5f) }));
        }

        [Test]
        public void ABoxWhoseCentreIsJustOffTheEdgeStillCounts()
        {
            // Up close, a building fills the view while its centre sits a little off screen.
            Assert.AreEqual(0, PaseoCardChoice.Pick(new[] { new Vector3(1.1f, 0.5f, 5f) }));
        }

        [Test]
        public void NothingShownMeansNoChoice()
        {
            Assert.AreEqual(-1, PaseoCardChoice.Pick(new Vector3[0]));
        }
    }
}
