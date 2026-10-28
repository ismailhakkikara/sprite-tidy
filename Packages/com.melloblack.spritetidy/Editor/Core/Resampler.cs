using System;

namespace MelloBlack.SpriteTidy.Core
{
    public enum ResampleFilter
    {
        Nearest,
        Bilinear,

        /// <summary>Catmull-Rom style cubic (a = -0.5): sharp, with very little ringing. A good default.</summary>
        Bicubic,

        /// <summary>Lanczos with 3 lobes: the sharpest, but can ring around hard edges.</summary>
        Lanczos,
    }

    /// <summary>
    /// Separable image resizing. The filter widens when shrinking so every source pixel contributes (no aliasing),
    /// and colors are weighted by alpha so transparent pixels never bleed dark or colored fringes into edges.
    /// </summary>
    public static class Resampler
    {
        public static RgbaImage Resize(RgbaImage source, int width, int height, ResampleFilter filter)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (width == source.Width && height == source.Height) return source.Clone();
            if (filter == ResampleFilter.Nearest) return ResizeNearest(source, width, height);

            int sw = source.Width, sh = source.Height;
            var premultiplied = new float[sw * sh * 4];
            for (int i = 0; i < sw * sh; i++)
            {
                var p = source.Pixels[i];
                float a = p.A / 255f;
                premultiplied[i * 4] = p.R * a;
                premultiplied[i * 4 + 1] = p.G * a;
                premultiplied[i * 4 + 2] = p.B * a;
                premultiplied[i * 4 + 3] = a;
            }

            // Horizontal pass: sw x sh -> width x sh.
            var columns = Contributions(sw, width, filter);
            var horizontal = new float[width * sh * 4];
            for (int y = 0; y < sh; y++)
            {
                int srcRow = y * sw;
                int dstRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    var c = columns[x];
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int k = 0; k < c.Weights.Length; k++)
                    {
                        int s = (srcRow + c.Start + k) * 4;
                        float wgt = c.Weights[k];
                        r += premultiplied[s] * wgt;
                        g += premultiplied[s + 1] * wgt;
                        b += premultiplied[s + 2] * wgt;
                        a += premultiplied[s + 3] * wgt;
                    }

                    int d = (dstRow + x) * 4;
                    horizontal[d] = r;
                    horizontal[d + 1] = g;
                    horizontal[d + 2] = b;
                    horizontal[d + 3] = a;
                }
            }

            // Vertical pass: width x sh -> width x height, then back to straight alpha.
            var rows = Contributions(sh, height, filter);
            var result = new RgbaImage(width, height);
            for (int y = 0; y < height; y++)
            {
                var c = rows[y];
                for (int x = 0; x < width; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int k = 0; k < c.Weights.Length; k++)
                    {
                        int s = ((c.Start + k) * width + x) * 4;
                        float wgt = c.Weights[k];
                        r += horizontal[s] * wgt;
                        g += horizontal[s + 1] * wgt;
                        b += horizontal[s + 2] * wgt;
                        a += horizontal[s + 3] * wgt;
                    }

                    result.Pixels[y * width + x] = ToStraight(r, g, b, a);
                }
            }

            return result;
        }

        static RgbaImage ResizeNearest(RgbaImage source, int width, int height)
        {
            var result = new RgbaImage(width, height);
            double sx = (double)source.Width / width;
            double sy = (double)source.Height / height;
            for (int y = 0; y < height; y++)
            {
                int srcY = Math.Min(source.Height - 1, (int)((y + 0.5) * sy));
                for (int x = 0; x < width; x++)
                {
                    int srcX = Math.Min(source.Width - 1, (int)((x + 0.5) * sx));
                    result.Pixels[y * width + x] = source.Pixels[srcY * source.Width + srcX];
                }
            }

            return result;
        }

        static Rgba32 ToStraight(float r, float g, float b, float a)
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

        struct Contribution
        {
            public int Start;
            public float[] Weights;
        }

        /// <summary>For each output pixel along one axis: the first source pixel it reads and the weight of each one.</summary>
        static Contribution[] Contributions(int sourceSize, int targetSize, ResampleFilter filter)
        {
            double scale = (double)sourceSize / targetSize;
            double filterScale = Math.Max(1.0, scale);
            double support = Support(filter) * filterScale;

            var result = new Contribution[targetSize];
            for (int i = 0; i < targetSize; i++)
            {
                double center = (i + 0.5) * scale;
                int start = Math.Max(0, (int)Math.Floor(center - support));
                int end = Math.Min(sourceSize, (int)Math.Ceiling(center + support));

                var weights = new float[end - start];
                double total = 0;
                for (int s = start; s < end; s++)
                {
                    double weight = Kernel(filter, (s + 0.5 - center) / filterScale);
                    weights[s - start] = (float)weight;
                    total += weight;
                }

                if (Math.Abs(total) > 1e-9)
                    for (int k = 0; k < weights.Length; k++) weights[k] = (float)(weights[k] / total);

                result[i] = new Contribution { Start = start, Weights = weights };
            }

            return result;
        }

        static double Support(ResampleFilter filter)
        {
            switch (filter)
            {
                case ResampleFilter.Bilinear: return 1.0;
                case ResampleFilter.Bicubic: return 2.0;
                case ResampleFilter.Lanczos: return 3.0;
                default: return 0.5;
            }
        }

        static double Kernel(ResampleFilter filter, double x)
        {
            x = Math.Abs(x);
            switch (filter)
            {
                case ResampleFilter.Bilinear:
                    return x < 1.0 ? 1.0 - x : 0.0;
                case ResampleFilter.Bicubic:
                    const double a = -0.5;
                    if (x < 1.0) return ((a + 2.0) * x - (a + 3.0)) * x * x + 1.0;
                    if (x < 2.0) return (((x - 5.0) * x + 8.0) * x - 4.0) * a;
                    return 0.0;
                case ResampleFilter.Lanczos:
                    if (x < 1e-8) return 1.0;
                    if (x >= 3.0) return 0.0;
                    double px = Math.PI * x;
                    return 3.0 * Math.Sin(px) * Math.Sin(px / 3.0) / (px * px);
                default:
                    return x < 0.5 ? 1.0 : 0.0;
            }
        }
    }
}
