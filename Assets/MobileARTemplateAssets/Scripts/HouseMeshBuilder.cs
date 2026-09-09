using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Builds a simple gable-roofed building as a runtime mesh, in real-world metres.
    ///
    /// The mesh is authored at its final size instead of being scaled up from a unit cube: a house
    /// is not a stretched box, and the roof pitch has to stay independent from the walls. It is also
    /// built around the pivot the calibration flow needs - centred horizontally, base at y = 0,
    /// facade at +Z - so growing the depth pushes the building away from the sign instead of through
    /// the person looking at it.
    /// </summary>
    public static class HouseMeshBuilder
    {
        /// <param name="size">Overall bounding size in metres: x = facade length, y = total height
        /// including the roof, z = depth from the facade backwards.</param>
        /// <param name="roofHeight">How much of <paramref name="size"/>.y is roof. The walls get
        /// whatever is left.</param>
        /// <param name="ridgeAlongWidth">True when the ridge runs parallel to the facade, so the
        /// facade shows a wall with a sloping roof behind it. False puts the triangular gable on
        /// the facade itself.</param>
        public static Mesh Build(Vector3 size, float roofHeight, bool ridgeAlongWidth)
        {
            var width = Mathf.Max(0.1f, size.x);
            var height = Mathf.Max(0.2f, size.y);
            var depth = Mathf.Max(0.1f, size.z);

            // The roof has to leave a wall under it and must not poke above the declared height, so
            // its share of the total is clamped rather than trusted.
            var roof = Mathf.Clamp(roofHeight, 0f, height - 0.1f);
            var wall = height - roof;

            var hw = width * 0.5f;
            var hd = depth * 0.5f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Walls, in cycle order around each face.
            AddQuad(vertices, normals, triangles,
                new Vector3(-hw, 0f, hd), new Vector3(hw, 0f, hd),
                new Vector3(hw, wall, hd), new Vector3(-hw, wall, hd), Vector3.forward);
            AddQuad(vertices, normals, triangles,
                new Vector3(-hw, 0f, -hd), new Vector3(-hw, wall, -hd),
                new Vector3(hw, wall, -hd), new Vector3(hw, 0f, -hd), Vector3.back);
            AddQuad(vertices, normals, triangles,
                new Vector3(-hw, 0f, -hd), new Vector3(-hw, 0f, hd),
                new Vector3(-hw, wall, hd), new Vector3(-hw, wall, -hd), Vector3.left);
            AddQuad(vertices, normals, triangles,
                new Vector3(hw, 0f, -hd), new Vector3(hw, 0f, hd),
                new Vector3(hw, wall, hd), new Vector3(hw, wall, -hd), Vector3.right);
            AddQuad(vertices, normals, triangles,
                new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd),
                new Vector3(hw, 0f, hd), new Vector3(-hw, 0f, hd), Vector3.down);

            if (roof < 0.01f)
            {
                // No roof left to build: cap the box so it does not read as an open shell.
                AddQuad(vertices, normals, triangles,
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

                AddQuad(vertices, normals, triangles,
                    new Vector3(-hw, wall, hd), new Vector3(hw, wall, hd), ridgeRight, ridgeLeft,
                    new Vector3(0f, hd, roof));
                AddQuad(vertices, normals, triangles,
                    new Vector3(-hw, wall, -hd), ridgeLeft, ridgeRight, new Vector3(hw, wall, -hd),
                    new Vector3(0f, hd, -roof));

                AddTriangle(vertices, normals, triangles,
                    new Vector3(-hw, wall, -hd), new Vector3(-hw, wall, hd), ridgeLeft, Vector3.left);
                AddTriangle(vertices, normals, triangles,
                    new Vector3(hw, wall, -hd), new Vector3(hw, wall, hd), ridgeRight, Vector3.right);
            }
            else
            {
                // Ridge perpendicular to the facade, so the facade shows the triangular gable.
                var ridgeFront = new Vector3(0f, height, hd);
                var ridgeBack = new Vector3(0f, height, -hd);

                AddQuad(vertices, normals, triangles,
                    new Vector3(hw, wall, -hd), new Vector3(hw, wall, hd), ridgeFront, ridgeBack,
                    new Vector3(roof, hw, 0f));
                AddQuad(vertices, normals, triangles,
                    new Vector3(-hw, wall, -hd), ridgeBack, ridgeFront, new Vector3(-hw, wall, hd),
                    new Vector3(-roof, hw, 0f));

                AddTriangle(vertices, normals, triangles,
                    new Vector3(-hw, wall, hd), new Vector3(hw, wall, hd), ridgeFront, Vector3.forward);
                AddTriangle(vertices, normals, triangles,
                    new Vector3(-hw, wall, -hd), new Vector3(hw, wall, -hd), ridgeBack, Vector3.back);
            }

            var mesh = new Mesh { name = "AncoRA House" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
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
