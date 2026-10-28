using MelloBlack.SpriteTidy.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static MelloBlack.SpriteTidy.Tests.TestImages;

namespace MelloBlack.SpriteTidy.Tests
{
    public class SpriteTidyProcessorTests
    {
        const string Folder = "Assets/SpriteTidyTests_Temp";

        SpriteTidyPreset preset;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "SpriteTidyTests_Temp");
            preset = ScriptableObject.CreateInstance<SpriteTidyPreset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(preset);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void WritesEachObjectAsASpriteWithThePivotAtTheAnchor()
        {
            var image = Checkerboard(256, 128, 8, White, LightGray);
            FillDisc(image, 64, 64, 40, Outline);
            FillDisc(image, 64, 64, 36, Gold);
            FillDisc(image, 192, 64, 30, Outline);
            FillDisc(image, 192, 64, 26, Red);
            string source = Folder + "/sheet.png";
            SpriteTidyIO.SavePng(image, source);
            AssetDatabase.ImportAsset(source);

            preset.width = 128;
            preset.height = 128;
            preset.padding = 8;
            preset.verticalAnchor = VerticalAnchor.Bottom;
            var written = SpriteTidyProcessor.Process(new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(source) }, preset, "");

            CollectionAssert.AreEqual(new[] { Folder + "/Tidy/sheet_01.png", Folder + "/Tidy/sheet_02.png" }, written);
            foreach (string path in written)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.IsTrue(importer.alphaIsTransparency);
                Assert.AreEqual(0.5f, importer.spritePivot.x, 1e-4f);
                Assert.AreEqual(8f / 128f, importer.spritePivot.y, 1e-4f);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite);
                Assert.AreEqual(128, sprite.texture.width);
            }
        }

        [Test]
        public void ReprocessingOverwritesTheSameFiles()
        {
            var image = Transparent(64, 64);
            FillRect(image, 16, 16, 32, 32, Red);
            string source = Folder + "/single.png";
            SpriteTidyIO.SavePng(image, source);
            AssetDatabase.ImportAsset(source);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source);

            var first = SpriteTidyProcessor.Process(new[] { texture }, preset, "");
            string guid = AssetDatabase.AssetPathToGUID(first[0]);
            var second = SpriteTidyProcessor.Process(new[] { texture }, preset, "");

            CollectionAssert.AreEqual(new[] { Folder + "/Tidy/single.png" }, second);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(second[0]), "the GUID is kept so references survive");
        }
    }
}
