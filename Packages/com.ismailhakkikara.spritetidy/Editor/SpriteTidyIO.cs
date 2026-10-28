using System.IO;
using IsmailHakkiKara.SpriteTidy.Core;
using UnityEngine;

namespace IsmailHakkiKara.SpriteTidy
{
    /// <summary>Moves images between files, Unity textures and <see cref="RgbaImage"/>.</summary>
    public static class SpriteTidyIO
    {
        /// <summary>
        /// Reads a PNG or JPG straight from disk. Going through the file instead of the imported texture means the
        /// texture doesn't need Read/Write enabled and import compression or max size never affects the result.
        /// </summary>
        public static RgbaImage Load(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false))
                    throw new InvalidDataException($"'{path}' is not a PNG or JPG image Unity can read.");
                return FromTexture(texture);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public static void SavePng(RgbaImage image, string path)
        {
            var texture = ToTexture(image);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public static RgbaImage FromTexture(Texture2D texture)
        {
            var source = texture.GetPixels32();
            var pixels = new Rgba32[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var c = source[i];
                pixels[i] = new Rgba32(c.r, c.g, c.b, c.a);
            }

            return new RgbaImage(texture.width, texture.height, pixels);
        }

        /// <summary>Creates an editor-only texture; the caller is responsible for destroying it.</summary>
        public static Texture2D ToTexture(RgbaImage image)
        {
            var colors = new Color32[image.Pixels.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                var p = image.Pixels[i];
                colors[i] = new Color32(p.R, p.G, p.B, p.A);
            }

            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(colors);
            texture.Apply(false);
            return texture;
        }
    }
}
