using System;

namespace MelloBlack.SpriteTidy.Core
{
    /// <summary>
    /// Resizes images with a bicubic filter. When shrinking, the filter gets wider so every source pixel counts
    /// (no jagged edges), and colors are weighted by alpha so transparent pixels never leave dark fringes on edges.
    /// </summary>
    public static class Resampler
    {
        // Sharpness of the bicubic filter (Catmull-Rom).
        const double CubicA = -0.5;

        public static RgbaImage Resize(RgbaImage source, int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (width == source.Width && height == source.Height) return source.Clone();

            // Work with colors multiplied by alpha, so a transparent pixel adds no color.
            int sw = source.Width, sh = source.Height;
            var weighted = new float[sw * sh * 4];
            for (int i = 0; i < sw * sh; i++)
            {
                var p = source.Pixels[i];
                float a = p.A / 255f;
                weighted[i * 4] = p.R * a;
                weighted[i * 4 + 1] = p.G * a;
                weighted[i * 4 + 2] = p.B * a;
                weighted[i * 4 + 3] = a;
            }

            // First resize the width, then the height.
            var columns = Weights(sw, width);
            var wide = new float[width * sh * 4];
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < width; x++)
                Sum(weighted, columns[x], (y * sw + columns[x].Start) * 4, 4, wide, (y * width + x) * 4);

            var rows = Weights(sh, height);
            var result = new RgbaImage(width, height);
            var pixel = new float[4];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Sum(wide, rows[y], (rows[y].Start * width + x) * 4, width * 4, pixel, 0);
                result.Pixels[y * width + x] = ToPixel(pixel[0], pixel[1], pixel[2], pixel[3]);
            }

            return result;
        }

        /// <summary>Adds up the source pixels under one filter, writing four channels to <paramref name="target"/>.</summary>
        static void Sum(float[] source, Filter filter, int first, int step, float[] target, int at)
        {
            float r = 0, g = 0, b = 0, a = 0;
            for (int k = 0; k < filter.Weights.Length; k++)
            {
                int s = first + k * step;
                float weight = filter.Weights[k];
                r += source[s] * weight;
                g += source[s + 1] * weight;
                b += source[s + 2] * weight;
                a += source[s + 3] * weight;
            }

            target[at] = r;
            target[at + 1] = g;
            target[at + 2] = b;
            target[at + 3] = a;
        }

        static Rgba32 ToPixel(float r, float g, float b, float a)
        {
            if (a <= 1f / 512f) return Rgba32.Transparent;
            if (a > 1f) a = 1f;
            return new Rgba32(ToByte(r / a), ToByte(g / a), ToByte(b / a), ToByte(a * 255f));
        }

        static byte ToByte(float value)
        {
            if (value <= 0f) return 0;
            if (value >= 255f) return 255;
            return (byte)(value + 0.5f);
        }

        /// <summary>The source pixels one output pixel reads (from <see cref="Start"/>) and how much each one counts.</summary>
        struct Filter
        {
            public int Start;
            public float[] Weights;
        }

        static Filter[] Weights(int sourceSize, int targetSize)
        {
            double scale = (double)sourceSize / targetSize;
            double widen = Math.Max(1.0, scale);
            double reach = 2.0 * widen;

            var filters = new Filter[targetSize];
            for (int i = 0; i < targetSize; i++)
            {
                double center = (i + 0.5) * scale;
                int start = Math.Max(0, (int)Math.Floor(center - reach));
                int end = Math.Min(sourceSize, (int)Math.Ceiling(center + reach));

                var weights = new float[end - start];
                double total = 0;
                for (int s = start; s < end; s++)
                {
                    double weight = Cubic((s + 0.5 - center) / widen);
                    weights[s - start] = (float)weight;
                    total += weight;
                }

                if (Math.Abs(total) > 1e-9)
                    for (int k = 0; k < weights.Length; k++) weights[k] = (float)(weights[k] / total);

                filters[i] = new Filter { Start = start, Weights = weights };
            }

            return filters;
        }

        static double Cubic(double x)
        {
            x = Math.Abs(x);
            if (x < 1.0) return ((CubicA + 2.0) * x - (CubicA + 3.0)) * x * x + 1.0;
            if (x < 2.0) return (((x - 5.0) * x + 8.0) * x - 4.0) * CubicA;
            return 0.0;
        }
    }
}
