using System;

namespace MelloBlack.SpriteTidy.Core
{
    public enum CanvasMode
    {
        /// <summary>Scale every sprite to fit a canvas of fixed size.</summary>
        Fixed,

        /// <summary>Keep the original pixel size; the canvas is the trimmed content plus padding.</summary>
        TrimOnly,
    }

    public enum FitMode
    {
        /// <summary>Each sprite is scaled to fill the canvas.</summary>
        FitEach,

        /// <summary>All sprites from one image share the same scale, so their relative sizes are kept.</summary>
        KeepRelativeSize,
    }

    public enum HorizontalAnchor
    {
        Left,
        Center,
        Right,
    }

    public enum VerticalAnchor
    {
        Bottom,
        Center,
        Top,
    }

    [Serializable]
    public sealed class CanvasSettings
    {
        public CanvasMode Mode = CanvasMode.Fixed;
        public int Width = 512;
        public int Height = 512;

        /// <summary>Empty space kept between the content and each edge of the canvas, in output pixels.</summary>
        public int Padding = 16;

        public HorizontalAnchor HorizontalAnchor = HorizontalAnchor.Center;
        public VerticalAnchor VerticalAnchor = VerticalAnchor.Center;
        public FitMode FitMode = FitMode.FitEach;

        /// <summary>Allow content smaller than the canvas to be scaled up.</summary>
        public bool AllowUpscale = true;

        public ResampleFilter Filter = ResampleFilter.Bicubic;
    }

    public sealed class TidySprite
    {
        public TidySprite(RgbaImage image, PixelRect sourceRect, float pivotX, float pivotY)
        {
            Image = image;
            SourceRect = sourceRect;
            PivotX = pivotX;
            PivotY = pivotY;
        }

        public RgbaImage Image { get; }

        /// <summary>Where the sprite's content was in the source image.</summary>
        public PixelRect SourceRect { get; }

        /// <summary>The anchor point in normalized canvas coordinates (0..1), suitable as the sprite pivot.</summary>
        public float PivotX { get; }

        public float PivotY { get; }
    }

    /// <summary>Places trimmed content on a canvas: scales it to fit inside the padding and aligns it to the anchor.</summary>
    public static class CanvasFitter
    {
        /// <summary>Scale that makes content of the given size fit inside the padded canvas. Always 1 in <see cref="CanvasMode.TrimOnly"/>.</summary>
        public static double FitScale(int contentWidth, int contentHeight, CanvasSettings settings)
        {
            if (settings.Mode == CanvasMode.TrimOnly) return 1.0;

            int availableWidth = Math.Max(1, settings.Width - 2 * settings.Padding);
            int availableHeight = Math.Max(1, settings.Height - 2 * settings.Padding);
            double scale = Math.Min((double)availableWidth / contentWidth, (double)availableHeight / contentHeight);
            return settings.AllowUpscale ? scale : Math.Min(scale, 1.0);
        }

        public static TidySprite Fit(TidyObject content, double scale, CanvasSettings settings)
        {
            var image = content.Image;
            int canvasWidth, canvasHeight, width, height;
            if (settings.Mode == CanvasMode.TrimOnly)
            {
                width = image.Width;
                height = image.Height;
                canvasWidth = width + 2 * settings.Padding;
                canvasHeight = height + 2 * settings.Padding;
            }
            else
            {
                canvasWidth = settings.Width;
                canvasHeight = settings.Height;
                int availableWidth = Math.Max(1, canvasWidth - 2 * settings.Padding);
                int availableHeight = Math.Max(1, canvasHeight - 2 * settings.Padding);
                width = Clamp((int)Math.Round(image.Width * scale), 1, availableWidth);
                height = Clamp((int)Math.Round(image.Height * scale), 1, availableHeight);
            }

            var scaled = width == image.Width && height == image.Height
                ? image
                : Resampler.Resize(image, width, height, settings.Filter);

            int x = Offset((int)settings.HorizontalAnchor, canvasWidth, width, settings.Padding);
            int y = Offset((int)settings.VerticalAnchor, canvasHeight, height, settings.Padding);

            var canvas = new RgbaImage(canvasWidth, canvasHeight);
            canvas.Blit(scaled, x, y);

            float pivotX = Pivot((int)settings.HorizontalAnchor, x, width, canvasWidth);
            float pivotY = Pivot((int)settings.VerticalAnchor, y, height, canvasHeight);
            return new TidySprite(canvas, content.SourceRect, pivotX, pivotY);
        }

        // Anchors are 0 = start (left/bottom), 1 = center, 2 = end (right/top) on both axes.
        static int Offset(int anchor, int canvasSize, int contentSize, int padding)
        {
            switch (anchor)
            {
                case 0: return padding;
                case 2: return canvasSize - padding - contentSize;
                default: return (canvasSize - contentSize) / 2;
            }
        }

        static float Pivot(int anchor, int offset, int contentSize, int canvasSize)
        {
            switch (anchor)
            {
                case 0: return (float)offset / canvasSize;
                case 2: return (float)(offset + contentSize) / canvasSize;
                default: return 0.5f;
            }
        }

        static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
