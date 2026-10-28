using System.Linq;
using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static MelloBlack.SpriteTidy.Tests.TestImages;

namespace MelloBlack.SpriteTidy.Tests
{
    public class SpriteTidySaverTests
    {
        const string Folder = "Assets/SpriteTidyTests_Temp";
        const string Source = Folder + "/frames.png";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "SpriteTidyTests_Temp");

            var image = Transparent(200, 100);
            FillRect(image, 20, 20, 60, 60, Red);
            FillRect(image, 120, 20, 60, 60, Gold);
            SpriteTidyIO.SavePng(image, Source);
            AssetDatabase.ImportAsset(Source);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        static System.Collections.Generic.List<string> Save(OutputType output)
        {
            var settings = new FixSettings { Mode = ImageMode.Multiple, Output = output, Size = 128, Pivot = Pivot.Bottom };
            return SpriteTidySaver.Save(Source, SpriteFixer.Fix(SpriteTidyIO.Load(Source), settings));
        }

        static Sprite[] SpritesAt(string path) => AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();

        [Test]
        public void SavesASheetAsOneTextureCutIntoSprites()
        {
            var paths = Save(OutputType.Sheet);

            CollectionAssert.AreEqual(new[] { Folder + "/frames_fixed.png" }, paths);
            var importer = (TextureImporter)AssetImporter.GetAtPath(paths[0]);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);

            var sprites = SpritesAt(paths[0]);
            CollectionAssert.AreEqual(new[] { "frames_fixed_01", "frames_fixed_02" }, sprites.Select(s => s.name).ToArray());
            Assert.AreEqual(256, sprites[0].texture.width, "imported at full size");
            Assert.AreEqual(new Rect(0, 0, 128, 128), sprites[0].rect);
            Assert.AreEqual(new Rect(128, 0, 128, 128), sprites[1].rect);
            Assert.AreEqual(new Vector2(64, 2), sprites[0].pivot, "pivot in pixels: bottom center");
        }

        [Test]
        public void SavesSeparateFilesEachWithItsPivot()
        {
            var paths = Save(OutputType.Separate);

            CollectionAssert.AreEqual(new[] { Folder + "/frames_fixed_01.png", Folder + "/frames_fixed_02.png" }, paths);
            foreach (string path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
                Assert.AreEqual(0.5f, importer.spritePivot.x, 1e-4f);
                Assert.AreEqual(2f / 128f, importer.spritePivot.y, 1e-4f);
            }
        }

        [Test]
        public void SavingAgainKeepsTheSpriteIds()
        {
            var path = Save(OutputType.Sheet)[0];
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(SpritesAt(path)[0], out _, out long before);

            Save(OutputType.Sheet);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(SpritesAt(path)[0], out _, out long after);

            Assert.AreEqual(before, after, "animations that use the sprite keep working");
        }
    }
}
