using System;
using System.Collections.Generic;
using System.Linq;

namespace MelloBlack.SpriteTidy.Core
{
    public enum BackgroundType
    {
        /// <summary>The image already has a transparent background.</summary>
        Transparent,

        /// <summary>One plain color.</summary>
        Solid,

        /// <summary>Two colors in squares: the "fake transparency" pattern image generators often paint.</summary>
        Checkerboard,
    }

    /// <summary>Finds out what the background of an image is by looking at a ring of pixels near its border.</summary>
    public static class BackgroundDetector
    {
        internal const int TransparentAlpha = 16;

        // Two colors closer than this are noise around one color, not a checkerboard.
        const int MinCheckerSeparation = 10;

        // Share of the opaque border pixels the second checkerboard color must cover.
        const double MinSecondColorShare = 0.15;

        // A checkerboard alternates along the border; content that merely touches the border does not.
        const int MinCheckerRuns = 6;

        // For low-contrast checkerboards: each shade's share of the background pixels, the shortest run that counts
        // as a block, and the share of background pixels that must lie in such blocks.
        const double MinSubtleShadeShare = 0.2;
        const int MinBlockRun = 4;
        const double MinBlockShare = 0.7;

        // Distance from the image edge of the sampled ring, skipping the outermost pixels where generators leave artifacts.
        const int RingInset = 2;

        /// <param name="colors">The background colors: none, one, or the two checkerboard colors.</param>
        public static BackgroundType Detect(RgbaImage image, out Rgba32[] colors)
        {
            int tolerance = BackgroundRemover.Tolerance;
            var ring = SampleRing(image);
            var opaque = new List<Rgba32>(ring.Count);
            foreach (var p in ring)
                if (p.A >= TransparentAlpha) opaque.Add(p);

            colors = Array.Empty<Rgba32>();
            if (opaque.Count * 2 <= ring.Count) return BackgroundType.Transparent;

            var first = DominantColor(opaque, c => true).Value;
            int separation2 = MinCheckerSeparation * MinCheckerSeparation;
            var second = DominantColor(opaque, c => Rgba32.DistanceSquared(c, first) > separation2);

            if (second.HasValue && LooksLikeCheckerboard(ring, first, second.Value, tolerance))
            {
                colors = new[] { first, second.Value };
                return BackgroundType.Checkerboard;
            }

            if (TryFindSubtleCheckerboard(ring, first, tolerance, out var darker, out var lighter))
            {
                colors = new[] { darker, lighter };
                return BackgroundType.Checkerboard;
            }

            colors = new[] { first };
            return BackgroundType.Solid;
        }

        /// <summary>The pixels of a rectangle just inside the image border, in order around the image.</summary>
        static List<Rgba32> SampleRing(RgbaImage image)
        {
            int w = image.Width, h = image.Height;
            int inset = Math.Max(0, Math.Min(RingInset, (Math.Min(w, h) - 1) / 2));
            int x0 = inset, y0 = inset, x1 = w - 1 - inset, y1 = h - 1 - inset;

            var ring = new List<Rgba32>(2 * (w + h));
            for (int x = x0; x <= x1; x++) ring.Add(image[x, y0]);
            for (int y = y0 + 1; y <= y1; y++) ring.Add(image[x1, y]);
            if (y1 > y0)
                for (int x = x1 - 1; x >= x0; x--) ring.Add(image[x, y1]);
            if (x1 > x0)
                for (int y = y1 - 1; y > y0; y--) ring.Add(image[x0, y]);
            return ring;
        }

        /// <summary>Average color of the most common 5-bit-per-channel bucket among the pixels that pass the filter.</summary>
        static Rgba32? DominantColor(List<Rgba32> pixels, Func<Rgba32, bool> filter)
        {
            var counts = new Dictionary<int, int>();
            int bestKey = -1, bestCount = 0;
            foreach (var p in pixels)
            {
                if (!filter(p)) continue;
                int key = Quantize(p);
                counts.TryGetValue(key, out int count);
                counts[key] = ++count;
                if (count > bestCount)
                {
                    bestCount = count;
                    bestKey = key;
                }
            }

            if (bestKey < 0) return null;

            long r = 0, g = 0, b = 0;
            int n = 0;
            foreach (var p in pixels)
            {
                if (!filter(p) || Quantize(p) != bestKey) continue;
                r += p.R;
                g += p.G;
                b += p.B;
                n++;
            }

            return new Rgba32((byte)(r / n), (byte)(g / n), (byte)(b / n));
        }

        static int Quantize(Rgba32 c) => ((c.R >> 3) << 10) | ((c.G >> 3) << 5) | (c.B >> 3);

        static bool LooksLikeCheckerboard(List<Rgba32> ring, Rgba32 first, Rgba32 second, int tolerance)
        {
            int tolerance2 = tolerance * tolerance;
            int opaqueCount = 0, secondCount = 0, runs = 0, runLength = 0;
            foreach (var p in ring)
            {
                bool isSecond = false;
                if (p.A >= TransparentAlpha)
                {
                    opaqueCount++;
                    int d1 = Rgba32.DistanceSquared(p, first);
                    int d2 = Rgba32.DistanceSquared(p, second);
                    isSecond = d2 < d1 && d2 <= tolerance2;
                }

                if (isSecond)
                {
                    secondCount++;
                    runLength++;
                }
                else
                {
                    if (runLength >= 2) runs++;
                    runLength = 0;
                }
            }

            if (runLength >= 2) runs++;
            return secondCount >= opaqueCount * MinSecondColorShare && runs >= MinCheckerRuns;
        }

        /// <summary>
        /// Finds checkerboards whose two shades are too close to separate by color (dark ones especially). Background
        /// pixels on the ring are split into darker and lighter than their mean brightness; a checkerboard turns that
        /// into regular blocks, while noise on a flat background gives short, irregular runs.
        /// </summary>
        static bool TryFindSubtleCheckerboard(List<Rgba32> ring, Rgba32 dominant, int tolerance, out Rgba32 darker, out Rgba32 lighter)
        {
            darker = lighter = default;
            int tolerance2 = tolerance * tolerance;
            var classes = new int[ring.Count]; // 0: not background, 1: darker, 2: lighter

            double meanLuma = 0;
            int matched = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                if (ring[i].A < TransparentAlpha || Rgba32.DistanceSquared(ring[i], dominant) > tolerance2) continue;
                meanLuma += Luma(ring[i]);
                matched++;
            }

            if (matched == 0) return false;
            meanLuma /= matched;

            var sums = new long[2, 3];
            var counts = new int[2];
            for (int i = 0; i < ring.Count; i++)
            {
                var p = ring[i];
                if (p.A < TransparentAlpha || Rgba32.DistanceSquared(p, dominant) > tolerance2) continue;
                int shade = Luma(p) > meanLuma ? 1 : 0;
                classes[i] = shade + 1;
                counts[shade]++;
                sums[shade, 0] += p.R;
                sums[shade, 1] += p.G;
                sums[shade, 2] += p.B;
            }

            if (counts[0] < matched * MinSubtleShadeShare || counts[1] < matched * MinSubtleShadeShare) return false;

            var runs = new List<int>();
            int runClass = 0, runLength = 0;
            foreach (int c in classes)
            {
                if (c != 0 && c == runClass)
                {
                    runLength++;
                    continue;
                }

                if (runClass != 0) runs.Add(runLength);
                runClass = c;
                runLength = c != 0 ? 1 : 0;
            }

            if (runClass != 0) runs.Add(runLength);
            if (runs.Count < 2 * MinCheckerRuns) return false;

            runs.Sort();
            int inBlocks = runs.Where(r => r >= MinBlockRun).Sum();
            if (runs[runs.Count / 2] < MinBlockRun || inBlocks < matched * MinBlockShare) return false;

            darker = new Rgba32((byte)(sums[0, 0] / counts[0]), (byte)(sums[0, 1] / counts[0]), (byte)(sums[0, 2] / counts[0]));
            lighter = new Rgba32((byte)(sums[1, 0] / counts[1]), (byte)(sums[1, 1] / counts[1]), (byte)(sums[1, 2] / counts[1]));
            return true;
        }

        static double Luma(Rgba32 c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
    }
}
