using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The single virtual facade frame of the building pilot. It is a child of XR Space (never of one
    /// XR Map), so the same frame is shown whichever map localizes. Width and height are real-world meters
    /// measured by the team; position and rotation are set in the Editor with the transform gizmo.
    /// Local +Z is the outward normal (towards the viewers), shown as a gizmo arrow.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EdificioFacadeFrame : MonoBehaviour
    {
        [Min(0.1f)] [SerializeField] float widthMeters = 1f;
        [Min(0.1f)] [SerializeField] float heightMeters = 1f;
        [Min(0.01f)] [SerializeField] float borderMeters = 0.15f;

        [Tooltip("Set by the team once the frame was placed against measured physical details. Not a proof of alignment.")]
        [SerializeField] bool placedByTeam;

        [SerializeField] Material borderMaterial;
        [SerializeField] Material fillMaterial;

        Mesh mesh;
        MeshRenderer meshRenderer;

        public float WidthMeters => widthMeters;
        public float HeightMeters => heightMeters;
        public bool PlacedByTeam => placedByTeam;
        public bool IsVisible => meshRenderer != null && meshRenderer.enabled;
        public bool IsInsideCamera => meshRenderer != null && meshRenderer.isVisible;

        public void Configure(float width, float height, Material border, Material fill)
        {
            widthMeters = width;
            heightMeters = height;
            borderMaterial = border;
            fillMaterial = fill;
            placedByTeam = false;
            Rebuild();
        }

        /// <summary>Changes the frame size at runtime (team field adjustment).</summary>
        public void SetSize(float width, float height)
        {
            widthMeters = width;
            heightMeters = height;
            Rebuild();
        }

        /// <summary>Shows or hides the frame without deactivating the component.</summary>
        public void SetVisible(bool visible)
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.enabled = visible;
        }

        void OnEnable() => Rebuild();

        void OnValidate()
        {
            // Mesh assignment inside OnValidate is not allowed; defer to the next editor tick.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                    Rebuild();
            };
#endif
        }

        void OnDisable() => ReleaseMesh();
        void OnDestroy() => ReleaseMesh();

        void ReleaseMesh()
        {
            if (mesh == null)
                return;
            var filter = GetComponent<MeshFilter>();
            if (filter != null)
                filter.sharedMesh = null;
            // Generated at runtime, so it has no owner: destroy it explicitly to avoid leaks.
            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
            mesh = null;
        }

        void Rebuild()
        {
            if (!isActiveAndEnabled)
                return;
            ReleaseMesh();
            meshRenderer = GetComponent<MeshRenderer>();
            float w = Mathf.Max(0.1f, widthMeters) * 0.5f;
            float h = Mathf.Max(0.1f, heightMeters) * 0.5f;
            float b = Mathf.Min(borderMeters, Mathf.Min(w, h) * 0.5f);

            // Border as four quads (submesh 0), translucent fill as one quad (submesh 1).
            var vertices = new Vector3[]
            {
                // bottom, top, left, right strips (outer rectangle minus inner rectangle)
                new(-w, -h, 0), new(w, -h, 0), new(w, -h + b, 0), new(-w, -h + b, 0),
                new(-w, h - b, 0), new(w, h - b, 0), new(w, h, 0), new(-w, h, 0),
                new(-w, -h + b, 0), new(-w + b, -h + b, 0), new(-w + b, h - b, 0), new(-w, h - b, 0),
                new(w - b, -h + b, 0), new(w, -h + b, 0), new(w, h - b, 0), new(w - b, h - b, 0),
                new(-w + b, -h + b, 0), new(w - b, -h + b, 0), new(w - b, h - b, 0), new(-w + b, h - b, 0)
            };
            var border = new int[24];
            for (int quad = 0; quad < 4; quad++)
            {
                int v = quad * 4;
                int i = quad * 6;
                border[i] = v; border[i + 1] = v + 2; border[i + 2] = v + 1;
                border[i + 3] = v; border[i + 4] = v + 3; border[i + 5] = v + 2;
            }
            var fill = new[] { 16, 18, 17, 16, 19, 18 };

            mesh = new Mesh { name = "Marco fachada (generado)", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(border, 0);
            mesh.SetTriangles(fill, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer.sharedMaterials = new[] { borderMaterial, fillMaterial };
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = placedByTeam ? new Color(0.1f, 0.9f, 0.3f) : new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(widthMeters, heightMeters, 0.01f));
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(Vector3.zero, Vector3.forward * Mathf.Min(widthMeters, heightMeters) * 0.3f);
        }
    }
}
