using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The single virtual facade frame of the building pilot. It is a child of XR Space (never of one
    /// XR Map), so the same frame is shown whichever map localizes. Width and height are real-world meters
    /// measured by the team; position and rotation are set in the Editor with the transform gizmo.
    /// Local +Z is the outward normal (towards the viewers), shown as a gizmo arrow.
    /// <para>
    /// With a depth greater than zero the same component draws a 3D box (edge bars plus six faces) centred on the
    /// transform, used to stand in for the whole building and judge whether localization keeps it in place. The face
    /// material can be flipped between translucent and opaque at runtime.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EdificioFacadeFrame : MonoBehaviour
    {
        // Below this depth the component keeps drawing the flat frame.
        const float MinBoxDepthMeters = 0.05f;

        [Min(0.1f)] [SerializeField] float widthMeters = 1f;
        [Min(0.1f)] [SerializeField] float heightMeters = 1f;
        [Tooltip("0 = flat frame. Greater than 0 = 3D box of that depth (local Z), centred on the transform.")]
        [Min(0f)] [SerializeField] float depthMeters;
        [Min(0.01f)] [SerializeField] float borderMeters = 0.15f;

        [Tooltip("Set by the team once the frame was placed against measured physical details. Not a proof of alignment.")]
        [SerializeField] bool placedByTeam;

        [SerializeField] Material borderMaterial;
        [SerializeField] Material fillMaterial;
        [Tooltip("Opaque face material of the box; used instead of the translucent fill while Solid is on.")]
        [SerializeField] Material solidMaterial;
        [SerializeField] bool solid;

        Mesh mesh;
        MeshRenderer meshRenderer;

        public float WidthMeters => widthMeters;
        public float HeightMeters => heightMeters;
        public float DepthMeters => depthMeters;
        public bool IsBox => depthMeters >= MinBoxDepthMeters;
        public bool Solid => solid;
        public bool HasSolidMaterial => solidMaterial != null;
        public bool PlacedByTeam => placedByTeam;
        public bool IsVisible => meshRenderer != null && meshRenderer.enabled;
        public bool IsInsideCamera => meshRenderer != null && meshRenderer.isVisible;

        public void Configure(float width, float height, Material border, Material fill)
            => Configure(width, height, 0f, border, fill, null, false);

        public void Configure(float width, float height, float depth, Material border, Material fill, Material solidFill, bool startSolid)
        {
            widthMeters = width;
            heightMeters = height;
            depthMeters = Mathf.Max(0f, depth);
            borderMaterial = border;
            fillMaterial = fill;
            solidMaterial = solidFill;
            solid = startSolid && solidFill != null;
            placedByTeam = false;
            Rebuild();
        }

        /// <summary>Changes the frame size at runtime (team field adjustment); the depth is kept.</summary>
        public void SetSize(float width, float height) => SetSize(width, height, depthMeters);

        public void SetSize(float width, float height, float depth)
        {
            widthMeters = width;
            heightMeters = height;
            depthMeters = Mathf.Max(0f, depth);
            Rebuild();
        }

        /// <summary>Marks the placement as done by the team (a saved or baked pose), not the scene default.</summary>
        public void MarkPlaced(bool placed) => placedByTeam = placed;

        /// <summary>Flips the box faces between translucent and opaque. Ignored without an opaque material.</summary>
        public void SetSolid(bool value)
        {
            solid = value && solidMaterial != null;
            ApplyMaterials();
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
            mesh = IsBox ? BuildBoxMesh() : BuildFlatMesh();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            ApplyMaterials();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        void ApplyMaterials()
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new[] { borderMaterial, solid && solidMaterial != null ? solidMaterial : fillMaterial };
        }

        Mesh BuildFlatMesh()
        {
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

            var flat = new Mesh { name = "Marco fachada (generado)", hideFlags = HideFlags.DontSave };
            flat.vertices = vertices;
            flat.subMeshCount = 2;
            flat.SetTriangles(border, 0);
            flat.SetTriangles(fill, 1);
            flat.RecalculateBounds();
            flat.RecalculateNormals();
            return flat;
        }

        // Submesh 0: twelve edge bars (thin boxes) and the front cross. Submesh 1: the six faces. Both materials are drawn without
        // culling, so winding order does not matter and the box also reads from inside.
        Mesh BuildBoxMesh()
        {
            float w = Mathf.Max(0.1f, widthMeters) * 0.5f;
            float h = Mathf.Max(0.1f, heightMeters) * 0.5f;
            float d = Mathf.Max(MinBoxDepthMeters, depthMeters) * 0.5f;
            float b = Mathf.Min(borderMeters, Mathf.Min(w, Mathf.Min(h, d)) * 0.5f);

            var vertices = new List<Vector3>(128);
            var edges = new List<int>(12 * 36 + 12);
            var faces = new List<int>(36);

            // Six faces as quads over the outer box.
            var half = new Vector3(w, h, d);
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                foreach (float side in new[] { -1f, 1f })
                {
                    int start = vertices.Count;
                    foreach (var (su, sv) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                    {
                        var p = Vector3.zero;
                        p[axis] = side * half[axis];
                        p[u] = su * half[u];
                        p[v] = sv * half[v];
                        vertices.Add(p);
                    }
                    faces.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }

            // Twelve edges: along each axis, at the four corners of the other two.
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                foreach (float su in new[] { -1f, 1f })
                    foreach (float sv in new[] { -1f, 1f })
                    {
                        var center = Vector3.zero;
                        center[u] = su * half[u];
                        center[v] = sv * half[v];
                        var size = new Vector3(b, b, b);
                        size[axis] = half[axis] * 2f + b;
                        AddBox(vertices, edges, center, size);
                    }
            }

            // A cross on the +Z face marks the front. Without it a box looks the same turned 180 degrees, and placing it
            // turned in one map and not in the other would corrupt the map-to-map alignment derived from both poses.
            AddCross(vertices, edges, w, h, d + b * 0.5f, b);

            var box = new Mesh { name = "Caja edificio (generada)", hideFlags = HideFlags.DontSave };
            box.SetVertices(vertices);
            box.subMeshCount = 2;
            box.SetTriangles(edges, 0);
            box.SetTriangles(faces, 1);
            box.RecalculateBounds();
            box.RecalculateNormals();
            return box;
        }

        // Two flat diagonal strips across the rectangle [-w, w] x [-h, h] at depth z.
        static void AddCross(List<Vector3> vertices, List<int> triangles, float w, float h, float z, float thickness)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var from = new Vector2(-w, -side * h);
                var to = new Vector2(w, side * h);
                var dir = (to - from).normalized;
                var offset = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
                int start = vertices.Count;
                vertices.Add(new Vector3(from.x - offset.x, from.y - offset.y, z));
                vertices.Add(new Vector3(to.x - offset.x, to.y - offset.y, z));
                vertices.Add(new Vector3(to.x + offset.x, to.y + offset.y, z));
                vertices.Add(new Vector3(from.x + offset.x, from.y + offset.y, z));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        static void AddBox(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 size)
        {
            var e = size * 0.5f;
            int start = vertices.Count;
            for (int i = 0; i < 8; i++)
                vertices.Add(center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
            // Vertex index bits are (x, y, z); each face is two triangles over four of the eight corners.
            int[] quads =
            {
                0, 1, 3, 2,  4, 6, 7, 5,  0, 2, 6, 4,  1, 5, 7, 3,  0, 4, 5, 1,  2, 3, 7, 6
            };
            for (int q = 0; q < quads.Length; q += 4)
            {
                int a = start + quads[q], b = start + quads[q + 1], c = start + quads[q + 2], d = start + quads[q + 3];
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = placedByTeam ? new Color(0.1f, 0.9f, 0.3f) : new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(widthMeters, heightMeters, IsBox ? depthMeters : 0.01f));
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(Vector3.zero, Vector3.forward * Mathf.Min(widthMeters, heightMeters) * 0.3f);
        }
    }
}
