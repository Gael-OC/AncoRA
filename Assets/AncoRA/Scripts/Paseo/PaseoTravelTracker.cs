using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Distance walked by the ARCore camera along a recording: the sum of the straight steps between the positions it is
    /// given. A step longer than <see cref="MaxStepMeters"/> is a tracking jump (relocalization), not walking: it is skipped
    /// and counted. The construction walk needs a real trajectory; a capture of 1–5 m is no use (CLAUDE.md).
    /// </summary>
    public sealed class PaseoTravelTracker
    {
        /// <summary>Walked less than this and the walk cannot build anything.</summary>
        public const float MinUsefulMeters = 5f;
        public const float DefaultMaxStepMeters = 2f;

        public PaseoTravelTracker(float maxStepMeters = DefaultMaxStepMeters) => MaxStepMeters = maxStepMeters;

        Vector3 last;
        bool hasLast;

        public float MaxStepMeters { get; }
        public double Meters { get; private set; }
        /// <summary>Steps skipped for being longer than the cap.</summary>
        public int Jumps { get; private set; }
        public bool Short => Meters < MinUsefulMeters;

        public void Add(Vector3 position)
        {
            if (hasLast)
            {
                float step = Vector3.Distance(last, position);
                if (step > MaxStepMeters)
                    Jumps++;
                else
                    Meters += step;
            }
            last = position;
            hasLast = true;
        }

        public void Reset()
        {
            Meters = 0;
            Jumps = 0;
            hasLast = false;
        }
    }
}
