using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoParkScheduleTests
    {
        static readonly PaseoMove[] Rep = { PaseoMove.Bailar, PaseoMove.Saltar, PaseoMove.Saludar };

        [Test]
        public void CyclesThroughRepertoire()
        {
            Assert.AreEqual(PaseoMove.Bailar, PaseoParkSchedule.MoveAt(Rep, 0f, 0f));
            Assert.AreEqual(PaseoMove.Bailar, PaseoParkSchedule.MoveAt(Rep, 5.9f, 0f));
            Assert.AreEqual(PaseoMove.Saltar, PaseoParkSchedule.MoveAt(Rep, 6.1f, 0f));
            Assert.AreEqual(PaseoMove.Saludar, PaseoParkSchedule.MoveAt(Rep, 12.1f, 0f));
            Assert.AreEqual(PaseoMove.Bailar, PaseoParkSchedule.MoveAt(Rep, 18.1f, 0f));
        }

        [Test]
        public void PhaseOffsetsTheCycle()
        {
            Assert.AreEqual(PaseoMove.Saltar, PaseoParkSchedule.MoveAt(Rep, 0f, 1f / 3f + 0.01f));
            Assert.AreEqual(PaseoMove.Saludar, PaseoParkSchedule.MoveAt(Rep, 0f, 2f / 3f + 0.01f));
        }

        [Test]
        public void CustomSlotLength() =>
            Assert.AreEqual(PaseoMove.Saltar, PaseoParkSchedule.MoveAt(Rep, 3.5f, 0f, 3f));

        [Test]
        public void EmptyRepertoireDances()
        {
            Assert.AreEqual(PaseoMove.Bailar, PaseoParkSchedule.MoveAt(new PaseoMove[0], 10f, 0.5f));
            Assert.AreEqual(PaseoMove.Bailar, PaseoParkSchedule.MoveAt(null, 10f, 0.5f));
        }

        [Test]
        public void NegativeTimeStaysInRange() =>
            Assert.DoesNotThrow(() => PaseoParkSchedule.MoveAt(Rep, -20f, 0f));
    }
}
