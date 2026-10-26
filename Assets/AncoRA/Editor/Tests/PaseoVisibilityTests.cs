using System;
using System.Linq;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoVisibilityTests
    {
        static PaseoVisibility Tour()
        {
            var v = new PaseoVisibility();
            v.AddBuilding("CienciasBasicas");
            v.AddBuilding("X1");
            return v;
        }

        [Test]
        public void NothingLocatedBeforeAnyLocalization()
        {
            var v = Tour();
            Assert.IsFalse(v.IsLocated("CienciasBasicas"));
            Assert.IsFalse(v.IsLocated("X1"));
        }

        [Test]
        public void LocatedBuildingBecomesVisible()
        {
            var v = Tour();
            Assert.IsTrue(v.MarkLocated("X1", 1f));
            Assert.IsTrue(v.IsLocated("X1"));
            Assert.IsFalse(v.IsLocated("CienciasBasicas"));
        }

        [Test]
        public void TwoBuildingsCanBeLocatedAtOnce()
        {
            var v = Tour();
            v.MarkLocated("X1", 1f);
            v.MarkLocated("CienciasBasicas", 2f);
            Assert.IsTrue(v.IsLocated("X1"));
            Assert.IsTrue(v.IsLocated("CienciasBasicas"));
        }

        [Test]
        public void LosingTrackingHidesEverythingUntilANewLocalization()
        {
            var v = Tour();
            v.MarkLocated("CienciasBasicas", 1f);
            v.MarkLocated("X1", 1f);
            v.LoseTracking();
            Assert.IsFalse(v.IsLocated("CienciasBasicas"));
            Assert.IsFalse(v.IsLocated("X1"));
            v.MarkLocated("X1", 2f);
            Assert.IsTrue(v.IsLocated("X1"));
            Assert.IsFalse(v.IsLocated("CienciasBasicas"), "a building does not come back on another building's localization");
        }

        [Test]
        public void HeldAfterTenSecondsWithoutANewSuccess()
        {
            var v = Tour();
            v.MarkLocated("X1", 1f);
            Assert.IsFalse(v.IsHeld("X1", 10.9f));
            Assert.IsTrue(v.IsHeld("X1", 11f));
            v.MarkLocated("X1", 11f);
            Assert.IsFalse(v.IsHeld("X1", 11.5f));
        }

        [Test]
        public void ABuildingThatWasNeverLocatedIsNotHeld()
        {
            Assert.IsFalse(Tour().IsHeld("X1", 100f));
        }

        [Test]
        public void UnknownBuildingIsIgnored()
        {
            var v = Tour();
            Assert.IsFalse(v.MarkLocated("Nadie", 1f));
            Assert.IsFalse(v.IsLocated("Nadie"));
        }

        [Test]
        public void BuildingsKeepInsertionOrderAndRepeatedBuildingThrows()
        {
            var v = Tour();
            CollectionAssert.AreEqual(new[] { "CienciasBasicas", "X1" }, v.Buildings.ToArray());
            Assert.Throws<ArgumentException>(() => v.AddBuilding("X1"));
        }
    }
}
