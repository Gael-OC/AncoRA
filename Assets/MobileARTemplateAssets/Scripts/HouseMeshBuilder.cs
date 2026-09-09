using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Builds a simple gable-roofed building as a runtime mesh, in real-world metres.
    ///
    /// The mesh is authored at its final size instead of being scaled up from a unit cube: a house
    /// is not a stretched box, and the roof pitch has to stay independent from the walls. It is also
    /// built around the origin the calibration flow expects - centred horizontally, base at y = 0,
    /// facade at +Z.
    ///
    /// Walls and roof come out as two separate submeshes so each can carry its own colour. A single
    /// flat volume reads as a block; the moment the roof is a different shade, it reads as a
    /// building.
    /// </summary>
    public static class HouseMeshBuilder
    {
        /// <summary>Submesh index of the walls and floor.</summary>
        public const int WallSubmesh = 0;

        /// <summary>Submesh index of the roof.</summary>
        public const int RoofSubmesh = 1;

        /// <param name="size">Overall bounding size in metres: x = facade length, y = total height
        /// including the roof, z = depth from the facade backwards.</param>
        /// <param name="roofHeight">How much of <paramref name="size"/>.y is roof. The walls get
        /// whatever is left.</param>
        /// <param name="ridgeAlongWidth">True when the ridge runs parallel to the facade, so the
        /// facade shows a wall with a sloping roof behind it. False puts the triangular gable on
        /// the facade itself.</param>
        public static Mesh Build(Vector3 size, float roofHeight, bool ridgeAlongWidth)
        {
            Measure(size, roofHeight, out var hw, out var hd, out var wall, out var height, out var roof);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var walls = new List<int>();
            var roofTriangles = new List<int>();

            // Walls, in cycle order around each face.
            AddQuad(vertices, normals, walls,
                new Vector3(-hw, 0f, hd), new Vector3(hw, 0f, hd),
                new Vector3(hw, wall, hd), new Vector3(-hw, wall, hd), Vector3.forward);
            AddQuad(vertices, normals, walls,
                new Vector3(-hw, 0f, -hd), new Vector3(-hw, wall, -hd),
                new Vector3(hw, wall, -hd), new Vector3(hw, 0f, -hd), Vector3.back);
            AddQuad(vertices, normals, walls,
                new Vector3(-hw, 0f, -hd), new Vector3(-hw, 0f, hd),
                new Vector3(-hw, wall, hd), new Vector3(-hw, wall, -hd), Vector3.left);
            AddQuad(vertices, normals, walls,
                new Vector3(hw, 0f, -hd), new Vector3(hw, 0f, hd),
                new Vector3(hw, wall, hd), new Vector3(hw, wall, -hd), Vector3.right);
            AddQuad(vertices, normals, walls,
                new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd),
                new Vector3(hw, 0f, hd), new Vector3(-hw, 0f, hd), Vector3.down);

            if (roof < 0.01f)
            {
                // No roof left to build: cap the box so it does not read as an open shell.
                AddQuad(vertices, normals, walls,
                    new Vector3(-hw, wall, -hd), new Vector3(hw, wall, -hd),
                    new Vector3(hw, wall, hd), new Vector3(-hw, wall, hd), Vector3.up);
            }
            else if (ridgeAlongWidth)
            {
                // Ridge parallel to the facade. The slope normals interpolate between straight up
                // for a flat roof and straight out for a vertical one, which is what (run, rise)
                // gives once the two are swapped between the horizontal and vertical components.
                var ridgeLeft = new Vector3(-hw, height, 0f);
                var ridgeRight = new Vector3(hw, height, 0f);

                AddQuad(vertices, normals, roofTriangles,
                    new Vector3(-hw, wall, hd), new Vector3(hw, wall, hd), ridgeRight, ridgeLeft,
                    new Vector3(0f, hd, roof));
                AddQuad(vertices, normals, roofTriangles,
                    new Vector3(-hw, wall, -hd), ridgeLeft, ridgeRight, new Vector3(hw, wall, -hd),
                    new Vector3(0f, hd, -roof));

                AddTriangle(vertices, normals, roofTriangles,
                    new Vector3(-hw, wall, -hd), new Vector3(-hw, wall, hd), ridgeLeft, Vector3.left);
                AddTriangle(vertices, normals, roofTriangles,
                    new Vector3(hw, wall, -hd), new Vector3(hw, wall, hd), ridgeRight, Vector3.right);
            }
            else
            {
                // Ridge perpendicular to the facade, so the facade shows the triangular gable.
                var ridgeFront = new Vector3(0f, height, hd);
                var ridgeBack = new Vector3(0f, height, -hd);

                AddQuad(vertices, normals, roofTriangles,
                    new Vector3(hw, wall, -hd), new Vector3(hw, wall, hd), ridgeFront, ridgeBack,
                    new Vector3(roof, hw, 0f));
                AddQuad(vertices, normals, roofTriangles,
                    new Vector3(-hw, wall, -hd), ridgeBack, ridgeFront, new Vector3(-hw, wall, hd),
                    new Vector3(-roof, hw, 0f));

                AddTriangle(vertices, normals, roofTriangles,
                    new Vector3(-hw, wall, hd), new Vector3(hw, wall, hd), ridgeFront, Vector3.forward);
                AddTriangle(vertices, normals, roofTriangles,
                    new Vector3(-hw, wall, -hd), new Vector3(hw, wall, -hd), ridgeBack, Vector3.back);
            }

            var mesh = new Mesh { name = "AncoRA House" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(walls, WallSubmesh);
            mesh.SetTriangles(roofTriangles, RoofSubmesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds the building's real edges as a line-topology mesh: wall corners, the eaves line,
        /// the ridge and the gable slopes.
        /// </summary>
        /// <remarks>
        /// This traces the actual silhouette rather than the axis-aligned bounding box. A bounding
        /// box around a gable roof draws a lid where the roof slopes are, which reads as a crate
        /// instead of a house and hides exactly the feature that tells you the model is oriented
        /// correctly.
        /// </remarks>
        public static Mesh BuildEdges(Vector3 size, float roofHeight, bool ridgeAlongWidth)
        {
            Measure(size, roofHeight, out var hw, out var hd, out var wall, out var height, out var roof);

            var vertices = new List<Vector3>();
            var indices = new List<int>();

            // Base and the top of the walls.
            AddRectangle(vertices, indices, 0f, hw, hd);
            AddRectangle(vertices, indices, wall, hw, hd);

            // Wall corners.
            AddLine(vertices, indices, new Vector3(-hw, 0f, -hd), new Vector3(-hw, wall, -hd));
            AddLine(vertices, indices, new Vector3(hw, 0f, -hd), new Vector3(hw, wall, -hd));
            AddLine(vertices, indices, new Vector3(hw, 0f, hd), new Vector3(hw, wall, hd));
            AddLine(vertices, indices, new Vector3(-hw, 0f, hd), new Vector3(-hw, wall, hd));

            if (roof >= 0.01f)
            {
                if (ridgeAlongWidth)
                {
                    var ridgeLeft = new Vector3(-hw, height, 0f);
                    var ridgeRight = new Vector3(hw, height, 0f);

                    AddLine(vertices, indices, ridgeLeft, ridgeRight);
                    AddLine(vertices, indices, new Vector3(-hw, wall, -hd), ridgeLeft);
                    AddLine(vertices, indices, new Vector3(-hw, wall, hd), ridgeLeft);
                    AddLine(vertices, indices, new Vector3(hw, wall, -hd), ridgeRight);
                    AddLine(vertices, indices, new Vector3(hw, wall, hd), ridgeRight);
                }
                else
                {
                    var ridgeFront = new Vector3(0f, height, hd);
                    var ridgeBack = new Vector3(0f, height, -hd);

                    AddLine(vertices, indices, ridgeBack, ridgeFront);
                    AddLine(vertices, indices, new Vector3(-hw, wall, hd), ridgeFront);
                    AddLine(vertices, indices, new Vector3(hw, wall, hd), ridgeFront);
                    AddLine(vertices, indices, new Vector3(-hw, wall, -hd), ridgeBack);
                    AddLine(vertices, indices, new Vector3(hw, wall, -hd), ridgeBack);
                }
            }

            var mesh = new Mesh { name = "AncoRA House Edges" };
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Builds the 12 edges of an arbitrary box as a line-topology mesh.</summary>
        public static Mesh BuildBoxEdges(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;

            var vertices = new List<Vector3>
            {
                new(min.x, min.y, min.z), new(max.x, min.y, min.z),
                new(max.x, min.y, max.z), new(min.x, min.y, max.z),
                new(min.x, max.y, min.z), new(max.x, max.y, min.z),
                new(max.x, max.y, max.z), new(min.x, max.y, max.z),
            };

            var indices = new List<int>
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7,
            };

            var mesh = new Mesh { name = "AncoRA Box Edges" };
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Resolves the dimensions both builders need, so they cannot disagree.</summary>
        static void Measure(
            Vector3 size, float roofHeight,
            out float halfWidth, out float halfDepth, out float wallHeight, out float totalHeight,
            out float roof)
        {
            var width = Mathf.Max(0.1f, size.x);
            totalHeight = Mathf.Max(0.2f, size.y);
            var depth = Mathf.Max(0.1f, size.z);

            // The roof has to leave a wall under it and must not poke above the declared height, so
            // its share of the total is clamped rather than trusted.
            roof = Mathf.Clamp(roofHeight, 0f, totalHeight - 0.1f);
            wallHeight = totalHeight - roof;

            halfWidth = width * 0.5f;
            halfDepth = depth * 0.5f;
        }

        static void AddRectangle(List<Vector3> vertices, List<int> indices, float y, float hw, float hd)
        {
            var a = new Vector3(-hw, y, -hd);
            var b = new Vector3(hw, y, -hd);
            var c = new Vector3(hw, y, hd);
            var d = new Vector3(-hw, y, hd);

            AddLine(vertices, indices, a, b);
            AddLine(vertices, indices, b, c);
            AddLine(vertices, indices, c, d);
            AddLine(vertices, indices, d, a);
        }

        static void AddLine(List<Vector3> vertices, List<int> indices, Vector3 from, Vector3 to)
        {
            indices.Add(vertices.Count);
            indices.Add(vertices.Count + 1);
            vertices.Add(from);
            vertices.Add(to);
        }

        static void AddQuad(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            AddTriangle(vertices, normals, triangles, a, b, c, outward);
            AddTriangle(vertices, normals, triangles, a, c, d, outward);
        }

        static void AddTriangle(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            // Unity culls back faces by winding order, so one quad wound the wrong way leaves a hole
            // in a wall. State which way the face should look and let the order follow from that,
            // rather than tracking clockwise-ness by hand across a dozen faces.
            var normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, outward) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            normal = normal.sqrMagnitude > 1e-10f ? normal.normalized : outward.normalized;

            var index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
        }
    }
}
