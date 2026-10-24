using System;

namespace AncorRA.AR
{
    /// <summary>
    /// Contrast enhancement of an 8-bit grey image for the team's contrast test (1.3.6): CLAHE, contrast-limited adaptive
    /// histogram equalisation, in OpenCV's sense. The image is cut into tiles; each tile gets the equalising curve of its own
    /// histogram, with the histogram clipped at <c>clipLimit</c> times the average bin count (the excess is spread over all
    /// bins) so noise is not blown up; each pixel then blends the curves of the four nearest tile centres bilinearly (the
    /// border pixels use the nearest tiles alone). In the field logs of 23-oct the facades in shade against a bright sky
    /// were the ones that did not localize; the maps were captured on overcast days.
    /// Plain loops over arrays, no allocation per call after the first use of each thread: it runs on the worker thread of
    /// an attempt (PaseoLocalization) over a 1920 × 1080 image.
    /// </summary>
    public static class PaseoImageEnhance
    {
        public const int DefaultTiles = 8;
        public const float DefaultClipLimit = 2.5f;

        const int Bins = 256;

        // Scratch of the calling thread; every call overwrites what it reads.
        [ThreadStatic] static byte[] curves;
        [ThreadStatic] static int[] histogram;
        [ThreadStatic] static int[] columns;

        /// <summary>
        /// Equalises <paramref name="src"/> into <paramref name="dst"/>, both row after row with no padding (at least
        /// width × height bytes; the same array works in place). The tiles are <paramref name="tiles"/> per side, fewer when
        /// the image is smaller, and need not divide the image evenly. A clip limit of 0 or less does not clip.
        /// </summary>
        public static void Clahe(byte[] src, byte[] dst, int width, int height, int tiles = DefaultTiles, float clipLimit = DefaultClipLimit)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));
            if (width <= 0 || height <= 0)
                return;
            long count = (long)width * height;
            if (src.Length < count || dst.Length < count)
                throw new ArgumentException($"El búfer es menor que la imagen de {width}x{height}.");

            int tilesX = Math.Min(Math.Max(tiles, 1), width);
            int tilesY = Math.Min(Math.Max(tiles, 1), height);

            // One curve of 256 entries per tile.
            Reserve(ref curves, tilesX * tilesY * Bins);
            Reserve(ref histogram, Bins);
            for (int ty = 0; ty < tilesY; ty++)
            {
                int y0 = (int)((long)ty * height / tilesY), y1 = (int)((long)(ty + 1) * height / tilesY);
                for (int tx = 0; tx < tilesX; tx++)
                {
                    int x0 = (int)((long)tx * width / tilesX), x1 = (int)((long)(tx + 1) * width / tilesX);
                    Array.Clear(histogram, 0, Bins);
                    for (int y = y0; y < y1; y++)
                    {
                        int row = y * width;
                        for (int x = x0; x < x1; x++)
                            histogram[src[row + x]]++;
                    }
                    BuildCurve(histogram, (x1 - x0) * (y1 - y0), clipLimit, curves, (ty * tilesX + tx) * Bins);
                }
            }

            // Per column: the curve offsets of the two tile columns it blends and the weight of the second, in 1/256.
            Reserve(ref columns, width * 3);
            float tileWidth = width / (float)tilesX, tileHeight = height / (float)tilesY;
            for (int x = 0; x < width; x++)
            {
                Neighbours((x + 0.5f) / tileWidth - 0.5f, tilesX, out int first, out int second, out int weight);
                columns[x] = first * Bins;
                columns[width + x] = second * Bins;
                columns[2 * width + x] = weight;
            }

            for (int y = 0; y < height; y++)
            {
                Neighbours((y + 0.5f) / tileHeight - 0.5f, tilesY, out int upper, out int lower, out int weightY);
                int upperRow = upper * tilesX * Bins, lowerRow = lower * tilesX * Bins;
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int value = src[row + x];
                    int left = columns[x], right = columns[width + x], weightX = columns[2 * width + x];
                    int top = curves[upperRow + left + value] * (256 - weightX) + curves[upperRow + right + value] * weightX;
                    int bottom = curves[lowerRow + left + value] * (256 - weightX) + curves[lowerRow + right + value] * weightX;
                    // Weights sum to 256 * 256 and the curves stay within 0-255: the result cannot leave 0-255.
                    dst[row + x] = (byte)((top * (256 - weightY) + bottom * weightY + 32768) >> 16);
                }
            }
        }

        // The equalising curve of one tile: clipped histogram (as OpenCV does), then its cumulative sum scaled to 0-255.
        static void BuildCurve(int[] bins, int area, float clipLimit, byte[] curve, int offset)
        {
            if (clipLimit > 0f)
            {
                int clip = Math.Max((int)(clipLimit * area / Bins), 1);
                int clipped = 0;
                for (int i = 0; i < Bins; i++)
                    if (bins[i] > clip)
                    {
                        clipped += bins[i] - clip;
                        bins[i] = clip;
                    }
                int batch = clipped / Bins;
                int residual = clipped - batch * Bins;
                for (int i = 0; i < Bins; i++)
                    bins[i] += batch;
                if (residual > 0)
                {
                    int step = Math.Max(Bins / residual, 1);
                    for (int i = 0; i < Bins && residual > 0; i += step, residual--)
                        bins[i]++;
                }
            }
            long sum = 0;
            for (int i = 0; i < Bins; i++)
            {
                sum += bins[i];
                long scaled = (sum * 255 + area / 2) / area;
                curve[offset + i] = (byte)(scaled > 255 ? 255 : scaled);
            }
        }

        // The two tiles a coordinate (in tile units, 0 = the centre of the first tile) lies between, and the weight of the
        // second in 1/256. Past the first and last centres only the nearest tile counts.
        static void Neighbours(float position, int tiles, out int first, out int second, out int weight)
        {
            if (position <= 0f)
            {
                first = second = 0;
                weight = 0;
            }
            else if (position >= tiles - 1)
            {
                first = second = tiles - 1;
                weight = 0;
            }
            else
            {
                first = (int)position;
                second = first + 1;
                weight = (int)((position - first) * 256f + 0.5f);
            }
        }

        static void Reserve<T>(ref T[] buffer, int length)
        {
            if (buffer == null || buffer.Length < length)
                buffer = new T[length];
        }
    }
}
