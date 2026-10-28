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
/// Development helper, not part of the package: runs the pipeline over a folder of images outside the project and
/// writes contact sheets for checking the results by eye.
///
/// Unity -batchmode -quit -projectPath . -executeMethod SampleBatch.Run -sampleIn &lt;folder&gt; -sampleOut &lt;folder&gt;
/// </summary>
public static class SampleBatch
{
    const int SheetCell = 256;

    public static void Run()
    {
        string input = Argument("-sampleIn");
        string output = Argument("-sampleOut");
        Directory.CreateDirectory(output);

        var settings = new TidySettings();
        var summary = new StringBuilder();
        var files = Directory.GetFiles(input)
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            var source = SpriteTidyIO.Load(file);
            var stopwatch = Stopwatch.StartNew();
            var result = TidyPipeline.Process(source, settings);
            stopwatch.Stop();

            SpriteTidyIO.SavePng(OverChecker(result.Cleaned), Path.Combine(output, name + "__cleaned.png"));
            SpriteTidyIO.SavePng(ContactSheet(result), Path.Combine(output, name + "__sprites.png"));

            string line = $"{name}: {source.Width}x{source.Height}, background {result.Background}, " +
                          $"{result.Sprites.Count} sprites, opaque {OpaqueShare(source):P0} -> {OpaqueShare(result.Cleaned):P0}, " +
                          $"{stopwatch.ElapsedMilliseconds} ms";
            summary.AppendLine(line);
            Debug.Log("[SampleBatch] " + line);
        }

        File.WriteAllText(Path.Combine(output, "summary.txt"), summary.ToString());
    }

    static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException($"Missing {name} <folder> argument.");
        return args[index + 1];
    }

    /// <summary>All sprites side by side, each over a checkerboard with its canvas edge marked.</summary>
    static RgbaImage ContactSheet(TidyResult result)
    {
        int count = Math.Max(1, result.Sprites.Count);
        int columns = Math.Min(count, 5);
        int rows = (count + columns - 1) / columns;
        var sheet = new RgbaImage(columns * (SheetCell + 8) + 8, rows * (SheetCell + 8) + 8);
        Fill(sheet, new Rgba32(40, 40, 40));

        for (int i = 0; i < result.Sprites.Count; i++)
        {
            var image = result.Sprites[i].Image;
            double scale = Math.Min((double)SheetCell / image.Width, (double)SheetCell / image.Height);
            var small = Resampler.Resize(image, Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)), ResampleFilter.Bilinear);
            var cell = OverChecker(small);
            int column = i % columns, row = rows - 1 - i / columns;
            sheet.Blit(cell, 8 + column * (SheetCell + 8), 8 + row * (SheetCell + 8));
        }

        return sheet;
    }

    static RgbaImage OverChecker(RgbaImage image)
    {
        var result = new RgbaImage(image.Width, image.Height);
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            bool edge = x == 0 || y == 0 || x == image.Width - 1 || y == image.Height - 1;
            var back = edge ? new Rgba32(255, 0, 255) : ((x / 8 + y / 8) & 1) == 0 ? new Rgba32(205, 205, 205) : new Rgba32(150, 150, 150);
            var p = image[x, y];
            float a = edge ? 0f : p.A / 255f;
            result[x, y] = new Rgba32(
                (byte)(p.R * a + back.R * (1 - a)),
                (byte)(p.G * a + back.G * (1 - a)),
                (byte)(p.B * a + back.B * (1 - a)));
        }

        return result;
    }

    /// <summary>Share of visible pixels that are fully opaque.</summary>
    static double OpaqueShare(RgbaImage image)
    {
        int visible = image.Pixels.Count(p => p.A > 0);
        return visible == 0 ? 0 : (double)image.Pixels.Count(p => p.A == 255) / visible;
    }

    static void Fill(RgbaImage image, Rgba32 color)
    {
        for (int i = 0; i < image.Pixels.Length; i++) image.Pixels[i] = color;
    }
}
