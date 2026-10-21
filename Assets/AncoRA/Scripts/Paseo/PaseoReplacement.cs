using UnityEngine;
using UnityEngine.Rendering;

namespace AncorRA.AR
{
    /// <summary>
    /// Stands in the place of the real building. The camera cannot see what is behind the building, so it is painted
    /// over: a mask, the box grown by a margin, drawn from the inside with sky above the horizon and ground below
    /// (shader AncoRA/Paseo/Fondo, opaque and writing depth). The model stands inside it on the ground and may turn.
    /// Child of the box (PaseoMapContent), so it follows its pose; it follows the box size every frame, so team
    /// adjustments carry over. The mask mesh is generated at runtime and released here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoReplacement : MonoBehaviour
    {
        [SerializeField] EdificioFacadeFrame box;
        [SerializeField] Material maskMaterial;
        [SerializeField] bool hideBuilding = true;
        [SerializeField] float marginMeters = 1f;
        [SerializeField] Transform model;
        // Empty for the cube, else the model file in the building folder (edificio.json «reemplazo.modelo»).
        [SerializeField] string modelFile = "";
        // Without a model file, the shape shown (edificio.json «reemplazo.forma»).
        [SerializeField] string shape = "cubo";
        [SerializeField] float modelScale = 1f;
        [SerializeField] Vector3 modelOffset;
        [SerializeField] float modelYaw;
        [SerializeField] float spinDegreesPerSecond;

        GameObject mask;
        Mesh maskMesh;
        Vector3 builtSize = -Vector3.one;
        bool shown;

        public bool IsShown => shown;
        public bool HidesBuilding => hideBuilding;
        public float MarginMeters => marginMeters;
        public Material MaskMaterial => maskMaterial;
        public Transform Model => model;
        public string ModelFile => modelFile;
        public string Shape => shape;
        public float ModelScale => modelScale;
        public Vector3 ModelOffset => modelOffset;
        public float ModelYaw => modelYaw;
        public float SpinDegreesPerSecond => spinDegreesPerSecond;

        Vector3 BoxSize => box != null ? new Vector3(box.WidthMeters, box.HeightMeters, box.DepthMeters) : Vector3.one;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(EdificioFacadeFrame buildingBox, Material maskMat, bool hide, float margin, Transform modelInstance,
            string file, string shapeName, float scale, Vector3 offset, float yaw, float spin)
        {
            box = buildingBox;
            maskMaterial = maskMat;
            hideBuilding = hide;
            marginMeters = Mathf.Max(0f, margin);
            model = modelInstance;
            modelFile = file ?? "";
            shape = shapeName ?? "";
            modelScale = scale;
            modelOffset = offset;
            modelYaw = yaw;
            spinDegreesPerSecond = spin;
            Place(BoxSize);
        }

        public void SetVisible(bool visible)
        {
            shown = visible;
            if (visible && BoxSize != builtSize)
                Rebuild(BoxSize);
            if (mask != null)
                mask.SetActive(visible && hideBuilding);
            if (model != null)
                model.gameObject.SetActive(visible);
        }

        void LateUpdate()
        {
            if (!shown)
                return;
            if (BoxSize != builtSize)
                Rebuild(BoxSize);
            if (model != null && spinDegreesPerSecond != 0f)
                model.localRotation = Quaternion.Euler(0f, PaseoReplacementLayout.SpinYaw(modelYaw, spinDegreesPerSecond, Time.time), 0f);
        }

        void OnDestroy() => ReleaseMesh();

        void Rebuild(Vector3 size)
        {
            builtSize = size;
            Place(size);
            if (!hideBuilding || maskMaterial == null)
                return;
            if (mask == null)
            {
                mask = new GameObject("Mascara");
                mask.transform.SetParent(transform, false);
                mask.AddComponent<MeshFilter>();
                var meshRenderer = mask.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = maskMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                mask.SetActive(shown);
            }
            ReleaseMesh();
            maskMesh = BuildBox(PaseoReplacementLayout.MaskSize(size, marginMeters));
            mask.GetComponent<MeshFilter>().sharedMesh = maskMesh;
        }

        void Place(Vector3 size)
        {
            if (model == null)
                return;
            model.localPosition = PaseoReplacementLayout.ModelPosition(size, modelOffset);
            model.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
            model.localScale = Vector3.one * modelScale;
        }

        void ReleaseMesh()
        {
            if (maskMesh == null)
                return;
            if (mask != null)
                mask.GetComponent<MeshFilter>().sharedMesh = null;
            // Generated at runtime, so it has no owner: destroy it explicitly.
            if (Application.isPlaying)
                Destroy(maskMesh);
            else
                DestroyImmediate(maskMesh);
            maskMesh = null;
        }

        // A closed box wound outwards: the mask shader culls front faces and draws the inner ones, which lie behind the
        // model, so the model keeps its depth while the mask still covers the whole silhouette.
        static Mesh BuildBox(Vector3 size)
        {
            var h = size * 0.5f;
            var mesh = new Mesh { name = "Mascara del reemplazo (generada)", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
            };
            mesh.triangles = new[]
            {
                0, 3, 2, 0, 2, 1, // -Z
                5, 6, 7, 5, 7, 4, // +Z
                4, 7, 3, 4, 3, 0, // -X
                1, 2, 6, 1, 6, 5, // +X
                3, 7, 6, 3, 6, 2, // +Y
                1, 5, 4, 1, 4, 0  // -Y
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
