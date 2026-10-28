using System;

namespace MelloBlack.SpriteTidy.Core
{
    /// <summary>
    /// Removes plain and checkerboard backgrounds. The background is filled in from the image border, holes that show
    /// a checkerboard (like the middle of a ring) are removed too, and the soft edge pixels are un-blended so no light
    /// or dark halo is left around the objects.
    /// </summary>
    public static class BackgroundRemover
    {
        // How far (RGB distance) a pixel's color may be from a background color and still count as background.
        internal const int Tolerance = 40;

        // Width in pixels of the soft edge whose colors are un-blended from the background.
        const int EdgeWidth = 2;

        // Below this distance between object and background color an edge pixel can't be un-blended reliably.
        const int MinUnblendContrast = 30;

        // Smallest hole that is removed, as a share of the image area.
        const double MinHoleShare = 0.0005;

        // Each of the two checkerboard colors must cover at least this share of a hole.
        const double MinHoleColorShare = 0.2;

        // A hole must be at least this wide (share of the image's longer side). A real hole shows at least one
        // checkerboard square; a dark outline next to a dark checkerboard is only a few pixels wide.
        const double MinHoleWidth = 0.01;

        // Lowest tolerance used when looking for checkerboard holes.
        const int MinHoleTolerance = 6;

        // How far a found hole may grow with the full tolerance, as a share of the image's longer side.
        const double HoleGrowth = 0.01;

        /// <summary>Makes the background of <paramref name="image"/> transparent and returns what kind it was.</summary>
        public static BackgroundType Remove(RgbaImage image)
        {
            var type = BackgroundDetector.Detect(image, out var colors);
            if (type == BackgroundType.Transparent) return type;

            int w = image.Width, h = image.Height, n = w * h;
            var pixels = image.Pixels;
            var matches = MatchColors(pixels, colors, Tolerance);

            var background = new bool[n];
            var queue = new int[n];
            FloodFromBorder(matches, background, queue, w, h);

            // On a plain background, a closed area of the same color is often part of the art (a black panel inside a
            // frame), so only checkerboard holes are removed. A hole must show both checkerboard colors. When the two
            // colors are close (dark checkerboards), that test only works if hole pixels match them closely; with the
            // full tolerance, dark outlines would pass as holes. A found hole then grows a short way with the full
            // tolerance to take in the noise and shading generators add inside it.
            if (type == BackgroundType.Checkerboard)
            {
                double colorDistance = Math.Sqrt(Rgba32.DistanceSquared(colors[0], colors[1]));
                double holeTolerance = Math.Min(Tolerance, Math.Max(Math.Max(MinHoleTolerance, Tolerance / 3.0), colorDistance / 2));
                var holeMatches = MatchColors(pixels, colors, holeTolerance);
                int minArea = Math.Max(16, (int)(n * MinHoleShare));
                int minHalfWidth = Math.Max(4, (int)Math.Round(Math.Max(w, h) * MinHoleWidth / 2));
                int growSteps = Math.Max(2, (int)Math.Round(Math.Max(w, h) * HoleGrowth));
                FillHoles(pixels, holeMatches, matches, background, queue, w, h, colors, minArea, minHalfWidth, growSteps);
            }

            RefineEdges(pixels, background, queue, w, h, EdgeWidth);

            for (int i = 0; i < n; i++)
                if (background[i]) pixels[i] = Rgba32.Transparent;

            return type;
        }

        static bool[] MatchColors(Rgba32[] pixels, Rgba32[] colors, double tolerance)
        {
            double tolerance2 = tolerance * tolerance;
            var matches = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                matches[i] = pixels[i].A < BackgroundDetector.TransparentAlpha || DistanceSquaredToBackground(pixels[i], colors) <= tolerance2;
            return matches;
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
            Rgba32[] colors, int minArea, int minHalfWidth, int growSteps)
        {
            int n = w * h;
            var visited = new bool[n];
            var steps = new int[n];
            var regionOf = new int[n];
            var work = new int[n];
            for (int i = 0; i < n; i++) regionOf[i] = -1;

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
                if (!CoversBothColors(pixels, queue, tail, colors)) continue;
                if (HalfWidth(queue, tail, start, regionOf, steps, work, w, h) < minHalfWidth) continue;

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

        /// <summary>
        /// How far the deepest pixel of a region is from the region's edge, counting diagonal steps as one so that
        /// slanted shapes are not measured wider than they are: a ring 6 pixels wide gives 3.
        /// Uses <paramref name="depth"/> and <paramref name="work"/> as scratch space.
        /// </summary>
        static int HalfWidth(int[] region, int count, int id, int[] regionOf, int[] depth, int[] work, int w, int h)
        {
            for (int k = 0; k < count; k++) regionOf[region[k]] = id;

            // Start from the edge pixels of the region and walk inward.
            int tail = 0;
            for (int k = 0; k < count; k++)
            {
                int i = region[k];
                bool edge = false;
                ForEachNeighbor(i, j => edge |= j < 0 || regionOf[j] != id);
                depth[i] = edge ? 1 : 0;
                if (edge) work[tail++] = i;
            }

            int deepest = tail > 0 ? 1 : 0;
            for (int head = 0; head < tail; head++)
            {
                int i = work[head];
                ForEachNeighbor(i, j =>
                {
                    if (j < 0 || regionOf[j] != id || depth[j] != 0) return;
                    depth[j] = depth[i] + 1;
                    if (depth[j] > deepest) deepest = depth[j];
                    work[tail++] = j;
                });
            }

            return deepest;

            // Calls the action with each of the 8 neighbors of a pixel, or -1 for a neighbor outside the image.
            void ForEachNeighbor(int i, Action<int> action)
            {
                int x = i % w, y = i / w;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    action(nx < 0 || nx >= w || ny < 0 || ny >= h ? -1 : ny * w + nx);
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

        /// <summary>
        /// How far a pixel's color is from the background. On a checkerboard, the pixels between two squares are a
        /// mix of both colors, so the distance is to the nearest mix of the two colors, not just to each color.
        /// </summary>
        static double DistanceSquaredToBackground(Rgba32 p, Rgba32[] colors)
        {
            var a = colors[0];
            if (colors.Length == 1) return Rgba32.DistanceSquared(p, a);

            var b = colors[1];
            double abR = b.R - a.R, abG = b.G - a.G, abB = b.B - a.B;
            double length2 = abR * abR + abG * abG + abB * abB;
            double t = length2 == 0 ? 0 : ((p.R - a.R) * abR + (p.G - a.G) * abG + (p.B - a.B) * abB) / length2;
            t = Math.Max(0, Math.Min(1, t));

            double dr = p.R - (a.R + t * abR);
            double dg = p.G - (a.G + t * abG);
            double db = p.B - (a.B + t * abB);
            return dr * dr + dg * dg + db * db;
        }
    }
}
