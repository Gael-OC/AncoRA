using System;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>Why the camera heading is, or is not, usable right now.</summary>
    public enum PaseoHeadingState
    {
        // Off first: a default PaseoHeading means «no heading».
        Off,
        NoData,
        Stale,
        FieldStrength,
        WeakHorizontalField,
        CameraVertical,
        Valid
    }

    /// <summary>
    /// The direction the BACK CAMERA looks in, as a compass bearing (0 = north, clockwise). Not Unity's
    /// <c>Input.compass.trueHeading</c>: that is the heading of the device's top edge, and with the phone held upright to film
    /// a building the top edge points at the sky.
    /// </summary>
    public readonly struct PaseoHeading
    {
        public readonly PaseoHeadingState State;
        /// <summary>Degrees in [0, 360); meaningful only when <see cref="Valid"/>.</summary>
        public readonly float Degrees;
        /// <summary>Strength of the geomagnetic field in µT; NaN when the sensor gave nothing, meaningless when <see cref="State"/> is Off.</summary>
        public readonly float FieldMicroTesla;
        /// <summary>Input.compass.headingAccuracy (degrees; negative or NaN when the phone does not say).</summary>
        public readonly float AccuracyDegrees;

        public PaseoHeading(PaseoHeadingState state, float degrees, float fieldMicroTesla, float accuracyDegrees)
        {
            State = state;
            Degrees = degrees;
            FieldMicroTesla = fieldMicroTesla;
            AccuracyDegrees = accuracyDegrees;
        }

        public bool Valid => State == PaseoHeadingState.Valid;

        /// <summary>No heading (compass off, no data, or the team turned the ranking by compass off).</summary>
        public static PaseoHeading None => default;

        /// <summary>A usable heading, e.g. for tests.</summary>
        public static PaseoHeading Of(double degrees) =>
            new(PaseoHeadingState.Valid, (float)PaseoGeo.NormalizeDegrees(degrees), float.NaN, float.NaN);

        /// <summary>Why there is no heading, for the team sheet.</summary>
        public static string Describe(PaseoHeadingState state) => state switch
        {
            PaseoHeadingState.Valid => "sirve",
            PaseoHeadingState.NoData => "sin lecturas",
            PaseoHeadingState.Stale => "lecturas detenidas",
            PaseoHeadingState.FieldStrength => "campo magnético fuera de rango",
            PaseoHeadingState.WeakHorizontalField => "campo horizontal débil",
            PaseoHeadingState.CameraVertical => "cámara casi vertical",
            _ => "apagada"
        };

        /// <summary>The state as the DEPURACION_POSE line writes it.</summary>
        public static string StateName(PaseoHeadingState state) => state switch
        {
            PaseoHeadingState.Valid => "valido",
            PaseoHeadingState.NoData => "sin-datos",
            PaseoHeadingState.Stale => "desactualizada",
            PaseoHeadingState.FieldStrength => "campo-fuera-de-rango",
            PaseoHeadingState.WeakHorizontalField => "campo-horizontal-debil",
            PaseoHeadingState.CameraVertical => "camara-vertical",
            _ => "apagada"
        };
    }

    /// <summary>
    /// The heading of the back camera from the raw sensors, in the device frame of Android (right-handed, portrait): x to the
    /// right of the screen, y towards the top edge, z out of the screen towards the user; the back camera looks along −z.
    /// Unity's <c>Input.acceleration</c> is the gravity direction in that frame (face up on a table it reads about (0, 0, −1)),
    /// and <c>Input.compass.rawVector</c> is the geomagnetic field in µT. With up U = −normalize(acceleration), the
    /// horizontal north N = normalize(m − (m·U)U) and east E = N × U (N = +y, U = +z gives E = +x), the heading of a device
    /// direction v is atan2(v·E, v·N). None of this is confirmed on a phone yet: the DEPURACION_POSE line logs the raw inputs.
    /// </summary>
    public static class PaseoCompassMath
    {
        /// <summary>The back camera's view direction in the device frame.</summary>
        public static readonly Vector3 CameraView = new(0f, 0f, -1f);

        // The Earth's field is 25-65 µT; Santiago and La Serena are about 23-25 µT. Outside 15-80 µT the phone is next to
        // something magnetic (or the sensor is uncalibrated).
        public const float MinFieldMicroTesla = 15f;
        public const float MaxFieldMicroTesla = 80f;
        // Near a magnetic pole the field is vertical and gives no heading; the horizontal part must be this large.
        public const float MinHorizontalFieldMicroTesla = 5f;
        // The heading of a camera that looks straight up or down is not defined.
        public const float MinDegreesFromVertical = 20f;
        // g: a phone in free fall (or an unwritten sensor) has no gravity direction.
        const float MinGravity = 0.5f;

        /// <summary>
        /// The heading from one reading of the sensors, without smoothing. Invalid (with the reason) when the field is
        /// out of range, its horizontal part too small, the camera within <see cref="MinDegreesFromVertical"/> of vertical,
        /// or there is no gravity.
        /// </summary>
        public static PaseoHeading Instant(Vector3 acceleration, Vector3 field, Vector3 view, float accuracyDegrees = float.NaN)
        {
            float gravity = acceleration.magnitude;
            float strength = field.magnitude;
            if (!IsFinite(gravity) || !IsFinite(strength) || gravity < MinGravity)
                return new PaseoHeading(PaseoHeadingState.NoData, 0f, IsFinite(strength) ? strength : float.NaN, accuracyDegrees);
            if (strength < MinFieldMicroTesla || strength > MaxFieldMicroTesla)
                return new PaseoHeading(PaseoHeadingState.FieldStrength, 0f, strength, accuracyDegrees);

            var up = -acceleration / gravity;
            var horizontal = field - Vector3.Dot(field, up) * up;
            if (horizontal.magnitude < MinHorizontalFieldMicroTesla)
                return new PaseoHeading(PaseoHeadingState.WeakHorizontalField, 0f, strength, accuracyDegrees);

            var north = horizontal.normalized;
            var east = Vector3.Cross(north, up);
            var direction = view.normalized;
            if (Mathf.Abs(Vector3.Dot(direction, up)) > Mathf.Cos(MinDegreesFromVertical * Mathf.Deg2Rad))
                return new PaseoHeading(PaseoHeadingState.CameraVertical, 0f, strength, accuracyDegrees);

            double degrees = Math.Atan2(Vector3.Dot(direction, east), Vector3.Dot(direction, north)) * 180.0 / Math.PI;
            return new PaseoHeading(PaseoHeadingState.Valid, (float)PaseoGeo.NormalizeDegrees(degrees), strength, accuracyDegrees);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Turns the compass and accelerometer readings of every frame into a steady heading of the back camera
    /// (<see cref="PaseoCompassMath"/>): the gravity direction is low-passed over about 0.3 s and the heading is averaged
    /// as unit vectors over about 0.5 s, so it does not jump at 359° → 1°. It is only valid while the compass is on and its
    /// timestamp keeps advancing.
    /// </summary>
    public sealed class PaseoCompass
    {
        public const float AccelerationSeconds = 0.3f;
        public const float HeadingSeconds = 0.5f;
        // A compass whose timestamp has not moved for this long is not delivering readings.
        public const float StaleSeconds = 1f;
        // After a gap this long (the app was paused) the smoothing starts again.
        const float GapSeconds = 1f;

        Vector3 acceleration;
        bool hasAcceleration;
        bool hasHeading;
        float headingSin;
        float headingCos;
        float lastTime = float.NaN;
        double lastStamp = double.NaN;
        float stampChangedAt = float.NegativeInfinity;

        /// <param name="now">Seconds of a steady clock.</param>
        /// <param name="enabled">Input.compass.enabled.</param>
        /// <param name="accelerationG">Input.acceleration.</param>
        /// <param name="field">Input.compass.rawVector, µT.</param>
        /// <param name="timestamp">Input.compass.timestamp.</param>
        /// <param name="accuracyDegrees">Input.compass.headingAccuracy.</param>
        public PaseoHeading Update(float now, bool enabled, Vector3 accelerationG, Vector3 field, double timestamp, float accuracyDegrees)
        {
            float dt = float.IsNaN(lastTime) ? 0f : Mathf.Max(0f, now - lastTime);
            lastTime = now;
            bool gap = dt > GapSeconds;
            if (!enabled)
            {
                Reset();
                return PaseoHeading.None;
            }
            if (gap)
                hasHeading = false;

            if (!hasAcceleration || gap)
                acceleration = accelerationG;
            else
                acceleration = Vector3.Lerp(acceleration, accelerationG, Blend(dt, AccelerationSeconds));
            hasAcceleration = true;

            if (timestamp > 0.0 && timestamp != lastStamp)
            {
                lastStamp = timestamp;
                stampChangedAt = now;
            }
            if (float.IsNegativeInfinity(stampChangedAt))
                return Drop(PaseoHeadingState.NoData, field, accuracyDegrees);
            if (now - stampChangedAt > StaleSeconds)
                return Drop(PaseoHeadingState.Stale, field, accuracyDegrees);

            var instant = PaseoCompassMath.Instant(acceleration, field, PaseoCompassMath.CameraView, accuracyDegrees);
            if (!instant.Valid)
            {
                hasHeading = false;
                return instant;
            }

            float radians = instant.Degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            if (!hasHeading)
            {
                headingSin = sin;
                headingCos = cos;
                hasHeading = true;
            }
            else
            {
                float blend = Blend(dt, HeadingSeconds);
                headingSin += (sin - headingSin) * blend;
                headingCos += (cos - headingCos) * blend;
            }
            double degrees = Math.Atan2(headingSin, headingCos) * 180.0 / Math.PI;
            return new PaseoHeading(PaseoHeadingState.Valid, (float)PaseoGeo.NormalizeDegrees(degrees), instant.FieldMicroTesla, accuracyDegrees);
        }

        void Reset()
        {
            hasAcceleration = false;
            hasHeading = false;
            lastStamp = double.NaN;
            stampChangedAt = float.NegativeInfinity;
        }

        PaseoHeading Drop(PaseoHeadingState state, Vector3 field, float accuracyDegrees)
        {
            hasHeading = false;
            float strength = field.magnitude;
            return new PaseoHeading(state, 0f, float.IsNaN(strength) || float.IsInfinity(strength) ? float.NaN : strength, accuracyDegrees);
        }

        // Share of the way to the new sample after dt seconds of an exponential low-pass with the given time constant.
        static float Blend(float dt, float seconds) => 1f - Mathf.Exp(-dt / seconds);
    }
}
