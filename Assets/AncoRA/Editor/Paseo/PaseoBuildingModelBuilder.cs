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
    /// <summary>Demo architecture, not historical reconstructions. One baked mesh per building, five shared materials.</summary>
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

        /// <summary>Materials of an era model (1950 or 1980), in the same slots as the demo model: wall, trim, glass, roof, accent.</summary>
        public static Material[] CreateEpochMaterials(string folder, Shader shader, PaseoEpoch epoch) => epoch switch
        {
            // Whitewash, dark wood, old glass, clay tiles, stone.
            PaseoEpoch.Y1950 => CreateMaterials(folder, shader, new[] { "Cal", "Madera", "VidrioAntiguo", "Teja", "Piedra" },
                new[]
                {
                    new Color(0.93f, 0.90f, 0.82f), new Color(0.36f, 0.24f, 0.15f), new Color(0.10f, 0.12f, 0.13f),
                    new Color(0.62f, 0.27f, 0.16f), new Color(0.72f, 0.64f, 0.50f)
                }),
            // Exposed concrete, darker concrete, tinted glass, roof membrane, brick.
            PaseoEpoch.Y1980 => CreateMaterials(folder, shader, new[] { "Hormigon", "HormigonOscuro", "VidrioOscuro", "Membrana", "Ladrillo" },
                new[]
                {
                    new Color(0.68f, 0.67f, 0.64f), new Color(0.45f, 0.45f, 0.44f), new Color(0.16f, 0.20f, 0.22f),
                    new Color(0.30f, 0.30f, 0.31f), new Color(0.55f, 0.30f, 0.20f)
                }),
            _ => throw new ArgumentException($"La época {epoch} usa el modelo de demo o ninguno.")
        };

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
            var mesh = CreateEpochMesh(epoch, size);
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

        // Era models: illustrative architecture of each period, not a reconstruction of these buildings. Same rules as the
        // demo model: authored in metres, a closed shell over the whole box (+20 cm per side), normalized to the box.
        internal static Mesh CreateEpochMesh(PaseoEpoch epoch, Vector3 size) => epoch switch
        {
            PaseoEpoch.Y1950 => Create1950(size),
            PaseoEpoch.Y1980 => Create1980(size),
            _ => throw new ArgumentException($"La época {epoch} no tiene modelo propio.")
        };

        // Whitewashed walls on a stone plinth, tall shuttered windows, a columned portico with a pediment on each long
        // facade, a clay-tile gable roof with chimneys and a small bell tower.
        static Mesh Create1950(Vector3 size)
        {
            var b = new Geometry(size);
            float w = size.x + 0.4f, h = size.y, d = size.z + 0.4f;
            float bottom = -h / 2f, top = h / 2f;
            b.Box(Wall, Vector3.zero, new Vector3(w, h, d));
            b.Box(Accent, new Vector3(0f, bottom + 0.45f, 0f), new Vector3(w + 0.16f, 0.9f, d + 0.16f));
            b.Box(Trim, new Vector3(0f, top - 0.2f, 0f), new Vector3(w + 0.5f, 0.4f, d + 0.5f));

            int floors = Mathf.Clamp(Mathf.RoundToInt(h / 3.6f), 1, 3);
            float storey = h / floors;
            int bays = Mathf.Max(4, Mathf.RoundToInt(w / 3f));
            float bayWidth = w / bays;
            int porticoBay = bays / 2;
            float porticoX = -w / 2f + (porticoBay + 0.5f) * bayWidth;
            for (int floor = 1; floor < floors; floor++)
                b.Box(Accent, new Vector3(0f, bottom + floor * storey, 0f), new Vector3(w + 0.12f, 0.18f, d + 0.12f));

            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * (d / 2f + 0.06f);
                for (int floor = 0; floor < floors; floor++)
                    for (int bay = 0; bay < bays; bay++)
                    {
                        float x = -w / 2f + (bay + 0.5f) * bayWidth;
                        bool door = floor == 0 && bay == porticoBay;
                        float wh = door ? storey * 0.72f : storey * 0.5f;
                        float ww = door ? bayWidth * 0.5f : bayWidth * 0.32f;
                        float wy = door ? bottom + wh / 2f : bottom + (floor + 0.55f) * storey;
                        b.Box(Trim, new Vector3(x, wy, z), new Vector3(ww + 0.24f, wh + 0.24f, 0.16f));
                        b.Box(Glass, new Vector3(x, wy, z + side * 0.1f), new Vector3(ww, wh, 0.1f));
                        b.Box(Trim, new Vector3(x, wy, z + side * 0.17f), new Vector3(0.06f, wh, 0.05f));
                        if (door)
                            continue;
                        // Open wooden shutters on both sides of the window.
                        foreach (int s in new[] { -1, 1 })
                            b.Box(Trim, new Vector3(x + s * (ww * 0.75f + 0.14f), wy, z + side * 0.06f), new Vector3(ww * 0.5f, wh, 0.06f));
                    }

                // Portico: four columns one storey high, an entablature back to the wall and a pediment over it.
                float span = bayWidth * 1.8f;
                float columnTop = bottom + Mathf.Min(storey * 0.95f, h * 0.8f);
                float front = d / 2f + 2.2f;
                for (int i = 0; i < 4; i++)
                {
                    float x = porticoX + (i - 1.5f) * span / 3f;
                    b.Box(Wall, new Vector3(x, (bottom + columnTop) / 2f, side * (front - 0.3f)), new Vector3(0.42f, columnTop - bottom, 0.42f));
                }
                float entablatureDepth = front - d / 2f;
                b.Box(Wall, new Vector3(porticoX, columnTop + 0.22f, side * (d / 2f + entablatureDepth / 2f)),
                    new Vector3(span + 0.8f, 0.45f, entablatureDepth));
                float zNear = side * d / 2f, zFar = side * front;
                b.GableZ(porticoX, columnTop + 0.45f, Mathf.Min(zNear, zFar), Mathf.Max(zNear, zFar), span + 1f, 1.1f);
            }

            // Short facades: two windows per storey.
            for (int side = -1; side <= 1; side += 2)
                for (int floor = 0; floor < floors; floor++)
                    foreach (float windowZ in new[] { -d * 0.24f, d * 0.24f })
                    {
                        var p = new Vector3(side * (w / 2f + 0.06f), bottom + (floor + 0.55f) * storey, windowZ);
                        b.Box(Trim, p, new Vector3(0.16f, storey * 0.5f + 0.24f, 1.2f));
                        p.x += side * 0.1f;
                        b.Box(Glass, p, new Vector3(0.1f, storey * 0.5f, 0.96f));
                    }

            float rise = Mathf.Min(2.4f, d * 0.22f);
            b.Gable(w + 0.9f, d + 0.9f, top, rise);
            foreach (float x in new[] { -w * 0.3f, w * 0.3f })
                b.Box(Accent, new Vector3(x, top + rise * 0.6f, 0f), new Vector3(0.7f, 1.6f, 0.7f));
            // Bell tower on the ridge, open on two faces.
            float towerBase = top + rise - 0.2f;
            b.Box(Wall, new Vector3(0f, towerBase + 0.9f, 0f), new Vector3(1.6f, 1.8f, 1.6f));
            b.Box(Glass, new Vector3(0f, towerBase + 1.0f, 0f), new Vector3(0.7f, 0.9f, 1.7f));
            b.Gable(1.9f, 1.9f, towerBase + 1.8f, 0.9f);
            return b.ToMesh();
        }

        // Exposed concrete with a brick base, continuous ribbon windows behind vertical fins, a thick flat roof slab with
        // a water tank and a stair core, and a cantilevered entrance canopy on pilotis.
        static Mesh Create1980(Vector3 size)
        {
            var b = new Geometry(size);
            float w = size.x + 0.4f, h = size.y, d = size.z + 0.4f;
            float bottom = -h / 2f, top = h / 2f;
            b.Box(Wall, Vector3.zero, new Vector3(w, h, d));
            b.Box(Accent, new Vector3(0f, bottom + 0.5f, 0f), new Vector3(w + 0.1f, 1f, d + 0.1f));

            int floors = Mathf.Clamp(Mathf.RoundToInt(h / 3.4f), 1, 4);
            float storey = h / floors;
            for (int floor = 0; floor < floors; floor++)
            {
                float y = bottom + floor * storey + storey * 0.58f;
                float ribbon = storey * 0.38f;
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Box(Glass, new Vector3(0f, y, side * (d / 2f + 0.05f)), new Vector3(w - 1.2f, ribbon, 0.1f));
                    b.Box(Glass, new Vector3(side * (w / 2f + 0.05f), y, 0f), new Vector3(0.1f, ribbon, d - 1.2f));
                }
            }

            int fins = Mathf.Max(4, Mathf.RoundToInt(w / 1.8f));
            float finHeight = h - 1f;
            for (int i = 0; i < fins; i++)
            {
                float x = -w / 2f + (i + 0.5f) * w / fins;
                for (int side = -1; side <= 1; side += 2)
                    b.Box(Trim, new Vector3(x, bottom + 1f + finHeight / 2f, side * (d / 2f + 0.3f)), new Vector3(0.22f, finHeight, 0.6f));
            }

            b.Box(Trim, new Vector3(0f, top + 0.4f, 0f), new Vector3(w + 1.4f, 0.8f, d + 1.4f));
            b.Box(Roof, new Vector3(w * 0.25f, top + 1.6f, 0f), new Vector3(3f, 1.6f, 2.4f));
            b.Box(Wall, new Vector3(-w * 0.3f, top + 1.9f, 0f), new Vector3(2.4f, 2.2f, 2.4f));

            float canopyY = bottom + Mathf.Min(3f, h * 0.6f);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(Trim, new Vector3(0f, canopyY, side * (d / 2f + 1.2f)), new Vector3(4.5f, 0.35f, 2.4f));
                b.Box(Glass, new Vector3(0f, (bottom + canopyY) / 2f, side * (d / 2f + 0.08f)), new Vector3(2.4f, canopyY - bottom - 0.4f, 0.1f));
                foreach (float x in new[] { -2f, 2f })
                    b.Box(Trim, new Vector3(x, (bottom + canopyY) / 2f, side * (d / 2f + 2.2f)), new Vector3(0.4f, canopyY - bottom, 0.4f));
            }
            return b.ToMesh();
        }

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

            /// <summary>A gable whose ridge runs along z, from <paramref name="zMin"/> to <paramref name="zMax"/> (pediments).</summary>
            public void GableZ(float cx, float y, float zMin, float zMax, float width, float rise)
            {
                var a = new Vector3(cx - width / 2f, y, zMin);
                var b = new Vector3(cx + width / 2f, y, zMin);
                var c = new Vector3(cx - width / 2f, y, zMax);
                var d = new Vector3(cx + width / 2f, y, zMax);
                var f = new Vector3(cx, y + rise, zMin);
                var g = new Vector3(cx, y + rise, zMax);
                Face(Roof, a, c, g, f);
                Face(Roof, b, f, g, d);
                Face(Wall, a, f, b);
                Face(Wall, c, d, g);
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
