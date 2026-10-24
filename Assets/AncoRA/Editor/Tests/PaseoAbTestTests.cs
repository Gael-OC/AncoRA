using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoAbTestTests
    {
        const PaseoAbMode Normal = PaseoAbMode.Normal;
        const PaseoAbMode Variant = PaseoAbMode.Variant;
        const PaseoAbMode Alternate = PaseoAbMode.Alternate;

        [Test]
        public void ByDefaultNothingChanges()
        {
            for (int attempt = 1; attempt <= 8; attempt++)
            {
                var choice = PaseoAbTest.Choose(Normal, Normal, attempt);
                Assert.IsFalse(choice.Contrast, $"attempt {attempt}");
                Assert.IsFalse(choice.Gravity, $"attempt {attempt}");
            }
        }

        [Test]
        public void TheVariantAppliesToEveryAttempt()
        {
            for (int attempt = 1; attempt <= 8; attempt++)
            {
                var choice = PaseoAbTest.Choose(Variant, Variant, attempt);
                Assert.IsTrue(choice.Contrast, $"attempt {attempt}");
                Assert.IsTrue(choice.Gravity, $"attempt {attempt}");
            }
        }

        [Test]
        public void AlternatingContrastEnhancesTheOddAttempts()
        {
            Assert.IsTrue(PaseoAbTest.Choose(Alternate, Normal, 1).Contrast);
            Assert.IsFalse(PaseoAbTest.Choose(Alternate, Normal, 2).Contrast);
            Assert.IsTrue(PaseoAbTest.Choose(Alternate, Normal, 3).Contrast);
            Assert.IsFalse(PaseoAbTest.Choose(Alternate, Normal, 4).Contrast);
            Assert.IsFalse(PaseoAbTest.Choose(Alternate, Normal, 2).Gravity, "the solver stays normal");
        }

        [Test]
        public void AlternatingTheSolverAloneGivesTheGravitySolverToTheOddAttempts()
        {
            Assert.IsTrue(PaseoAbTest.Choose(Normal, Alternate, 1).Gravity);
            Assert.IsFalse(PaseoAbTest.Choose(Normal, Alternate, 2).Gravity);
            Assert.IsTrue(PaseoAbTest.Choose(Normal, Alternate, 3).Gravity);
            Assert.IsFalse(PaseoAbTest.Choose(Normal, Alternate, 4).Gravity);
            Assert.IsFalse(PaseoAbTest.Choose(Normal, Alternate, 1).Contrast, "the contrast stays normal");
        }

        [Test]
        public void AlternatingBothBalancesTheFourCombinations()
        {
            // Attempts 1-4 are enhanced+default, plain+gravity, enhanced+gravity, plain+default; then the cycle repeats.
            var expected = new[] { (true, false), (false, true), (true, true), (false, false) };
            for (int attempt = 1; attempt <= 12; attempt++)
            {
                var choice = PaseoAbTest.Choose(Alternate, Alternate, attempt);
                Assert.AreEqual(expected[(attempt - 1) % 4], (choice.Contrast, choice.Gravity), $"attempt {attempt}");
            }
        }

        [Test]
        public void AlternatingBothGivesEachCombinationTheSameShare()
        {
            var counts = new Dictionary<(bool, bool), int>();
            for (int attempt = 1; attempt <= 400; attempt++)
            {
                var choice = PaseoAbTest.Choose(Alternate, Alternate, attempt);
                var key = (choice.Contrast, choice.Gravity);
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            Assert.AreEqual(4, counts.Count);
            foreach (var count in counts.Values)
                Assert.AreEqual(100, count);
        }

        [Test]
        public void AlternatingOneAndForcingTheOtherKeepsTheForcedOne()
        {
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                Assert.IsTrue(PaseoAbTest.Choose(Variant, Alternate, attempt).Contrast);
                Assert.IsTrue(PaseoAbTest.Choose(Alternate, Variant, attempt).Gravity);
            }
            // With the contrast forced the solver alternates by plain parity, not by the pair rule.
            Assert.IsTrue(PaseoAbTest.Choose(Variant, Alternate, 1).Gravity);
            Assert.IsFalse(PaseoAbTest.Choose(Variant, Alternate, 2).Gravity);
        }

        [Test]
        public void TheModesCycleNormalVariantAlternate()
        {
            Assert.AreEqual(Variant, PaseoAbTest.Next(Normal));
            Assert.AreEqual(Alternate, PaseoAbTest.Next(Variant));
            Assert.AreEqual(Normal, PaseoAbTest.Next(Alternate));
        }

        [Test]
        public void EachTestNamesItsModes()
        {
            Assert.AreEqual("normal", PaseoAbTest.ContrastName(Normal));
            Assert.AreEqual("realzado", PaseoAbTest.ContrastName(Variant));
            Assert.AreEqual("alterno", PaseoAbTest.ContrastName(Alternate));
            Assert.AreEqual("normal", PaseoAbTest.SolverName(Normal));
            Assert.AreEqual("gravedad", PaseoAbTest.SolverName(Variant));
            Assert.AreEqual("alterno", PaseoAbTest.SolverName(Alternate));
        }
    }
}
