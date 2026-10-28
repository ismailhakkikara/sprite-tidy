using System.Linq;
using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using static MelloBlack.SpriteTidy.Tests.TestImages;

namespace MelloBlack.SpriteTidy.Tests
{
    public class ObjectSplitterTests
    {
        [Test]
        public void SplitsSeparateObjectsInReadingOrder()
        {
            var image = Transparent(200, 200);
            FillRect(image, 20, 20, 40, 40, Red);   // bottom left
            FillRect(image, 140, 20, 40, 40, Red);  // bottom right
            FillRect(image, 20, 140, 40, 40, Red);  // top left
            FillRect(image, 140, 140, 40, 40, Red); // top right

            var objects = ObjectSplitter.Split(image, new SplitSettings());

            var origins = objects.Select(o => (o.SourceRect.X, o.SourceRect.Y)).ToArray();
            CollectionAssert.AreEqual(new[] { (20, 140), (140, 140), (20, 20), (140, 20) }, origins);
            Assert.IsTrue(objects.All(o => o.Image.Width == 40 && o.Image.Height == 40));
        }

        [Test]
        public void AttachesANearbyFragmentToItsObject()
        {
            var image = Transparent(200, 200);
            FillRect(image, 50, 50, 40, 40, Red);
            FillRect(image, 95, 70, 3, 3, Gold); // a sparkle 5 px to the right

            var objects = ObjectSplitter.Split(image, new SplitSettings());

            Assert.AreEqual(1, objects.Count);
            Assert.AreEqual(new PixelRect(50, 50, 48, 40), objects[0].SourceRect);
        }

        [Test]
        public void DropsDistantSpecks()
        {
            var image = Transparent(200, 200);
            FillRect(image, 50, 50, 40, 40, Red);
            FillRect(image, 180, 180, 2, 2, Gold);

            var objects = ObjectSplitter.Split(image, new SplitSettings());

            Assert.AreEqual(1, objects.Count);
            Assert.AreEqual(new PixelRect(50, 50, 40, 40), objects[0].SourceRect);
        }

        [Test]
        public void ObjectsJoinedOnlyByFaintGlowStaySeparate()
        {
            var image = Transparent(200, 200);
            FillRect(image, 20, 80, 40, 40, Red);
            FillRect(image, 100, 80, 40, 40, Red);
            FillRect(image, 60, 95, 40, 10, new Rgba32(255, 220, 120, 40));

            var objects = ObjectSplitter.Split(image, new SplitSettings());

            Assert.AreEqual(2, objects.Count);
            Assert.That(objects[0].SourceRect.XMax, Is.InRange(61, 70), "takes the glow next to it, up to the halo distance");
            Assert.That(objects[1].SourceRect.X, Is.InRange(90, 99));
        }

        [Test]
        public void MergesPiecesWhoseBoundsOverlap()
        {
            var image = Transparent(120, 120);
            FillDisc(image, 60, 60, 30, Gold);
            ForEachInDisc(image, 60, 60, 24, (x, y) => image[x, y] = Rgba32.Transparent);
            FillDisc(image, 60, 60, 10, Red); // a dot inside the ring, not touching it

            var objects = ObjectSplitter.Split(image, new SplitSettings());

            Assert.AreEqual(1, objects.Count);
        }

        [Test]
        public void OffModeKeepsEverythingAsOneObject()
        {
            var image = Transparent(200, 200);
            FillRect(image, 20, 20, 40, 40, Red);
            FillRect(image, 140, 140, 40, 40, Red);

            var objects = ObjectSplitter.Split(image, new SplitSettings { Mode = SplitMode.Off });

            Assert.AreEqual(1, objects.Count);
            Assert.AreEqual(new PixelRect(20, 20, 160, 160), objects[0].SourceRect);
        }

        [Test]
        public void EmptyImageHasNoObjects()
        {
            Assert.AreEqual(0, ObjectSplitter.Split(Transparent(16, 16), new SplitSettings()).Count);
        }
    }
}
