using System;
using System.Collections.Generic;
using System.Linq;

namespace IsmailHakkiKara.SpriteTidy.Core
{
    /// <summary>
    /// Scales a set of objects with one shared scale, so no object grows or shrinks against the others, and places
    /// each one in a cell. Objects are either pinned one by one at the pivot, or, for the frames of an animation,
    /// lined up on their base first and then placed as one block, so no frame moves against the others.
    /// </summary>
    sealed class SpriteLayout
    {
        // The base of an object is this share of its height (or width) at the pivot side. For the frames of a sheet
        // it is this share of the shortest frame, so the base is equally thick in every frame. It must be thick enough
        // to reach past feet and corners to the body: a slanted chest's lowest rows show only one of its feet.
        const double BaseShare = 0.15;

        // Pixels at least this opaque count when finding the base.
        const int SolidAlpha = 128;

        readonly List<ImagePart> parts;
        readonly int size;

        // Empty pixels kept free inside each cell edge.
        readonly int margin;

        // Where the pivot is: column -1 left, 0 center, 1 right; row -1 bottom, 0 center, 1 top.
        readonly int column;
        readonly int row;

        readonly List<RgbaImage> scaled = new List<RgbaImage>();

        // The pinned point of each scaled object, in its own pixels.
        readonly List<(double x, double y)> pins = new List<(double x, double y)>();

        // Where the pinned points go inside a cell, and the sprite pivot inside a cell, in pixels.
        readonly (double x, double y) cellPin;
        readonly (double x, double y) spritePivot;

        /// <param name="lineUp">
        /// For the frames of an animation: the part of every frame that is kept in the same place. The frames are
        /// lined up on it, and the whole animation is placed in the cell as the pivot says. Null to place every
        /// object at the pivot on its own.
        /// </param>
        public SpriteLayout(List<ImagePart> parts, int size, int margin, Pivot pivot, LineUp? lineUp)
        {
            this.parts = parts;
            this.size = size;
            this.margin = margin;
            column = (int)pivot % 3 - 1;
            row = 1 - (int)pivot / 3;

            List<(double x, double y)> sourcePins;
            double scale;
            if (lineUp.HasValue)
            {
                sourcePins = LineUpPoints(lineUp.Value);

                // Seen from the line-up point, the frames together reach this far in each direction.
                // Scale and place that block as a whole.
                double left = sourcePins.Max(p => p.x);
                double right = parts.Select((p, i) => p.Image.Width - sourcePins[i].x).Max();
                double below = sourcePins.Max(p => p.y);
                double above = parts.Select((p, i) => p.Image.Height - sourcePins[i].y).Max();
                double room = size - 2 * margin;
                scale = Math.Min(room / (left + right), room / (below + above));

                double blockWidth = (left + right) * scale, blockHeight = (below + above) * scale;
                double blockX = column < 0 ? margin : column > 0 ? size - margin - blockWidth : (size - blockWidth) / 2;
                double blockY = row < 0 ? margin : row > 0 ? size - margin - blockHeight : (size - blockHeight) / 2;
                cellPin = (blockX + left * scale, blockY + below * scale);
                spritePivot = (blockX + (column + 1) / 2.0 * blockWidth, blockY + (row + 1) / 2.0 * blockHeight);
            }
            else
            {
                sourcePins = parts.Select(p => PinPoint(p.Image)).ToList();
                scale = SharedScale(sourcePins);
                cellPin = PivotInCell();
                spritePivot = cellPin;
            }

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
            var image = scaled[index];
            int x = cellX + (int)Math.Round(cellPin.x - pins[index].x);
            int y = cellY + (int)Math.Round(cellPin.y - pins[index].y);

            // Rounding can push an object a pixel past its cell when the margin is 0; keep it inside.
            x = Math.Max(cellX, Math.Min(x, cellX + size - image.Width));
            y = Math.Max(cellY, Math.Min(y, cellY + size - image.Height));
            target.Blit(image, x, y);
        }

        SpriteArea CellSprite(int x, int y)
        {
            return new SpriteArea(new PixelRect(x, y, size, size), (float)(spritePivot.x / size), (float)(spritePivot.y / size));
        }

        /// <summary>Where the pivot is inside a cell, in pixels.</summary>
        (double x, double y) PivotInCell()
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
            int rows = BandFor(image.Height), columns = BandFor(image.Width);
            double x = column < 0 ? 0
                : column > 0 ? image.Width
                : row != 0 ? BaseMiddle(image, alongRows: true, atStart: row < 0, rows)
                : image.Width / 2.0;
            double y = row < 0 ? 0
                : row > 0 ? image.Height
                : column != 0 ? BaseMiddle(image, alongRows: false, atStart: column < 0, columns)
                : image.Height / 2.0;
            return (x, y);
        }

        /// <summary>
        /// The point of every frame that is kept in the same place. Bottom and Top use the middle of the frame's
        /// bottom or top rows, with the same number of rows for every frame. Center uses the middle of its bounds.
        /// </summary>
        List<(double x, double y)> LineUpPoints(LineUp lineUp)
        {
            int rows = BandFor(parts.Min(p => p.Image.Height));
            return parts.Select(p =>
            {
                var image = p.Image;
                switch (lineUp)
                {
                    case LineUp.Bottom: return (BaseMiddle(image, alongRows: true, atStart: true, rows), 0.0);
                    case LineUp.Top: return (BaseMiddle(image, alongRows: true, atStart: false, rows), (double)image.Height);
                    default: return (image.Width / 2.0, image.Height / 2.0);
                }
            }).ToList();
        }

        static int BandFor(int length) => Math.Max(1, (int)Math.Round(length * BaseShare));

        /// <summary>
        /// The middle of the solid pixels in the base of an object, <paramref name="band"/> pixels thick: its bottom
        /// or top rows when <paramref name="alongRows"/>, otherwise its left or right columns.
        /// </summary>
        static double BaseMiddle(RgbaImage image, bool alongRows, bool atStart, int band)
        {
            int depth = alongRows ? image.Height : image.Width;
            int across = alongRows ? image.Width : image.Height;
            band = Math.Min(band, depth);
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
