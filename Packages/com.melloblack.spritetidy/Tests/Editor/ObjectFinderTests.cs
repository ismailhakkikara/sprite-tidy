using System.Linq;
using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using static MelloBlack.SpriteTidy.Tests.TestImages;

namespace MelloBlack.SpriteTidy.Tests
{
    public class ObjectFinderTests
    {
        [Test]
        public void FindsObjectsInReadingOrderWithTheirRows()
        {
            var image = Transparent(200, 200);
            FillRect(image, 20, 20, 40, 40, Red);   // bottom left
            FillRect(image, 140, 20, 40, 40, Red);  // bottom right
            FillRect(image, 20, 140, 40, 40, Red);  // top left
            FillRect(image, 140, 140, 40, 40, Red); // top right

            var parts = ObjectFinder.Find(image);

            CollectionAssert.AreEqual(new[] { (20, 140), (140, 140), (20, 20), (140, 20) }, parts.Select(p => (p.SourceRect.X, p.SourceRect.Y)).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, parts.Select(p => p.Row).ToArray());
            Assert.IsTrue(parts.All(p => p.Image.Width == 40 && p.Image.Height == 40));
        }

        [Test]
        public void GivesANearbyFragmentToItsObject()
        {
            var image = Transparent(200, 200);
            FillRect(image, 50, 50, 40, 40, Red);
            FillRect(image, 95, 70, 3, 3, Gold); // a sparkle 5 px to the right

            var parts = ObjectFinder.Find(image);

            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual(new PixelRect(50, 50, 48, 40), parts[0].SourceRect);
        }

        [Test]
        public void DropsDistantSpecks()
        {
            var image = Transparent(200, 200);
            FillRect(image, 50, 50, 40, 40, Red);
            FillRect(image, 180, 180, 2, 2, Gold);

            var parts = ObjectFinder.Find(image);

            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual(new PixelRect(50, 50, 40, 40), parts[0].SourceRect);
        }

        [Test]
        public void ObjectsJoinedOnlyByFaintGlowStaySeparate()
        {
            var image = Transparent(200, 200);
            FillRect(image, 20, 80, 40, 40, Red);
            FillRect(image, 100, 80, 40, 40, Red);
            FillRect(image, 60, 95, 40, 10, new Rgba32(255, 220, 120, 40));

            var parts = ObjectFinder.Find(image);

            Assert.AreEqual(2, parts.Count);
            Assert.That(parts[0].SourceRect.XMax, Is.InRange(61, 70), "takes the glow next to it");
            Assert.That(parts[1].SourceRect.X, Is.InRange(90, 99));
        }

        [Test]
        public void JoinsPiecesWhoseBoundsOverlap()
        {
            var image = Transparent(120, 120);
            FillDisc(image, 60, 60, 30, Gold);
            ForEachInDisc(image, 60, 60, 24, (x, y) => image[x, y] = Rgba32.Transparent);
            FillDisc(image, 60, 60, 10, Red); // a dot inside the ring, not touching it

            Assert.AreEqual(1, ObjectFinder.Find(image).Count);
        }

        [Test]
        public void EmptyImageHasNoObjects()
        {
            Assert.AreEqual(0, ObjectFinder.Find(Transparent(16, 16)).Count);
        }
    }
}
