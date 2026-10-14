namespace AncorRA.AR
{
    /// <summary>
    /// How often the tour asks Immersal to localize. Fast while searching, which is what makes a building appear sooner;
    /// slow while the camera looks at a building already located and settled, where a new localization only refines a
    /// box that is already right. On the Xiaomi 14 a field session at a steady 0.5 s used ~1.2 cores and left the CPU at
    /// 82–90 °C. Looking away from every located box goes back to fast at once.
    /// </summary>
    public sealed class PaseoPace
    {
        public const float FastSeconds = 0.5f;
        public const float SlowSeconds = 2f;
        // After a lock the filter still averages its first localizations: keep them coming for a moment.
        public const float SettleSeconds = 3f;

        float inViewSince = float.NaN;

        public float Interval(float now, bool locatedBoxInView)
        {
            if (!locatedBoxInView)
            {
                inViewSince = float.NaN;
                return FastSeconds;
            }
            if (float.IsNaN(inViewSince))
                inViewSince = now;
            return now - inViewSince >= SettleSeconds ? SlowSeconds : FastSeconds;
        }
    }
}
