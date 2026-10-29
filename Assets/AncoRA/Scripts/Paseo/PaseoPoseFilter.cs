using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AncorRA.AR
{
    public enum PaseoPoseVerdict
    {
        Locked,
        Accepted,
        Rejected,
        Relocked,
        Tilted,
        // Held back, not used: a weak first estimate (below SingleLockConfidence) waiting for a second that agrees.
        Pending
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
        // Whether, after this localization, the box stands on two estimates that agree; the first one alone shows it.
        public readonly bool Confirmed;
        // The inlier gate this estimate was judged against, and how far (horizontally) its camera was from the camera of the
        // last estimate the filter used; both NaN before the first lock.
        public readonly float GateMeters;
        public readonly float TravelMeters;

        public PaseoPoseReport(PaseoPoseVerdict verdict, float confidence, float tiltDegrees, float boxMoveMeters, float cameraToBoxMeters,
            bool confirmed = false, float gateMeters = float.NaN, float travelMeters = float.NaN)
        {
            Verdict = verdict;
            Confidence = confidence;
            TiltDegrees = tiltDegrees;
            BoxMoveMeters = boxMoveMeters;
            CameraToBoxMeters = cameraToBoxMeters;
            Confirmed = confirmed;
            GateMeters = gateMeters;
            TravelMeters = travelMeters;
        }

        /// <summary>The localization moved or confirmed the box (as opposed to an outlier or a tilted one).</summary>
        public bool Used => Verdict is PaseoPoseVerdict.Locked or PaseoPoseVerdict.Accepted or PaseoPoseVerdict.Relocked;
    }

    /// <summary>
    /// Steadies the pose an XR Space receives from Immersal. Every localization is a new, noisy estimate of where the map
    /// sits in the AR world; applied raw, the box jumps with each one. The first estimate shows the box at once,
    /// unconfirmed; a second one that agrees confirms it. From there the filter follows the confidence-weighted average
    /// of the last accepted ones, drops estimates far from it (usually a wrong match on a plain facade), relocks when
    /// enough remembered rejected ones agree among themselves and outweigh the current pose, or one strong rejected one
    /// outweighs it by itself (the lock was the wrong one), and slides to each new average instead of jumping.
    /// <para>
    /// Relocking (field test 29-oct: a conf-24 lock stayed 2 m off while a conf-128 estimate and two agreeing ones were
    /// rejected, because one disagreeing rejection in between reset the count): the last <see cref="RejectedMemory"/>
    /// rejected estimates are remembered even when they are not consecutive. A new rejected one relocks if it and the
    /// remembered ones within <see cref="RelockAgreementMeters"/> of it number two (unconfirmed) or three (confirmed) and
    /// weigh more than the support of the current pose; or, alone, if its confidence reaches
    /// <see cref="StrongRelockConfidence"/> and its weight is <see cref="StrongRelockRatio"/> times that support.
    /// </para>
    /// <para>
    /// When a wrong first lock would be costly (campus mode, where one pose places every box) the filter can refuse to lock
    /// on a weak estimate alone (<see cref="SingleLockConfidence"/>): it holds it as pending and locks once a second one
    /// agrees with it. A strong first estimate still locks at once.
    /// </para>
    /// <para>
    /// ARCore's world drifts with the distance walked, so the filter also remembers where the camera stood for each
    /// estimate. The inlier gate widens with the horizontal distance from the camera of the last used estimate
    /// (<see cref="DriftPerMeter"/> per metre, up to <see cref="MaxInlierMeters"/>): after a long walk the true correction
    /// is larger and a correct localization must not be rejected as an outlier. The average forgets in space too:
    /// Immersal's error depends on the viewpoint, so what was estimated near where the user stands now matters most. Each
    /// estimate weighs less the farther its camera was from the newest one (<see cref="TravelDecayMeters"/>) and leaves the
    /// window beyond <see cref="WindowRadiusMeters"/>.
    /// </para>
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
        // The gate grows with the distance the camera walked since the last used estimate (ARCore drifts roughly 1–3 % of
        // the distance, plus yaw), up to a cap.
        public const float DriftPerMeter = 0.03f;
        public const float MaxInlierMeters = 2.5f;
        // Spatial forgetting: weight factor exp(-d / TravelDecayMeters) by camera distance to the newest estimate; beyond
        // WindowRadiusMeters an estimate leaves the window.
        public const float TravelDecayMeters = 8f;
        public const float WindowRadiusMeters = 25f;
        public const float MaxTiltDegrees = 5f;
        // Confidence from which a localization weighs fully (the SDK's own filter uses the same cap).
        public const float FullConfidence = 50f;
        public const float SmoothSeconds = 0.5f;
        public const int Window = 6;
        public const int RelockCount = 3;
        // Before confirmation the box rests on one estimate, which two that agree with each other outweigh.
        public const int UnconfirmedRelockCount = 2;
        // Rejected estimates remembered for relocking (not necessarily consecutive), and how close two must be to agree.
        public const int RejectedMemory = 6;
        public const float RelockAgreementMeters = 1f;
        // A single rejected estimate this confident relocks if it weighs this many times the support of the current pose.
        public const float StrongRelockConfidence = FullConfidence;
        public const float StrongRelockRatio = 2f;
        const float MinWeight = 0.1f;

        readonly struct Estimate
        {
            public readonly Vector3 Position;
            public readonly float Yaw;
            public readonly float Weight;
            // Camera position in the AR world when the localization was made (unused in derived estimates).
            public readonly Vector3 CameraPosition;
            // Immersal's raw confidence (0 in derived estimates).
            public readonly float Confidence;

            public Estimate(Vector3 position, float yaw, float weight, Vector3 cameraPosition = default, float confidence = 0f)
            {
                Confidence = confidence;
                Position = position;
                Yaw = yaw;
                Weight = weight;
                CameraPosition = cameraPosition;
            }
        }

        readonly List<Estimate> window = new(); // accepted since the lock
        readonly List<Estimate> rejected = new(); // last rejections since the lock or the last accepted one
        readonly List<Estimate> pending = new(); // weak estimates held before the first lock
        readonly Vector3[] corners = new Vector3[8];
        PaseoBoxGeometry box;
        Estimate target;
        Estimate current;
        Estimate slideFrom;
        float slideElapsed;
        Vector3 lastUsedCamera; // camera of the last locked, accepted or relocked estimate

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

        /// <summary>
        /// Confidence under which a first estimate cannot lock the filter by itself. 0 (the default) locks on any estimate;
        /// above it, a weaker one stays pending until a second estimate agrees with it (within <see cref="ConsensusMeters"/>
        /// at the box) and the two lock together, confirmed. Only applies while the filter has no pose.
        /// </summary>
        public float SingleLockConfidence { get; set; }

        public bool HasPose { get; private set; }
        public bool Confirmed { get; private set; }
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
            var estimate = Level(position, rotation, cameraPosition, Mathf.Clamp(confidence / FullConfidence, MinWeight, 1f), confidence);
            float move = HasPose ? BoxMove(estimate, target) : float.NaN;
            float travel = HasPose ? Horizontal(cameraPosition, lastUsedCamera) : float.NaN;
            float gate = HasPose ? GateFor(travel) : float.NaN;
            float cameraToBox = Vector3.Distance(cameraPosition, estimate.Position + Turn(estimate.Yaw) * box.Center);
            var verdict = tilt > MaxTiltDegrees ? PaseoPoseVerdict.Tilted : Judge(estimate, gate, confidence);
            return new PaseoPoseReport(verdict, confidence, tilt, move, cameraToBox, Confirmed, gate, travel);
        }

        /// <summary>How far an estimate may move the box and still count as an inlier after the camera walked this far.</summary>
        public static float GateFor(float travelMeters) => Mathf.Min(InlierMeters + DriftPerMeter * travelMeters, MaxInlierMeters);

        PaseoPoseVerdict Judge(Estimate estimate, float gate, float confidence)
        {
            if (!HasPose)
            {
                // Field test 14-oct: waiting for a second agreeing estimate kept the box hidden for tens of seconds.
                if (SingleLockConfidence > 0f && confidence < SingleLockConfidence)
                    return Hold(estimate);
                LockOn(new List<Estimate> { estimate });
                return PaseoPoseVerdict.Locked;
            }

            if (BoxMove(estimate, target) <= gate)
            {
                rejected.Clear();
                window.Add(estimate);
                if (window.Count > Window)
                    window.RemoveAt(0);
                window.RemoveAll(e => Horizontal(e.CameraPosition, estimate.CameraPosition) > WindowRadiusMeters);
                lastUsedCamera = estimate.CameraPosition;
                target = Average(window);
                slideFrom = current;
                slideElapsed = 0f;
                Confirmed = true;
                return PaseoPoseVerdict.Accepted;
            }

            // The agreeing group: this estimate plus the remembered rejected ones that sit near it at the box.
            var group = rejected.Where(r => BoxMove(r, estimate) <= RelockAgreementMeters).ToList();
            group.Add(estimate);
            float support = Support(estimate);
            bool enough = group.Count >= (Confirmed ? RelockCount : UnconfirmedRelockCount) && group.Sum(e => e.Weight) > support;
            bool strong = estimate.Confidence >= StrongRelockConfidence && estimate.Weight >= StrongRelockRatio * support;
            if (enough || strong)
            {
                LockOn(group);
                return PaseoPoseVerdict.Relocked;
            }

            rejected.Add(estimate);
            if (rejected.Count > RejectedMemory)
                rejected.RemoveAt(0);
            return PaseoPoseVerdict.Rejected;
        }

        // How much the current pose rests on: its window weights, faded by camera distance to the new estimate like Average.
        float Support(Estimate from)
        {
            float total = 0f;
            foreach (var e in window)
                total += e.Weight * Mathf.Exp(-Horizontal(e.CameraPosition, from.CameraPosition) / TravelDecayMeters);
            return total;
        }

        // A weak first estimate waits for company: a second that agrees with any held one locks them together (confirmed).
        // Held ones that never agree with anything are dropped oldest first.
        PaseoPoseVerdict Hold(Estimate estimate)
        {
            foreach (var held in pending)
                if (BoxMove(held, estimate) <= ConsensusMeters)
                {
                    LockOn(new List<Estimate> { held, estimate });
                    return PaseoPoseVerdict.Locked;
                }
            pending.Add(estimate);
            if (pending.Count > Window)
                pending.RemoveAt(0);
            return PaseoPoseVerdict.Pending;
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
            window.Clear();
            rejected.Clear();
            pending.Clear();
            HasPose = false;
            Confirmed = false;
            target = current = slideFrom = default;
            lastUsedCamera = default;
        }

        // A lock (first one or relock) places the box at once: sliding there from somewhere else would sweep it across
        // the scene. Several estimates that agree confirm it; a single one does not. Estimates that agree at the box stay
        // even if their cameras stood far apart: only the next accepted one trims the window by distance.
        void LockOn(List<Estimate> estimates)
        {
            rejected.Clear();
            pending.Clear();
            window.Clear();
            window.AddRange(estimates.Skip(Mathf.Max(0, estimates.Count - Window)));
            lastUsedCamera = estimates[estimates.Count - 1].CameraPosition;
            target = current = slideFrom = Average(window);
            slideElapsed = SmoothSeconds;
            HasPose = true;
            Confirmed = window.Count > 1;
        }

        // Keeps the camera where both frames put it and drops the tilt: the level rotation turns around the vertical only.
        static Estimate Level(Vector3 position, Quaternion rotation, Vector3 cameraPosition, float weight, float confidence)
        {
            var forward = rotation * Vector3.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            var cameraInSpace = Quaternion.Inverse(rotation) * (cameraPosition - position);
            return new Estimate(cameraPosition - Turn(yaw) * cameraInSpace, yaw, weight, cameraPosition, confidence);
        }

        // Weighted average taken at the box: average its centre and its turn, then put the space back around it. Each
        // estimate fades with the distance between its camera and the newest one's (the last in the list).
        Estimate Average(List<Estimate> estimates)
        {
            var newest = estimates[estimates.Count - 1];
            float reference = estimates[0].Yaw;
            var centre = Vector3.zero;
            float offset = 0f, total = 0f;
            foreach (var e in estimates)
            {
                float weight = e.Weight * Mathf.Exp(-Horizontal(e.CameraPosition, newest.CameraPosition) / TravelDecayMeters);
                centre += BoxCentre(e) * weight;
                offset += Mathf.DeltaAngle(reference, e.Yaw) * weight;
                total += weight;
            }
            if (total <= 0f)
                return new Estimate(newest.Position, newest.Yaw, 1f);
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

        // Distance on the ground plane: the camera's height changes as the user holds the phone and says nothing about travel.
        static float Horizontal(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static Quaternion Turn(float yaw) => Quaternion.Euler(0f, yaw, 0f);
    }
}
