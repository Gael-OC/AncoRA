using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoParkPathTests
    {
        [Test]
        public void LengthIsPerimeter() => Assert.AreEqual(2f * (8f + 4f), new PaseoParkPath(4f, 2f).Length, 1e-4f);

        [Test]
        public void IsPeriodic()
        {
            var path = new PaseoParkPath(4f, 2f);
            foreach (float d in new[] { 0f, 1.3f, 5.5f, 9.9f })
            {
                path.Sample(d, out var a, out float ya);
                path.Sample(d + path.Length, out var b, out float yb);
                path.Sample(d - 2f * path.Length, out var c, out float yc);
                Assert.AreEqual(0f, (a - b).magnitude, 1e-3f);
                Assert.AreEqual(0f, (a - c).magnitude, 1e-3f);
                Assert.AreEqual(ya, yb, 1e-3f);
                Assert.AreEqual(ya, yc, 1e-3f);
            }
        }

        [Test]
        public void PositionIsContinuousAndOnTheLoop()
        {
            var path = new PaseoParkPath(4f, 2f);
            path.Sample(0f, out var prev, out _);
            for (float d = 0.05f; d < path.Length + 0.1f; d += 0.05f)
            {
                path.Sample(d, out var p, out _);
                Assert.LessOrEqual((p - prev).magnitude, 0.0501f + 1e-3f, $"salto en {d}");
                Assert.IsTrue(Mathf.Approximately(Mathf.Abs(p.x), 4f) || Mathf.Approximately(Mathf.Abs(p.y), 2f));
                prev = p;
            }
        }

        [Test]
        public void YawIsTravelDirection()
        {
            var path = new PaseoParkPath(4f, 2f);
            path.Sample(1f, out _, out float y1);   // first side, +X
            path.Sample(8.5f, out _, out float y2); // second side, +Z
            path.Sample(13f, out _, out float y3);  // third side, -X
            path.Sample(21f, out _, out float y4);  // fourth side, -Z
            Assert.AreEqual(90f, y1);
            Assert.AreEqual(0f, y2);
            Assert.AreEqual(270f, y3);
            Assert.AreEqual(180f, y4);
        }

        [Test]
        public void YawMatchesMovement()
        {
            var path = new PaseoParkPath(4f, 2f);
            for (float d = 0.5f; d < path.Length; d += 1.7f)
            {
                path.Sample(d, out var p, out float yaw);
                path.Sample(d + 0.01f, out var q, out _);
                var dir = (q - p).normalized;
                Assert.AreEqual(Mathf.Sin(yaw * Mathf.Deg2Rad), dir.x, 1e-3f);
                Assert.AreEqual(Mathf.Cos(yaw * Mathf.Deg2Rad), dir.y, 1e-3f);
            }
        }

        [Test]
        public void RoofIsInsetAndClamped()
        {
            var p = PaseoParkPath.Roof(new Vector3(20f, 8f, 12f), 1.2f);
            Assert.AreEqual(8.8f, p.HalfX, 1e-4f);
            Assert.AreEqual(4.8f, p.HalfZ, 1e-4f);
            var tiny = PaseoParkPath.Roof(new Vector3(1f, 8f, 1f), 1.2f);
            Assert.AreEqual(0.5f, tiny.HalfX);
            Assert.AreEqual(0.5f, tiny.HalfZ);
        }

        [Test]
        public void GroundIsOutset()
        {
            var p = PaseoParkPath.Ground(new Vector3(20f, 8f, 12f), 2f);
            Assert.AreEqual(12f, p.HalfX, 1e-4f);
            Assert.AreEqual(8f, p.HalfZ, 1e-4f);
        }
    }
}
