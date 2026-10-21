using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The content of one map: the building box, child of that map's XR Space. Its local pose is in the map's frame (the
    /// XR Map sits at identity inside its space). The name and description are drawn by the interface (PaseoUi).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoMapContent : MonoBehaviour
    {
        [SerializeField] int mapId;
        [SerializeField] string buildingId;
        [SerializeField] string buildingName;
        [SerializeField] string description;
        [SerializeField] EdificioFacadeFrame box;
        // Centre of the building box on the ground (edificio.json «gps»), for the search by distance (PaseoSearchPlan).
        [SerializeField] bool hasGps;
        [SerializeField] double latitude;
        [SerializeField] double longitude;
        // What stands in the building's place (edificio.json «reemplazo»); null keeps the box.
        [SerializeField] PaseoReplacement replacement;

        public int MapId => mapId;
        public string BuildingId => buildingId;
        public string BuildingName => buildingName;
        public string Description => description;
        public bool HasGps => hasGps;
        public double Latitude => latitude;
        public double Longitude => longitude;
        public PaseoReplacement Replacement => replacement;
        /// <summary>The building is on screen: its box, or its replacement in the box's place.</summary>
        public bool IsShown => replacement != null ? replacement.IsShown : box != null && box.IsVisible;
        public EdificioFacadeFrame Box => box;
        public bool Placed => box != null && box.PlacedByTeam;
        public Vector3 LocalPosition => transform.localPosition;
        public float LocalYaw => transform.localEulerAngles.y;
        public Vector3 SizeMeters => new(box.WidthMeters, box.HeightMeters, box.DepthMeters);

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(int map, string building, string displayName, string buildingDescription, EdificioFacadeFrame frame)
        {
            mapId = map;
            buildingId = building;
            buildingName = displayName;
            description = buildingDescription ?? "";
            box = frame;
        }

        /// <summary>Editor setup (PaseoSetup.Prepare): the replacement that takes the box's place, or none.</summary>
        public void SetReplacement(PaseoReplacement standIn) => replacement = standIn;

        /// <summary>Editor setup (PaseoSetup.Prepare): the building centre, or none.</summary>
        public void SetGps(bool known, double centreLatitude, double centreLongitude)
        {
            hasGps = known;
            latitude = known ? centreLatitude : 0.0;
            longitude = known ? centreLongitude : 0.0;
        }

        public void SetPose(Vector3 localPosition, float yaw, bool placed)
        {
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            box.MarkPlaced(placed);
        }

        public void SetSize(Vector3 size, bool solid)
        {
            box.SetSize(size.x, size.y, size.z);
            box.SetSolid(solid);
        }

        /// <summary>With a replacement the box stays hidden and the replacement shows in its place.</summary>
        public void SetVisible(bool visible)
        {
            box.SetVisible(visible && replacement == null);
            if (replacement != null)
                replacement.SetVisible(visible);
        }
    }
}
