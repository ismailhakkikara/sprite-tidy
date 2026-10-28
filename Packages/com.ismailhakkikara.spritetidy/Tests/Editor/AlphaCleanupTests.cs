using IsmailHakkiKara.SpriteTidy.Core;
using NUnit.Framework;

namespace IsmailHakkiKara.SpriteTidy.Tests
{
    public class AlphaCleanupTests
    {
        [Test]
        public void MakesNearlyOpaquePixelsOpaqueAndClearsHaze()
        {
            var image = new RgbaImage(3, 1);
            image[0, 0] = new Rgba32(10, 20, 30, 252);
            image[1, 0] = new Rgba32(10, 20, 30, 128);
            image[2, 0] = new Rgba32(10, 20, 30, 5);

            AlphaCleanup.Apply(image);

            Assert.AreEqual(new Rgba32(10, 20, 30, 255), image[0, 0]);
            Assert.AreEqual(new Rgba32(10, 20, 30, 128), image[1, 0]);
            Assert.AreEqual(Rgba32.Transparent, image[2, 0]);
        }
    }
}
