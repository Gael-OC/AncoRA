using System.Globalization;
using System.Threading.Tasks;
using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The only data processor of each tour XR Space (XRSpace.ProcessPoses on). It hands every localization to a
    /// PaseoPoseFilter, measured at the box this space carries, and gives the space the filtered pose. Until two
    /// localizations agree it tells the space to stay still (Ignore), so the box neither shows at a wrong first match nor
    /// sweeps in from the world origin. Each localization leaves one UBICACION_PASEO log line to calibrate the filter.
    /// </summary>
    public sealed class PaseoPoseProcessor : MonoBehaviour, IDataProcessor<SceneUpdateData>
    {
        const string Tag = "[AncoRA Paseo]";

        readonly PaseoPoseFilter filter = new();
        readonly SceneUpdateData output = new() { Ignore = true };
        PaseoMapContent content;

        public bool HasPose => filter.HasPose;
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
                output.TrackerSpace = data.TrackerSpace;
                output.MapSpacePose = data.MapSpacePose;
                output.CameraData = data.CameraData;
                output.LocalizeInfo = data.LocalizeInfo;
                output.MapEntry = data.MapEntry;
                Debug.Log(LogLine(data.MapEntry?.Map != null ? data.MapEntry.Map.mapId : 0, report));
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
            PaseoPoseVerdict.Waiting => "esperando",
            PaseoPoseVerdict.Locked => "fijada",
            PaseoPoseVerdict.Accepted => "aceptada",
            PaseoPoseVerdict.Rejected => "lejos",
            PaseoPoseVerdict.Relocked => "reanclada",
            PaseoPoseVerdict.Tilted => "inclinada",
            _ => verdict.ToString()
        };

        // key=value with invariant numbers, so a logcat dump can be parsed: «mueve» is how far this localization would
        // move the box from the filtered pose, «dist» the camera's distance to the box centre.
        static string LogLine(int mapId, PaseoPoseReport report)
        {
            var inv = CultureInfo.InvariantCulture;
            string move = float.IsNaN(report.BoxMoveMeters) ? "-" : report.BoxMoveMeters.ToString("F2", inv);
            return string.Format(inv, "{0} UBICACION_PASEO mapa={1} veredicto={2} conf={3:F0} incl={4:F2} mueve={5} dist={6:F1}",
                Tag, mapId, VerdictName(report.Verdict), report.Confidence, report.TiltDegrees, move, report.CameraToBoxMeters);
        }
    }
}
