using System.Globalization;
using System.Threading.Tasks;
using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The only data processor of each tour XR Space (XRSpace.ProcessPoses on). It hands every localization to a
    /// PaseoPoseFilter, measured at the box this space carries, and gives the space the filtered pose. Until the first
    /// localization it tells the space to stay still (Ignore), so the box does not sweep in from the world origin. Each
    /// localization leaves one UBICACION_PASEO log line to calibrate the filter.
    /// </summary>
    public sealed class PaseoPoseProcessor : MonoBehaviour, IDataProcessor<SceneUpdateData>
    {
        const string Tag = "[AncoRA Paseo]";

        readonly PaseoPoseFilter filter = new();
        readonly SceneUpdateData output = new() { Ignore = true };
        PaseoMapContent content;

        public bool HasPose => filter.HasPose;
        /// <summary>The box rests on two localizations that agree, not only on the first one.</summary>
        public bool Confirmed => filter.Confirmed;
        /// <summary>When <see cref="LastReport"/> was made (realtimeSinceStartup), to pair it with the attempt that found it.</summary>
        public float LastReportAt { get; private set; }
        /// <summary>Localizations the filter has seen, and how many of them it used (locked, accepted or relocked).</summary>
        public int Results { get; private set; }
        public int UsedResults { get; private set; }
        public PaseoPoseReport? LastReport { get; private set; }

        public Task<SceneUpdateData> ProcessData(SceneUpdateData data, DataProcessorTrigger trigger)
        {
            filter.Box = BoxInSpace();
            if (trigger == DataProcessorTrigger.NewData && data != null && data != output)
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
                int mapId = data.MapEntry?.Map != null ? data.MapEntry.Map.mapId : 0;
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

        // Read every time: the team may move or resize the box with the field panel.
        PaseoBoxGeometry BoxInSpace()
        {
            if (content == null)
                content = GetComponentInChildren<PaseoMapContent>(true);
            if (content == null || content.Box == null)
                return default;
            var t = content.transform;
            float yaw = (Quaternion.Inverse(transform.rotation) * t.rotation).eulerAngles.y;
            return new PaseoBoxGeometry(transform.InverseTransformPoint(t.position), yaw, content.SizeMeters);
        }

        public static string VerdictName(PaseoPoseVerdict verdict) => verdict switch
        {
            PaseoPoseVerdict.Locked => "fijada",
            PaseoPoseVerdict.Accepted => "aceptada",
            PaseoPoseVerdict.Rejected => "lejos",
            PaseoPoseVerdict.Relocked => "reanclada",
            PaseoPoseVerdict.Tilted => "inclinada",
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
