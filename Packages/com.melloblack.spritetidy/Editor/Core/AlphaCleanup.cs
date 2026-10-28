using System;

namespace MelloBlack.SpriteTidy.Core
{
    [Serializable]
    public sealed class AlphaCleanupSettings
    {
        public bool Enabled = true;

        /// <summary>
        /// Alpha at or above this becomes fully opaque. Image generators often leave the "opaque" parts
        /// of a transparent image at 250–253, which makes sprites faintly see-through.
        /// </summary>
        public int OpaqueThreshold = 240;

        /// <summary>Alpha below this becomes fully transparent, removing faint haze around objects. 0 disables it.</summary>
        public int ClearThreshold = 8;
    }

    public static class AlphaCleanup
    {
        public static void Apply(RgbaImage image, AlphaCleanupSettings settings)
        {
            var pixels = image.Pixels;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte a = pixels[i].A;
                if (a >= settings.OpaqueThreshold) pixels[i].A = 255;
                else if (a < settings.ClearThreshold) pixels[i] = Rgba32.Transparent;
            }
        }
    }
}
