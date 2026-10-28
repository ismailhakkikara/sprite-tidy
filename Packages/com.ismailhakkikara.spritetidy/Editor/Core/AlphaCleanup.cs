namespace IsmailHakkiKara.SpriteTidy.Core
{
    /// <summary>Makes nearly opaque pixels fully opaque and removes nearly invisible haze.</summary>
    public static class AlphaCleanup
    {
        // Generated "transparent" images keep most of their solid pixels at alpha 250–253,
        // which makes sprites a little see-through. Alpha from this value up becomes 255.
        const int OpaqueFrom = 240;

        // Alpha below this value is haze you can hardly see; it becomes 0.
        const int ClearBelow = 8;

        public static void Apply(RgbaImage image)
        {
            var pixels = image.Pixels;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte a = pixels[i].A;
                if (a >= OpaqueFrom) pixels[i].A = 255;
                else if (a < ClearBelow) pixels[i] = Rgba32.Transparent;
            }
        }
    }
}
