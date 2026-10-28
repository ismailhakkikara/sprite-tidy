using System;
using System.Collections.Generic;
using System.Linq;
using IsmailHakkiKara.SpriteTidy.Core;
using NUnit.Framework;
using static IsmailHakkiKara.SpriteTidy.Tests.TestImages;

namespace IsmailHakkiKara.SpriteTidy.Tests
{
    public class SpriteFixerTests
    {
        static IReadOnlyList<OutputImage> Fix(RgbaImage image, ImageMode mode, OutputType output = OutputType.Sheet, int size = 128,
            Pivot pivot = Pivot.Center, LineUp lineUp = LineUp.Bottom, int margin = 2)
        {
            var settings = new FixSettings { Mode = mode, Output = output, Size = size, Margin = margin, Pivot = pivot, LineUp = lineUp };
            return SpriteFixer.Fix(image, settings);
        }

        /// <summary>The middle of the widest row of solid pixels in each cell of a sheet, and that row.</summary>
        static List<(double middle, int row)> WidestRows(OutputImage sheet)
        {
            var result = new List<(double middle, int row)>();
            foreach (var sprite in sheet.Sprites)
            {
                var cell = sheet.Image.Crop(sprite.Rect);
                int bestWidth = -1, bestRow = 0;
                double bestMiddle = 0;
                for (int y = 0; y < cell.Height; y++)
                {
                    int first = -1, last = -1;
                    for (int x = 0; x < cell.Width; x++)
                    {
                        if (cell[x, y].A < 128) continue;
                        if (first < 0) first = x;
                        last = x;
                    }

                    if (first < 0 || last - first <= bestWidth) continue;
                    bestWidth = last - first;
                    bestMiddle = (first + last + 1) / 2.0;
                    bestRow = y;
                }

                result.Add((bestMiddle, bestRow));
            }

            return result;
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

        [TestCase(16, 16, 96)]
        [TestCase(0, 0, 128)]
        public void MarginKeepsEmptySpaceAroundTheObject(int margin, int expectedStart, int expectedSize)
        {
            var image = Transparent(100, 100);
            FillRect(image, 20, 20, 60, 60, Red);

            var result = Fix(image, ImageMode.Single, margin: margin);

            Assert.AreEqual(new PixelRect(expectedStart, expectedStart, expectedSize, expectedSize), result[0].Image.FindBounds());
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

        [TestCase(Pivot.Center)]
        [TestCase(Pivot.Bottom)]
        [TestCase(Pivot.TopLeft)]
        public void SheetFramesStayInPlaceWhateverThePivot(Pivot pivot)
        {
            // Like a chest opening: the same box in every frame, a lid that grows upward in the second frame and
            // a sparkle on the left in the third. Their bounds differ, but the box must not move.
            var image = Transparent(400, 150);
            FillRect(image, 20, 10, 60, 40, Red);
            FillRect(image, 150, 10, 60, 40, Red);
            FillRect(image, 150, 50, 60, 40, Gold);
            FillRect(image, 280, 10, 60, 40, Red);
            FillRect(image, 266, 30, 14, 10, Gold);

            var sheet = Fix(image, ImageMode.Multiple, OutputType.Sheet, 128, pivot)[0];

            var bottoms = new List<int>();
            var baseMiddles = new List<double>();
            foreach (var sprite in sheet.Sprites)
            {
                var cell = sheet.Image.Crop(sprite.Rect);
                int bottom = cell.FindBounds().Y;
                int first = -1, last = -1;
                for (int x = 0; x < cell.Width; x++)
                {
                    if (cell[x, bottom].A < 128) continue;
                    if (first < 0) first = x;
                    last = x;
                }

                bottoms.Add(bottom);
                baseMiddles.Add((first + last + 1) / 2.0);
            }

            Assert.That(bottoms.Max() - bottoms.Min(), Is.LessThanOrEqualTo(1), "the box stays on one line");
            Assert.That(baseMiddles.Max() - baseMiddles.Min(), Is.LessThanOrEqualTo(1), "the box does not slide sideways");
            Assert.AreEqual(1, sheet.Sprites.Select(s => (s.PivotX, s.PivotY)).Distinct().Count(), "one pivot for all frames");
        }

        [Test]
        public void BottomLineUpIgnoresAFootThatSticksOutBelow()
        {
            // Like a slanted chest: in the middle frame one foot reaches lower than the rest, so its lowest rows
            // show only that foot. The body must still line up with the other frames.
            var image = Transparent(300, 260);
            foreach (int x in new[] { 20, 200 })
            {
                FillRect(image, x, 20, 10, 10, Gold);      // left foot
                FillRect(image, x + 50, 20, 10, 10, Gold); // right foot
                FillRect(image, x, 30, 60, 190, Red);      // body
            }

            FillRect(image, 160, 20, 10, 25, Gold); // right foot, sticking out below
            FillRect(image, 110, 45, 60, 190, Red); // body

            var sheet = Fix(image, ImageMode.Multiple, OutputType.Sheet, 128, Pivot.Center)[0];

            var middles = WidestRows(sheet).Select(r => r.middle).ToList();
            Assert.That(middles.Max() - middles.Min(), Is.LessThanOrEqualTo(1), "the bodies line up");
        }

        [Test]
        public void TopLineUpKeepsTheObjectStillWhenEffectsChangeBelowIt()
        {
            // A box with a flame under it that grows from frame to frame.
            var image = Transparent(300, 200);
            int[] flameWidths = { 10, 30, 50 };
            int[] flameHeights = { 20, 50, 80 };
            for (int i = 0; i < 3; i++)
            {
                int x = 20 + i * 90;
                FillRect(image, x, 100, 60, 60, Red);
                FillRect(image, x + 30 - flameWidths[i] / 2, 100 - flameHeights[i], flameWidths[i], flameHeights[i], Gold);
            }

            var sheet = Fix(image, ImageMode.Multiple, OutputType.Sheet, 128, Pivot.Center, LineUp.Top)[0];

            var widest = WidestRows(sheet);
            Assert.That(widest.Max(r => r.middle) - widest.Min(r => r.middle), Is.LessThanOrEqualTo(1), "the box does not slide sideways");
            var tops = sheet.Sprites.Select(s => sheet.Image.Crop(s.Rect).FindBounds().YMax).ToList();
            Assert.That(tops.Max() - tops.Min(), Is.LessThanOrEqualTo(1), "the box stays at the same height");
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
