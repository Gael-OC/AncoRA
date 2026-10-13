using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoTapGestureTests
    {
        [Test]
        public void FiveQuickTapsToggle()
        {
            var gesture = new PaseoTapGesture();
            for (int i = 0; i < 4; i++)
                Assert.IsFalse(gesture.Tap(i * 0.3f));
            Assert.IsTrue(gesture.Tap(1.2f));
        }

        [Test]
        public void ASlowTapStartsTheSeriesAgain()
        {
            var gesture = new PaseoTapGesture();
            gesture.Tap(0f);
            gesture.Tap(0.3f);
            gesture.Tap(0.6f);
            Assert.IsFalse(gesture.Tap(0.6f + PaseoTapGesture.WindowSeconds + 0.1f));
            Assert.IsFalse(gesture.Tap(2.5f));
            Assert.IsFalse(gesture.Tap(2.7f));
            Assert.IsFalse(gesture.Tap(2.9f));
            Assert.IsTrue(gesture.Tap(3.1f));
        }

        [Test]
        public void AfterAToggleItTakesFiveMoreTaps()
        {
            var gesture = new PaseoTapGesture();
            for (int i = 0; i < 5; i++)
                gesture.Tap(i * 0.2f);
            for (int i = 5; i < 9; i++)
                Assert.IsFalse(gesture.Tap(i * 0.2f));
            Assert.IsTrue(gesture.Tap(1.8f));
        }
    }
}
