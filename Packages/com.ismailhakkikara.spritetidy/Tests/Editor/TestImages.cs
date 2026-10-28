using System;
using IsmailHakkiKara.SpriteTidy.Core;

namespace IsmailHakkiKara.SpriteTidy.Tests
{
    /// <summary>Builds small synthetic images for the tests.</summary>
    static class TestImages
    {
        public static readonly Rgba32 White = new Rgba32(255, 255, 255);
        public static readonly Rgba32 LightGray = new Rgba32(204, 204, 204);
        public static readonly Rgba32 Black = new Rgba32(0, 0, 0);
        public static readonly Rgba32 Red = new Rgba32(220, 40, 40);
        public static readonly Rgba32 Gold = new Rgba32(240, 190, 40);
        public static readonly Rgba32 Outline = new Rgba32(60, 30, 10);

        public static RgbaImage Filled(int width, int height, Rgba32 color)
        {
            var image = new RgbaImage(width, height);
            for (int i = 0; i < image.Pixels.Length; i++) image.Pixels[i] = color;
            return image;
        }

        public static RgbaImage Transparent(int width, int height) => new RgbaImage(width, height);

        public static RgbaImage Checkerboard(int width, int height, int cell, Rgba32 a, Rgba32 b)
        {
            var image = new RgbaImage(width, height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                image[x, y] = CheckerColor(x, y, cell, a, b);
            return image;
        }

        public static Rgba32 CheckerColor(int x, int y, int cell, Rgba32 a, Rgba32 b) => ((x / cell + y / cell) & 1) == 0 ? a : b;

        public static void FillRect(RgbaImage image, int x, int y, int width, int height, Rgba32 color)
        {
            for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
                image[px, py] = color;
        }

        /// <summary>Fills a rectangle with a border of <paramref name="outline"/> pixels.</summary>
        public static void FillOutlinedRect(RgbaImage image, int x, int y, int width, int height, int border, Rgba32 fill, Rgba32 outline)
        {
            FillRect(image, x, y, width, height, outline);
            FillRect(image, x + border, y + border, width - 2 * border, height - 2 * border, fill);
        }

        public static void FillDisc(RgbaImage image, double centerX, double centerY, double radius, Rgba32 color)
        {
            ForEachInDisc(image, centerX, centerY, radius, (x, y) => image[x, y] = color);
        }

        public static void ForEachInDisc(RgbaImage image, double centerX, double centerY, double radius, Action<int, int> action)
        {
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                double dx = x + 0.5 - centerX, dy = y + 0.5 - centerY;
                if (dx * dx + dy * dy < radius * radius) action(x, y);
            }
        }

        public static int Count(RgbaImage image, Func<Rgba32, bool> predicate)
        {
            int count = 0;
            foreach (var p in image.Pixels)
                if (predicate(p)) count++;
            return count;
        }
    }
}
