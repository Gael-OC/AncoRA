#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>What a building needs to be placed in the simulated view: its box in the frame the scene uses (the campus frame).</summary>
    public readonly struct PaseoSimInput
    {
        public readonly string Id;
        public readonly string Name;
        public readonly Vector3 Center;
        public readonly float Yaw;
        public readonly Vector3 Size;
        /// <summary>edificio.json «parque.frente» (+z, -z, +x, -x, in the box frame), or null when the building has no park.</summary>
        public readonly string Front;

        public PaseoSimInput(string id, string name, Vector3 center, float yaw, Vector3 size, string front)
        {
            Id = id;
            Name = name;
            Center = center;
            Yaw = yaw;
            Size = size;
            Front = front;
        }
    }

    /// <summary>A building box of the simulated view with the face the viewer stands in front of.</summary>
    public readonly struct PaseoSimBuilding
    {
        public readonly string Id;
        public readonly string Name;
        public readonly Vector3 Center;
        public readonly float Yaw;
        public readonly Vector3 Size;
        /// <summary>Horizontal unit vector, world frame, pointing out of the face the viewer looks at.</summary>
        public readonly Vector3 Front;
        /// <summary>Half the box depth measured along <see cref="Front"/>: the distance from the centre to that face.</summary>
        public readonly float FrontHalfDepth;
        /// <summary>The front came from edificio.json «parque.frente», not from the fallback rule.</summary>
        public readonly bool FrontDeclared;

        public PaseoSimBuilding(string id, string name, Vector3 center, float yaw, Vector3 size, Vector3 front, float frontHalfDepth, bool frontDeclared)
        {
            Id = id;
            Name = name;
            Center = center;
            Yaw = yaw;
            Size = size;
            Front = front;
            FrontHalfDepth = frontHalfDepth;
            FrontDeclared = frontDeclared;
        }

        public float BottomY => Center.y - Size.y * 0.5f;
    }

    /// <summary>
    /// Pure geometry of the simulated view (the Play-mode preview without a phone, PaseoSimulation): where the viewer stands
    /// in front of each building and the pose a simulated localization hands the XR Space. Everything is in the world frame
    /// of the scene, which in campus mode is the campus frame (x east, y up, z north): the XR Space of the tour sits at the
    /// origin, so the box of a building is at its edificio.json «campus» pose.
    /// <para>
    /// Which face is the front: «parque.frente» when the building has a park (the face the park stands on); otherwise the
    /// long face of the box (the faces whose extent is the larger of width and depth) that looks away from the other
    /// buildings, on the guess that the sidewalks are outside the cluster. It is a guess, like «parque.frente».
    /// </para>
    /// </summary>
    public static class PaseoSimulationGeometry
    {
        public const float EyeHeightMeters = 1.6f;
        public const float DefaultDistanceMeters = 25f;
        public const float NearDistanceMeters = 15f;
        public const float FarDistanceMeters = 50f;
        /// <summary>The deliberate error of the E key: how far the space is moved and how much it is turned.</summary>
        public const float ErrorMeters = 1.5f;
        public const float ErrorDegrees = 3f;

        /// <summary>Folder with the building folders (Assets/AncoRA/Paseo).</summary>
        public static string DataRoot => Path.Combine(Application.dataPath, "AncoRA", "Paseo");

        /// <summary>The building boxes with their fronts, in the order of <paramref name="inputs"/>.</summary>
        public static List<PaseoSimBuilding> Build(IReadOnlyList<PaseoSimInput> inputs)
        {
            var result = new List<PaseoSimBuilding>(inputs.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                var others = new List<Vector3>();
                for (int j = 0; j < inputs.Count; j++)
                    if (j != i)
                        others.Add(inputs[j].Center);
                result.Add(Create(input, others));
            }
            return result;
        }

        static PaseoSimBuilding Create(PaseoSimInput input, List<Vector3> others)
        {
            var turn = Quaternion.Euler(0f, input.Yaw, 0f);
            Vector3 localFront;
            bool declared = TryParseFront(input.Front, out localFront);
            if (!declared)
            {
                // The long faces are the ones whose normal runs along the shorter horizontal side.
                var axis = input.Size.x >= input.Size.z ? Vector3.forward : Vector3.right;
                var away = Vector3.zero;
                if (others.Count > 0)
                {
                    var centroid = Vector3.zero;
                    foreach (var other in others)
                        centroid += other;
                    centroid /= others.Count;
                    away = input.Center - centroid;
                    away.y = 0f;
                }
                // With no other building (or the centre on top of them) any sign will do: the positive one.
                localFront = Vector3.Dot(turn * axis, away) < 0f ? -axis : axis;
            }
            var front = turn * localFront;
            front.y = 0f;
            front.Normalize();
            float half = Mathf.Abs(localFront.z) > 0.5f ? input.Size.z * 0.5f : input.Size.x * 0.5f;
            return new PaseoSimBuilding(input.Id, input.Name, input.Center, input.Yaw, input.Size, front, half, declared);
        }

        static bool TryParseFront(string front, out Vector3 local)
        {
            switch (front)
            {
                case "+z": local = Vector3.forward; return true;
                case "-z": local = Vector3.back; return true;
                case "+x": local = Vector3.right; return true;
                case "-x": local = Vector3.left; return true;
                default: local = Vector3.forward; return false;
            }
        }

        /// <summary>The ground of the simulated view: the lowest box bottom, so no block hangs below it.</summary>
        public static float GroundY(IReadOnlyList<PaseoSimBuilding> buildings) =>
            buildings.Count == 0 ? 0f : buildings.Min(b => b.BottomY);

        /// <summary>Distance from the front face for a hotkey: Ctrl (or Alt) 15 m, Shift 50 m, none 25 m. Ctrl wins over Shift.</summary>
        public static float DistanceFor(bool shift, bool ctrl) => ctrl ? NearDistanceMeters : shift ? FarDistanceMeters : DefaultDistanceMeters;

        /// <summary>
        /// Where the viewer stands: on the ground in front of the face, <paramref name="distanceFromFace"/> metres from it,
        /// eye height above the ground, looking at the centre of the box.
        /// </summary>
        public static Pose Viewpoint(PaseoSimBuilding building, float distanceFromFace, float groundY, float eyeHeight = EyeHeightMeters)
        {
            var onGround = building.Center + building.Front * (building.FrontHalfDepth + distanceFromFace);
            var position = new Vector3(onGround.x, groundY + eyeHeight, onGround.z);
            var toBox = building.Center - position;
            return new Pose(position, Quaternion.LookRotation(toBox.normalized, Vector3.up));
        }

        /// <summary>How far a viewpoint, and the line from it to the front face, keeps from every other box.</summary>
        public const float ClearanceMeters = 1.5f;
        /// <summary>The closest a viewpoint gets to the front face when everything farther is blocked.</summary>
        public const float MinDistanceMeters = 3f;

        /// <summary>
        /// The largest distance up to <paramref name="requested"/> (in steps of 0.5 m, at least <see cref="MinDistanceMeters"/>)
        /// at which the viewer stands outside every other box and sees the front face without another box in between. Teología's
        /// front faces X1 about 13 m away, so a 25 m viewpoint there would stand inside X1 and see it from within.
        /// </summary>
        public static float ClearDistance(PaseoSimBuilding building, float requested, IReadOnlyList<PaseoSimBuilding> all)
        {
            var face = building.Center + building.Front * building.FrontHalfDepth;
            for (float d = requested; d > MinDistanceMeters; d -= 0.5f)
            {
                var viewer = face + building.Front * d;
                if (!BlockedByOther(building, face, viewer, all))
                    return d;
            }
            return MinDistanceMeters;
        }

        static bool BlockedByOther(PaseoSimBuilding building, Vector3 face, Vector3 viewer, IReadOnlyList<PaseoSimBuilding> all)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(face, viewer) / 0.5f));
            foreach (var other in all)
            {
                if (other.Id == building.Id)
                    continue;
                for (int i = 0; i <= steps; i++)
                    if (InsideFootprint(other, Vector3.Lerp(face, viewer, i / (float)steps), ClearanceMeters))
                        return true;
            }
            return false;
        }

        /// <summary>The point lies over the box's ground footprint grown by <paramref name="margin"/> (height ignored).</summary>
        public static bool InsideFootprint(PaseoSimBuilding box, Vector3 point, float margin)
        {
            var local = Quaternion.Inverse(Quaternion.Euler(0f, box.Yaw, 0f)) * (point - box.Center);
            return Mathf.Abs(local.x) <= box.Size.x * 0.5f + margin && Mathf.Abs(local.z) <= box.Size.z * 0.5f + margin;
        }

        /// <summary>
        /// The pose a localization with an error hands the space: the authored pose turned <paramref name="yawDegrees"/> around
        /// the vertical through <paramref name="pivot"/> (the viewer, as Immersal's error pivots on the viewpoint) and then
        /// moved <paramref name="meters"/> along <paramref name="direction"/> (flattened to the ground).
        /// </summary>
        public static Pose WithError(Pose authored, Vector3 pivot, Vector3 direction, float meters, float yawDegrees)
        {
            var turn = Quaternion.AngleAxis(yawDegrees, Vector3.up);
            var position = pivot + turn * (authored.position - pivot);
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 1e-6f)
                position += flat.normalized * meters;
            return new Pose(position, turn * authored.rotation);
        }

        /// <summary>
        /// Every building of edificio.json that carries a «campus» pose (the frame of the scene in campus mode), with its front.
        /// Buildings without maps count: the simulated view shows the real building, mapped or not.
        /// </summary>
        public static List<PaseoSimBuilding> LoadBuildings(string dataRoot) => Build(LoadInputs(dataRoot, out _));

        /// <summary>The same buildings before the front is decided, and their parsed edificio.json by building id.</summary>
        public static List<PaseoSimInput> LoadInputs(string dataRoot, out Dictionary<string, PaseoBuildingConfig> configs)
        {
            var inputs = new List<PaseoSimInput>();
            configs = new Dictionary<string, PaseoBuildingConfig>();
            if (!Directory.Exists(dataRoot))
                return inputs;
            foreach (string folder in Directory.GetDirectories(dataRoot).OrderBy(d => d, System.StringComparer.Ordinal))
            {
                string id = Path.GetFileName(folder);
                string file = Path.Combine(folder, PaseoConfig.FileName);
                if (id.StartsWith("_") || !File.Exists(file))
                    continue;
                PaseoBuildingConfig config;
                try
                {
                    config = PaseoConfig.Parse(File.ReadAllText(file));
                }
                catch (System.FormatException)
                {
                    continue;
                }
                if (!PaseoConfig.HasCampus(config) || config.tamano.Length != 3)
                    continue;
                var p = config.campus.posicion;
                inputs.Add(new PaseoSimInput(id, config.nombre, new Vector3(p[0], p[1], p[2]), config.campus.giro,
                    new Vector3(config.tamano[0], config.tamano[1], config.tamano[2]), PaseoConfig.HasPark(config) ? config.parque.frente : null));
                configs[id] = config;
            }
            return inputs;
        }

        /// <summary>The «parque.frente» of a building folder, or null when it has no park (read by the Play-mode simulation).</summary>
        public static string ReadFront(string dataRoot, string buildingId)
        {
            string file = Path.Combine(dataRoot, buildingId, PaseoConfig.FileName);
            if (!File.Exists(file))
                return null;
            try
            {
                var config = PaseoConfig.Parse(File.ReadAllText(file));
                return PaseoConfig.HasPark(config) ? config.parque.frente : null;
            }
            catch (System.FormatException)
            {
                return null;
            }
        }
    }
}
#endif
