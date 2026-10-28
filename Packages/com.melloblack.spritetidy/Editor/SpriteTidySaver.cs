using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelloBlack.SpriteTidy.Core;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MelloBlack.SpriteTidy
{
    /// <summary>Saves fixed images next to the source image and imports them as sprites.</summary>
    public static class SpriteTidySaver
    {
        public const string Suffix = "_fixed";

        /// <summary>
        /// The asset paths the images will be saved to: "name_fixed.png" for one image,
        /// "name_fixed_01.png", "name_fixed_02.png", … for several.
        /// </summary>
        public static List<string> OutputPaths(string sourcePath, int count)
        {
            string folder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(sourcePath) + Suffix;
            if (count == 1) return new List<string> { $"{folder}/{name}.png" };
            return Enumerable.Range(1, count).Select(i => $"{folder}/{name}_{i:00}.png").ToList();
        }

        /// <summary>Saves the images and imports them as sprites. Saving again overwrites the same files.</summary>
        /// <returns>The asset paths that were written.</returns>
        public static List<string> Save(string sourcePath, IReadOnlyList<OutputImage> images)
        {
            var paths = OutputPaths(sourcePath, images.Count);
            for (int i = 0; i < images.Count; i++)
            {
                SpriteTidyIO.SavePng(images[i].Image, paths[i]);
                AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
                SetUpSprites(paths[i], images[i]);
            }

            return paths;
        }

        static void SetUpSprites(string path, OutputImage output)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = TextureSizeFor(output.Image);

            if (output.Sprites.Count == 1)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(output.Sprites[0].PivotX, output.Sprites[0].PivotY);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                return;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();
            SetSpriteRects(importer, Path.GetFileNameWithoutExtension(path), output.Sprites);
        }

        /// <summary>
        /// Cuts the texture into sprites. A sprite that existed before with the same name keeps its ID,
        /// so animations and prefabs that use it still work after saving again.
        /// </summary>
        static void SetSpriteRects(TextureImporter importer, string baseName, IReadOnlyList<SpriteArea> sprites)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var oldIds = new Dictionary<string, GUID>();
            foreach (var old in provider.GetSpriteRects()) oldIds[old.name] = old.spriteID;

            var rects = new SpriteRect[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                string name = $"{baseName}_{i + 1:00}";
                rects[i] = new SpriteRect
                {
                    name = name,
                    rect = new Rect(sprite.Rect.X, sprite.Rect.Y, sprite.Rect.Width, sprite.Rect.Height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(sprite.PivotX, sprite.PivotY),
                    spriteID = oldIds.TryGetValue(name, out var id) ? id : GUID.Generate(),
                };
            }

            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>The smallest import size that keeps the texture at full resolution.</summary>
        static int TextureSizeFor(RgbaImage image)
        {
            int longest = Math.Max(image.Width, image.Height);
            int size = 32;
            while (size < longest && size < 16384) size *= 2;
            return size;
        }
    }
}
