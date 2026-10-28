using System;

namespace MelloBlack.SpriteTidy.Core
{
    /// <summary>
    /// A mutable RGBA image in memory. Pixels are stored row by row starting with the bottom row,
    /// the same layout as Texture2D.GetPixels32, so no flipping is needed when moving data in and out of Unity.
    /// </summary>
    public sealed class RgbaImage
    {
        public RgbaImage(int width, int height)
            : this(width, height, new Rgba32[checked(width * height)])
        {
        }

        public RgbaImage(int width, int height, Rgba32[] pixels)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != width * height)
                throw new ArgumentException($"Expected {width * height} pixels, got {pixels.Length}.", nameof(pixels));

            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }

        public int Height { get; }

        public Rgba32[] Pixels { get; }

        public Rgba32 this[int x, int y]
        {
            get => Pixels[y * Width + x];
            set => Pixels[y * Width + x] = value;
        }

        public RgbaImage Clone() => new RgbaImage(Width, Height, (Rgba32[])Pixels.Clone());

        public RgbaImage Crop(PixelRect rect)
        {
            if (rect.IsEmpty || rect.X < 0 || rect.Y < 0 || rect.XMax > Width || rect.YMax > Height)
                throw new ArgumentOutOfRangeException(nameof(rect), $"{rect} is outside the {Width}x{Height} image.");

            var result = new RgbaImage(rect.Width, rect.Height);
            for (int y = 0; y < rect.Height; y++)
                Array.Copy(Pixels, (rect.Y + y) * Width + rect.X, result.Pixels, y * rect.Width, rect.Width);
            return result;
        }

        /// <summary>Copies <paramref name="source"/> into this image with its bottom-left corner at (x, y), clipping at the edges.</summary>
        public void Blit(RgbaImage source, int x, int y)
        {
            int x0 = Math.Max(0, x);
            int y0 = Math.Max(0, y);
            int x1 = Math.Min(Width, x + source.Width);
            int y1 = Math.Min(Height, y + source.Height);
            if (x1 <= x0 || y1 <= y0) return;

            for (int ty = y0; ty < y1; ty++)
                Array.Copy(source.Pixels, (ty - y) * source.Width + (x0 - x), Pixels, ty * Width + x0, x1 - x0);
        }

        /// <summary>Bounds of all pixels whose alpha is at least <paramref name="minAlpha"/>; empty if there are none.</summary>
        public PixelRect FindBounds(int minAlpha = 1)
        {
            int minX = Width, minY = Height, maxX = -1, maxY = -1;
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    if (Pixels[row + x].A < minAlpha) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return maxX < 0 ? PixelRect.Empty : PixelRect.FromMinMax(minX, minY, maxX + 1, maxY + 1);
        }
    }
}
