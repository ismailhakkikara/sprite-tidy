using System;

namespace IsmailHakkiKara.SpriteTidy.Core
{
    /// <summary>A pixel with 8-bit straight (non-premultiplied) RGBA channels, laid out like UnityEngine.Color32.</summary>
    [Serializable]
    public struct Rgba32 : IEquatable<Rgba32>
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Rgba32(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Rgba32 Transparent => default;

        /// <summary>Squared Euclidean distance between the RGB channels, ignoring alpha.</summary>
        public static int DistanceSquared(Rgba32 a, Rgba32 b)
        {
            int dr = a.R - b.R;
            int dg = a.G - b.G;
            int db = a.B - b.B;
            return dr * dr + dg * dg + db * db;
        }

        public bool Equals(Rgba32 other) => R == other.R && G == other.G && B == other.B && A == other.A;

        public override bool Equals(object obj) => obj is Rgba32 other && Equals(other);

        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;

        public override string ToString() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";

        public static bool operator ==(Rgba32 a, Rgba32 b) => a.Equals(b);

        public static bool operator !=(Rgba32 a, Rgba32 b) => !a.Equals(b);
    }
}
