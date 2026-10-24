using System;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoImageEnhanceTests
    {
        static byte[] Image(int width, int height, Func<int, int, int> pixel)
        {
            var data = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    data[y * width + x] = (byte)pixel(x, y);
            return data;
        }

        // Deterministic noise in 0-255, so the tests do not depend on System.Random.
        static byte[] Noise(int width, int height, uint seed)
        {
            var data = new byte[width * height];
            uint state = seed;
            for (int i = 0; i < data.Length; i++)
            {
                state = state * 1664525u + 1013904223u;
                data[i] = (byte)(state >> 24);
            }
            return data;
        }

        static byte[] Enhanced(byte[] src, int width, int height, int tiles = PaseoImageEnhance.DefaultTiles, float clip = PaseoImageEnhance.DefaultClipLimit)
        {
            var dst = new byte[src.Length];
            PaseoImageEnhance.Clahe(src, dst, width, height, tiles, clip);
            return dst;
        }

        // Standard deviation of the pixels of a rectangle.
        static double StdDev(byte[] data, int width, int x0, int y0, int x1, int y1)
        {
            double sum = 0, sumSquares = 0;
            int count = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    double v = data[y * width + x];
                    sum += v;
                    sumSquares += v * v;
                    count++;
                }
            double mean = sum / count;
            return Math.Sqrt(Math.Max(0.0, sumSquares / count - mean * mean));
        }

        [Test]
        public void AUniformImageStaysUniformAndNearItsValue()
        {
            var dst = Enhanced(Image(160, 120, (x, y) => 100), 160, 120);
            foreach (byte value in dst)
                Assert.AreEqual(dst[0], value, "every tile has the same curve, so nothing may vary across the image");
            Assert.AreEqual(100, dst[0], 5);
        }

        [Test]
        public void UniformBlackAndWhiteImagesStayAtTheEnds()
        {
            var white = Enhanced(Image(160, 120, (x, y) => 255), 160, 120);
            foreach (byte value in white)
                Assert.AreEqual(255, value, "a brighter weighted sum must never wrap around to a dark value");
            var black = Enhanced(Image(160, 120, (x, y) => 0), 160, 120);
            foreach (byte value in black)
                Assert.LessOrEqual(value, 5);
        }

        // Texture in a narrow band of 20 grey levels, like a facade in shade.
        static byte[] LowContrastTexture(int width, int height)
        {
            var data = Noise(width, height, 99);
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(100 + data[i] % 20);
            return data;
        }

        [Test]
        public void ALowContrastTextureGainsContrast()
        {
            var src = LowContrastTexture(256, 192);
            var dst = Enhanced(src, 256, 192);
            double before = StdDev(src, 256, 0, 0, 256, 192), after = StdDev(dst, 256, 0, 0, 256, 192);
            Assert.Greater(after, before * 2.0, $"std {before:F1} -> {after:F1}");
        }

        [Test]
        public void AGradientKeepsItsBrightnessOrder()
        {
            var src = Image(256, 192, (x, y) => 100 + x * 40 / 256);
            var dst = Enhanced(src, 256, 192);
            double left = 0, right = 0;
            for (int y = 0; y < 192; y++)
                for (int x = 0; x < 64; x++)
                {
                    left += dst[y * 256 + x];
                    right += dst[y * 256 + 192 + x];
                }
            Assert.Less(left, right, "the dark side must stay darker than the bright side");
        }

        [Test]
        public void ADarkShadedHalfGainsLocalContrastBesideABrightSky()
        {
            // The case that motivated the test: a facade in shade (a few dark levels) under a bright sky.
            var src = Image(256, 192, (x, y) => y < 96 ? 200 + (x + y) % 40 : 30 + (x * y) % 16);
            var dst = Enhanced(src, 256, 192);
            double before = StdDev(src, 256, 0, 112, 256, 176), after = StdDev(dst, 256, 0, 112, 256, 176);
            Assert.Greater(after, before * 1.5, $"std of the shaded half {before:F1} -> {after:F1}");
        }

        [Test]
        public void AHigherClipLimitGivesMoreContrast()
        {
            var src = LowContrastTexture(256, 192);
            double gentle = StdDev(Enhanced(src, 256, 192, clip: 1f), 256, 0, 0, 256, 192);
            double strong = StdDev(Enhanced(src, 256, 192, clip: 6f), 256, 0, 0, 256, 192);
            Assert.Greater(strong, gentle);
        }

        [Test]
        public void WithoutAClipLimitASingleTileIsPlainEqualisation()
        {
            // 40 grey levels spread over the whole range.
            var src = Image(160, 120, (x, y) => 100 + x * 40 / 160);
            var dst = Enhanced(src, 160, 120, tiles: 1, clip: 0f);
            int min = 255, max = 0;
            foreach (byte value in dst)
            {
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
            Assert.Greater(max - min, 200);
            Assert.AreEqual(255, max);
        }

        [Test]
        public void SizesTheTilesDoNotDivideEvenlyWork()
        {
            foreach (var (width, height) in new[] { (101, 77), (13, 9), (64, 65), (1919, 7), (9, 1081) })
            {
                var src = Noise(width, height, (uint)(width * 31 + height));
                var dst = Enhanced(src, width, height);
                Assert.AreEqual(src.Length, dst.Length);
                Assert.Greater(StdDev(dst, width, 0, 0, width, height), 10.0, $"{width}x{height}: the output must keep its detail");
            }
        }

        [Test]
        public void TinyImagesWork()
        {
            foreach (var (width, height) in new[] { (1, 1), (3, 2), (2, 5), (7, 3), (1, 9), (8, 8) })
            {
                var src = Noise(width, height, 7);
                var first = Enhanced(src, width, height);
                var second = Enhanced(src, width, height);
                CollectionAssert.AreEqual(first, second, $"{width}x{height}");
            }
        }

        [Test]
        public void MoreTilesThanPixelsAreClamped()
        {
            var src = Noise(10, 6, 3);
            CollectionAssert.AreEqual(Enhanced(src, 10, 6, tiles: 10), Enhanced(src, 10, 6, tiles: 64));
            // And a request for fewer than one tile means one.
            CollectionAssert.AreEqual(Enhanced(src, 10, 6, tiles: 1), Enhanced(src, 10, 6, tiles: 0));
        }

        [Test]
        public void WhitePixelsStayWhiteInExtremeImages()
        {
            // The curve of every tile ends at 255, so a white pixel is 255 whatever its neighbours' curves are: the fixed-point
            // blend must reach the top of the range without wrapping around. Black and white squares, and a full ramp.
            var checker = Image(200, 150, (x, y) => ((x / 7 + y / 5) & 1) == 0 ? 0 : 255);
            var ramp = Image(256, 64, (x, y) => x);
            foreach (var (src, width, height) in new[] { (checker, 200, 150), (ramp, 256, 64) })
                foreach (float clip in new[] { 0f, PaseoImageEnhance.DefaultClipLimit, 100f })
                {
                    var dst = Enhanced(src, width, height, clip: clip);
                    for (int i = 0; i < src.Length; i++)
                        if (src[i] == 255)
                            Assert.AreEqual(255, dst[i], $"pixel {i} of {width}x{height}, clip {clip}");
                }
        }

        [Test]
        public void TheSameInputGivesTheSameOutputWhateverTheOutputHeldBefore()
        {
            var src = Noise(173, 91, 11);
            var clean = new byte[src.Length];
            var dirty = new byte[src.Length];
            for (int i = 0; i < dirty.Length; i++)
                dirty[i] = 200;
            PaseoImageEnhance.Clahe(src, clean, 173, 91);
            PaseoImageEnhance.Clahe(src, dirty, 173, 91);
            CollectionAssert.AreEqual(clean, dirty);
            // The scratch of the thread is reused by a bigger image and a smaller one without changing the result.
            PaseoImageEnhance.Clahe(Noise(300, 200, 5), new byte[300 * 200], 300, 200);
            var again = new byte[src.Length];
            PaseoImageEnhance.Clahe(src, again, 173, 91);
            CollectionAssert.AreEqual(clean, again);
        }

        [Test]
        public void TheSameArrayCanBeEnhancedInPlace()
        {
            var src = Noise(120, 80, 21);
            var separate = Enhanced(src, 120, 80);
            var inPlace = (byte[])src.Clone();
            PaseoImageEnhance.Clahe(inPlace, inPlace, 120, 80);
            CollectionAssert.AreEqual(separate, inPlace);
        }

        [Test]
        public void ABufferSmallerThanTheImageIsRefused()
        {
            Assert.Throws<ArgumentException>(() => PaseoImageEnhance.Clahe(new byte[10], new byte[12], 4, 3));
            Assert.Throws<ArgumentException>(() => PaseoImageEnhance.Clahe(new byte[12], new byte[10], 4, 3));
            Assert.Throws<ArgumentNullException>(() => PaseoImageEnhance.Clahe(null, new byte[12], 4, 3));
            Assert.Throws<ArgumentNullException>(() => PaseoImageEnhance.Clahe(new byte[12], null, 4, 3));
        }

        [Test]
        public void AnEmptyImageDoesNothing()
        {
            var dst = new byte[] { 9, 9, 9 };
            PaseoImageEnhance.Clahe(new byte[3], dst, 0, 3);
            PaseoImageEnhance.Clahe(new byte[3], dst, 3, 0);
            CollectionAssert.AreEqual(new byte[] { 9, 9, 9 }, dst);
        }

        [Test]
        public void ABufferLargerThanTheImageIsLeftBeyondItsEnd()
        {
            // The caller keeps one buffer: only the first width × height bytes are the image.
            var src = new byte[100];
            var dst = new byte[100];
            for (int i = 0; i < dst.Length; i++)
                dst[i] = 77;
            PaseoImageEnhance.Clahe(src, dst, 5, 4);
            for (int i = 20; i < dst.Length; i++)
                Assert.AreEqual(77, dst[i]);
        }
    }
}
