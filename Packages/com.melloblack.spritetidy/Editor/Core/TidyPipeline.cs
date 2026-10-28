using System;
using System.Collections.Generic;
using System.Linq;

namespace MelloBlack.SpriteTidy.Core
{
    [Serializable]
    public sealed class TidySettings
    {
        public BackgroundSettings Background = new BackgroundSettings();
        public AlphaCleanupSettings Alpha = new AlphaCleanupSettings();
        public SplitSettings Split = new SplitSettings();
        public CanvasSettings Canvas = new CanvasSettings();
    }

    public sealed class TidyResult
    {
        public TidyResult(BackgroundModel background, RgbaImage cleaned, IReadOnlyList<TidySprite> sprites)
        {
            Background = background;
            Cleaned = cleaned;
            Sprites = sprites;
        }

        /// <summary>The background that was detected, or null when background removal was off.</summary>
        public BackgroundModel Background { get; }

        /// <summary>The whole source image after background removal and alpha cleanup, before splitting.</summary>
        public RgbaImage Cleaned { get; }

        /// <summary>The finished sprites in reading order.</summary>
        public IReadOnlyList<TidySprite> Sprites { get; }
    }

    /// <summary>Runs the full clean-up: background removal, alpha cleanup, object splitting and canvas fitting.</summary>
    public static class TidyPipeline
    {
        public static TidyResult Process(RgbaImage source, TidySettings settings)
        {
            var image = source.Clone();

            BackgroundModel background = null;
            if (settings.Background.Enabled) background = BackgroundRemover.Apply(image, settings.Background);
            if (settings.Alpha.Enabled) AlphaCleanup.Apply(image, settings.Alpha);

            var objects = ObjectSplitter.Split(image, settings.Split);
            var canvas = settings.Canvas;

            double sharedScale = 0;
            if (canvas.FitMode == FitMode.KeepRelativeSize && objects.Count > 0)
                sharedScale = objects.Min(o => CanvasFitter.FitScale(o.Image.Width, o.Image.Height, canvas));

            var sprites = objects
                .Select(o => CanvasFitter.Fit(o, sharedScale > 0 ? sharedScale : CanvasFitter.FitScale(o.Image.Width, o.Image.Height, canvas), canvas))
                .ToList();

            return new TidyResult(background, image, sprites);
        }
    }
}
