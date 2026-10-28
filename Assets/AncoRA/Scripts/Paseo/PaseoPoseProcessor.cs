using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>What one map of the building contributed to the filter: localizations seen, used, and the last verdict.</summary>
    public readonly struct PaseoMapStats
    {
        public readonly int Seen;
        public readonly int Used;
        public readonly PaseoPoseReport? Last;

        public PaseoMapStats(int seen, int used, PaseoPoseReport? last)
        {
            Seen = seen;
            Used = used;
            Last = last;
        }
    }

    /// <summary>
    /// The only data processor of each tour XR Space (XRSpace.ProcessPoses on). It hands every localization, from any map
    /// of the space, to a PaseoPoseFilter, measured at the box of the building whose map produced it, and gives the space
    /// the filtered pose. Until the first localization it tells the space to stay still (Ignore), so the box does not
    /// sweep in from the world origin. Each localization leaves one UBICACION_PASEO log line to calibrate the filter.
    /// <para>
    /// A space holds the maps and the box of one building, or (campus mode) the maps and the four boxes of all of them.
    /// The processor tells them apart by counting the PaseoMapContent under its space: more than one is campus mode. There
    /// a wrong first lock would move every box at once, so a weak first localization waits for a second one that agrees
    /// (<see cref="CampusSingleLockConfidence"/>); the wrong matches between buildings seen in the field had confidence 12–15.
    /// </para>
    /// </summary>
    public sealed class PaseoPoseProcessor : MonoBehaviour, IDataProcessor<SceneUpdateData>
    {
        const string Tag = "[AncoRA Paseo]";
        /// <summary>Confidence from which a first localization locks the campus space by itself.</summary>
        public const float CampusSingleLockConfidence = 20f;

        readonly PaseoPoseFilter filter = new();
        readonly SceneUpdateData output = new() { Ignore = true };
        readonly Dictionary<int, PaseoMapStats> perMap = new();
        PaseoMapContent[] contents = System.Array.Empty<PaseoMapContent>();
        // The box the filter measures at: the one of the building whose map produced the last localization.
        PaseoMapContent measured;

        public bool HasPose => filter.HasPose;
        /// <summary>The box rests on two localizations that agree, not only on the first one.</summary>
        public bool Confirmed => filter.Confirmed;
        /// <summary>When <see cref="LastReport"/> was made (realtimeSinceStartup), to pair it with the attempt that found it.</summary>
        public float LastReportAt { get; private set; }
        /// <summary>Localizations the filter has seen, and how many of them it used (locked, accepted or relocked).</summary>
        public int Results { get; private set; }
        public int UsedResults { get; private set; }
        public PaseoPoseReport? LastReport { get; private set; }
        /// <summary>The map whose localization produced <see cref="LastReport"/>.</summary>
        public int LastMapId { get; private set; }

        /// <summary>What a map of this building contributed so far (false: none of its localizations arrived).</summary>
        public bool TryGetMapStats(int mapId, out PaseoMapStats stats) => perMap.TryGetValue(mapId, out stats);

        public Task<SceneUpdateData> ProcessData(SceneUpdateData data, DataProcessorTrigger trigger)
        {
            bool isNewData = trigger == DataProcessorTrigger.NewData && data != null && data != output;
            int mapId = isNewData && data.MapEntry?.Map != null ? data.MapEntry.Map.mapId : 0;
            filter.Box = BoxInSpace(isNewData, mapId);
            // More than one box under this space is campus mode.
            filter.SingleLockConfidence = contents.Length > 1 ? CampusSingleLockConfidence : 0f;
            if (isNewData)
            {
                var report = filter.Add(data.Pose.GetPosition(), data.Pose.rotation, data.TrackerSpace.GetPosition(), data.LocalizeInfo.confidence);
                Results++;
                if (report.Used)
                    UsedResults++;
                LastReport = report;
                LastReportAt = Time.realtimeSinceStartup;
                output.TrackerSpace = data.TrackerSpace;
                output.MapSpacePose = data.MapSpacePose;
                output.CameraData = data.CameraData;
                output.LocalizeInfo = data.LocalizeInfo;
                output.MapEntry = data.MapEntry;
                LastMapId = mapId;
                perMap.TryGetValue(mapId, out var before);
                perMap[mapId] = new PaseoMapStats(before.Seen + 1, before.Used + (report.Used ? 1 : 0), report);
                Debug.Log(LogLine(mapId, report));
                if (PaseoDebug.Enabled && filter.HasPose)
                {
                    // Box centre in the AR world: from this localization alone, and where the filter takes it.
                    var box = filter.Box;
                    var estimated = data.Pose.MultiplyPoint3x4(box.Center);
                    var filtered = filter.TargetPosition + filter.TargetRotation * box.Center;
                    float yaw = (filter.TargetRotation * Quaternion.Euler(0f, box.Yaw, 0f)).eulerAngles.y;
                    Debug.Log(PaseoLogFormat.BoxAfterLocalization(mapId, estimated, filtered, yaw, filter.Confirmed));
                }
            }
            else if (trigger == DataProcessorTrigger.Update)
                filter.Step(Time.deltaTime);

            output.Ignore = !filter.HasPose;
            if (filter.HasPose)
                output.Pose = Matrix4x4.TRS(filter.Position, filter.Rotation, Vector3.one);
            return Task.FromResult(output);
        }

        public Task ResetProcessor()
        {
            filter.Reset();
            output.Ignore = true;
            return Task.CompletedTask;
        }

        // Read every time: the team may move or resize the box with the field panel. A localization is judged at the box of
        // the building that owns its map (the first box if none does, e.g. no map id); between localizations the filter
        // keeps measuring at the last one.
        PaseoBoxGeometry BoxInSpace(bool newLocalization, int mapId)
        {
            if (contents.Length == 0 || contents.Any(c => c == null))
                contents = GetComponentsInChildren<PaseoMapContent>(true);
            if (newLocalization)
                measured = contents.FirstOrDefault(c => c.MapIds.Contains(mapId));
            if (measured == null)
                measured = contents.Length > 0 ? contents[0] : null;
            if (measured == null || measured.Box == null)
                return default;
            var t = measured.transform;
            float yaw = (Quaternion.Inverse(transform.rotation) * t.rotation).eulerAngles.y;
            return new PaseoBoxGeometry(transform.InverseTransformPoint(t.position), yaw, measured.SizeMeters);
        }

        public static string VerdictName(PaseoPoseVerdict verdict) => verdict switch
        {
            PaseoPoseVerdict.Locked => "fijada",
            PaseoPoseVerdict.Accepted => "aceptada",
            PaseoPoseVerdict.Rejected => "lejos",
            PaseoPoseVerdict.Relocked => "reanclada",
            PaseoPoseVerdict.Tilted => "inclinada",
            PaseoPoseVerdict.Pending => "pendiente",
            _ => verdict.ToString()
        };

        // key=value with invariant numbers, so a logcat dump can be parsed: «mueve» is how far this localization would
        // move the box from the filtered pose, «dist» the camera's distance to the box centre, «confirmada» whether the
        // box now rests on two localizations that agree, «umbral» the inlier gate this one was judged against and
        // «recorrido» the metres the camera walked since the last localization the filter used (both «-» before the first lock).
        public static string LogLine(int mapId, PaseoPoseReport report)
        {
            var inv = CultureInfo.InvariantCulture;
            string move = float.IsNaN(report.BoxMoveMeters) ? "-" : report.BoxMoveMeters.ToString("F2", inv);
            string gate = float.IsNaN(report.GateMeters) ? "-" : report.GateMeters.ToString("F2", inv);
            string travel = float.IsNaN(report.TravelMeters) ? "-" : report.TravelMeters.ToString("F1", inv);
            return string.Format(inv, "{0} UBICACION_PASEO mapa={1} veredicto={2} conf={3:F0} incl={4:F2} mueve={5} dist={6:F1} confirmada={7} umbral={8} recorrido={9}",
                Tag, mapId, VerdictName(report.Verdict), report.Confidence, report.TiltDegrees, move, report.CameraToBoxMeters,
                report.Confirmed ? 1 : 0, gate, travel);
        }
    }
}
