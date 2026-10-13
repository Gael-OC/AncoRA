namespace AncorRA.AR
{
    /// <summary>Five quick taps on the top-left corner open or close the team sheet; a visitor never hits it by chance.</summary>
    public sealed class PaseoTapGesture
    {
        public const int Taps = 5;
        public const float WindowSeconds = 1.5f;

        int count;
        float last = float.NegativeInfinity;

        /// <summary>True on the tap that completes the series.</summary>
        public bool Tap(float now)
        {
            count = now - last < WindowSeconds ? count + 1 : 1;
            last = now;
            if (count < Taps)
                return false;
            count = 0;
            return true;
        }
    }
}
