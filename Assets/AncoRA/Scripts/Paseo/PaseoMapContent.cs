using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The content of one building: its box, child of the building's XR Space (one space, one box and one pose filter per
    /// building, with all its maps inside the space). The box pose is in the building frame, which is the frame of the
    /// reference map (PaseoBuildingFrame). In campus mode all the buildings share one space: the box pose is then in the
    /// campus frame and there is no reference map (<see cref="ReferenceMapId"/> is 0). The name and description are drawn
    /// by the interface (PaseoUi). The class keeps its old name so the scene's script reference stays valid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoMapContent : MonoBehaviour
    {
        // The building's maps in edificio.json order, and the one whose frame is the building frame (0 in campus mode).
        [SerializeField] int[] mapIds = System.Array.Empty<int>();
        [SerializeField] int referenceMapId;
        [SerializeField] string buildingId;
        [SerializeField] string buildingName;
        [SerializeField] string description;
        [SerializeField] EdificioFacadeFrame box;
        // Centre of the building box on the ground (edificio.json «gps»), for the search by distance (PaseoSearchPlan).
        [SerializeField] bool hasGps;
        [SerializeField] double latitude;
        [SerializeField] double longitude;

        // The building is on screen (the tour located it).
        bool shown;

        public IReadOnlyList<int> MapIds => mapIds;
        public int ReferenceMapId => referenceMapId;
        public string BuildingId => buildingId;
        public string BuildingName => buildingName;
        public string Description => description;
        public bool HasGps => hasGps;
        public double Latitude => latitude;
        public double Longitude => longitude;
        /// <summary>The building is on screen: its box is drawn.</summary>
        public bool IsShown => shown;
        public EdificioFacadeFrame Box => box;
        public bool Placed => box != null && box.PlacedByTeam;
        public Vector3 LocalPosition => transform.localPosition;
        public float LocalYaw => transform.localEulerAngles.y;
        public Vector3 SizeMeters => new(box.WidthMeters, box.HeightMeters, box.DepthMeters);

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(int[] maps, int referenceMap, string building, string displayName, string buildingDescription,
            EdificioFacadeFrame frame)
        {
            mapIds = (int[])maps.Clone();
            referenceMapId = referenceMap;
            buildingId = building;
            buildingName = displayName;
            description = buildingDescription ?? "";
            box = frame;
        }

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

        public void SetVisible(bool visible)
        {
            shown = visible;
            box.SetVisible(visible);
        }
    }
}
