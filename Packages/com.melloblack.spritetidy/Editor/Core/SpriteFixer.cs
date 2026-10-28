using System;
using System.Collections.Generic;
using System.Linq;

namespace MelloBlack.SpriteTidy.Core
{
    /// <summary>Does the image show one object or several?</summary>
    public enum ImageMode
    {
        Single,
        Multiple,
    }

    /// <summary>How the objects of a <see cref="ImageMode.Multiple"/> image are saved.</summary>
    public enum OutputType
    {
        /// <summary>
        /// One texture with the objects in a grid of equal cells, in the same rows as the source. For animations: the
        /// frames are lined up on their base, so they don't move, and the whole animation is placed at the pivot.
        /// </summary>
        Sheet,

        /// <summary>One file for each object. To pack them for performance, add them to a Unity Sprite Atlas.</summary>
        Separate,
    }

    /// <summary>The point each object is pinned to inside its sprite. The sprite pivot is set to the same point.</summary>
    public enum Pivot
    {
        TopLeft,
        Top,
        TopRight,
        Left,
        Center,
        Right,
        BottomLeft,
        Bottom,
        BottomRight,
    }

    [Serializable]
    public sealed class FixSettings
    {
        public ImageMode Mode = ImageMode.Single;
        public OutputType Output = OutputType.Sheet;

        /// <summary>Width and height of each sprite, in pixels.</summary>
        public int Size = 512;

        public Pivot Pivot = Pivot.Center;
    }

    /// <summary>The source image after background removal and alpha cleanup, and the objects found in it.</summary>
    public sealed class CleanImage
    {
        public CleanImage(BackgroundType background, RgbaImage image, IReadOnlyList<ImagePart> parts)
        {
            Background = background;
            Image = image;
            Parts = parts;
        }

        public BackgroundType Background { get; }

        public RgbaImage Image { get; }

        public IReadOnlyList<ImagePart> Parts { get; }
    }

    /// <summary>One sprite inside an output image: its rectangle, and its pivot from 0 to 1 inside that rectangle.</summary>
    public sealed class SpriteArea
    {
        public SpriteArea(PixelRect rect, float pivotX, float pivotY)
        {
            Rect = rect;
            PivotX = pivotX;
            PivotY = pivotY;
        }

        public PixelRect Rect { get; }

        public float PivotX { get; }

        public float PivotY { get; }
    }

    /// <summary>One image file to save, and the sprites in it.</summary>
    public sealed class OutputImage
    {
        public OutputImage(RgbaImage image, IReadOnlyList<SpriteArea> sprites)
        {
            Image = image;
            Sprites = sprites;
        }

        public RgbaImage Image { get; }

        public IReadOnlyList<SpriteArea> Sprites { get; }
    }

    /// <summary>
    /// Fixes a generated image in two steps. <see cref="Clean"/> removes the background, cleans up alpha and finds the
    /// objects; it is the slow step. <see cref="Arrange"/> scales the objects and lays them out as the settings say.
    /// </summary>
    public static class SpriteFixer
    {
        public static IReadOnlyList<OutputImage> Fix(RgbaImage source, FixSettings settings) => Arrange(Clean(source), settings);

        public static CleanImage Clean(RgbaImage source)
        {
            var image = source.Clone();
            var background = BackgroundRemover.Remove(image);
            AlphaCleanup.Apply(image);
            return new CleanImage(background, image, ObjectFinder.Find(image));
        }

        public static IReadOnlyList<OutputImage> Arrange(CleanImage clean, FixSettings settings)
        {
            var parts = clean.Parts.ToList();
            if (parts.Count == 0) return Array.Empty<OutputImage>();
            if (settings.Mode == ImageMode.Single) parts = new List<ImagePart> { Combine(parts) };

            bool sheet = settings.Mode == ImageMode.Multiple && settings.Output == OutputType.Sheet;
            var layout = new SpriteLayout(parts, settings.Size, settings.Pivot, lineUpFrames: sheet);
            if (sheet) return new[] { layout.Sheet() };
            return Enumerable.Range(0, parts.Count).Select(layout.Cell).ToList();
        }

        /// <summary>Puts all objects back into one, for an image that shows a single object.</summary>
        static ImagePart Combine(List<ImagePart> parts)
        {
            if (parts.Count == 1) return parts[0];

            var bounds = parts.Aggregate(PixelRect.Empty, (all, p) => all.Union(p.SourceRect));
            var image = new RgbaImage(bounds.Width, bounds.Height);
            foreach (var part in parts)
            {
                int offsetX = part.SourceRect.X - bounds.X, offsetY = part.SourceRect.Y - bounds.Y;
                for (int y = 0; y < part.Image.Height; y++)
                for (int x = 0; x < part.Image.Width; x++)
                {
                    var p = part.Image[x, y];
                    if (p.A > 0) image[offsetX + x, offsetY + y] = p;
                }
            }

            return new ImagePart(image, bounds, 0);
        }
    }
}
