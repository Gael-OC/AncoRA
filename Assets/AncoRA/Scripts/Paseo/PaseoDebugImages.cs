using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace AncorRA.AR
{
    /// <summary>
    /// Debug photos (PaseoDebug): every few seconds the image an attempt handed the plugin, small, grey and upright, saved
    /// next to the session log as imagenes/&lt;session&gt;/&lt;attempt&gt;-&lt;map or ninguno&gt;.jpg, so a failed attempt can be seen
    /// afterwards. Converted and written off the main thread.
    /// </summary>
    public static class PaseoDebugImages
    {
        public const float EverySeconds = 3f;
        // ~15 KB each: some 6 MB for a long session, small enough to send by WhatsApp.
        public const int MaxPerSession = 400;
        public const int MaxWidth = 480;
        const int JpgQuality = 70;

        public static bool ShouldCapture(float now, float lastCapture, int saved) => saved < MaxPerSession && now - lastCapture >= EverySeconds;

        public static string FileName(int attempt, int matchedMapId) => $"{attempt:D4}-{(matchedMapId > 0 ? matchedMapId.ToString() : "ninguno")}.jpg";

        /// <summary>
        /// The first channel of the image as grey RGB24, shrunk by a whole step to <paramref name="maxWidth"/> at most and,
        /// for a phone held upright, turned a quarter clockwise (the camera sensor lies landscape). Rows go from the bottom,
        /// as Unity's encoders read them.
        /// </summary>
        public static byte[] ToRgb(byte[] pixels, int width, int height, int channels, int maxWidth, bool rotateClockwise,
            out int outWidth, out int outHeight)
        {
            channels = Math.Max(1, channels);
            int step = Math.Max(1, (width + maxWidth - 1) / maxWidth);
            int w = width / step, h = height / step;
            outWidth = rotateClockwise ? h : w;
            outHeight = rotateClockwise ? w : h;
            var rgb = new byte[outWidth * outHeight * 3];
            for (int y = 0; y < outHeight; y++)
            for (int x = 0; x < outWidth; x++)
            {
                // Source pixel, top-left origin: turned clockwise, the output (x, y) is the source (y, h - 1 - x).
                int sx = rotateClockwise ? y : x;
                int sy = rotateClockwise ? h - 1 - x : y;
                byte value = pixels[((sy * step) * width + sx * step) * channels];
                int o = ((outHeight - 1 - y) * outWidth + x) * 3;
                rgb[o] = rgb[o + 1] = rgb[o + 2] = value;
            }
            return rgb;
        }

        public static Task Save(string folder, PaseoAttempt attempt, byte[] pixels, int width, int height, int channels, bool rotateClockwise)
        {
            string path = Path.Combine(folder, FileName(attempt.Number, attempt.MatchedMapId));
            return Task.Run(() =>
            {
                try
                {
                    var rgb = ToRgb(pixels, width, height, channels, MaxWidth, rotateClockwise, out int w, out int h);
                    var jpg = ImageConversion.EncodeArrayToJPG(rgb, GraphicsFormat.R8G8B8_UNorm, (uint)w, (uint)h, 0, JpgQuality);
                    Directory.CreateDirectory(folder);
                    File.WriteAllBytes(path, jpg);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AncoRA Paseo] No se pudo guardar la foto de depuración {path}: {e.Message}");
                }
            });
        }
    }
}
