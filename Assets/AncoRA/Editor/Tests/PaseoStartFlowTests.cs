using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoStartFlowTests
    {
        [Test]
        public void PressingStartWhileTheDialogsAreOpenAsksOnce()
        {
            var flow = new PaseoStartFlow();
            Assert.IsTrue(flow.Press());
            Assert.AreEqual(PaseoStartState.Requesting, flow.State);
            Assert.IsFalse(flow.Press());
        }

        [Test]
        public void WithTheCameraTheTourStartsEvenWithoutLocation()
        {
            var flow = new PaseoStartFlow();
            flow.Press();
            flow.Answer(camera: true, location: false);
            Assert.AreEqual(PaseoStartState.Started, flow.State);
            Assert.IsFalse(flow.LocationGranted);
            Assert.IsFalse(flow.Press(), "already started: the button only closes the welcome");
        }

        [Test]
        public void WithoutTheCameraTheWelcomeStaysAndCanAskAgain()
        {
            var flow = new PaseoStartFlow();
            flow.Press();
            flow.Answer(camera: false, location: true);
            Assert.AreEqual(PaseoStartState.NeedsCamera, flow.State);
            Assert.IsTrue(flow.Press());
        }

        [Test]
        public void AnAnswerNobodyAskedForIsIgnored()
        {
            var flow = new PaseoStartFlow();
            flow.Answer(camera: true, location: true);
            Assert.AreEqual(PaseoStartState.Welcome, flow.State);
        }
    }
}
