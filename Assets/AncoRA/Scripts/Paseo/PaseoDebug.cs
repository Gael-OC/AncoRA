using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The detection debug of the nicowo branch: the DEPURACION_ log lines and the team's detection tests. Only in
    /// development builds (PaseoSetup.BuildAndroid makes them), so a release build of the final version has none of it.
    /// </summary>
    public static class PaseoDebug
    {
        public static bool Enabled => Debug.isDebugBuild;
    }

    /// <summary>What the tour knew when an attempt ended, for its DEPURACION_INTENTO line. GPS values are the raw ones.</summary>
    public struct PaseoDebugContext
    {
        public string Tracking;
        // Building located and in view (the search tries it first); null for none.
        public string InView;
        public float Interval;
        public bool GpsRunning;
        public bool GpsUsable;
        public double Latitude;
        public double Longitude;
        public float GpsAccuracy;
        public double GpsAge;
    }

    /// <summary>A shown box as the camera sees it: how far its centre is and how many degrees to the right (negative: left).</summary>
    public readonly struct PaseoBoxSighting
    {
        public readonly string Building;
        public readonly float Distance;
        public readonly float Bearing;

        public PaseoBoxSighting(string building, float distance, float bearing)
        {
            Building = building;
            Distance = distance;
            Bearing = bearing;
        }
    }

    /// <summary>The camera once a second, for the DEPURACION_POSE line: where it is, where it looks, how fast it moves.</summary>
    public struct PaseoPoseSample
    {
        public float Seconds;
        public Vector3 Camera;
        public float Yaw;
        // Degrees above the horizon; negative looks down.
        public float Pitch;
        public float Speed;
        public float TurnSpeed;
        public string Tracking;
        public List<PaseoBoxSighting> Boxes;
    }

    public static class PaseoDebugGeometry
    {
        /// <summary>Horizontal angle from where the camera looks to the target, in degrees: positive to the right.</summary>
        public static float Bearing(Vector3 camera, Vector3 forward, Vector3 target)
        {
            var look = new Vector3(forward.x, 0f, forward.z);
            var to = new Vector3(target.x - camera.x, 0f, target.z - camera.z);
            return look.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f ? 0f : Vector3.SignedAngle(look, to, Vector3.up);
        }
    }
}
