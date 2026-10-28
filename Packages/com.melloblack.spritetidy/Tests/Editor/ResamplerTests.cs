using System;
using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using static MelloBlack.SpriteTidy.Tests.TestImages;

namespace MelloBlack.SpriteTidy.Tests
{
    public class ResamplerTests
    {
        [TestCase(17, 11)]
        [TestCase(90, 70)]
        public void KeepsAPlainColorPlain(int width, int height)
        {
            var color = new Rgba32(100, 150, 200);
            var result = Resampler.Resize(Filled(40, 30, color), width, height);

            Assert.AreEqual(width, result.Width);
            Assert.AreEqual(height, result.Height);
            foreach (var p in result.Pixels)
            {
                Assert.That(Math.Abs(p.R - color.R) + Math.Abs(p.G - color.G) + Math.Abs(p.B - color.B), Is.LessThanOrEqualTo(3));
                Assert.AreEqual(255, p.A);
            }
        }

        [Test]
        public void TransparentPixelsDoNotColorTheEdges()
        {
            var image = Filled(40, 40, new Rgba32(0, 255, 0, 0)); // transparent, but green underneath
            FillRect(image, 0, 0, 20, 40, new Rgba32(255, 0, 0));

            var result = Resampler.Resize(image, 13, 13);

            foreach (var p in result.Pixels)
                if (p.A > 0) Assert.AreEqual(0, p.G, "no green fringe");
        }

        [Test]
        public void AveragesWhenShrinking()
        {
            var image = Checkerboard(64, 64, 1, new Rgba32(0, 0, 0), new Rgba32(255, 255, 255));

            var result = Resampler.Resize(image, 8, 8);

            foreach (var p in result.Pixels) Assert.That(p.R, Is.InRange(110, 145));
        }
    }
}
