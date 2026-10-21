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
}
