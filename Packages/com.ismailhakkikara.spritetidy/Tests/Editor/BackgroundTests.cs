using System;
using IsmailHakkiKara.SpriteTidy.Core;
using NUnit.Framework;
using static IsmailHakkiKara.SpriteTidy.Tests.TestImages;

namespace IsmailHakkiKara.SpriteTidy.Tests
{
    public class BackgroundDetectorTests
    {
        [Test]
        public void FindsTransparentBackground()
        {
            var image = Transparent(64, 64);
            FillRect(image, 20, 20, 24, 24, Red);

            Assert.AreEqual(BackgroundType.Transparent, BackgroundDetector.Detect(image, out _));
        }

        [Test]
        public void FindsPlainBackgroundEvenWhenAnObjectTouchesTheBorder()
        {
            var image = Filled(64, 64, White);
            FillRect(image, 40, 20, 24, 24, Red);

            Assert.AreEqual(BackgroundType.Solid, BackgroundDetector.Detect(image, out var colors));
            Assert.AreEqual(White, colors[0]);
        }

        [Test]
        public void FindsLightCheckerboard()
        {
            var image = Checkerboard(128, 128, 8, White, LightGray);
            FillDisc(image, 64, 64, 40, Red);

            Assert.AreEqual(BackgroundType.Checkerboard, BackgroundDetector.Detect(image, out var colors));
            CollectionAssert.AreEquivalent(new[] { White, LightGray }, colors);
        }

        [Test]
        public void FindsCheckerboardWhoseColorsAreNearlyTheSame()
        {
            // Measured from a generated image: the two colors are less than 10 apart.
            var image = Checkerboard(160, 120, 17, new Rgba32(15, 17, 19), new Rgba32(20, 22, 24));

            Assert.AreEqual(BackgroundType.Checkerboard, BackgroundDetector.Detect(image, out _));
        }

        [Test]
        public void NoiseOnAPlainBackgroundIsNotACheckerboard()
        {
            var random = new Random(1);
            var image = new RgbaImage(160, 120);
            for (int i = 0; i < image.Pixels.Length; i++)
            {
                byte v = (byte)(18 + random.Next(-3, 4));
                image.Pixels[i] = new Rgba32(v, v, v);
            }

            Assert.AreEqual(BackgroundType.Solid, BackgroundDetector.Detect(image, out _));
        }
    }

    public class BackgroundRemoverTests
    {
        [Test]
        public void RemovesPlainBackground()
        {
            var image = Filled(64, 64, White);
            FillOutlinedRect(image, 20, 20, 24, 24, 2, Red, Outline);

            Assert.AreEqual(BackgroundType.Solid, BackgroundRemover.Remove(image));

            Assert.AreEqual(0, image[0, 0].A);
            Assert.AreEqual(0, image[10, 50].A);
            Assert.AreEqual(Red, image[32, 32]);
            Assert.AreEqual(24 * 24, Count(image, p => p.A > 0));
        }

        [Test]
        public void RemovesCheckerboardShowingThroughAHole()
        {
            var image = Checkerboard(128, 128, 8, White, LightGray);
            FillDisc(image, 64, 64, 40, Outline);
            FillDisc(image, 64, 64, 37, Gold);
            FillDisc(image, 64, 64, 23, Outline);
            ForEachInDisc(image, 64, 64, 20, (x, y) => image[x, y] = CheckerColor(x, y, 8, White, LightGray));

            BackgroundRemover.Remove(image);

            Assert.AreEqual(0, image[64, 64].A, "hole in the middle of the ring");
            Assert.AreEqual(0, image[2, 2].A, "outside the ring");
            Assert.AreEqual(Gold, image[64 + 30, 64], "body of the ring");
        }

        [Test]
        public void RemovesTheSoftLinesBetweenCheckerboardSquares()
        {
            // Measured from a generated image: the pixels between two squares mix both colors, and the mix is
            // farther than the tolerance from either color.
            var light = new Rgba32(203, 202, 202);
            var dark = new Rgba32(147, 147, 147);
            var mix = new Rgba32(175, 175, 175);
            var image = Checkerboard(160, 160, 16, light, dark);
            for (int y = 0; y < 160; y++)
            for (int x = 0; x < 160; x++)
                if (x % 16 == 0 || y % 16 == 0) image[x, y] = mix;
            FillOutlinedRect(image, 50, 50, 60, 60, 3, Red, Outline);

            BackgroundRemover.Remove(image);

            Assert.AreEqual(60 * 60, Count(image, p => p.A > 0), "only the object is left");
        }

        [Test]
        public void KeepsClosedAreasOnPlainBackgrounds()
        {
            // A dark panel in a gold frame on black: the panel is close to the background color but part of the art.
            var image = Filled(64, 64, Black);
            FillOutlinedRect(image, 12, 12, 40, 40, 3, new Rgba32(12, 12, 12), Gold);

            BackgroundRemover.Remove(image);

            Assert.AreEqual(255, image[32, 32].A);
        }

        [Test]
        public void KeepsOutlinesThatAreCloseToADarkCheckerboard()
        {
            var darkA = new Rgba32(15, 17, 19);
            var darkB = new Rgba32(20, 22, 24);
            var image = Checkerboard(160, 160, 17, darkA, darkB);
            FillDisc(image, 80, 80, 60, new Rgba32(240, 240, 240)); // sticker-style white stroke
            // A dark outline, nearly the background color and shaded from darker to lighter like real outlines,
            // so it matches both checkerboard colors.
            ForEachInDisc(image, 80, 80, 56, (x, y) =>
            {
                double r = Math.Sqrt((x + 0.5 - 80) * (x + 0.5 - 80) + (y + 0.5 - 80) * (y + 0.5 - 80));
                byte v = (byte)(12 + Math.Max(0, r - 50) * 2);
                image[x, y] = new Rgba32(v, (byte)(v + 2), (byte)(v + 4));
            });
            FillDisc(image, 80, 80, 50, new Rgba32(180, 180, 190));
            ForEachInDisc(image, 80, 80, 20, (x, y) => image[x, y] = CheckerColor(x, y, 17, darkA, darkB));

            BackgroundRemover.Remove(image);

            Assert.AreEqual(255, image[80 + 51, 80].A, "the outline stays");
            Assert.AreEqual(255, image[80 + 55, 80].A, "the outline stays");
            Assert.AreEqual(0, image[80, 80].A, "the hole is cleared");
        }

        [Test]
        public void UnblendsSoftEdgePixels()
        {
            var dark = new Rgba32(40, 40, 40);
            var image = Filled(32, 32, White);
            FillRect(image, 10, 10, 12, 12, dark);
            // A one-pixel soft edge: half dark, half white.
            FillRect(image, 9, 10, 1, 12, new Rgba32(148, 148, 148));

            BackgroundRemover.Remove(image);

            var edge = image[9, 15];
            Assert.That(edge.A, Is.InRange(115, 140), "alpha follows the blend");
            Assert.That(Math.Abs(edge.R - dark.R), Is.LessThanOrEqualTo(6), "the white is gone from the color");
            Assert.AreEqual(dark, image[15, 15]);
        }

        [Test]
        public void LeavesTransparentImagesAlone()
        {
            var image = Transparent(32, 32);
            FillRect(image, 8, 8, 16, 16, White);
            var before = image.Clone();

            Assert.AreEqual(BackgroundType.Transparent, BackgroundRemover.Remove(image));
            CollectionAssert.AreEqual(before.Pixels, image.Pixels);
        }
    }
}
