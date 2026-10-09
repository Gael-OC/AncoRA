using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AncorRA.AR
{
    public enum PaseoPoseVerdict
    {
        Waiting,
        Locked,
        Accepted,
        Rejected,
        Relocked
    }

    /// <summary>
    /// Steadies the pose an XR Space receives from Immersal. Every localization is a new, noisy estimate of where the map
    /// sits in the AR world; applied raw, the box jumps with each one. The filter shows nothing until two estimates agree,
    /// then follows the average of the last accepted ones, drops estimates far from it (usually a wrong match on a plain
    /// facade), relocks when several rejected ones agree among themselves (the first lock was the wrong one), and slides
    /// to each new average instead of jumping. Pure logic: PaseoPoseProcessor feeds it inside the XR Space.
    /// </summary>
    public sealed class PaseoPoseFilter
    {
        public const float ConsensusMeters = 0.6f;
        public const float ConsensusDegrees = 3f;
        public const float InlierMeters = 1f;
        public const float InlierDegrees = 5f;
        public const float SmoothSeconds = 0.5f;
        public const int Window = 6;
        public const int RelockCount = 3;

        readonly struct Estimate
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public Estimate(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
        }

        readonly List<Estimate> candidates = new(); // before the first lock
        readonly List<Estimate> window = new(); // accepted since the lock
        readonly List<Estimate> rejected = new(); // consecutive rejections
        Vector3 slideFromPosition;
        Quaternion slideFromRotation = Quaternion.identity;
        float slideElapsed;

        public bool HasPose { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public Vector3 TargetPosition { get; private set; }
        public Quaternion TargetRotation { get; private set; } = Quaternion.identity;

        public PaseoPoseVerdict Add(Vector3 position, Quaternion rotation)
        {
            var estimate = new Estimate(position, rotation.normalized);
            if (!HasPose)
            {
                // Lock on the newest estimate together with every earlier one that agrees with it.
                var agreeing = candidates.Where(c => Agree(c, estimate, ConsensusMeters, ConsensusDegrees)).ToList();
                candidates.Add(estimate);
                if (agreeing.Count == 0)
                {
                    if (candidates.Count > Window)
                        candidates.RemoveAt(0);
                    return PaseoPoseVerdict.Waiting;
                }
                agreeing.Add(estimate);
                LockOn(agreeing);
                return PaseoPoseVerdict.Locked;
            }

            if (Agree(estimate, new Estimate(TargetPosition, TargetRotation), InlierMeters, InlierDegrees))
            {
                rejected.Clear();
                window.Add(estimate);
                if (window.Count > Window)
                    window.RemoveAt(0);
                SetTarget(Average(window));
                slideFromPosition = Position;
                slideFromRotation = Rotation;
                slideElapsed = 0f;
                return PaseoPoseVerdict.Accepted;
            }

            if (rejected.Any(r => !Agree(r, estimate, ConsensusMeters, ConsensusDegrees)))
                rejected.Clear();
            rejected.Add(estimate);
            if (rejected.Count < RelockCount)
                return PaseoPoseVerdict.Rejected;
            LockOn(rejected.ToList());
            return PaseoPoseVerdict.Relocked;
        }

        /// <summary>Advances the slide towards the current average.</summary>
        public void Step(float deltaSeconds)
        {
            if (!HasPose)
                return;
            slideElapsed += deltaSeconds;
            float t = Mathf.Clamp01(slideElapsed / SmoothSeconds);
            float eased = t * t * (3f - 2f * t);
            Position = Vector3.Lerp(slideFromPosition, TargetPosition, eased);
            Rotation = Quaternion.Slerp(slideFromRotation, TargetRotation, eased);
        }

        /// <summary>Forgets everything, e.g. after AR tracking was lost and the world frame may have shifted.</summary>
        public void Reset()
        {
            candidates.Clear();
            window.Clear();
            rejected.Clear();
            HasPose = false;
            Position = Vector3.zero;
            Rotation = Quaternion.identity;
            TargetPosition = Vector3.zero;
            TargetRotation = Quaternion.identity;
        }

        // A lock (first one or relock) places the box at once: sliding there from somewhere else would sweep it across
        // the scene.
        void LockOn(List<Estimate> estimates)
        {
            candidates.Clear();
            rejected.Clear();
            window.Clear();
            window.AddRange(estimates.Skip(Mathf.Max(0, estimates.Count - Window)));
            SetTarget(Average(window));
            Position = TargetPosition;
            Rotation = TargetRotation;
            slideFromPosition = Position;
            slideFromRotation = Rotation;
            slideElapsed = SmoothSeconds;
            HasPose = true;
        }

        void SetTarget(Estimate estimate)
        {
            TargetPosition = estimate.Position;
            TargetRotation = estimate.Rotation;
        }

        static bool Agree(Estimate a, Estimate b, float meters, float degrees) =>
            Vector3.Distance(a.Position, b.Position) <= meters && Quaternion.Angle(a.Rotation, b.Rotation) <= degrees;

        // Estimates are close to each other, so a normalised sum of sign-aligned quaternions is a good average.
        static Estimate Average(List<Estimate> estimates)
        {
            var position = Vector3.zero;
            var sum = Vector4.zero;
            var first = estimates[0].Rotation;
            foreach (var e in estimates)
            {
                position += e.Position;
                var q = e.Rotation;
                if (Quaternion.Dot(first, q) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                sum += new Vector4(q.x, q.y, q.z, q.w);
            }
            sum.Normalize();
            return new Estimate(position / estimates.Count, new Quaternion(sum.x, sum.y, sum.z, sum.w));
        }
    }
}
