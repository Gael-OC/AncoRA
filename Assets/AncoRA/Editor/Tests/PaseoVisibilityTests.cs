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
            v.AddMap(152192, "CienciasBasicas");
            v.AddMap(152196, "CienciasBasicas");
            v.AddMap(152198, "X1");
            return v;
        }

        [Test]
        public void NothingVisibleBeforeAnyLocalization()
        {
            var v = Tour();
            Assert.AreEqual(-1, v.VisibleMap("CienciasBasicas"));
            Assert.AreEqual(-1, v.VisibleMap("X1"));
        }

        [Test]
        public void LocatedMapBecomesVisible()
        {
            var v = Tour();
            Assert.IsTrue(v.MarkLocated(152198, 1f));
            Assert.AreEqual(152198, v.VisibleMap("X1"));
            Assert.AreEqual(-1, v.VisibleMap("CienciasBasicas"));
        }

        [Test]
        public void TwoBuildingsCanBeVisibleAtOnce()
        {
            var v = Tour();
            v.MarkLocated(152198, 1f);
            v.MarkLocated(152192, 2f);
            Assert.AreEqual(152198, v.VisibleMap("X1"));
            Assert.AreEqual(152192, v.VisibleMap("CienciasBasicas"));
        }

        [Test]
        public void LatestMapOfABuildingWins()
        {
            var v = Tour();
            v.MarkLocated(152192, 1f);
            v.MarkLocated(152196, 2f);
            Assert.AreEqual(152196, v.VisibleMap("CienciasBasicas"));
            v.MarkLocated(152192, 3f);
            Assert.AreEqual(152192, v.VisibleMap("CienciasBasicas"));
        }

        [Test]
        public void SameCycleTieGoesToTheOneMarkedLast()
        {
            var v = Tour();
            v.MarkLocated(152196, 5f);
            v.MarkLocated(152192, 5f);
            Assert.AreEqual(152192, v.VisibleMap("CienciasBasicas"));
        }

        [Test]
        public void LosingTrackingHidesEverythingUntilANewLocalization()
        {
            var v = Tour();
            v.MarkLocated(152192, 1f);
            v.MarkLocated(152198, 1f);
            v.LoseTracking();
            Assert.AreEqual(-1, v.VisibleMap("CienciasBasicas"));
            Assert.AreEqual(-1, v.VisibleMap("X1"));
            v.MarkLocated(152198, 2f);
            Assert.AreEqual(152198, v.VisibleMap("X1"));
            Assert.AreEqual(-1, v.VisibleMap("CienciasBasicas"), "a building does not come back on another map's localization");
        }

        [Test]
        public void HeldAfterTenSecondsWithoutANewSuccess()
        {
            var v = Tour();
            v.MarkLocated(152198, 1f);
            Assert.IsFalse(v.IsHeld(152198, 10.9f));
            Assert.IsTrue(v.IsHeld(152198, 11f));
            v.MarkLocated(152198, 11f);
            Assert.IsFalse(v.IsHeld(152198, 11.5f));
        }

        [Test]
        public void UnknownMapIsIgnored()
        {
            var v = Tour();
            Assert.IsFalse(v.MarkLocated(999, 1f));
            Assert.IsFalse(v.IsLocated(999));
        }

        [Test]
        public void BuildingsKeepInsertionOrderAndRepeatedMapThrows()
        {
            var v = Tour();
            CollectionAssert.AreEqual(new[] { "CienciasBasicas", "X1" }, v.Buildings.ToArray());
            CollectionAssert.AreEquivalent(new[] { 152192, 152196 }, v.MapsOf("CienciasBasicas").ToArray());
            Assert.Throws<ArgumentException>(() => v.AddMap(152198, "X1"));
        }
    }
}
