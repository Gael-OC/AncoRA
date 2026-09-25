using Immersal.XR;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AncorRA.AR
{
    public enum TeologiaMode { Mapa1 = 0, Mapa2 = 1, Ambos = 2 }

    /// <summary>
    /// Chooses which Teologia map(s) the SDK gets: only map 1, only map 2, or both. The SDK registers the active XR Maps
    /// once, inside its own Awake, so the choice is applied here (an earlier Awake) by deactivating the maps that are not
    /// wanted, and changing it reloads the scene. It also keeps the box pose per map, because a pose only means something
    /// inside the frame of its own map, and derives the map 2 to map 1 alignment for the "both" mode from the two box
    /// poses: the team places the box on the same building in each map, so nobody aligns maps by eye.
    /// Nothing here proves physical alignment.
    /// </summary>
    [DefaultExecutionOrder(-3000)]
    public sealed class TeologiaMapSelector : MonoBehaviour
    {
        const string Tag = "[AncoRA Teologia]";

        [SerializeField] XRMap mapA;
        [SerializeField] XRMap mapB;
        [SerializeField] ImmersalMapAlignment alignmentB;
        [SerializeField] EdificioFacadeFrame frame;
        [SerializeField] string keyPrefix = "AncoRA.Teologia.v1.";
        [SerializeField] TeologiaMode defaultMode = TeologiaMode.Mapa2;

        [Header("Box pose per map (scene defaults; map 1 = the frame's own transform)")]
        [SerializeField] Vector3 defaultPositionB;
        [SerializeField] float defaultYawB;
        [Tooltip("Tick when the team fixed that pose against the real building (baked from a field adjustment).")]
        [SerializeField] bool defaultPoseASet;
        [SerializeField] bool defaultPoseBSet;

        TeologiaBoxStore store;
        Vector3 defaultPositionA;
        float defaultYawA;
        Vector3 defaultSize;
        bool defaultSolid;

        public TeologiaMode Mode { get; private set; }
        public string Notice { get; private set; } = "";
        public int MapAId => mapA.mapId;
        public int MapBId => mapB.mapId;
        public XRMap ReferenceMap => Mode == TeologiaMode.Mapa2 ? mapB : mapA;
        // In "both" the box pose is the result of the two per-map poses; editing it would silently move map 2.
        public bool BoxPoseEditable => Mode != TeologiaMode.Ambos;

        public string ModeLabel => Mode switch
        {
            TeologiaMode.Mapa1 => "Solo Mapa 1",
            TeologiaMode.Mapa2 => "Solo Mapa 2",
            _ => "Ambos mapas"
        };

        /// <summary>
        /// Map-2-inside-map-1 relation such that a box placed by eye in each map lands on the same physical box:
        /// boxA = T * boxB, so T.rotation = qA * inverse(qB) and T.position = posA - T.rotation * posB.
        /// </summary>
        public static void DeriveAlignment(Vector3 posA, float yawA, Vector3 posB, float yawB, out Vector3 position, out float yawDegrees)
        {
            var rotation = Quaternion.Euler(0f, yawA, 0f) * Quaternion.Inverse(Quaternion.Euler(0f, yawB, 0f));
            position = posA - rotation * posB;
            yawDegrees = rotation.eulerAngles.y;
        }

        void Awake()
        {
            store = new TeologiaBoxStore(keyPrefix);
            Mode = (TeologiaMode)Mathf.Clamp(store.LoadMode((int)defaultMode), 0, 2);

            var t = frame.transform;
            defaultPositionA = t.localPosition;
            defaultYawA = t.localEulerAngles.y;
            defaultSize = new Vector3(frame.WidthMeters, frame.HeightMeters, frame.DepthMeters);
            defaultSolid = frame.Solid;

            // Must happen before the SDK's Awake: it only registers XR Maps that are active at that moment.
            mapA.gameObject.SetActive(Mode != TeologiaMode.Mapa2);
            mapB.gameObject.SetActive(Mode != TeologiaMode.Mapa1);

            ApplyAlignmentB();
            ApplyStoredBox();
            Debug.Log($"{Tag} Modo de mapas: {ModeLabel} (mapa 1 = {mapA.mapId}, mapa 2 = {mapB.mapId}). {Notice}".TrimEnd());
        }

        /// <summary>Pose of the box in the frame of the given map. True when the team fixed it (saved or baked).</summary>
        public bool GetPose(int mapId, out Vector3 position, out float yawDegrees)
        {
            if (store == null)
                store = new TeologiaBoxStore(keyPrefix);
            if (store.TryLoadPose(mapId, out position, out yawDegrees))
                return true;
            bool isA = mapId == mapA.mapId;
            position = isA ? defaultPositionA : defaultPositionB;
            yawDegrees = isA ? defaultYawA : defaultYawB;
            return isA ? defaultPoseASet : defaultPoseBSet;
        }

        void ApplyAlignmentB()
        {
            var position = Vector3.zero;
            float yaw = 0f;
            Notice = "";
            if (Mode == TeologiaMode.Ambos)
            {
                bool aSet = GetPose(mapA.mapId, out var posA, out float yawA);
                bool bSet = GetPose(mapB.mapId, out var posB, out float yawB);
                if (aSet && bSet)
                {
                    DeriveAlignment(posA, yawA, posB, yawB, out position, out yaw);
                    Notice = $"Mapa 2 alineado con el Mapa 1 según las dos cajas: pos ({position.x:F1}, {position.y:F1}, {position.z:F1}), giro {yaw:F0}°.";
                }
                else
                {
                    Notice = "Ambos SIN alinear: primero coloca la caja sobre el edificio en «Solo Mapa 1» y en «Solo Mapa 2».";
                }
            }
            // Alone, a map is its own frame; only "both" places map 2 inside map 1.
            alignmentB.Initialize(mapB, false, position, new Vector3(0f, yaw, 0f));
        }

        void ApplyStoredBox()
        {
            bool placed = GetPose(ReferenceMap.mapId, out var position, out float yaw);
            var size = defaultSize;
            bool solid = defaultSolid;
            if (store.TryLoadSize(out var savedSize, out bool savedSolid))
            {
                size = savedSize;
                solid = savedSolid && frame.HasSolidMaterial;
            }
            frame.SetSize(size.x, size.y, size.z);
            frame.SetSolid(solid);
            frame.transform.localPosition = position;
            frame.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            frame.MarkPlaced(placed);
        }

        /// <summary>Saves what the panel shows: the pose for the map that defines the current frame, and the shared size and fill.</summary>
        public void SaveBox(Vector3 position, float yawDegrees, Vector3 size, bool solid)
        {
            if (BoxPoseEditable)
                store.SavePose(ReferenceMap.mapId, position, yawDegrees);
            store.SaveSize(size, solid);
        }

        /// <summary>Forgets what was saved for the current map and for the size, and goes back to the scene values.</summary>
        public void ResetBox()
        {
            if (BoxPoseEditable)
                store.ClearPose(ReferenceMap.mapId);
            store.ClearSize();
            ApplyStoredBox();
            Debug.Log($"{Tag} Caja restaurada a los valores de la escena ({ModeLabel}).");
        }

        /// <summary>Saves the mode and reloads the scene: the SDK only reads its maps at start-up.</summary>
        public void RequestMode(TeologiaMode mode)
        {
            if (mode == Mode)
                return;
            store.SaveMode((int)mode);
            Debug.Log($"{Tag} Cambio de modo {ModeLabel} → {mode}: se reinicia la escena para que el SDK cargue solo esos mapas.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                PlayerPrefs.Save();
        }
    }
}
