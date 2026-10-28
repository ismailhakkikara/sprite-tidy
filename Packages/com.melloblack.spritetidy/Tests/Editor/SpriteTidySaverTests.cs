using System.Linq;
using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
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
            var settings = new FixSettings { Mode = ImageMode.Multiple, Output = output, Size = 128, Margin = 2, Pivot = Pivot.Bottom };
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
            Assert.AreEqual(TextureImporterCompression.CompressedHQ, importer.textureCompression, "Normal Quality compression is too lossy");

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

        [Test]
        public void OnlyPngFilesCanBeOverwritten()
        {
            Assert.IsTrue(SpriteTidySaver.CanOverwrite("Assets/art/chest.png"));
            Assert.IsTrue(SpriteTidySaver.CanOverwrite("Assets/art/chest.PNG"));
            Assert.IsFalse(SpriteTidySaver.CanOverwrite("Assets/art/chest.jpg"), "a JPG can't keep the transparency");
        }

        [Test]
        public void OverwritingKeepsTheImageAndItsSprite()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Source);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            string guid = AssetDatabase.AssetPathToGUID(Source);
            long before = IdOf(SpritesAt(Source).Single());

            var settings = new FixSettings { Size = 128, Margin = 2, Pivot = Pivot.Bottom };
            SpriteTidySaver.Overwrite(Source, SpriteFixer.Fix(SpriteTidyIO.Load(Source), settings)[0]);

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(Source));
            var sprite = SpritesAt(Source).Single();
            Assert.AreEqual(before, IdOf(sprite), "scenes and prefabs that use the sprite keep working");
            Assert.AreEqual(128, sprite.texture.width);
            Assert.AreEqual(new Vector2(64, 2), sprite.pivot);
        }

        [Test]
        public void OverwritingKeepsTheSpritesCutInTheSpriteEditor()
        {
            CutLikeTheSpriteEditor(Source, "frames_0", "frames_1");
            var before = SpritesAt(Source).Select(IdOf).ToArray();

            var settings = new FixSettings { Mode = ImageMode.Multiple, Output = OutputType.Sheet, Size = 128, Margin = 2 };
            SpriteTidySaver.Overwrite(Source, SpriteFixer.Fix(SpriteTidyIO.Load(Source), settings)[0]);

            var sprites = SpritesAt(Source);
            CollectionAssert.AreEqual(new[] { "frames_0", "frames_1" }, sprites.Select(s => s.name).ToArray());
            CollectionAssert.AreEqual(before, sprites.Select(IdOf).ToArray(), "animations that use the sprites keep working");
            Assert.AreEqual(new Rect(128, 0, 128, 128), sprites[1].rect);
        }

        static long IdOf(Sprite sprite)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out _, out long id);
            return id;
        }

        /// <summary>Cuts the image into sprites the way the Sprite Editor does, with its names.</summary>
        static void CutLikeTheSpriteEditor(string path, params string[] names)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = names.Select((name, i) => new SpriteRect { name = name, rect = new Rect(i * 100, 0, 100, 100), spriteID = GUID.Generate() }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }
    }
}
