using UnityEngine;
using UnityEngine.Rendering;

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
            if (color.a < 0.999f)
                ConfigureTransparency(material);
            return material;
        }

        static void ConfigureTransparency(Material material)
        {
            // URP does not infer a transparent surface from the colour alpha. Configure the same
            // state its material inspector would write so the diagnostic shell remains see-through
            // on the phone instead of hiding the facade we are trying to compare against.
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
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
