using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Builds the soft dark patch drawn under the building where it meets the ground.
    /// </summary>
    /// <remarks>
    /// A model with no shadow reads as pasted onto the photo no matter how well it is placed,
    /// because nothing ties it to the ground plane. A real shadow is not an option here: there is no
    /// ground geometry to receive one - the floor is camera pixels - so a real-time shadow would fall
    /// on nothing.
    ///
    /// This fakes the contact instead. It is a quad lying on the base of the building, with a
    /// procedural texture that fades out towards its edges, which is the part of a shadow the eye
    /// actually uses to judge that an object is resting on something.
    /// </remarks>
    public static class ContactShadowBuilder
    {
        const int k_Resolution = 128;

        /// <summary>How far past the footprint the shadow spreads, as a fraction of the footprint.</summary>
        public const float Spread = 0.35f;

        /// <summary>A unit quad in the XZ plane, centred on its origin and facing up.</summary>
        public static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "AncoRA Contact Shadow" };

            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
            });

            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            });

            // Wound so the visible side faces up, which is where the camera is.
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A texture that is opaque over the footprint and fades to nothing at the border.
        /// </summary>
        /// <remarks>
        /// Generated rather than shipped as an asset: it is a gradient, so authoring it in a file
        /// would add an import to keep in sync for no gain. The falloff is squared so the darkness
        /// stays concentrated near the walls, which is how a real contact shadow behaves.
        /// </remarks>
        public static Texture2D BuildFalloffTexture()
        {
            var texture = new Texture2D(k_Resolution, k_Resolution, TextureFormat.RGBA32, false)
            {
                name = "AncoRA Contact Shadow Falloff",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            // The footprint occupies the middle of the texture and the spread is the margin around
            // it, so the fade has to start where the walls are, not at the texture border.
            var inner = 0.5f / (1f + 2f * Spread);
            var pixels = new Color32[k_Resolution * k_Resolution];

            for (var y = 0; y < k_Resolution; y++)
            {
                for (var x = 0; x < k_Resolution; x++)
                {
                    var u = (x + 0.5f) / k_Resolution - 0.5f;
                    var v = (y + 0.5f) / k_Resolution - 0.5f;

                    // Distance to the footprint rectangle, not to its centre: a radial blob under a
                    // long building leaves the ends of it floating.
                    var dx = Mathf.Max(0f, Mathf.Abs(u) - inner);
                    var dy = Mathf.Max(0f, Mathf.Abs(v) - inner);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    var falloff = Mathf.Clamp01(1f - distance / Mathf.Max(1e-4f, Spread * inner * 2f));
                    var alpha = falloff * falloff;

                    pixels[y * k_Resolution + x] = new Color32(0, 0, 0, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
