using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>
    /// Old film wear over the camera image: thin vertical scratches that wander, dust specks that change every frame and
    /// a brightness flicker. Drawn with the vector API, nothing to load; it lets every touch through. Strengths come from
    /// PaseoEpochFilter (0 = nothing drawn).
    /// </summary>
    public sealed class PaseoFilmOverlay
    {
        const int ScratchCount = 3;
        const int DustCount = 14;

        readonly float[] scratchX = new float[ScratchCount];
        readonly float[] scratchLife = new float[ScratchCount];
        float scratches;
        float flicker;
        float flickerNow;
        System.Random random = new(1950);

        public VisualElement Root { get; }

        public PaseoFilmOverlay()
        {
            Root = new VisualElement { name = "pelicula", pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.left = 0;
            Root.style.right = 0;
            Root.style.top = 0;
            Root.style.bottom = 0;
            Root.generateVisualContent += Draw;
            for (int i = 0; i < ScratchCount; i++)
                scratchX[i] = Next();
        }

        /// <summary>Called every frame by PaseoUi.</summary>
        public void Tick(float scratchStrength, float flickerStrength, float deltaTime)
        {
            scratches = Mathf.Clamp01(scratchStrength);
            flicker = Mathf.Clamp01(flickerStrength);
            bool visible = scratches > 0.001f || flicker > 0.001f;
            Root.visible = visible;
            if (!visible)
                return;
            // Scratches drift sideways, then jump somewhere else after a short life.
            for (int i = 0; i < ScratchCount; i++)
            {
                scratchLife[i] -= deltaTime;
                if (scratchLife[i] <= 0f)
                {
                    scratchX[i] = Next();
                    scratchLife[i] = 0.2f + Next() * 1.4f;
                }
                scratchX[i] = Mathf.Repeat(scratchX[i] + (Next() - 0.5f) * 0.004f, 1f);
            }
            flickerNow = flicker * Next();
            Root.MarkDirtyRepaint();
        }

        float Next() => (float)random.NextDouble();

        void Draw(MeshGenerationContext context)
        {
            var rect = Root.contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;
            var painter = context.painter2D;
            if (flickerNow > 0.001f)
            {
                painter.fillColor = new Color(0f, 0f, 0f, flickerNow);
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, 0f));
                painter.LineTo(new Vector2(rect.width, 0f));
                painter.LineTo(new Vector2(rect.width, rect.height));
                painter.LineTo(new Vector2(0f, rect.height));
                painter.ClosePath();
                painter.Fill();
            }
            if (scratches <= 0.001f)
                return;
            painter.lineWidth = 1.2f;
            for (int i = 0; i < ScratchCount; i++)
            {
                // Not every scratch shows on every frame.
                if (Next() > 0.75f)
                    continue;
                float x = scratchX[i] * rect.width;
                float y0 = Next() * rect.height * 0.3f;
                float y1 = rect.height * (0.6f + Next() * 0.4f);
                bool light = i % 2 == 0;
                painter.strokeColor = light ? new Color(1f, 0.97f, 0.9f, 0.35f * scratches) : new Color(0.1f, 0.08f, 0.05f, 0.4f * scratches);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y0));
                painter.LineTo(new Vector2(x + (Next() - 0.5f) * 3f, y1));
                painter.Stroke();
            }
            int dust = Mathf.RoundToInt(DustCount * scratches);
            for (int i = 0; i < dust; i++)
            {
                if (Next() > 0.5f)
                    continue;
                var centre = new Vector2(Next() * rect.width, Next() * rect.height);
                float radius = 0.6f + Next() * 1.8f;
                painter.fillColor = Next() > 0.5f ? new Color(0.05f, 0.04f, 0.03f, 0.5f * scratches) : new Color(1f, 1f, 0.95f, 0.4f * scratches);
                painter.BeginPath();
                painter.Arc(centre, radius, 0f, 360f);
                painter.Fill();
            }
        }
    }
}
