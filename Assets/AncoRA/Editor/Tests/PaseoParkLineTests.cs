using System.Linq;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoParkLineTests
    {
        static void AssertAt(PaseoParkLine line, float distance, Vector2 expected, float yaw)
        {
            line.Sample(distance, out var xz, out float heading);
            Assert.AreEqual(expected.x, xz.x, 1e-3f, $"x at {distance}");
            Assert.AreEqual(expected.y, xz.y, 1e-3f, $"z at {distance}");
            Assert.AreEqual(yaw, heading, 1e-3f, $"yaw at {distance}");
        }

        [Test]
        public void WalksThereAndBackAndTurnsRoundAtTheEnds()
        {
            var line = new PaseoParkLine(new Vector2(0f, 0f), new Vector2(0f, 10f));
            Assert.AreEqual(20f, line.Length, 1e-4f);
            AssertAt(line, 0f, new Vector2(0f, 0f), 0f);
            AssertAt(line, 4f, new Vector2(0f, 4f), 0f);
            AssertAt(line, 15f, new Vector2(0f, 5f), 180f);
            AssertAt(line, 22f, new Vector2(0f, 2f), 0f);
            AssertAt(line, -5f, new Vector2(0f, 5f), 180f);
        }

        [Test]
        public void TheHeadingFollowsTheLine()
        {
            AssertAt(new PaseoParkLine(Vector2.zero, new Vector2(10f, 0f)), 1f, new Vector2(1f, 0f), 90f);
            AssertAt(new PaseoParkLine(Vector2.zero, new Vector2(-10f, 0f)), 1f, new Vector2(-1f, 0f), 270f);
        }

        [Test]
        public void ADegenerateLineDoesNotBreak()
        {
            var line = new PaseoParkLine(Vector2.one, Vector2.one);
            line.Sample(3f, out var xz, out _);
            Assert.AreEqual(1f, xz.x, 1e-3f);
            Assert.AreEqual(1f, xz.y, 1e-3f);
        }

        [Test]
        public void TheCampusLineIsPutInTheFrameOfTheBox()
        {
            // The box turned 90° has its local +Z towards campus +X (east): a point 5 m east of its centre is 5 m ahead of it.
            var pose = new PaseoCampusPose { posicion = new[] { 10f, 3f, 20f }, giro = 90f };
            var park = new PaseoParkConfig { tema = "pasillo", desde = new[] { 15f, 20f }, hasta = new[] { 10f, 25f }, suelo = 1f };
            park.SetBoxLocalLine(pose);
            Assert.AreEqual(0f, park.localFrom.x, 1e-3f);
            Assert.AreEqual(-2f, park.localFrom.y, 1e-3f);
            Assert.AreEqual(5f, park.localFrom.z, 1e-3f);
            // Its local +X points south, so a point 5 m north of the centre is at local x = -5.
            Assert.AreEqual(-5f, park.localTo.x, 1e-3f);
            Assert.AreEqual(0f, park.localTo.z, 1e-3f);
        }

        [Test]
        public void PasilloIsOnlyWalkersOnTheLineSpreadOverItsCycle()
        {
            var park = new PaseoParkConfig
            {
                tema = "pasillo", personas = 4, localFrom = new Vector3(1f, -2f, 3f), localTo = new Vector3(4f, -2f, 9f)
            };
            var plan = PaseoParkLayout.Build(park, new Vector3(20f, 8f, 12f), "Teologia");
            Assert.AreEqual(4, plan.Actors.Count);
            Assert.AreEqual(0, plan.Props.Count);
            Assert.IsTrue(plan.Actors.All(a => a.Walks && a.Line && a.Surface == PaseoSurface.Suelo));
            Assert.IsTrue(plan.Actors.All(a => a.LineFrom == park.localFrom && a.LineTo == park.localTo));
            CollectionAssert.AreEqual(new[] { 0f, 0.25f, 0.5f, 0.75f }, plan.Actors.Select(a => a.PathStart01).ToArray());
        }
    }
}
