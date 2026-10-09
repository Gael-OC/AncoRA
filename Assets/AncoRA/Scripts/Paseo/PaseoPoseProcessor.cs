using System.Threading.Tasks;
using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The only data processor of each tour XR Space (XRSpace.ProcessPoses on). It hands every localization to a
    /// PaseoPoseFilter and gives the space the filtered pose. Until two localizations agree it tells the space to stay
    /// still (Ignore), so the box neither shows at a wrong first match nor sweeps in from the world origin.
    /// </summary>
    public sealed class PaseoPoseProcessor : MonoBehaviour, IDataProcessor<SceneUpdateData>
    {
        const string Tag = "[AncoRA Paseo]";

        readonly PaseoPoseFilter filter = new();
        readonly SceneUpdateData output = new() { Ignore = true };

        public bool HasPose => filter.HasPose;

        public Task<SceneUpdateData> ProcessData(SceneUpdateData data, DataProcessorTrigger trigger)
        {
            if (trigger == DataProcessorTrigger.NewData && data != null && data != output)
            {
                var verdict = filter.Add(data.Pose.GetPosition(), data.Pose.rotation);
                output.TrackerSpace = data.TrackerSpace;
                output.MapSpacePose = data.MapSpacePose;
                output.CameraData = data.CameraData;
                output.LocalizeInfo = data.LocalizeInfo;
                output.MapEntry = data.MapEntry;
                if (verdict is PaseoPoseVerdict.Locked or PaseoPoseVerdict.Relocked or PaseoPoseVerdict.Rejected)
                    Debug.Log($"{Tag} Filtro de pose, mapa {data.MapEntry?.Map?.mapId}: {Describe(verdict)}");
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

        static string Describe(PaseoPoseVerdict verdict) => verdict switch
        {
            PaseoPoseVerdict.Locked => "dos ubicaciones coinciden; se fija la pose.",
            PaseoPoseVerdict.Relocked => $"{PaseoPoseFilter.RelockCount} ubicaciones descartadas coinciden entre sí; se vuelve a fijar ahí.",
            _ => $"ubicación descartada (a más de {PaseoPoseFilter.InlierMeters} m o {PaseoPoseFilter.InlierDegrees}° de la pose filtrada).",
        };
    }
}
