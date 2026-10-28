using System;
using System.Linq;
using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using static MelloBlack.SpriteTidy.Tests.TestImages;

namespace MelloBlack.SpriteTidy.Tests
{
    public class BackgroundDetectorTests
    {
        [Test]
        public void DetectsTransparentBackground()
        {
            var image = Transparent(64, 64);
            FillRect(image, 20, 20, 24, 24, Red);

            Assert.AreEqual(BackgroundKind.Transparent, BackgroundDetector.Detect(image, 40).Kind);
        }

        [Test]
        public void DetectsSolidBackgroundEvenWhenContentTouchesTheBorder()
        {
            var image = Filled(64, 64, White);
            FillRect(image, 40, 20, 24, 24, Red);

            var model = BackgroundDetector.Detect(image, 40);

            Assert.AreEqual(BackgroundKind.Solid, model.Kind);
            Assert.AreEqual(White, model.Colors[0]);
        }

        [Test]
        public void DetectsLightCheckerboard()
        {
            var image = Checkerboard(128, 128, 8, White, LightGray);
            FillDisc(image, 64, 64, 40, Red);

            var model = BackgroundDetector.Detect(image, 40);

            Assert.AreEqual(BackgroundKind.Checkerboard, model.Kind);
            CollectionAssert.AreEquivalent(new[] { White, LightGray }, model.Colors.ToArray());
        }

        [Test]
        public void DetectsDarkCheckerboardWithCloseColors()
        {
            var image = Checkerboard(128, 128, 8, new Rgba32(18, 18, 18), new Rgba32(34, 34, 34));

            Assert.AreEqual(BackgroundKind.Checkerboard, BackgroundDetector.Detect(image, 40).Kind);
        }

        [Test]
        public void DetectsCheckerboardWhoseShadesAreNearlyTheSame()
        {
            // Measured from a generated image: the two shades are less than 10 apart.
            var image = Checkerboard(160, 120, 17, new Rgba32(15, 17, 19), new Rgba32(20, 22, 24));

            Assert.AreEqual(BackgroundKind.Checkerboard, BackgroundDetector.Detect(image, 40).Kind);
        }

        [Test]
        public void NoiseOnAFlatBackgroundIsNotACheckerboard()
        {
            var random = new Random(1);
            var image = new RgbaImage(160, 120);
            for (int i = 0; i < image.Pixels.Length; i++)
            {
                byte v = (byte)(18 + random.Next(-3, 4));
                image.Pixels[i] = new Rgba32(v, v, v);
            }

            Assert.AreEqual(BackgroundKind.Solid, BackgroundDetector.Detect(image, 40).Kind);
        }
    }

    public class BackgroundRemoverTests
    {
        [Test]
        public void RemovesSolidBackground()
        {
            var image = Filled(64, 64, White);
            FillOutlinedRect(image, 20, 20, 24, 24, 2, Red, Outline);

            BackgroundRemover.Apply(image, new BackgroundSettings());

            Assert.AreEqual(0, image[0, 0].A);
            Assert.AreEqual(0, image[10, 50].A);
            Assert.AreEqual(Red, image[32, 32]);
            Assert.AreEqual(24 * 24, Count(image, p => p.A > 0));
        }

        [Test]
        public void RemovesCheckerboardShowingThroughAHole()
        {
            var image = RingOnCheckerboard();

            BackgroundRemover.Apply(image, new BackgroundSettings());

            Assert.AreEqual(0, image[64, 64].A, "hole in the middle of the ring");
            Assert.AreEqual(0, image[2, 2].A, "outside the ring");
            Assert.AreEqual(Gold, image[64 + 30, 64], "body of the ring");
        }

        [Test]
        public void KeepsOutlinesThatAreCloseToADarkCheckerboard()
        {
            var darkA = new Rgba32(15, 17, 19);
            var darkB = new Rgba32(20, 22, 24);
            var image = Checkerboard(160, 160, 17, darkA, darkB);
            FillDisc(image, 80, 80, 60, new Rgba32(240, 240, 240)); // sticker-style white stroke
            FillDisc(image, 80, 80, 56, new Rgba32(17, 19, 21));    // dark outline, nearly the background color
            FillDisc(image, 80, 80, 50, new Rgba32(180, 180, 190));
            ForEachInDisc(image, 80, 80, 20, (x, y) => image[x, y] = CheckerColor(x, y, 17, darkA, darkB));

            BackgroundRemover.Apply(image, new BackgroundSettings());

            Assert.AreEqual(255, image[80 + 53, 80].A, "the outline stays");
            Assert.AreEqual(0, image[80, 80].A, "the hole is cleared");
        }

        [Test]
        public void HoleFillingOffKeepsTheHole()
        {
            var image = RingOnCheckerboard();

            BackgroundRemover.Apply(image, new BackgroundSettings { Holes = HoleFilling.Off });

            Assert.AreEqual(255, image[64, 64].A);
        }

        [Test]
        public void AutoHoleFillingKeepsEnclosedAreasOnSolidBackgrounds()
        {
            var image = DarkPanelOnBlack();

            BackgroundRemover.Apply(image, new BackgroundSettings());

            Assert.AreEqual(255, image[32, 32].A);
        }

        [Test]
        public void HoleFillingOnRemovesEnclosedAreasOnSolidBackgrounds()
        {
            var image = DarkPanelOnBlack();

            BackgroundRemover.Apply(image, new BackgroundSettings { Holes = HoleFilling.On });

            Assert.AreEqual(0, image[32, 32].A);
            Assert.AreEqual(Gold, image[13, 32], "the frame stays");
        }

        [Test]
        public void UnblendsAntiAliasedEdgePixels()
        {
            var dark = new Rgba32(40, 40, 40);
            var image = Filled(32, 32, White);
            FillRect(image, 10, 10, 12, 12, dark);
            // A one-pixel anti-aliased edge: half dark, half white.
            FillRect(image, 9, 10, 1, 12, new Rgba32(148, 148, 148));

            BackgroundRemover.Apply(image, new BackgroundSettings());

            var edge = image[9, 15];
            Assert.That(edge.A, Is.InRange(115, 140), "alpha follows the blend factor");
            Assert.That(Math.Abs(edge.R - dark.R), Is.LessThanOrEqualTo(6), "the white is gone from the color");
            Assert.AreEqual(dark, image[15, 15]);
        }

        [Test]
        public void LeavesTransparentImagesAlone()
        {
            var image = Transparent(32, 32);
            FillRect(image, 8, 8, 16, 16, White);
            var before = image.Clone();

            var model = BackgroundRemover.Apply(image, new BackgroundSettings());

            Assert.AreEqual(BackgroundKind.Transparent, model.Kind);
            CollectionAssert.AreEqual(before.Pixels, image.Pixels);
        }

        static RgbaImage RingOnCheckerboard()
        {
            var image = Checkerboard(128, 128, 8, White, LightGray);
            FillDisc(image, 64, 64, 40, Outline);
            FillDisc(image, 64, 64, 37, Gold);
            FillDisc(image, 64, 64, 23, Outline);
            ForEachInDisc(image, 64, 64, 20, (x, y) => image[x, y] = CheckerColor(x, y, 8, White, LightGray));
            return image;
        }

        static RgbaImage DarkPanelOnBlack()
        {
            var image = Filled(64, 64, Black);
            FillOutlinedRect(image, 12, 12, 40, 40, 3, new Rgba32(12, 12, 12), Gold);
            return image;
        }
    }
}
