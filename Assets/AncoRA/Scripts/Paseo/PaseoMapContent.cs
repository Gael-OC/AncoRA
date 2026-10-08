using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The content of one map: the building box and its name, child of that map's XR Space. Its local pose is in the
    /// map's frame (the XR Map sits at identity inside its space).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoMapContent : MonoBehaviour
    {
        [SerializeField] int mapId;
        [SerializeField] string buildingId;
        [SerializeField] string buildingName;
        [SerializeField] EdificioFacadeFrame box;
        [SerializeField] PaseoLabel label;

        public int MapId => mapId;
        public string BuildingId => buildingId;
        public string BuildingName => buildingName;
        public EdificioFacadeFrame Box => box;
        public PaseoLabel Label => label;
        public bool Placed => box != null && box.PlacedByTeam;
        public Vector3 LocalPosition => transform.localPosition;
        public float LocalYaw => transform.localEulerAngles.y;
        public Vector3 SizeMeters => new(box.WidthMeters, box.HeightMeters, box.DepthMeters);

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(int map, string building, string displayName, EdificioFacadeFrame frame, PaseoLabel nameLabel)
        {
            mapId = map;
            buildingId = building;
            buildingName = displayName;
            box = frame;
            label = nameLabel;
            RefreshLabel();
        }

        public void SetPose(Vector3 localPosition, float yaw, bool placed)
        {
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            box.MarkPlaced(placed);
            RefreshLabel();
        }

        public void SetSize(Vector3 size, bool solid)
        {
            box.SetSize(size.x, size.y, size.z);
            box.SetSolid(solid);
            RefreshLabel();
        }

        public void SetVisible(bool visible)
        {
            box.SetVisible(visible);
            label.SetVisible(visible);
        }

        void RefreshLabel()
        {
            if (label != null && box != null)
                label.Show(box.PlacedByTeam ? buildingName : buildingName + " (sin colocar)", box.HeightMeters);
        }
    }
}
