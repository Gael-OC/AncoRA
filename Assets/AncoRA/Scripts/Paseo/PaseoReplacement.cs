using UnityEngine;
using UnityEngine.Rendering;

namespace AncorRA.AR
{
    /// <summary>
    /// Stands in the place of the real building. The camera cannot see what is behind the building, so it is painted
    /// over: a mask, the box grown by a margin, with sky above the horizon and ground below (shader AncoRA/Paseo/Fondo).
    /// The model stands inside it on the ground and may turn.
    /// <para>
    /// The mask is two draws of one mesh. The «marca» writes only stencil, and only where the box is not hidden by a real
    /// object clearly in front of it (with ARCore occlusion the depth buffer holds the environment depth; the mark's
    /// vertices are slid <see cref="SlackMeters"/> towards the camera so the rough depth of the far facade does not cut
    /// it). The «fondo» paints the inner faces where the mark is and writes their depth, so the model inside is not hidden
    /// by the building it replaces. Without environment depth the mark covers the whole silhouette: the box painted over.
    /// </para>
    /// Child of the box (PaseoMapContent), so it follows its pose; it follows the box size every frame, so team
    /// adjustments carry over. The mask mesh is generated at runtime and released here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoReplacement : MonoBehaviour
    {
        static readonly int SlackId = Shader.PropertyToID("_Holgura");

        [SerializeField] EdificioFacadeFrame box;
        // Stencil only, before the painting (PaseoReemplazoMarca).
        [SerializeField] Material markMaterial;
        // The sky and ground paint (PaseoReemplazoFondo).
        [SerializeField] Material maskMaterial;
        [SerializeField] bool hideBuilding = true;
        [SerializeField] float marginMeters = 1f;
        // Metres of depth a real object must be in front of the building to stay out of the mark (edificio.json «reemplazo.holgura»).
        [SerializeField] float slackMeters = PaseoReplacementConfig.DefaultSlackMeters;
        [SerializeField] Transform model;
        // Empty for the cube, else the model file in the building folder (edificio.json «reemplazo.modelo»).
        [SerializeField] string modelFile = "";
        // Without a model file, the shape shown (edificio.json «reemplazo.forma»).
        [SerializeField] string shape = "cubo";
        [SerializeField] float modelScale = 1f;
        [SerializeField] Vector3 modelOffset;
        [SerializeField] float modelYaw;
        [SerializeField] float spinDegreesPerSecond;

        GameObject mark;
        GameObject paint;
        Mesh maskMesh;
        Vector3 builtSize = -Vector3.one;
        // The mask (mark and paint) and the model show independently, so the team can look at the painted background alone.
        bool maskShown;
        bool modelShown;

        /// <summary>Any part is on screen.</summary>
        public bool IsShown => maskShown || modelShown;
        /// <summary>The mask is asked to show (it only draws when the building is hidden at all: <see cref="HidesBuilding"/>).</summary>
        public bool MaskShown => maskShown;
        public bool ModelShown => modelShown;
        public bool HidesBuilding => hideBuilding;
        public float MarginMeters => marginMeters;
        public float SlackMeters => slackMeters;
        public Material MarkMaterial => markMaterial;
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
        public void Configure(EdificioFacadeFrame buildingBox, Material markMat, Material maskMat, bool hide, float margin, float slack,
            Transform modelInstance, string file, string shapeName, float scale, Vector3 offset, float yaw, float spin)
        {
            box = buildingBox;
            markMaterial = markMat;
            maskMaterial = maskMat;
            hideBuilding = hide;
            marginMeters = Mathf.Max(0f, margin);
            slackMeters = Mathf.Max(0f, slack);
            model = modelInstance;
            modelFile = file ?? "";
            shape = shapeName ?? "";
            modelScale = scale;
            modelOffset = offset;
            modelYaw = yaw;
            spinDegreesPerSecond = spin;
            Place(BoxSize);
        }

        /// <summary>The whole replacement: the mask and the model.</summary>
        public void SetVisible(bool visible) => SetParts(visible, visible);

        /// <summary>
        /// The mask and the model separately (PaseoViewMode): the mask alone shows the painted background without the object
        /// standing on it. The mask is both draws or none.
        /// </summary>
        public void SetParts(bool showMask, bool showModel)
        {
            maskShown = showMask;
            modelShown = showModel;
            if ((showMask || showModel) && BoxSize != builtSize)
                Rebuild(BoxSize);
            // Both draws or none: a lone mark would only write stencil, a lone paint would never draw.
            if (mark != null)
                mark.SetActive(showMask && hideBuilding);
            if (paint != null)
                paint.SetActive(showMask && hideBuilding);
            if (model != null)
                model.gameObject.SetActive(showModel);
        }

        void LateUpdate()
        {
            if (!maskShown && !modelShown)
                return;
            if (BoxSize != builtSize)
                Rebuild(BoxSize);
            // The spin follows the clock, so a model shown again picks up where it would be, not where it stopped.
            if (modelShown && model != null && spinDegreesPerSecond != 0f)
                model.localRotation = Quaternion.Euler(0f, PaseoReplacementLayout.SpinYaw(modelYaw, spinDegreesPerSecond, Time.time), 0f);
        }

        void OnDestroy() => ReleaseMesh();

        void Rebuild(Vector3 size)
        {
            builtSize = size;
            Place(size);
            if (!hideBuilding || maskMaterial == null || markMaterial == null)
                return;
            if (mark == null)
            {
                mark = CreateMaskPart("Marca", markMaterial, carriesSlack: true);
                paint = CreateMaskPart("Fondo", maskMaterial, carriesSlack: false);
            }
            ReleaseMesh();
            maskMesh = BuildBox(PaseoReplacementLayout.MaskSize(size, marginMeters));
            mark.GetComponent<MeshFilter>().sharedMesh = maskMesh;
            paint.GetComponent<MeshFilter>().sharedMesh = maskMesh;
        }

        // One draw of the mask. The slack is per building, so the mark gets it in a property block (even a slack of 0, which must
        // not fall back to the material's default) and the materials stay shared.
        GameObject CreateMaskPart(string partName, Material material, bool carriesSlack)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>();
            var meshRenderer = part.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            if (carriesSlack)
            {
                var block = new MaterialPropertyBlock();
                block.SetFloat(SlackId, slackMeters);
                meshRenderer.SetPropertyBlock(block);
            }
            part.SetActive(maskShown);
            return part;
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
            if (mark != null)
                mark.GetComponent<MeshFilter>().sharedMesh = null;
            if (paint != null)
                paint.GetComponent<MeshFilter>().sharedMesh = null;
            // Generated at runtime, so it has no owner: destroy it explicitly.
            if (Application.isPlaying)
                Destroy(maskMesh);
            else
                DestroyImmediate(maskMesh);
            maskMesh = null;
        }

        // A closed box wound outwards: the paint culls front faces and draws the inner ones, which lie behind the model, so
        // the model keeps its depth while the mask still covers the whole silhouette. The mark draws both sides.
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
