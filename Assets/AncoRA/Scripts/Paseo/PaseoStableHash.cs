using System.Text;

namespace AncorRA.AR
{
    /// <summary>
    /// Hash that is the same on every platform and run (string.GetHashCode is not): FNV-1a 32 bit over the UTF-8 bytes.
    /// </summary>
    public static class PaseoStableHash
    {
        const uint OffsetBasis = 2166136261u;
        const uint Prime = 16777619u;

        public static uint Of(string text)
        {
            uint hash = OffsetBasis;
            foreach (byte b in Encoding.UTF8.GetBytes(text ?? ""))
            {
                hash ^= b;
                hash = unchecked(hash * Prime);
            }
            return hash;
        }
    }
}
