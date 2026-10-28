using System;
using System.Collections.Generic;
using System.Linq;
using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using static MelloBlack.SpriteTidy.Tests.TestImages;

namespace MelloBlack.SpriteTidy.Tests
{
    public class SpriteFixerTests
    {
        static IReadOnlyList<OutputImage> Fix(RgbaImage image, ImageMode mode, OutputType output = OutputType.Sheet, int size = 128, Pivot pivot = Pivot.Center)
        {
            return SpriteFixer.Fix(image, new FixSettings { Mode = mode, Output = output, Size = size, Pivot = pivot });
        }

        static RgbaImage BigAndSmallSquare()
        {
            var image = Transparent(300, 150);
            FillRect(image, 20, 25, 100, 100, Red);
            FillRect(image, 200, 50, 50, 50, Gold);
            return image;
        }

        [Test]
        public void SingleModeMakesOneSpriteFromEverything()
        {
            var result = Fix(BigAndSmallSquare(), ImageMode.Single);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(128, result[0].Image.Width);
            Assert.AreEqual(128, result[0].Image.Height);
            Assert.AreEqual(1, result[0].Sprites.Count);
            Assert.AreEqual(new PixelRect(0, 0, 128, 128), result[0].Sprites[0].Rect);
            Assert.That(result[0].Image.FindBounds().Width, Is.InRange(123, 125), "both squares together fill the sprite");
        }

        [Test]
        public void AllObjectsShareOneScale()
        {
            var result = Fix(BigAndSmallSquare(), ImageMode.Multiple, OutputType.Separate);

            Assert.AreEqual(2, result.Count);
            Assert.That(result[0].Image.FindBounds().Width, Is.InRange(123, 125));
            Assert.That(result[1].Image.FindBounds().Width, Is.InRange(61, 63), "the small square stays half as big");
        }

        [Test]
        public void BottomPivotStandsObjectsOnOneLineCenteredOnTheirBase()
        {
            // Three frames with the same body; the middle one has something sticking out on its left.
            var image = Transparent(300, 100);
            FillRect(image, 20, 10, 40, 30, Red);
            FillRect(image, 120, 10, 40, 30, Red);
            FillRect(image, 108, 30, 12, 10, Gold);
            FillRect(image, 220, 10, 40, 30, Red);

            var result = Fix(image, ImageMode.Multiple, OutputType.Separate, 128, Pivot.Bottom);

            Assert.AreEqual(3, result.Count);
            var baseWidths = new List<int>();
            foreach (var output in result)
            {
                var bounds = output.Image.FindBounds();
                Assert.AreEqual(2, bounds.Y, "standing on the bottom margin");

                int first = -1, last = -1;
                for (int x = 0; x < 128; x++)
                {
                    if (output.Image[x, bounds.Y].A < 128) continue;
                    if (first < 0) first = x;
                    last = x;
                }

                Assert.That((first + last + 1) / 2.0, Is.EqualTo(64).Within(1), "base centered");
                baseWidths.Add(last - first + 1);
                Assert.AreEqual(0.5f, output.Sprites[0].PivotX);
                Assert.AreEqual(2f / 128f, output.Sprites[0].PivotY);
            }

            Assert.That(baseWidths.Max() - baseWidths.Min(), Is.LessThanOrEqualTo(1), "every frame has the same size");
        }

        [Test]
        public void SheetKeepsTheRowsOfTheSource()
        {
            var image = Transparent(300, 200);
            for (int i = 0; i < 3; i++) FillRect(image, 20 + i * 100, 130, 40, 40, Red);
            for (int i = 0; i < 2; i++) FillRect(image, 20 + i * 100, 30, 40, 40, Gold);

            var result = Fix(image, ImageMode.Multiple, OutputType.Sheet, 64);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(3 * 64, result[0].Image.Width);
            Assert.AreEqual(2 * 64, result[0].Image.Height);
            var rects = result[0].Sprites.Select(s => s.Rect).ToArray();
            Assert.AreEqual(5, rects.Length);
            Assert.AreEqual(new PixelRect(0, 64, 64, 64), rects[0], "the first sprite is top left");
            Assert.AreEqual(new PixelRect(128, 64, 64, 64), rects[2]);
            Assert.AreEqual(new PixelRect(0, 0, 64, 64), rects[3], "the second row starts on the left");
        }

        [Test]
        public void AtlasPacksSpritesWithoutOverlap()
        {
            var image = Transparent(400, 200);
            int[] sizes = { 60, 20, 45, 30, 50 };
            for (int i = 0; i < sizes.Length; i++) FillRect(image, 10 + i * 78, 70, sizes[i], sizes[i], Red);

            var result = Fix(image, ImageMode.Multiple, OutputType.Atlas);

            Assert.AreEqual(1, result.Count);
            var atlas = result[0].Image;
            var rects = result[0].Sprites.Select(s => s.Rect).ToList();
            Assert.AreEqual(sizes.Length, rects.Count);
            for (int a = 0; a < rects.Count; a++)
            {
                Assert.IsTrue(rects[a].X >= 0 && rects[a].Y >= 0 && rects[a].XMax <= atlas.Width && rects[a].YMax <= atlas.Height);
                Assert.IsFalse(atlas.Crop(rects[a]).FindBounds().IsEmpty, "each sprite holds its object");
                for (int b = a + 1; b < rects.Count; b++) Assert.AreEqual(0, rects[a].IntersectionArea(rects[b]));
            }
        }

        [Test]
        public void CleansUpACheckerboardSheet()
        {
            var image = Checkerboard(256, 128, 8, White, LightGray);
            FillDisc(image, 64, 64, 40, Outline);
            FillDisc(image, 64, 64, 36, Gold);
            FillDisc(image, 192, 64, 30, Outline);
            FillDisc(image, 192, 64, 26, Red);

            var result = Fix(image, ImageMode.Multiple, OutputType.Separate);

            Assert.AreEqual(2, result.Count);
            foreach (var output in result)
            {
                Assert.AreEqual(0, output.Image[0, 0].A);
                Assert.AreEqual(255, output.Image[64, 64].A);
            }
        }

        [Test]
        public void DoesNotChangeTheSourceImage()
        {
            var image = Checkerboard(64, 64, 8, White, LightGray);
            FillDisc(image, 32, 32, 20, Red);
            var before = image.Clone();

            Fix(image, ImageMode.Single);

            CollectionAssert.AreEqual(before.Pixels, image.Pixels);
        }

        [Test]
        public void NothingToFixGivesNoImages()
        {
            Assert.AreEqual(0, Fix(Transparent(32, 32), ImageMode.Multiple).Count);
        }
    }
}
