using System;
using System.Collections.Generic;
using System.Linq;

namespace IsmailHakkiKara.SpriteTidy.Core
{
    /// <summary>One object found in an image.</summary>
    public sealed class ImagePart
    {
        public ImagePart(RgbaImage image, PixelRect sourceRect, int row)
        {
            Image = image;
            SourceRect = sourceRect;
            Row = row;
        }

        /// <summary>The object, cut to its bounds. Pixels of other objects are removed.</summary>
        public RgbaImage Image { get; }

        /// <summary>Where the object was in the source image.</summary>
        public PixelRect SourceRect { get; }

        /// <summary>The row the object was in, counting from the top of the source image (0 = top row).</summary>
        public int Row { get; }
    }

    /// <summary>Finds the separate objects in an image with a transparent background, in reading order.</summary>
    public static class ObjectFinder
    {
        // Pixels at least this opaque form the solid body of objects. Fainter pixels (glow, soft shadow)
        // are given to the nearest object afterwards.
        const int SolidAlpha = 128;

        // A piece smaller than this share of the largest piece is a fragment (a sparkle, a loose bit), not an object.
        const double FragmentShare = 0.03;

        // A fragment joins the nearest object if it is this close (share of the image's longer side).
        // Fragments farther away are dropped as noise.
        const double FragmentReach = 0.04;

        // Objects whose bounds overlap by at least this share of the smaller one are joined into one.
        const double OverlapShare = 0.25;

        // Faint pixels this close to an object (share of the image's longer side) belong to it; farther ones are dropped.
        const double GlowReach = 0.03;

        sealed class Piece
        {
            public int Area;
            public int MinX = int.MaxValue, MinY = int.MaxValue, MaxX = -1, MaxY = -1;

            public PixelRect Bounds => PixelRect.FromMinMax(MinX, MinY, MaxX + 1, MaxY + 1);

            public void Add(int x, int y)
            {
                Area++;
                if (x < MinX) MinX = x;
                if (x > MaxX) MaxX = x;
                if (y < MinY) MinY = y;
                if (y > MaxY) MaxY = y;
            }
        }

        public static List<ImagePart> Find(RgbaImage image)
        {
            int w = image.Width, h = image.Height, n = w * h;
            var pixels = image.Pixels;
            var queue = new int[n];

            var pieceOf = LabelSolidPixels(pixels, w, h, queue, out var pieces);
            if (pieces.Count == 0) return WholeImage(image);

            var groupOf = GroupPieces(pieces, Math.Max(w, h));

            // Number the groups that are kept and start each object from its solid pixels.
            var objectOfGroup = new Dictionary<int, int>();
            foreach (int g in groupOf)
                if (g >= 0 && !objectOfGroup.ContainsKey(g)) objectOfGroup[g] = objectOfGroup.Count;

            var owner = new int[n];
            var distance = new int[n];
            int tail = 0;
            for (int i = 0; i < n; i++)
            {
                owner[i] = -1;
                int piece = pieceOf[i];
                if (piece < 0 || groupOf[piece] < 0) continue;
                owner[i] = objectOfGroup[groupOf[piece]];
                queue[tail++] = i;
            }

            // Grow each object into the faint pixels around it.
            int glowSteps = (int)Math.Ceiling(GlowReach * Math.Max(w, h));
            for (int head = 0; head < tail; head++)
            {
                int i = queue[head];
                if (distance[i] >= glowSteps) continue;
                int x = i % w;
                if (x > 0) Grow(i, i - 1);
                if (x < w - 1) Grow(i, i + 1);
                if (i >= w) Grow(i, i - w);
                if (i < (h - 1) * w) Grow(i, i + w);
            }

            void Grow(int from, int to)
            {
                if (owner[to] >= 0 || pixels[to].A == 0) return;
                owner[to] = owner[from];
                distance[to] = distance[from] + 1;
                queue[tail++] = to;
            }

            return CutOut(image, owner, objectOfGroup.Count);
        }

        /// <summary>Used when nothing is solid enough to count as an object: everything visible is one object.</summary>
        static List<ImagePart> WholeImage(RgbaImage image)
        {
            var bounds = image.FindBounds();
            var result = new List<ImagePart>();
            if (!bounds.IsEmpty) result.Add(new ImagePart(image.Crop(bounds), bounds, 0));
            return result;
        }

        /// <summary>Gives each connected area of solid pixels (8 neighbors) its own number.</summary>
        static int[] LabelSolidPixels(Rgba32[] pixels, int w, int h, int[] queue, out List<Piece> pieces)
        {
            int n = w * h;
            var label = new int[n];
            for (int i = 0; i < n; i++) label[i] = -1;
            pieces = new List<Piece>();

            for (int start = 0; start < n; start++)
            {
                if (label[start] >= 0 || pixels[start].A < SolidAlpha) continue;

                var piece = new Piece();
                int id = pieces.Count;
                pieces.Add(piece);
                int tail = 0;
                label[start] = id;
                queue[tail++] = start;
                for (int head = 0; head < tail; head++)
                {
                    int i = queue[head];
                    int x = i % w, y = i / w;
                    piece.Add(x, y);
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;
                            int j = ny * w + nx;
                            if (label[j] >= 0 || pixels[j].A < SolidAlpha) continue;
                            label[j] = id;
                            queue[tail++] = j;
                        }
                    }
                }
            }

            return label;
        }

        /// <summary>
        /// Decides which pieces belong together. Returns, for each piece, the id of its group, or -1 when the piece is
        /// dropped as noise.
        /// </summary>
        static int[] GroupPieces(List<Piece> pieces, int longerSide)
        {
            int count = pieces.Count;
            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;

            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }

            double fragmentArea = pieces.Max(p => p.Area) * FragmentShare;
            var objects = Enumerable.Range(0, count).Where(i => pieces[i].Area >= fragmentArea).ToList();

            // Join objects whose bounds overlap a lot: parts of one drawing that happen not to touch.
            bool joined = true;
            while (joined)
            {
                joined = false;
                var bounds = GroupBounds(pieces, objects, Find);
                var roots = bounds.Keys.ToList();
                for (int a = 0; a < roots.Count && !joined; a++)
                for (int b = a + 1; b < roots.Count && !joined; b++)
                {
                    var ra = bounds[roots[a]];
                    var rb = bounds[roots[b]];
                    if (ra.IntersectionArea(rb) < OverlapShare * Math.Min(ra.Area, rb.Area)) continue;
                    parent[Find(roots[b])] = Find(roots[a]);
                    joined = true;
                }
            }

            // Give each fragment to the nearest object, or drop it if nothing is close.
            var dropped = new bool[count];
            var groupBounds = GroupBounds(pieces, objects, Find);
            double reach = FragmentReach * longerSide;
            for (int i = 0; i < count; i++)
            {
                if (pieces[i].Area >= fragmentArea) continue;

                int nearest = -1;
                double nearestGap = double.MaxValue;
                foreach (var entry in groupBounds)
                {
                    double gap = pieces[i].Bounds.GapTo(entry.Value);
                    if (gap >= nearestGap) continue;
                    nearestGap = gap;
                    nearest = entry.Key;
                }

                if (nearest >= 0 && nearestGap <= reach) parent[i] = nearest;
                else dropped[i] = true;
            }

            var group = new int[count];
            for (int i = 0; i < count; i++) group[i] = dropped[i] ? -1 : Find(i);
            return group;
        }

        static Dictionary<int, PixelRect> GroupBounds(List<Piece> pieces, List<int> members, Func<int, int> find)
        {
            var bounds = new Dictionary<int, PixelRect>();
            foreach (int i in members)
            {
                int root = find(i);
                bounds[root] = bounds.TryGetValue(root, out var existing) ? existing.Union(pieces[i].Bounds) : pieces[i].Bounds;
            }

            return bounds;
        }

        static List<ImagePart> CutOut(RgbaImage image, int[] owner, int objectCount)
        {
            int w = image.Width, h = image.Height;
            var extents = new Piece[objectCount];
            for (int o = 0; o < objectCount; o++) extents[o] = new Piece();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int o = owner[y * w + x];
                if (o >= 0) extents[o].Add(x, y);
            }

            var cut = new List<(RgbaImage image, PixelRect rect)>(objectCount);
            for (int o = 0; o < objectCount; o++)
            {
                if (extents[o].Area == 0) continue;
                var rect = extents[o].Bounds;
                var part = new RgbaImage(rect.Width, rect.Height);
                for (int y = 0; y < rect.Height; y++)
                for (int x = 0; x < rect.Width; x++)
                {
                    int i = (rect.Y + y) * w + rect.X + x;
                    if (owner[i] == o) part.Pixels[y * rect.Width + x] = image.Pixels[i];
                }

                cut.Add((part, rect));
            }

            return InReadingOrder(cut);
        }

        /// <summary>Puts the objects in rows from top to bottom, and left to right within a row.</summary>
        static List<ImagePart> InReadingOrder(List<(RgbaImage image, PixelRect rect)> objects)
        {
            var topFirst = objects.OrderByDescending(o => o.rect.Y + o.rect.Height / 2.0).ToList();
            var rows = new List<List<(RgbaImage image, PixelRect rect)>>();
            double rowCenter = 0, rowHalfHeight = 0;
            foreach (var o in topFirst)
            {
                double center = o.rect.Y + o.rect.Height / 2.0;
                if (rows.Count == 0 || rowCenter - center > rowHalfHeight)
                {
                    rows.Add(new List<(RgbaImage image, PixelRect rect)>());
                    rowCenter = center;
                    rowHalfHeight = o.rect.Height / 2.0;
                }

                rows[rows.Count - 1].Add(o);
            }

            var result = new List<ImagePart>(objects.Count);
            for (int r = 0; r < rows.Count; r++)
                foreach (var o in rows[r].OrderBy(o => o.rect.X))
                    result.Add(new ImagePart(o.image, o.rect, r));
            return result;
        }
    }
}
