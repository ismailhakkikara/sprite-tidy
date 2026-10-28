using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MelloBlack.SpriteTidy;
using MelloBlack.SpriteTidy.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Development helper, not part of the package: fixes every image in a folder outside the project as a Multiple
/// image and writes, for each one, the sheet over a checkerboard, an "onion skin" of all cells on top of each other,
/// and an alpha map. In the onion skin, frames that drift show up as doubled edges. In the alpha map every visible
/// pixel is white, so background left behind shows up even where it would hide on a gray checkerboard.
///
/// Unity -batchmode -quit -projectPath . -executeMethod SampleBatch.Run -sampleIn &lt;folder&gt; -sampleOut &lt;folder&gt; [-pivot Bottom]
/// </summary>
public static class SampleBatch
{
    public static void Run()
    {
        string input = Argument("-sampleIn");
        string output = Argument("-sampleOut");
        var pivot = (Pivot)Enum.Parse(typeof(Pivot), Argument("-pivot", "Bottom"));
        Directory.CreateDirectory(output);

        var settings = new FixSettings { Mode = ImageMode.Multiple, Output = OutputType.Sheet, Size = 256, Pivot = pivot };
        var summary = new StringBuilder();
        foreach (string file in Directory.GetFiles(input, "*.png").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            var stopwatch = Stopwatch.StartNew();
            var clean = SpriteFixer.Clean(SpriteTidyIO.Load(file));
            var sheet = SpriteFixer.Arrange(clean, settings)[0];
            stopwatch.Stop();

            SpriteTidyIO.SavePng(OverChecker(sheet.Image), Path.Combine(output, name + "__sheet.png"));
            SpriteTidyIO.SavePng(OverChecker(Onion(sheet, settings.Size)), Path.Combine(output, name + "__onion.png"));
            SpriteTidyIO.SavePng(AlphaMap(sheet.Image), Path.Combine(output, name + "__alpha.png"));

            string line = $"{name}: background {clean.Background}, {clean.Parts.Count} objects, {stopwatch.ElapsedMilliseconds} ms";
            summary.AppendLine(line);
            Debug.Log("[SampleBatch] " + line);
        }

        File.WriteAllText(Path.Combine(output, "summary.txt"), summary.ToString());
    }

    static string Argument(string name, string fallback = null)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        if (index >= 0 && index + 1 < args.Length) return args[index + 1];
        return fallback ?? throw new ArgumentException($"Missing {name} argument.");
    }

    /// <summary>All cells of a sheet blended on top of each other.</summary>
    static RgbaImage Onion(OutputImage sheet, int size)
    {
        var sums = new float[size * size * 4];
        foreach (var sprite in sheet.Sprites)
        {
            var cell = sheet.Image.Crop(sprite.Rect);
            for (int i = 0; i < cell.Pixels.Length; i++)
            {
                var p = cell.Pixels[i];
                float a = p.A / 255f;
                sums[i * 4] += p.R * a;
                sums[i * 4 + 1] += p.G * a;
                sums[i * 4 + 2] += p.B * a;
                sums[i * 4 + 3] += a;
            }
        }

        var onion = new RgbaImage(size, size);
        int n = sheet.Sprites.Count;
        for (int i = 0; i < onion.Pixels.Length; i++)
        {
            float a = sums[i * 4 + 3];
            if (a <= 0) continue;
            onion.Pixels[i] = new Rgba32((byte)(sums[i * 4] / a), (byte)(sums[i * 4 + 1] / a), (byte)(sums[i * 4 + 2] / a), (byte)Math.Min(255f, a / n * 255f));
        }

        return onion;
    }

    /// <summary>Alpha as gray: white where the image is opaque, black where it is transparent.</summary>
    static RgbaImage AlphaMap(RgbaImage image)
    {
        var map = new RgbaImage(image.Width, image.Height);
        for (int i = 0; i < image.Pixels.Length; i++)
        {
            byte a = image.Pixels[i].A;
            map.Pixels[i] = new Rgba32(a, a, a);
        }

        return map;
    }

    /// <summary>The image over a gray checkerboard, with a magenta line on each cell border.</summary>
    static RgbaImage OverChecker(RgbaImage image)
    {
        var result = new RgbaImage(image.Width, image.Height);
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            bool line = x % 256 == 0 || y % 256 == 0;
            var back = line ? new Rgba32(255, 0, 255) : ((x / 8 + y / 8) & 1) == 0 ? new Rgba32(205, 205, 205) : new Rgba32(150, 150, 150);
            var p = image[x, y];
            float a = line ? 0f : p.A / 255f;
            result[x, y] = new Rgba32(
                (byte)(p.R * a + back.R * (1 - a)),
                (byte)(p.G * a + back.G * (1 - a)),
                (byte)(p.B * a + back.B * (1 - a)));
        }

        return result;
    }
}
