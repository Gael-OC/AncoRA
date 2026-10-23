using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoCompassTests
    {
        // Santiago to La Serena: about 24 µT, pointing north and 25° up (southern hemisphere).
        const float FieldMicroTesla = 24f;
        const float InclinationDegrees = 25f;

        /// <summary>
        /// The sensors of a phone held upright (portrait) whose back camera looks at <paramref name="headingDegrees"/> (0 = north,
        /// clockwise) and is tilted <paramref name="downDegrees"/> below the horizon. The device frame is built from the world
        /// (x east, y north, z up) with its own right-handed basis, not with the formulas under test: x to the right of the
        /// heading, z out of the screen (away from the camera), y = z × x. Unity's acceleration is the gravity direction in g.
        /// </summary>
        static void Sensors(float headingDegrees, float downDegrees, float fieldScale, out Vector3 acceleration, out Vector3 field)
        {
            float h = headingDegrees * Mathf.Deg2Rad, p = downDegrees * Mathf.Deg2Rad;
            var look = new Vector3(Mathf.Sin(h) * Mathf.Cos(p), Mathf.Cos(h) * Mathf.Cos(p), -Mathf.Sin(p));
            var z = -look;
            var x = new Vector3(Mathf.Cos(h), -Mathf.Sin(h), 0f);
            var y = Vector3.Cross(z, x);
            Vector3 ToDevice(Vector3 world) => new(Vector3.Dot(world, x), Vector3.Dot(world, y), Vector3.Dot(world, z));

            float i = InclinationDegrees * Mathf.Deg2Rad;
            acceleration = ToDevice(new Vector3(0f, 0f, -1f));
            field = ToDevice(new Vector3(0f, Mathf.Cos(i), Mathf.Sin(i)) * (FieldMicroTesla * fieldScale));
        }

        static PaseoHeading Instant(float headingDegrees, float downDegrees = 0f, float fieldScale = 1f)
        {
            Sensors(headingDegrees, downDegrees, fieldScale, out var acceleration, out var field);
            return PaseoCompassMath.Instant(acceleration, field, PaseoCompassMath.CameraView);
        }

        [Test]
        public void TheDeviceFrameBuiltForTheTestsMatchesUnitysConventions()
        {
            // Held upright Unity reads (0, -1, 0); the back camera looks along -z.
            Sensors(0f, 0f, 1f, out var acceleration, out _);
            Assert.AreEqual(0f, acceleration.x, 1e-5f);
            Assert.AreEqual(-1f, acceleration.y, 1e-5f);
            Assert.AreEqual(0f, acceleration.z, 1e-5f);
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(270f)]
        public void AnUprightPhoneFacingACardinalDirectionReadsThatDirection(float heading)
        {
            var result = Instant(heading);
            Assert.IsTrue(result.Valid);
            Assert.AreEqual(0.0, PaseoGeo.AngleDifferenceDegrees(result.Degrees, heading), 0.01);
            Assert.AreEqual(FieldMicroTesla, result.FieldMicroTesla, 0.01f);
        }

        [Test]
        public void ThePhoneFacingNorthWithTheAxesWrittenOutByHand()
        {
            // Facing north, upright: x east, y up, z south (towards the user). Gravity (0, -1, 0); the field is north (-z) and up (+y).
            float i = InclinationDegrees * Mathf.Deg2Rad;
            var field = new Vector3(0f, Mathf.Sin(i), -Mathf.Cos(i)) * FieldMicroTesla;
            var north = PaseoCompassMath.Instant(new Vector3(0f, -1f, 0f), field, PaseoCompassMath.CameraView);
            Assert.AreEqual(0.0, PaseoGeo.AngleDifferenceDegrees(north.Degrees, 0.0), 0.01);

            // Facing east, upright: x south, y up, z west. North is now -x.
            field = new Vector3(-Mathf.Cos(i), Mathf.Sin(i), 0f) * FieldMicroTesla;
            var east = PaseoCompassMath.Instant(new Vector3(0f, -1f, 0f), field, PaseoCompassMath.CameraView);
            Assert.AreEqual(90.0, east.Degrees, 0.01);
        }

        [Test]
        public void TheHeadingFollowsTheCameraThroughEveryTurnAndTilt([Values(0f, 37f, 90f, 143f, 180f, 222f, 270f, 359f)] float heading,
            [Values(-30f, 0f, 20f, 45f)] float down)
        {
            var result = Instant(heading, down);
            Assert.IsTrue(result.Valid, $"heading {heading}, down {down}: {result.State}");
            Assert.AreEqual(0.0, PaseoGeo.AngleDifferenceDegrees(result.Degrees, heading), 0.05);
        }

        [Test]
        public void TiltedTwentyDegreesDownTheHeadingIsTheSame()
        {
            var level = Instant(90f);
            var tilted = Instant(90f, 20f);
            Assert.IsTrue(tilted.Valid);
            Assert.AreEqual(level.Degrees, tilted.Degrees, 0.05f);
        }

        [Test]
        public void LyingFlatTheCameraLooksDownAndThereIsNoHeading()
        {
            // Face up on a table: gravity (0, 0, -1); the field is north (+y) and down... up (+z).
            float i = InclinationDegrees * Mathf.Deg2Rad;
            var field = new Vector3(0f, Mathf.Cos(i), Mathf.Sin(i)) * FieldMicroTesla;
            var result = PaseoCompassMath.Instant(new Vector3(0f, 0f, -1f), field, PaseoCompassMath.CameraView);
            Assert.IsFalse(result.Valid);
            Assert.AreEqual(PaseoHeadingState.CameraVertical, result.State);
            // The same going the other way: the camera straight up.
            result = PaseoCompassMath.Instant(new Vector3(0f, 0f, 1f), field, PaseoCompassMath.CameraView);
            Assert.AreEqual(PaseoHeadingState.CameraVertical, result.State);
        }

        [Test]
        public void TheCameraWithinTwentyDegreesOfVerticalHasNoHeadingAndBeyondItHas()
        {
            Assert.AreEqual(PaseoHeadingState.CameraVertical, Instant(45f, 75f).State, "15° from straight down");
            Assert.AreEqual(PaseoHeadingState.Valid, Instant(45f, 65f).State, "25° from straight down");
            Assert.AreEqual(PaseoHeadingState.Valid, Instant(45f, -65f).State, "25° from straight up");
            Assert.AreEqual(PaseoHeadingState.CameraVertical, Instant(45f, -80f).State, "10° from straight up");
        }

        [Test]
        public void AFieldOutsideFifteenToEightyMicroTeslaIsRefused()
        {
            var weak = Instant(90f, 0f, 8f / FieldMicroTesla);
            Assert.AreEqual(PaseoHeadingState.FieldStrength, weak.State);
            Assert.AreEqual(8f, weak.FieldMicroTesla, 0.01f);
            var strong = Instant(90f, 0f, 100f / FieldMicroTesla);
            Assert.AreEqual(PaseoHeadingState.FieldStrength, strong.State);
            Assert.AreEqual(100f, strong.FieldMicroTesla, 0.01f);
            Assert.IsTrue(Instant(90f, 0f, 16f / FieldMicroTesla).Valid);
            Assert.IsTrue(Instant(90f, 0f, 79f / FieldMicroTesla).Valid);
        }

        [Test]
        public void AFieldThatIsAlmostVerticalGivesNoNorth()
        {
            // Near a magnetic pole: 30 µT straight up leaves 0 µT horizontal.
            var upright = new Vector3(0f, -1f, 0f);
            var result = PaseoCompassMath.Instant(upright, new Vector3(0f, 30f, 0f), PaseoCompassMath.CameraView);
            Assert.AreEqual(PaseoHeadingState.WeakHorizontalField, result.State);
            // 4 µT of 30 µT horizontal is below the minimum; 6 µT is not.
            result = PaseoCompassMath.Instant(upright, new Vector3(0f, 29.7f, -4f), PaseoCompassMath.CameraView);
            Assert.AreEqual(PaseoHeadingState.WeakHorizontalField, result.State);
            result = PaseoCompassMath.Instant(upright, new Vector3(0f, 29.4f, -6f), PaseoCompassMath.CameraView);
            Assert.AreEqual(PaseoHeadingState.Valid, result.State);
        }

        [Test]
        public void WithoutGravityOrFieldThereIsNoData()
        {
            Assert.AreEqual(PaseoHeadingState.NoData, PaseoCompassMath.Instant(Vector3.zero, new Vector3(0f, 10f, -20f), PaseoCompassMath.CameraView).State);
            Assert.AreEqual(PaseoHeadingState.FieldStrength, PaseoCompassMath.Instant(new Vector3(0f, -1f, 0f), Vector3.zero, PaseoCompassMath.CameraView).State);
        }

        [Test]
        public void AHeadingIsNeverNegativeOrThreeHundredSixty()
        {
            for (float heading = 0f; heading < 360f; heading += 7.5f)
            {
                var result = Instant(heading);
                Assert.GreaterOrEqual(result.Degrees, 0f);
                Assert.Less(result.Degrees, 360f);
            }
        }

        // The tracker, fed once per frame at 60 Hz.
        sealed class Feed
        {
            public readonly PaseoCompass Compass = new();
            public float Now;
            public double Stamp = 1.0;
            public PaseoHeading Last;

            public PaseoHeading Run(float seconds, float heading, float down = 0f, bool enabled = true, bool newReadings = true, float fieldScale = 1f)
            {
                for (float end = Now + seconds; Now < end; Now += 1f / 60f)
                {
                    Sensors(heading, down, fieldScale, out var acceleration, out var field);
                    if (newReadings)
                        Stamp += 1.0 / 60.0;
                    Last = Compass.Update(Now, enabled, acceleration, field, Stamp, 15f);
                }
                return Last;
            }
        }

        [Test]
        public void TheTrackerGivesTheHeadingOfASteadyPhone()
        {
            var feed = new Feed();
            var heading = feed.Run(1f, 123f);
            Assert.IsTrue(heading.Valid);
            Assert.AreEqual(123f, heading.Degrees, 0.05f);
            Assert.AreEqual(15f, heading.AccuracyDegrees);
            Assert.AreEqual(FieldMicroTesla, heading.FieldMicroTesla, 0.01f);
        }

        [Test]
        public void TheHeadingWaitsForTheFirstReading()
        {
            var feed = new Feed { Stamp = 0.0 };
            var heading = feed.Run(0.5f, 90f, newReadings: false);
            Assert.AreEqual(PaseoHeadingState.NoData, heading.State, "a compass whose timestamp is still 0 has not delivered anything");
            feed.Stamp = 5.0;
            Assert.IsTrue(feed.Run(0.2f, 90f, newReadings: false).Valid);
        }

        [Test]
        public void ACompassWhoseTimestampStopsAdvancingGoesStale()
        {
            var feed = new Feed();
            Assert.IsTrue(feed.Run(0.5f, 90f).Valid);
            Assert.IsTrue(feed.Run(PaseoCompass.StaleSeconds * 0.8f, 90f, newReadings: false).Valid, "a short pause is not stale");
            Assert.AreEqual(PaseoHeadingState.Stale, feed.Run(0.5f, 90f, newReadings: false).State);
            Assert.IsTrue(feed.Run(0.1f, 90f).Valid, "readings come back");
        }

        [Test]
        public void ADisabledCompassHasNoHeading()
        {
            var feed = new Feed();
            Assert.IsTrue(feed.Run(0.5f, 45f).Valid);
            var off = feed.Run(0.2f, 45f, enabled: false);
            Assert.AreEqual(PaseoHeadingState.Off, off.State);
            Assert.IsFalse(off.Valid);
        }

        [Test]
        public void ABadFieldOrAVerticalCameraMakesTheTrackerInvalidAndItRecoversAfterwards()
        {
            var feed = new Feed();
            Assert.IsTrue(feed.Run(0.5f, 200f).Valid);
            Assert.AreEqual(PaseoHeadingState.FieldStrength, feed.Run(0.2f, 200f, fieldScale: 5f).State);
            var back = feed.Run(0.2f, 200f);
            Assert.IsTrue(back.Valid);
            Assert.AreEqual(200f, back.Degrees, 0.1f);
            Assert.AreEqual(PaseoHeadingState.CameraVertical, feed.Run(0.5f, 200f, down: 85f).State);
            Assert.IsTrue(feed.Run(0.2f, 200f).Valid);
        }

        [Test]
        public void TheHeadingIsSmoothedOverHalfASecondAndSettlesOnTheNewDirection()
        {
            var feed = new Feed();
            feed.Run(1f, 0f);
            // A sudden turn to 90°: the first frames stay near the old heading, a half second later it is a good part of the way, and
            // after three seconds it has arrived.
            var early = feed.Run(1f / 60f, 90f);
            Assert.Less(early.Degrees, 10f);
            var half = feed.Run(0.5f, 90f);
            Assert.Greater(half.Degrees, 45f);
            Assert.Less(half.Degrees, 90f);
            Assert.AreEqual(90f, feed.Run(3f, 90f).Degrees, 0.5f);
        }

        [Test]
        public void TheSmoothingGoesTheShortWayAcrossNorth()
        {
            var feed = new Feed();
            feed.Run(1f, 350f);
            // Averaging the numbers 350 and 10 would pass through 180; unit vectors pass through 0.
            for (int i = 0; i < 30; i++)
            {
                var heading = feed.Run(1f / 60f, 10f);
                Assert.IsTrue(heading.Degrees >= 350f || heading.Degrees <= 10.5f, $"frame {i}: {heading.Degrees}");
            }
        }

        [Test]
        public void ALongGapStartsTheSmoothingAgain()
        {
            var feed = new Feed();
            feed.Run(1f, 0f);
            feed.Now += 5f;
            // Paused for five seconds, then the phone faces 180: no sweep from the old heading.
            var heading = feed.Run(1f / 60f, 180f);
            Assert.AreEqual(180f, heading.Degrees, 0.5f);
        }

        [Test]
        public void TheGravityIsLowPassedSoAJoltDoesNotTurnTheHeading()
        {
            var feed = new Feed();
            feed.Run(1f, 90f);
            Sensors(90f, 0f, 1f, out var acceleration, out var field);
            // One frame of a hard jolt along the camera's axis tilts the gravity direction a lot: read raw, it moves the heading
            // by about 21°; through the low-pass it stays close.
            var jolt = new Vector3(0f, 0f, 1.5f);
            var raw = PaseoCompassMath.Instant(acceleration + jolt, field, PaseoCompassMath.CameraView);
            Assert.Greater(PaseoGeo.AngleDifferenceDegrees(raw.Degrees, 90.0), 5.0);
            var smoothed = feed.Compass.Update(feed.Now, true, acceleration + jolt, field, feed.Stamp + 0.01, 15f);
            Assert.IsTrue(smoothed.Valid);
            Assert.AreEqual(0.0, PaseoGeo.AngleDifferenceDegrees(smoothed.Degrees, 90.0), 5.0);
        }

        [Test]
        public void TheStateNamesAreWhatTheLogWrites()
        {
            Assert.AreEqual("valido", PaseoHeading.StateName(PaseoHeadingState.Valid));
            Assert.AreEqual("camara-vertical", PaseoHeading.StateName(PaseoHeadingState.CameraVertical));
            Assert.AreEqual("apagada", PaseoHeading.StateName(PaseoHeadingState.Off));
            Assert.IsFalse(PaseoHeading.None.Valid);
            Assert.AreEqual(PaseoHeadingState.Off, PaseoHeading.None.State);
            Assert.AreEqual(310.0, PaseoHeading.Of(-50.0).Degrees, 1e-3);
        }
    }
}
