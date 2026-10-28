using System;
using System.Collections.Generic;

namespace MelloBlack.SpriteTidy.Core
{
    public enum HoleFilling
    {
        /// <summary>
        /// Remove enclosed holes on checkerboard backgrounds only. On a solid background an enclosed area
        /// of the background color is often part of the art (a black panel inside a frame, say).
        /// </summary>
        Auto,
        Off,
        On,
    }

    [Serializable]
    public sealed class BackgroundSettings
    {
        public bool Enabled = true;

        /// <summary>RGB distance within which a pixel counts as background.</summary>
        public int Tolerance = 40;

        public HoleFilling Holes = HoleFilling.Auto;

        /// <summary>Smallest enclosed hole that is removed, as a percentage of the image area.</summary>
        public float MinHoleAreaPercent = 0.05f;

        /// <summary>Width in pixels of the band along content edges whose colors are un-blended from the background.</summary>
        public int EdgeBand = 2;
    }

    /// <summary>
    /// Removes solid and checkerboard backgrounds: flood-fills the background from the image border, optionally removes
    /// enclosed holes, then un-blends the anti-aliased edge pixels so no light or dark halo is left around the content.
    /// </summary>
    public static class BackgroundRemover
    {
        // Below this distance between content and background color an edge pixel can't be un-blended reliably.
        const int MinUnblendContrast = 30;

        // Share of a checkerboard hole each of the two shades must cover.
        const double MinHoleColorShare = 0.2;

        // Lower bound of the tight tolerance used to match checkerboard holes.
        const int MinHoleTolerance = 6;

        // How far a confirmed hole may grow with the full tolerance, as a share of the image's longer side.
        const double HoleGrowth = 0.01;

        public static BackgroundModel Apply(RgbaImage image, BackgroundSettings settings)
        {
            var model = BackgroundDetector.Detect(image, settings.Tolerance);
            if (model.Kind == BackgroundKind.Transparent) return model;

            int w = image.Width, h = image.Height, n = w * h;
            var pixels = image.Pixels;
            var colors = new Rgba32[model.Colors.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = model.Colors[i];

            int tolerance2 = settings.Tolerance * settings.Tolerance;
            var matches = new bool[n];
            for (int i = 0; i < n; i++)
                matches[i] = pixels[i].A < BackgroundDetector.TransparentAlpha || NearestDistanceSquared(pixels[i], colors) <= tolerance2;

            var background = new bool[n];
            var queue = new int[n];
            FloodFromBorder(matches, background, queue, w, h);

            bool checkerboard = model.Kind == BackgroundKind.Checkerboard;
            bool fillHoles = settings.Holes == HoleFilling.On || (settings.Holes == HoleFilling.Auto && checkerboard);
            if (fillHoles)
            {
                // A checkerboard hole must show both shades. When the shades are close (dark checkerboards), that test
                // only means something if hole pixels match the shades closely; with the full tolerance, dark outlines
                // would pass as holes. A confirmed hole then grows a short way with the full tolerance to take in the
                // noise and shading generators add inside it.
                var seedMatches = matches;
                if (checkerboard)
                {
                    double separation = Math.Sqrt(Rgba32.DistanceSquared(colors[0], colors[1]));
                    double seedTolerance = Math.Min(settings.Tolerance, Math.Max(Math.Max(MinHoleTolerance, settings.Tolerance / 3.0), separation / 2));
                    seedMatches = new bool[n];
                    for (int i = 0; i < n; i++)
                        seedMatches[i] = pixels[i].A < BackgroundDetector.TransparentAlpha || NearestDistanceSquared(pixels[i], colors) <= seedTolerance * seedTolerance;
                }

                int minArea = Math.Max(16, (int)(n * (double)settings.MinHoleAreaPercent / 100.0));
                int growSteps = Math.Max(2, (int)Math.Round(Math.Max(w, h) * HoleGrowth));
                FillHoles(pixels, seedMatches, matches, background, queue, w, h, colors, checkerboard, minArea, growSteps);
            }

            if (settings.EdgeBand > 0) RefineEdges(pixels, background, queue, w, h, settings.EdgeBand);

            for (int i = 0; i < n; i++)
                if (background[i]) pixels[i] = Rgba32.Transparent;

            return model;
        }

        static void FloodFromBorder(bool[] matches, bool[] background, int[] queue, int w, int h)
        {
            int tail = 0;
            void Seed(int i)
            {
                if (!matches[i] || background[i]) return;
                background[i] = true;
                queue[tail++] = i;
            }

            for (int x = 0; x < w; x++)
            {
                Seed(x);
                Seed((h - 1) * w + x);
            }

            for (int y = 0; y < h; y++)
            {
                Seed(y * w);
                Seed(y * w + w - 1);
            }

            for (int head = 0; head < tail; head++)
            {
                int i = queue[head];
                int x = i % w;
                if (x > 0) Seed(i - 1);
                if (x < w - 1) Seed(i + 1);
                if (i >= w) Seed(i - w);
                if (i < (h - 1) * w) Seed(i + w);
            }
        }

        /// <summary>
        /// Marks enclosed regions of <paramref name="seedMatches"/> pixels as background, then grows each one up to
        /// <paramref name="growSteps"/> pixels into neighboring <paramref name="growMatches"/> pixels.
        /// </summary>
        static void FillHoles(Rgba32[] pixels, bool[] seedMatches, bool[] growMatches, bool[] background, int[] queue, int w, int h,
            Rgba32[] colors, bool requireBothColors, int minArea, int growSteps)
        {
            int n = w * h;
            var visited = new bool[n];
            var steps = new int[n];
            for (int start = 0; start < n; start++)
            {
                if (!seedMatches[start] || background[start] || visited[start]) continue;

                int tail = 0;
                visited[start] = true;
                queue[tail++] = start;
                for (int head = 0; head < tail; head++)
                {
                    int i = queue[head];
                    int x = i % w;
                    if (x > 0) Visit(i - 1);
                    if (x < w - 1) Visit(i + 1);
                    if (i >= w) Visit(i - w);
                    if (i < (h - 1) * w) Visit(i + w);
                }

                if (tail < minArea) continue;
                if (requireBothColors && !CoversBothColors(pixels, queue, tail, colors)) continue;

                for (int k = 0; k < tail; k++)
                {
                    background[queue[k]] = true;
                    steps[queue[k]] = 0;
                }

                for (int head = 0; head < tail; head++)
                {
                    int i = queue[head];
                    if (steps[i] >= growSteps) continue;
                    int x = i % w;
                    if (x > 0) Grow(i, i - 1);
                    if (x < w - 1) Grow(i, i + 1);
                    if (i >= w) Grow(i, i - w);
                    if (i < (h - 1) * w) Grow(i, i + w);
                }

                void Visit(int j)
                {
                    if (!seedMatches[j] || background[j] || visited[j]) return;
                    visited[j] = true;
                    queue[tail++] = j;
                }

                void Grow(int from, int to)
                {
                    if (!growMatches[to] || background[to]) return;
                    background[to] = true;
                    visited[to] = true;
                    steps[to] = steps[from] + 1;
                    queue[tail++] = to;
                }
            }
        }

        static bool CoversBothColors(Rgba32[] pixels, int[] region, int count, Rgba32[] colors)
        {
            int first = 0, second = 0;
            for (int k = 0; k < count; k++)
            {
                var p = pixels[region[k]];
                if (Rgba32.DistanceSquared(p, colors[0]) <= Rgba32.DistanceSquared(p, colors[1])) first++;
                else second++;
            }

            return first >= count * MinHoleColorShare && second >= count * MinHoleColorShare;
        }

        /// <summary>
        /// Edge pixels are a blend of content color F and background color B. For each pixel within
        /// <paramref name="band"/> of the background, take F from the nearest pixel just inside the band and B from
        /// the nearest background pixel, solve for the blend factor and store the un-blended color with that alpha.
        /// </summary>
        static void RefineEdges(Rgba32[] pixels, bool[] background, int[] queue, int w, int h, int band)
        {
            int n = w * h;
            var distance = new int[n];
            var backgroundSource = new int[n];
            int tail = 0;
            for (int i = 0; i < n; i++)
            {
                distance[i] = -1;
                if (!background[i]) continue;
                distance[i] = 0;
                backgroundSource[i] = i;
                queue[tail++] = i;
            }

            for (int head = 0; head < tail; head++)
            {
                int i = queue[head];
                if (distance[i] > band) continue;
                int x = i % w;
                if (x > 0) Spread(i, i - 1);
                if (x < w - 1) Spread(i, i + 1);
                if (i >= w) Spread(i, i - w);
                if (i < (h - 1) * w) Spread(i, i + w);
            }

            void Spread(int from, int to)
            {
                if (distance[to] >= 0) return;
                distance[to] = distance[from] + 1;
                backgroundSource[to] = backgroundSource[from];
                queue[tail++] = to;
            }

            // Carry the content color inward-out: from the first pixels past the band to the band pixels.
            var contentSource = new int[n];
            tail = 0;
            for (int i = 0; i < n; i++)
            {
                contentSource[i] = -1;
                if (distance[i] != band + 1) continue;
                contentSource[i] = i;
                queue[tail++] = i;
            }

            for (int head = 0; head < tail; head++)
            {
                int i = queue[head];
                int x = i % w;
                if (x > 0) Carry(i, i - 1);
                if (x < w - 1) Carry(i, i + 1);
                if (i >= w) Carry(i, i - w);
                if (i < (h - 1) * w) Carry(i, i + w);
            }

            void Carry(int from, int to)
            {
                if (contentSource[to] >= 0 || distance[to] < 1 || distance[to] > band) return;
                contentSource[to] = contentSource[from];
                queue[tail++] = to;
            }

            for (int i = 0; i < n; i++)
            {
                if (distance[i] < 1 || distance[i] > band || contentSource[i] < 0) continue;

                var p = pixels[i];
                var f = pixels[contentSource[i]];
                var b = pixels[backgroundSource[i]];
                if (b.A < BackgroundDetector.TransparentAlpha) continue;

                int fr = f.R - b.R, fg = f.G - b.G, fb = f.B - b.B;
                int contrast2 = fr * fr + fg * fg + fb * fb;
                if (contrast2 < MinUnblendContrast * MinUnblendContrast) continue;

                double t = ((p.R - b.R) * fr + (p.G - b.G) * fg + (p.B - b.B) * fb) / (double)contrast2;
                if (t >= 1.0) continue;
                if (t <= 0.02)
                {
                    background[i] = true;
                    continue;
                }

                pixels[i] = new Rgba32(
                    Unblend(p.R, b.R, t),
                    Unblend(p.G, b.G, t),
                    Unblend(p.B, b.B, t),
                    (byte)Math.Round(t * p.A));
            }
        }

        static byte Unblend(byte blended, byte background, double t)
        {
            double value = background + (blended - background) / t;
            return (byte)Math.Round(Math.Max(0.0, Math.Min(255.0, value)));
        }

        static int NearestDistanceSquared(Rgba32 p, Rgba32[] colors)
        {
            int best = int.MaxValue;
            for (int i = 0; i < colors.Length; i++)
            {
                int d = Rgba32.DistanceSquared(p, colors[i]);
                if (d < best) best = d;
            }

            return best;
        }
    }
}
