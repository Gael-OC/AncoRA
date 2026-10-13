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
        Relocked,
        Tilted
    }

    /// <summary>The building box in its XR Space frame (centre, turn around the vertical, size): where the filter measures.</summary>
    public readonly struct PaseoBoxGeometry
    {
        public readonly Vector3 Center;
        public readonly float Yaw;
        public readonly Vector3 Size;

        public PaseoBoxGeometry(Vector3 center, float yaw, Vector3 size)
        {
            Center = center;
            Yaw = yaw;
            Size = size;
        }
    }

    /// <summary>What the filter made of one localization, for the log and the team HUD.</summary>
    public readonly struct PaseoPoseReport
    {
        public readonly PaseoPoseVerdict Verdict;
        public readonly float Confidence;
        public readonly float TiltDegrees;
        // How far the box would move from the filtered pose (largest corner displacement); NaN before the first lock.
        public readonly float BoxMoveMeters;
        public readonly float CameraToBoxMeters;

        public PaseoPoseReport(PaseoPoseVerdict verdict, float confidence, float tiltDegrees, float boxMoveMeters, float cameraToBoxMeters)
        {
            Verdict = verdict;
            Confidence = confidence;
            TiltDegrees = tiltDegrees;
            BoxMoveMeters = boxMoveMeters;
            CameraToBoxMeters = cameraToBoxMeters;
        }

        /// <summary>The localization moved or confirmed the box (as opposed to waiting, an outlier or a tilted one).</summary>
        public bool Used => Verdict is PaseoPoseVerdict.Locked or PaseoPoseVerdict.Accepted or PaseoPoseVerdict.Relocked;
    }

    /// <summary>
    /// Steadies the pose an XR Space receives from Immersal. Every localization is a new, noisy estimate of where the map
    /// sits in the AR world; applied raw, the box jumps with each one. The filter shows nothing until two estimates agree,
    /// then follows the confidence-weighted average of the last accepted ones, drops estimates far from it (usually a
    /// wrong match on a plain facade), relocks when several rejected ones agree among themselves (the first lock was the
    /// wrong one), and slides to each new average instead of jumping.
    /// <para>
    /// Both the map and the AR world stand on gravity, so the true space pose only turns around the vertical: each
    /// estimate is levelled around the camera, and one tilted beyond <see cref="MaxTiltDegrees"/> is a bad match. Agreement
    /// is measured where the content is, as the largest move of a box corner, because the boxes sit 10–33 m from their map
    /// origins and a small turn there moves them metres. Pure logic: PaseoPoseProcessor feeds it inside the XR Space.
    /// </para>
    /// </summary>
    public sealed class PaseoPoseFilter
    {
        // First estimates, not measured on site: calibrate them with the UBICACION_PASEO log lines.
        public const float ConsensusMeters = 0.6f;
        public const float InlierMeters = 1f;
        public const float MaxTiltDegrees = 5f;
        // Confidence from which a localization weighs fully (the SDK's own filter uses the same cap).
        public const float FullConfidence = 50f;
        public const float SmoothSeconds = 0.5f;
        public const int Window = 6;
        public const int RelockCount = 3;
        const float MinWeight = 0.1f;

        readonly struct Estimate
        {
            public readonly Vector3 Position;
            public readonly float Yaw;
            public readonly float Weight;

            public Estimate(Vector3 position, float yaw, float weight)
            {
                Position = position;
                Yaw = yaw;
                Weight = weight;
            }
        }

        readonly List<Estimate> candidates = new(); // before the first lock
        readonly List<Estimate> window = new(); // accepted since the lock
        readonly List<Estimate> rejected = new(); // consecutive rejections
        readonly Vector3[] corners = new Vector3[8];
        PaseoBoxGeometry box;
        Estimate target;
        Estimate current;
        Estimate slideFrom;
        float slideElapsed;

        public PaseoPoseFilter() => Box = default;

        /// <summary>The box this space carries, in the space frame. Without one, the filter measures at the space origin.</summary>
        public PaseoBoxGeometry Box
        {
            get => box;
            set
            {
                box = value;
                var turn = Quaternion.Euler(0f, value.Yaw, 0f);
                var half = value.Size * 0.5f;
                for (int i = 0; i < corners.Length; i++)
                    corners[i] = value.Center + turn * new Vector3((i & 1) == 0 ? -half.x : half.x,
                        (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
            }
        }

        public bool HasPose { get; private set; }
        public Vector3 Position => current.Position;
        public Quaternion Rotation => Turn(current.Yaw);
        public Vector3 TargetPosition => target.Position;
        public Quaternion TargetRotation => Turn(target.Yaw);

        /// <param name="position">Space position in the AR world from this localization.</param>
        /// <param name="rotation">Space rotation from this localization, tilt included.</param>
        /// <param name="cameraPosition">Camera position in the AR world when the image was taken (the levelling pivot).</param>
        /// <param name="confidence">Immersal's confidence for this localization.</param>
        public PaseoPoseReport Add(Vector3 position, Quaternion rotation, Vector3 cameraPosition, float confidence)
        {
            float tilt = Vector3.Angle(rotation * Vector3.up, Vector3.up);
            var estimate = Level(position, rotation, cameraPosition, Mathf.Clamp(confidence / FullConfidence, MinWeight, 1f));
            float move = HasPose ? BoxMove(estimate, target) : float.NaN;
            float cameraToBox = Vector3.Distance(cameraPosition, estimate.Position + Turn(estimate.Yaw) * box.Center);
            return new PaseoPoseReport(tilt > MaxTiltDegrees ? PaseoPoseVerdict.Tilted : Judge(estimate), confidence, tilt, move, cameraToBox);
        }

        PaseoPoseVerdict Judge(Estimate estimate)
        {
            if (!HasPose)
            {
                // Lock on the newest estimate together with every earlier one that agrees with it.
                var agreeing = candidates.Where(c => BoxMove(c, estimate) <= ConsensusMeters).ToList();
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

            if (BoxMove(estimate, target) <= InlierMeters)
            {
                rejected.Clear();
                window.Add(estimate);
                if (window.Count > Window)
                    window.RemoveAt(0);
                target = Average(window);
                slideFrom = current;
                slideElapsed = 0f;
                return PaseoPoseVerdict.Accepted;
            }

            if (rejected.Any(r => BoxMove(r, estimate) > ConsensusMeters))
                rejected.Clear();
            rejected.Add(estimate);
            if (rejected.Count < RelockCount)
                return PaseoPoseVerdict.Rejected;
            LockOn(rejected.ToList());
            return PaseoPoseVerdict.Relocked;
        }

        /// <summary>Advances the slide towards the current average. The box centre travels straight; the space turns around it.</summary>
        public void Step(float deltaSeconds)
        {
            if (!HasPose)
                return;
            slideElapsed += deltaSeconds;
            float t = Mathf.Clamp01(slideElapsed / SmoothSeconds);
            float eased = t * t * (3f - 2f * t);
            var centre = Vector3.Lerp(BoxCentre(slideFrom), BoxCentre(target), eased);
            float yaw = Mathf.LerpAngle(slideFrom.Yaw, target.Yaw, eased);
            current = new Estimate(centre - Turn(yaw) * box.Center, yaw, 1f);
        }

        /// <summary>Forgets everything, e.g. after AR tracking was lost and the world frame may have shifted.</summary>
        public void Reset()
        {
            candidates.Clear();
            window.Clear();
            rejected.Clear();
            HasPose = false;
            target = current = slideFrom = default;
        }

        // A lock (first one or relock) places the box at once: sliding there from somewhere else would sweep it across
        // the scene.
        void LockOn(List<Estimate> estimates)
        {
            candidates.Clear();
            rejected.Clear();
            window.Clear();
            window.AddRange(estimates.Skip(Mathf.Max(0, estimates.Count - Window)));
            target = current = slideFrom = Average(window);
            slideElapsed = SmoothSeconds;
            HasPose = true;
        }

        // Keeps the camera where both frames put it and drops the tilt: the level rotation turns around the vertical only.
        static Estimate Level(Vector3 position, Quaternion rotation, Vector3 cameraPosition, float weight)
        {
            var forward = rotation * Vector3.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            var cameraInSpace = Quaternion.Inverse(rotation) * (cameraPosition - position);
            return new Estimate(cameraPosition - Turn(yaw) * cameraInSpace, yaw, weight);
        }

        // Weighted average taken at the box: average its centre and its turn, then put the space back around it.
        Estimate Average(List<Estimate> estimates)
        {
            float reference = estimates[0].Yaw;
            var centre = Vector3.zero;
            float offset = 0f, total = 0f;
            foreach (var e in estimates)
            {
                centre += BoxCentre(e) * e.Weight;
                offset += Mathf.DeltaAngle(reference, e.Yaw) * e.Weight;
                total += e.Weight;
            }
            centre /= total;
            float yaw = reference + offset / total;
            return new Estimate(centre - Turn(yaw) * box.Center, yaw, 1f);
        }

        Vector3 BoxCentre(Estimate e) => e.Position + Turn(e.Yaw) * box.Center;

        // How far the box moves between two space poses: the largest displacement of its corners.
        float BoxMove(Estimate a, Estimate b)
        {
            var turnA = Turn(a.Yaw);
            var turnB = Turn(b.Yaw);
            float largest = 0f;
            foreach (var corner in corners)
                largest = Mathf.Max(largest, Vector3.Distance(a.Position + turnA * corner, b.Position + turnB * corner));
            return largest;
        }

        static Quaternion Turn(float yaw) => Quaternion.Euler(0f, yaw, 0f);
    }
}
