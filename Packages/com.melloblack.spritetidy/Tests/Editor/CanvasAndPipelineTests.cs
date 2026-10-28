using System.Linq;
using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using static MelloBlack.SpriteTidy.Tests.TestImages;

namespace MelloBlack.SpriteTidy.Tests
{
    public class CanvasFitterTests
    {
        static TidySprite Fit(CanvasSettings settings, int width = 100, int height = 50)
        {
            var content = new TidyObject(Filled(width, height, Red), new PixelRect(0, 0, width, height));
            return CanvasFitter.Fit(content, CanvasFitter.FitScale(width, height, settings), settings);
        }

        [Test]
        public void ScalesContentToFitInsideThePaddingAndCentersIt()
        {
            var sprite = Fit(new CanvasSettings());

            Assert.AreEqual(512, sprite.Image.Width);
            Assert.AreEqual(512, sprite.Image.Height);
            Assert.AreEqual(new PixelRect(16, 136, 480, 240), sprite.Image.FindBounds());
            Assert.AreEqual(0.5f, sprite.PivotX);
            Assert.AreEqual(0.5f, sprite.PivotY);
        }

        [Test]
        public void BottomAnchorPutsContentOnThePaddingAndThePivotAtItsBase()
        {
            var sprite = Fit(new CanvasSettings { VerticalAnchor = VerticalAnchor.Bottom, HorizontalAnchor = HorizontalAnchor.Left });

            Assert.AreEqual(new PixelRect(16, 16, 480, 240), sprite.Image.FindBounds());
            Assert.AreEqual(16f / 512f, sprite.PivotX);
            Assert.AreEqual(16f / 512f, sprite.PivotY);
        }

        [Test]
        public void WithoutUpscalingSmallContentKeepsItsSize()
        {
            var sprite = Fit(new CanvasSettings { AllowUpscale = false });

            Assert.AreEqual(new PixelRect(206, 231, 100, 50), sprite.Image.FindBounds());
        }

        [Test]
        public void TrimOnlyWrapsTheContentInPadding()
        {
            var sprite = Fit(new CanvasSettings { Mode = CanvasMode.TrimOnly, Padding = 4 });

            Assert.AreEqual(108, sprite.Image.Width);
            Assert.AreEqual(58, sprite.Image.Height);
            Assert.AreEqual(new PixelRect(4, 4, 100, 50), sprite.Image.FindBounds());
        }
    }

    public class TidyPipelineTests
    {
        static RgbaImage BigAndSmallSquare()
        {
            var image = Transparent(300, 150);
            FillRect(image, 20, 25, 100, 100, Red);
            FillRect(image, 200, 50, 50, 50, Gold);
            return image;
        }

        [Test]
        public void FitEachMakesEverySpriteFillTheCanvas()
        {
            var result = TidyPipeline.Process(BigAndSmallSquare(), new TidySettings());

            Assert.AreEqual(2, result.Sprites.Count);
            Assert.IsTrue(result.Sprites.All(s => s.Image.FindBounds().Width == 480));
        }

        [Test]
        public void KeepRelativeSizeKeepsTheSmallObjectSmaller()
        {
            var settings = new TidySettings();
            settings.Canvas.FitMode = FitMode.KeepRelativeSize;

            var result = TidyPipeline.Process(BigAndSmallSquare(), settings);

            Assert.AreEqual(480, result.Sprites[0].Image.FindBounds().Width);
            Assert.AreEqual(240, result.Sprites[1].Image.FindBounds().Width);
        }

        [Test]
        public void CleansUpACheckerboardSheetEndToEnd()
        {
            var image = Checkerboard(256, 128, 8, White, LightGray);
            FillDisc(image, 64, 64, 40, Outline);
            FillDisc(image, 64, 64, 36, Gold);
            FillDisc(image, 192, 64, 30, Outline);
            FillDisc(image, 192, 64, 26, Red);

            var result = TidyPipeline.Process(image, new TidySettings());

            Assert.AreEqual(BackgroundKind.Checkerboard, result.Background.Kind);
            Assert.AreEqual(2, result.Sprites.Count);
            foreach (var sprite in result.Sprites)
            {
                Assert.AreEqual(0, sprite.Image[0, 0].A);
                Assert.AreEqual(255, sprite.Image[256, 256].A);
            }
        }

        [Test]
        public void DoesNotModifyTheSourceImage()
        {
            var image = Checkerboard(64, 64, 8, White, LightGray);
            FillDisc(image, 32, 32, 20, Red);
            var before = image.Clone();

            TidyPipeline.Process(image, new TidySettings());

            CollectionAssert.AreEqual(before.Pixels, image.Pixels);
        }
    }
}
