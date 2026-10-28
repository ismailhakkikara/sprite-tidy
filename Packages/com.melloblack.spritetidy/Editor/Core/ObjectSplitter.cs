using System;
using System.Collections.Generic;
using System.Linq;

namespace MelloBlack.SpriteTidy.Core
{
    public enum SplitMode
    {
        /// <summary>Find every separate object in the image and return each one on its own.</summary>
        Auto,

        /// <summary>Treat everything in the image as a single object.</summary>
        Off,
    }

    [Serializable]
    public sealed class SplitSettings
    {
        public SplitMode Mode = SplitMode.Auto;

        /// <summary>Pixels at least this opaque form the solid "core" of objects. Fainter pixels (glow, soft shadow) are attached to the nearest object afterwards.</summary>
        public int CoreAlpha = 128;

        /// <summary>Pieces smaller than this percentage of the largest piece are fragments (sparkles, detached bits) rather than objects.</summary>
        public float FragmentAreaPercent = 3f;

        /// <summary>A fragment joins the nearest object within this distance, as a percentage of the image's longer side; farther fragments are dropped as noise.</summary>
        public float MergeDistancePercent = 4f;

        /// <summary>Objects whose bounds overlap by at least this percentage of the smaller one are merged.</summary>
        public float OverlapMergePercent = 25f;

        /// <summary>Faint pixels within this distance of an object, as a percentage of the image's longer side, belong to it; farther ones are dropped.</summary>
        public float HaloDistancePercent = 3f;
    }

    public sealed class TidyObject
    {
        public TidyObject(RgbaImage image, PixelRect sourceRect)
        {
            Image = image;
            SourceRect = sourceRect;
        }

        /// <summary>The object cropped to its bounds, with everything that belongs to other objects removed.</summary>
        public RgbaImage Image { get; }

        /// <summary>Where the object was in the source image.</summary>
        public PixelRect SourceRect { get; }
    }

    /// <summary>Splits an image with a transparent background into its separate objects, in reading order.</summary>
    public static class ObjectSplitter
    {
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

        public static List<TidyObject> Split(RgbaImage image, SplitSettings settings)
        {
            if (settings.Mode == SplitMode.Off) return WholeImage(image);

            int w = image.Width, h = image.Height, n = w * h;
            var pixels = image.Pixels;
            var queue = new int[n];

            var pieceOf = LabelCores(pixels, w, h, settings.CoreAlpha, queue, out var pieces);
            if (pieces.Count == 0) return WholeImage(image);

            var groupOf = GroupPieces(pieces, settings, Math.Max(w, h));

            // Number the surviving groups and seed ownership from their core pixels.
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

            // Grow each object into the faint pixels around it, up to the halo distance.
            int haloSteps = (int)Math.Ceiling(settings.HaloDistancePercent / 100.0 * Math.Max(w, h));
            for (int head = 0; head < tail; head++)
            {
                int i = queue[head];
                if (distance[i] >= haloSteps) continue;
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

            return ExtractObjects(image, owner, objectOfGroup.Count);
        }

        static List<TidyObject> WholeImage(RgbaImage image)
        {
            var bounds = image.FindBounds();
            var result = new List<TidyObject>();
            if (!bounds.IsEmpty) result.Add(new TidyObject(image.Crop(bounds), bounds));
            return result;
        }

        /// <summary>8-connected labeling of the pixels at least <paramref name="coreAlpha"/> opaque.</summary>
        static int[] LabelCores(Rgba32[] pixels, int w, int h, int coreAlpha, int[] queue, out List<Piece> pieces)
        {
            int n = w * h;
            var label = new int[n];
            for (int i = 0; i < n; i++) label[i] = -1;
            pieces = new List<Piece>();

            for (int start = 0; start < n; start++)
            {
                if (label[start] >= 0 || pixels[start].A < coreAlpha) continue;

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
                            if (label[j] >= 0 || pixels[j].A < coreAlpha) continue;
                            label[j] = id;
                            queue[tail++] = j;
                        }
                    }
                }
            }

            return label;
        }

        /// <summary>
        /// Decides which pieces belong together. Returns, for each piece, the id of its group (a representative piece),
        /// or -1 when the piece is dropped as noise.
        /// </summary>
        static int[] GroupPieces(List<Piece> pieces, SplitSettings settings, int longerSide)
        {
            int count = pieces.Count;
            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;

            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }

            int largest = pieces.Max(p => p.Area);
            double fragmentArea = largest * settings.FragmentAreaPercent / 100.0;
            var majors = Enumerable.Range(0, count).Where(i => pieces[i].Area >= fragmentArea).ToList();

            // Merge objects whose bounds overlap a lot: parts of one drawing that happen not to touch.
            double overlapShare = settings.OverlapMergePercent / 100.0;
            bool merged = true;
            while (merged)
            {
                merged = false;
                var bounds = GroupBounds(pieces, majors, Find);
                var roots = bounds.Keys.ToList();
                for (int a = 0; a < roots.Count && !merged; a++)
                for (int b = a + 1; b < roots.Count && !merged; b++)
                {
                    var ra = bounds[roots[a]];
                    var rb = bounds[roots[b]];
                    if (ra.IntersectionArea(rb) < overlapShare * Math.Min(ra.Area, rb.Area)) continue;
                    parent[Find(roots[b])] = Find(roots[a]);
                    merged = true;
                }
            }

            // Attach each fragment to the nearest object, or drop it if nothing is close.
            var dropped = new bool[count];
            var groupBounds = GroupBounds(pieces, majors, Find);
            double mergeDistance = settings.MergeDistancePercent / 100.0 * longerSide;
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

                if (nearest >= 0 && nearestGap <= mergeDistance) parent[i] = nearest;
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

        static List<TidyObject> ExtractObjects(RgbaImage image, int[] owner, int objectCount)
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

            var objects = new List<TidyObject>(objectCount);
            for (int o = 0; o < objectCount; o++)
            {
                if (extents[o].Area == 0) continue;
                var rect = extents[o].Bounds;
                var cropped = new RgbaImage(rect.Width, rect.Height);
                for (int y = 0; y < rect.Height; y++)
                for (int x = 0; x < rect.Width; x++)
                {
                    int i = (rect.Y + y) * w + rect.X + x;
                    if (owner[i] == o) cropped.Pixels[y * rect.Width + x] = image.Pixels[i];
                }

                objects.Add(new TidyObject(cropped, rect));
            }

            return SortInReadingOrder(objects);
        }

        /// <summary>Rows from top to bottom, left to right within a row.</summary>
        static List<TidyObject> SortInReadingOrder(List<TidyObject> objects)
        {
            var byTop = objects.OrderByDescending(o => o.SourceRect.Y + o.SourceRect.Height / 2.0).ToList();
            var rows = new List<List<TidyObject>>();
            double rowCenter = 0, rowHalfHeight = 0;
            foreach (var o in byTop)
            {
                double center = o.SourceRect.Y + o.SourceRect.Height / 2.0;
                if (rows.Count == 0 || rowCenter - center > rowHalfHeight)
                {
                    rows.Add(new List<TidyObject>());
                    rowCenter = center;
                    rowHalfHeight = o.SourceRect.Height / 2.0;
                }

                rows[rows.Count - 1].Add(o);
            }

            return rows.SelectMany(row => row.OrderBy(o => o.SourceRect.X)).ToList();
        }
    }
}
