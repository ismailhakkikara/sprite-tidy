using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IsmailHakkiKara.SpriteTidy.Core;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace IsmailHakkiKara.SpriteTidy
{
    /// <summary>Saves fixed images next to the source image, or over it, and imports them as sprites.</summary>
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

        /// <summary>Whether <see cref="Overwrite"/> can replace this file. Only PNG files can keep the transparency.</summary>
        public static bool CanOverwrite(string sourcePath) =>
            string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Replaces the source image with the fixed one and imports it as sprites. The asset keeps its GUID and its
        /// sprites keep their IDs, so scenes, prefabs and animations that use them keep working.
        /// </summary>
        public static void Overwrite(string sourcePath, OutputImage image)
        {
            if (!CanOverwrite(sourcePath))
                throw new ArgumentException($"Only PNG files can be overwritten, not '{sourcePath}'.", nameof(sourcePath));

            SpriteTidyIO.SavePng(image.Image, sourcePath);
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
            SetUpSprites(sourcePath, image);
        }

        static void SetUpSprites(string path, OutputImage output)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = TextureSizeFor(output.Image);

            // Unity's default compression (Normal Quality) loses a lot on generated art: on a 512 × 512 sprite some
            // pixels were off by up to 149. High Quality (BC7 on desktop, ASTC 4×4 on mobile) stays close to the PNG,
            // and on desktop it uses the same memory.
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

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
        /// Cuts the texture into sprites. The sprites that were there before keep their names and IDs, so animations
        /// and prefabs that use them still work after saving again or overwriting: a sprite with the same name keeps
        /// its ID, and sprites cut some other way (in the Sprite Editor, say) are kept in order.
        /// </summary>
        static void SetSpriteRects(TextureImporter importer, string baseName, IReadOnlyList<SpriteArea> sprites)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var ourNames = Enumerable.Range(1, sprites.Count).Select(i => $"{baseName}_{i:00}").ToList();
            var old = provider.GetSpriteRects();
            var others = new Queue<SpriteRect>(old.Where(o => !ourNames.Contains(o.name)));

            var rects = new SpriteRect[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                var before = old.FirstOrDefault(o => o.name == ourNames[i]) ?? (others.Count > 0 ? others.Dequeue() : null);
                rects[i] = new SpriteRect
                {
                    name = before?.name ?? ourNames[i],
                    rect = new Rect(sprite.Rect.X, sprite.Rect.Y, sprite.Rect.Width, sprite.Rect.Height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(sprite.PivotX, sprite.PivotY),
                    spriteID = before?.spriteID ?? GUID.Generate(),
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
