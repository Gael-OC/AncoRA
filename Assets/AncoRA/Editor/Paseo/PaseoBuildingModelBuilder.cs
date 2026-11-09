#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AncorRA.AR;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AncorRA.Editor
{
    /// <summary>2000 demo architecture and imported era models, baked in the Editor. Illustrative, not historical reconstructions.</summary>
    internal static class PaseoBuildingModelBuilder
    {
        const int Wall = 0, Trim = 1, Glass = 2, Roof = 3, Accent = 4;

        public static Material[] CreateMaterials(string folder, Shader shader) => CreateMaterials(folder, shader,
            new[] { "Estuco", "Marfil", "Ventanas", "Terracota", "Verde" },
            new[]
            {
                new Color(0.91f, 0.83f, 0.67f), new Color(0.98f, 0.95f, 0.86f),
                new Color(0.12f, 0.30f, 0.35f), new Color(0.65f, 0.25f, 0.13f), new Color(0.20f, 0.49f, 0.47f)
            });

        /// <summary>Original colours/textures from the supplied model of 1950 or 1980.</summary>
        public static Material[] CreateEpochMaterials(string folder, Shader shader, PaseoEpoch epoch)
        {
            Directory.CreateDirectory(folder);
            var materials = PaseoEpochModelImporter.CreateMaterials(shader, epoch);
            foreach (var material in materials)
                AssetDatabase.CreateAsset(material, $"{folder}/{material.name}.mat");
            return materials;
        }

        static Material[] CreateMaterials(string folder, Shader shader, string[] names, Color[] colors)
        {
            Directory.CreateDirectory(folder);
            var materials = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                // Same fixed-light shader as the walkers, in the opaque queue: buildings write depth before the park.
                var material = new Material(shader) { name = names[i], renderQueue = (int)RenderQueue.Geometry };
                material.SetColor("_BaseColor", colors[i]);
                AssetDatabase.CreateAsset(material, $"{folder}/{names[i]}.mat");
                materials[i] = material;
            }
            return materials;
        }

        public static Transform Build(Transform parent, string id, string style, Vector3 size, string folder, Material[] materials)
        {
            Directory.CreateDirectory(folder);
            var mesh = CreateMesh(style, size);
            mesh.name = $"Edificio demo {id}";
            AssetDatabase.CreateAsset(mesh, $"{folder}/{id}.asset");
            return Place(parent, $"Edificio low poly {id}", mesh, size, materials);
        }

        /// <summary>The 1950 or 1980 model of a building: same box, same normalization, its own mesh asset.</summary>
        public static Transform BuildEpoch(Transform parent, string id, PaseoEpoch epoch, Vector3 size, string folder, Material[] materials)
        {
            Directory.CreateDirectory(folder);
            var mesh = CreateEpochMesh(epoch);
            mesh.name = $"Edificio {PaseoEpochs.Key(epoch)} {id}";
            AssetDatabase.CreateAsset(mesh, $"{folder}/{id}-{PaseoEpochs.Key(epoch)}.asset");
            return Place(parent, $"Edificio {PaseoEpochs.Key(epoch)} {id}", mesh, size, materials);
        }

        static Transform Place(Transform parent, string name, Mesh mesh, Vector3 size, Material[] materials)
        {
            var model = new GameObject(name);
            model.transform.SetParent(parent, false);
            model.transform.localScale = size;
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return model.transform;
        }

        // Geometry is authored in metres, then normalized around the measured box centre. Resizing in the field only scales it.
        internal static Mesh CreateMesh(string style, Vector3 size)
        {
            if (style != "academico" && style != "pabellon" && style != "clasico")
                throw new ArgumentException($"Modelo desconocido: {style}");
            var b = new Geometry(size);
            float w = size.x + 0.4f, h = size.y, d = size.z + 0.4f;
            float bottom = -h / 2f, top = h / 2f;
            // Closed, opaque shell covers the entire measured box, with 20 cm extra per side. Windows are painted panels.
            b.Box(Wall, Vector3.zero, new Vector3(w, h, d));
            b.Box(Accent, new Vector3(0f, bottom + 0.3f, 0f), new Vector3(w + 0.12f, 0.6f, d + 0.12f));
            b.Box(Trim, new Vector3(0f, top - 0.12f, 0f), new Vector3(w + 0.35f, 0.3f, d + 0.35f));

            int floors = style == "pabellon" ? 1 : 2;
            int bays = Mathf.Max(3, Mathf.RoundToInt(w / 3.3f));
            for (int side = -1; side <= 1; side += 2)
            {
                // Both long facades have doors, windows and a canopy: no assumption about which side the visitor faces.
                float z = side * (d / 2f + 0.07f);
                for (int floor = 0; floor < floors; floor++)
                {
                    float y = bottom + (floor + 0.55f) * h / floors;
                    for (int bay = 0; bay < bays; bay++)
                    {
                        float x = -w / 2f + (bay + 0.5f) * w / bays;
                        bool door = floor == 0 && bay == bays / 2;
                        float windowHeight = door ? h / floors * 0.74f : h / floors * 0.43f;
                        float windowY = door ? bottom + windowHeight / 2f : y;
                        float windowWidth = w / bays * 0.58f;
                        b.Box(Trim, new Vector3(x, windowY, z), new Vector3(windowWidth + 0.22f, windowHeight + 0.22f, 0.16f));
                        b.Box(Glass, new Vector3(x, windowY, z + side * 0.1f), new Vector3(windowWidth, windowHeight, 0.1f));
                        b.Box(Trim, new Vector3(x, windowY, z + side * 0.17f), new Vector3(0.07f, windowHeight, 0.05f));
                    }
                }
                float doorX = -w / 2f + (bays / 2 + 0.5f) * w / bays;
                float canopyY = bottom + h / floors * 0.82f;
                b.Box(style == "academico" ? Accent : Roof, new Vector3(doorX, canopyY, side * (d / 2f + 0.6f)),
                    new Vector3(w / bays + 0.6f, 0.2f, 1.4f));
                if (style == "clasico")
                    foreach (float x in new[] { doorX - w / bays * 0.6f, doorX + w / bays * 0.6f })
                        b.Box(Trim, new Vector3(x, (bottom + canopyY) / 2f, side * (d / 2f + 1.1f)),
                            new Vector3(0.32f, canopyY - bottom, 0.32f));
                if (floors == 2)
                    b.Box(Trim, new Vector3(0f, 0f, side * (d / 2f + 0.1f)), new Vector3(w + 0.15f, 0.18f, 0.2f));

                // Short facades close the silhouette too, with two windows on each storey.
                for (int floor = 0; floor < floors; floor++)
                    foreach (float windowZ in new[] { -d * 0.24f, d * 0.24f })
                    {
                        var p = new Vector3(side * (w / 2f + 0.06f), bottom + (floor + 0.55f) * h / floors, windowZ);
                        b.Box(Trim, p, new Vector3(0.16f, h / floors * 0.43f + 0.22f, d * 0.23f + 0.22f));
                        p.x += side * 0.1f;
                        b.Box(Glass, p, new Vector3(0.1f, h / floors * 0.43f, d * 0.23f));
                    }
            }

            if (style == "academico")
            {
                b.Box(Accent, new Vector3(0f, top + 0.25f, 0f), new Vector3(w + 0.4f, 0.5f, d + 0.4f));
                b.Box(Glass, new Vector3(0f, top + 0.52f, 0f), new Vector3(w - 0.8f, 0.08f, d - 0.8f));
                b.Box(Trim, new Vector3(0f, top + 0.7f, 0f), new Vector3(w * 0.38f, 0.6f, d * 0.5f));
                b.Box(Accent, new Vector3(0f, top + 1.04f, 0f), new Vector3(w * 0.4f, 0.12f, d * 0.52f));
            }
            else
                b.Gable(w + 0.7f, d + 0.7f, top, style == "clasico" ? 1.65f : 1.1f);
            return b.ToMesh();
        }

        // Imported geometry is normalized to a unit box centred at the origin. Each building's measured size is
        // applied by Place/SetSize, keeping every era in the same reference frame.
        internal static Mesh CreateEpochMesh(PaseoEpoch epoch) => PaseoEpochModelImporter.CreateMesh(epoch);

        sealed class Geometry
        {
            readonly Vector3 size;
            readonly List<Vector3> vertices = new();
            readonly List<int>[] triangles = { new(), new(), new(), new(), new() };

            public Geometry(Vector3 size) => this.size = size;

            void Face(int material, params Vector3[] points)
            {
                int start = vertices.Count;
                foreach (var p in points)
                    vertices.Add(new Vector3(p.x / size.x, p.y / size.y, p.z / size.z));
                for (int i = 1; i < points.Length - 1; i++)
                    triangles[material].AddRange(new[] { start, start + i, start + i + 1 });
            }

            public void Box(int material, Vector3 centre, Vector3 dimensions)
            {
                var e = dimensions * 0.5f;
                var p = new Vector3[8];
                for (int i = 0; i < p.Length; i++)
                    p[i] = centre + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                // Outward winding, separate vertices per face for hard edges.
                foreach (var q in new[] { new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 4, 6, 2 },
                             new[] { 1, 3, 7, 5 }, new[] { 0, 1, 5, 4 }, new[] { 2, 6, 7, 3 } })
                    Face(material, p[q[0]], p[q[1]], p[q[2]], p[q[3]]);
            }

            public void Gable(float width, float depth, float y, float rise)
            {
                var a = new Vector3(-width / 2f, y, -depth / 2f);
                var b = new Vector3(width / 2f, y, -depth / 2f);
                var c = new Vector3(-width / 2f, y, depth / 2f);
                var d = new Vector3(width / 2f, y, depth / 2f);
                var l = new Vector3(-width / 2f, y + rise, 0f);
                var r = new Vector3(width / 2f, y + rise, 0f);
                Face(Roof, a, l, r, b);
                Face(Roof, c, d, r, l);
                Face(Wall, a, c, l);
                Face(Wall, b, r, d);
                Face(Roof, a, b, d, c);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh { name = "Edificio low poly", subMeshCount = triangles.Length };
                mesh.SetVertices(vertices);
                for (int i = 0; i < triangles.Length; i++)
                    mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
#endif
