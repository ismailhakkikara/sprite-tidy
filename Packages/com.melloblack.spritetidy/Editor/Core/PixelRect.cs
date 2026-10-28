using System;

namespace MelloBlack.SpriteTidy.Core
{
    /// <summary>An integer pixel rectangle. Y grows upward, matching Unity textures.</summary>
    [Serializable]
    public struct PixelRect : IEquatable<PixelRect>
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public PixelRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public static PixelRect Empty => default;

        /// <summary>Exclusive right edge.</summary>
        public int XMax => X + Width;

        /// <summary>Exclusive top edge.</summary>
        public int YMax => Y + Height;

        public int Area => IsEmpty ? 0 : Width * Height;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public static PixelRect FromMinMax(int xMin, int yMin, int xMaxExclusive, int yMaxExclusive)
        {
            return new PixelRect(xMin, yMin, xMaxExclusive - xMin, yMaxExclusive - yMin);
        }

        public PixelRect Union(PixelRect other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;
            return FromMinMax(Math.Min(X, other.X), Math.Min(Y, other.Y), Math.Max(XMax, other.XMax), Math.Max(YMax, other.YMax));
        }

        public int IntersectionArea(PixelRect other)
        {
            int w = Math.Min(XMax, other.XMax) - Math.Max(X, other.X);
            int h = Math.Min(YMax, other.YMax) - Math.Max(Y, other.Y);
            return w > 0 && h > 0 ? w * h : 0;
        }

        /// <summary>Distance between the closest edges of the two rectangles; 0 when they touch or overlap.</summary>
        public double GapTo(PixelRect other)
        {
            int dx = Math.Max(0, Math.Max(other.X - XMax, X - other.XMax));
            int dy = Math.Max(0, Math.Max(other.Y - YMax, Y - other.YMax));
            return Math.Sqrt((double)dx * dx + (double)dy * dy);
        }

        public bool Equals(PixelRect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is PixelRect other && Equals(other);

        public override int GetHashCode() => ((X * 397 ^ Y) * 397 ^ Width) * 397 ^ Height;

        public override string ToString() => $"({X}, {Y}, {Width}x{Height})";
    }
}
