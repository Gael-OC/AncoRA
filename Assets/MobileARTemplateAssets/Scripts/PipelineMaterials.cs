using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Creates materials on shaders that survive into a player build.
    /// </summary>
    /// <remarks>
    /// Two tempting shortcuts do not work here. <c>GameObject.CreatePrimitive</c> hands back the
    /// legacy built-in material, which URP cannot render and draws as flat magenta. And
    /// <c>RenderPipelineAsset.defaultMaterial</c> is not a fix, because URP returns null for it
    /// outside the Editor - so anything built on it looks right on the desktop and comes out purple
    /// on the phone, which is exactly the bug this exists to prevent.
    /// </remarks>
    public static class PipelineMaterials
    {
        /// <summary>A lit surface material in the given colour.</summary>
        public static Material CreateLit(Color color) =>
            Create(color, "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit");

        /// <summary>An unlit material, for lines and gizmos that should not pick up scene lighting.</summary>
        public static Material CreateUnlit(Color color) =>
            Create(color, "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Lit");

        static Material Create(Color color, string preferred, string fallback)
        {
            var shader = Shader.Find(preferred) ?? Shader.Find(fallback) ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning(
                    $"No encontré el shader '{preferred}' ni un reemplazo. Revisa que esté incluido " +
                    "en el build; el contenido va a salir magenta.");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var material = new Material(shader);
            SetColor(material, color);
            return material;
        }

        /// <summary>Writes a colour into whichever colour property the shader actually exposes.</summary>
        public static void SetColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }
    }
}
