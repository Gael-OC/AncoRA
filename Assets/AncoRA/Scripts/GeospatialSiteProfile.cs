using UnityEngine;

namespace AncorRA.AR
{
    public enum GeospatialAltitudeMode
    {
        Terrain,
        Rooftop,
    }

    /// <summary>
    /// Surveyed, deployable transform from an Earth anchor to the building proxy.
    /// This belongs in the build; it is not end-user calibration data.
    /// </summary>
    [CreateAssetMenu(fileName = "GeospatialSiteProfile", menuName = "AncoRA/Geospatial Site Profile")]
    public sealed class GeospatialSiteProfile : ScriptableObject
    {
        [Header("Site")]
        [SerializeField] string m_SiteName = "Casa Región 2";
        [SerializeField] double m_Latitude;
        [SerializeField] double m_Longitude;
        [SerializeField] GeospatialAltitudeMode m_AltitudeMode = GeospatialAltitudeMode.Terrain;
        [SerializeField] double m_AltitudeAboveSurface;
        [Tooltip("Clockwise from true north. Measure the building facade direction, not the phone heading.")]
        [SerializeField, Range(-180f, 180f)] float m_HeadingDegrees;

        [Header("Building relative to the Earth anchor")]
        [SerializeField] Vector3 m_LocalPosition;
        [SerializeField] Vector3 m_LocalEulerAngles;
        [SerializeField] Vector3 m_SizeMeters = new(18f, 5f, 10f);
        [SerializeField, Min(0f)] float m_RoofHeight = 1.5f;
        [SerializeField] bool m_RidgeAlongWidth = true;

        public string SiteName => m_SiteName;
        public double Latitude => m_Latitude;
        public double Longitude => m_Longitude;
        public GeospatialAltitudeMode AltitudeMode => m_AltitudeMode;
        public double AltitudeAboveSurface => m_AltitudeAboveSurface;
        public float HeadingDegrees => m_HeadingDegrees;
        public Vector3 LocalPosition => m_LocalPosition;
        public Vector3 LocalEulerAngles => m_LocalEulerAngles;
        public Quaternion LocalRotation => Quaternion.Euler(m_LocalEulerAngles);
        public Vector3 SizeMeters => m_SizeMeters;
        public float RoofHeight => m_RoofHeight;
        public bool RidgeAlongWidth => m_RidgeAlongWidth;

        public bool HasCoordinates =>
            m_Latitude >= -90d && m_Latitude <= 90d &&
            m_Longitude >= -180d && m_Longitude <= 180d &&
            !(Mathf.Approximately((float)m_Latitude, 0f) && Mathf.Approximately((float)m_Longitude, 0f));

        void OnValidate()
        {
            m_Latitude = System.Math.Max(-90d, System.Math.Min(90d, m_Latitude));
            m_Longitude = System.Math.Max(-180d, System.Math.Min(180d, m_Longitude));
            m_SizeMeters.x = Mathf.Max(0.1f, m_SizeMeters.x);
            m_SizeMeters.y = Mathf.Max(0.2f, m_SizeMeters.y);
            m_SizeMeters.z = Mathf.Max(0.1f, m_SizeMeters.z);
            m_RoofHeight = Mathf.Clamp(m_RoofHeight, 0f, m_SizeMeters.y - 0.1f);
        }
    }
}
