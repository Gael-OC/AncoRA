using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoViewModeTests
    {
        [Test]
        public void TheAppStartsOnTheBox()
        {
            Assert.AreEqual(PaseoViewMode.Box, PaseoViewModes.AppStart);
        }

        [Test]
        public void TheModesCycleBoxBackgroundObjectAndBack()
        {
            Assert.AreEqual(PaseoViewMode.Background, PaseoViewModes.Next(PaseoViewMode.Box));
            Assert.AreEqual(PaseoViewMode.Object, PaseoViewModes.Next(PaseoViewMode.Background));
            Assert.AreEqual(PaseoViewMode.Box, PaseoViewModes.Next(PaseoViewMode.Object));
        }

        [Test]
        public void TheLabelsAreSpanish()
        {
            Assert.AreEqual("Caja", PaseoViewModes.Label(PaseoViewMode.Box));
            Assert.AreEqual("Fondo", PaseoViewModes.Label(PaseoViewMode.Background));
            Assert.AreEqual("Objeto", PaseoViewModes.Label(PaseoViewMode.Object));
            Assert.AreEqual("Vista: Caja", PaseoViewModes.ButtonText(PaseoViewMode.Box));
            Assert.AreEqual("Vista: Fondo", PaseoViewModes.ButtonText(PaseoViewMode.Background));
            Assert.AreEqual("Vista: Objeto", PaseoViewModes.ButtonText(PaseoViewMode.Object));
        }

        [Test]
        public void TheLogNameIsTheLabelInLowerCase()
        {
            Assert.AreEqual("caja", PaseoViewModes.LogName(PaseoViewMode.Box));
            Assert.AreEqual("fondo", PaseoViewModes.LogName(PaseoViewMode.Background));
            Assert.AreEqual("objeto", PaseoViewModes.LogName(PaseoViewMode.Object));
        }

        static void AssertParts(PaseoViewParts parts, bool box, bool mask, bool model, string why)
        {
            Assert.AreEqual(box, parts.Box, "box: " + why);
            Assert.AreEqual(mask, parts.Mask, "mask: " + why);
            Assert.AreEqual(model, parts.Model, "model: " + why);
        }

        [Test]
        public void ABuildingWithAReplacementDrawsThePartsOfItsMode()
        {
            AssertParts(PaseoViewModes.PartsFor(PaseoViewMode.Box, true, false, true), true, false, false, "Caja");
            AssertParts(PaseoViewModes.PartsFor(PaseoViewMode.Background, true, false, true), false, true, false, "Fondo");
            AssertParts(PaseoViewModes.PartsFor(PaseoViewMode.Object, true, false, true), false, true, true, "Objeto");
        }

        [Test]
        public void AdjustingShowsTheBoxInEveryMode()
        {
            foreach (var mode in new[] { PaseoViewMode.Box, PaseoViewMode.Background, PaseoViewMode.Object })
                AssertParts(PaseoViewModes.PartsFor(mode, true, true, true), true, false, false, "adjusting in " + mode);
        }

        [Test]
        public void ABuildingWithoutAReplacementIsAlwaysItsBox()
        {
            foreach (var mode in new[] { PaseoViewMode.Box, PaseoViewMode.Background, PaseoViewMode.Object })
                foreach (bool adjusting in new[] { false, true })
                    AssertParts(PaseoViewModes.PartsFor(mode, true, adjusting, false), true, false, false, $"{mode}, adjusting {adjusting}");
        }

        [Test]
        public void AnOffScreenBuildingDrawsNothing()
        {
            foreach (var mode in new[] { PaseoViewMode.Box, PaseoViewMode.Background, PaseoViewMode.Object })
                foreach (bool adjusting in new[] { false, true })
                    foreach (bool replacement in new[] { false, true })
                        AssertParts(PaseoViewModes.PartsFor(mode, false, adjusting, replacement), false, false, false,
                            $"{mode}, adjusting {adjusting}, replacement {replacement}");
        }
    }
}
