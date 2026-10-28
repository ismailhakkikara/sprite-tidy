using System;
using System.Collections.Generic;
using System.Linq;

namespace MelloBlack.SpriteTidy.Core
{
    /// <summary>
    /// Scales a set of objects with one shared scale and pins each one at the pivot of its cell.
    /// The scale is the largest at which every object, pinned at the pivot, stays inside its cell on all four sides.
    /// Because all objects share it, no frame of an animation grows or shrinks against the others.
    /// </summary>
    sealed class SpriteLayout
    {
        // Free space inside each cell edge is the cell size divided by this (at least 2 pixels).
        const int MarginDivisor = 64;

        // The base of an object is this share of its height (or width) at the pivot side.
        const double BaseShare = 0.1;

        // Pixels at least this opaque count when finding the base.
        const int SolidAlpha = 128;

        readonly List<ImagePart> parts;
        readonly int size;
        readonly int margin;

        // Where the pivot is: column -1 left, 0 center, 1 right; row -1 bottom, 0 center, 1 top.
        readonly int column;
        readonly int row;

        readonly List<RgbaImage> scaled = new List<RgbaImage>();

        // The pinned point of each scaled object, in its own pixels.
        readonly List<(double x, double y)> pins = new List<(double x, double y)>();

        public SpriteLayout(List<ImagePart> parts, int size, Pivot pivot)
        {
            this.parts = parts;
            this.size = size;
            margin = Math.Max(2, size / MarginDivisor);
            column = (int)pivot % 3 - 1;
            row = 1 - (int)pivot / 3;

            var sourcePins = parts.Select(p => PinPoint(p.Image)).ToList();
            double scale = SharedScale(sourcePins);
            for (int i = 0; i < parts.Count; i++)
            {
                var image = parts[i].Image;
                int width = Math.Max(1, (int)Math.Round(image.Width * scale));
                int height = Math.Max(1, (int)Math.Round(image.Height * scale));
                scaled.Add(Resampler.Resize(image, width, height));
                pins.Add((sourcePins[i].x * width / image.Width, sourcePins[i].y * height / image.Height));
            }
        }

        /// <summary>The object on its own <c>size</c> × <c>size</c> canvas.</summary>
        public OutputImage Cell(int index)
        {
            var canvas = new RgbaImage(size, size);
            Draw(canvas, index, 0, 0);
            return new OutputImage(canvas, new[] { CellSprite(0, 0) });
        }

        /// <summary>All objects in a grid of equal cells, keeping the rows of the source image.</summary>
        public OutputImage Sheet()
        {
            int rows = parts.Max(p => p.Row) + 1;
            var countInRow = new int[rows];
            foreach (var part in parts) countInRow[part.Row]++;

            var sheet = new RgbaImage(countInRow.Max() * size, rows * size);
            var sprites = new List<SpriteArea>();
            var nextColumn = new int[rows];
            for (int i = 0; i < parts.Count; i++)
            {
                int r = parts[i].Row;
                int x = nextColumn[r]++ * size;
                int y = (rows - 1 - r) * size; // row 0 is the top row, but texture y counts from the bottom
                Draw(sheet, i, x, y);
                sprites.Add(CellSprite(x, y));
            }

            return new OutputImage(sheet, sprites);
        }

        void Draw(RgbaImage target, int index, int cellX, int cellY)
        {
            var cellPin = CellPin();
            int x = cellX + (int)Math.Round(cellPin.x - pins[index].x);
            int y = cellY + (int)Math.Round(cellPin.y - pins[index].y);
            target.Blit(scaled[index], x, y);
        }

        SpriteArea CellSprite(int x, int y)
        {
            var cellPin = CellPin();
            return new SpriteArea(new PixelRect(x, y, size, size), (float)(cellPin.x / size), (float)(cellPin.y / size));
        }

        /// <summary>Where the pivot is inside a cell, in pixels.</summary>
        (double x, double y) CellPin()
        {
            double x = column < 0 ? margin : column > 0 ? size - margin : size / 2.0;
            double y = row < 0 ? margin : row > 0 ? size - margin : size / 2.0;
            return (x, y);
        }

        /// <summary>
        /// The point of an object that is pinned at the pivot. Corners and sides use the object's bounds. For Top and
        /// Bottom, the left-right position is the middle of the object's base (its top or bottom rows); Left and Right
        /// work the same way up and down. So a chest with sparkles on one side still stands in the same place.
        /// </summary>
        (double x, double y) PinPoint(RgbaImage image)
        {
            double x = column < 0 ? 0
                : column > 0 ? image.Width
                : row != 0 ? BaseMiddle(image, alongRows: true, atStart: row < 0)
                : image.Width / 2.0;
            double y = row < 0 ? 0
                : row > 0 ? image.Height
                : column != 0 ? BaseMiddle(image, alongRows: false, atStart: column < 0)
                : image.Height / 2.0;
            return (x, y);
        }

        /// <summary>
        /// The middle of the solid pixels in the base of an object: its bottom or top rows when
        /// <paramref name="alongRows"/>, otherwise its left or right columns.
        /// </summary>
        static double BaseMiddle(RgbaImage image, bool alongRows, bool atStart)
        {
            int depth = alongRows ? image.Height : image.Width;
            int across = alongRows ? image.Width : image.Height;
            int band = Math.Max(1, (int)Math.Round(depth * BaseShare));
            int from = atStart ? 0 : depth - band;

            int first = int.MaxValue, last = -1;
            for (int d = from; d < from + band; d++)
            for (int a = 0; a < across; a++)
            {
                var p = alongRows ? image[a, d] : image[d, a];
                if (p.A < SolidAlpha) continue;
                if (a < first) first = a;
                if (a > last) last = a;
            }

            return last < 0 ? across / 2.0 : (first + last + 1) / 2.0;
        }

        /// <summary>The largest scale at which every object, pinned at the pivot, fits inside the cell.</summary>
        double SharedScale(List<(double x, double y)> sourcePins)
        {
            double scale = double.MaxValue;
            for (int i = 0; i < parts.Count; i++)
            {
                var image = parts[i].Image;
                scale = Math.Min(scale, Fit(column, sourcePins[i].x, image.Width - sourcePins[i].x));
                scale = Math.Min(scale, Fit(row, sourcePins[i].y, image.Height - sourcePins[i].y));
            }

            return scale;
        }

        /// <summary>
        /// Along one axis: the largest scale at which an object that reaches <paramref name="before"/> pixels before
        /// its pin and <paramref name="after"/> pixels after it stays inside the cell.
        /// </summary>
        double Fit(int side, double before, double after)
        {
            double whole = size - 2 * margin;
            double half = size / 2.0 - margin;
            if (side < 0) return Room(after, whole);
            if (side > 0) return Room(before, whole);
            return Math.Min(Room(before, half), Room(after, half));
        }

        static double Room(double length, double space) => length <= 0 ? double.MaxValue : space / length;
    }
}
