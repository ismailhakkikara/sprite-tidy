# Sprite Tidy

A Unity editor tool that cleans up AI-generated sprites so they are ready to use.

Images from ChatGPT and other image generators rarely come out consistent:

- canvas sizes vary (1024×1024, 1536×1024, 1254×1254, …)
- margins differ from one image to the next, and the subject is not centered
- the background is not transparent: a flat color, or a fake checkerboard painted into the image
- "transparent" images are not quite opaque either: most of their solid pixels have alpha 250–253, so sprites are faintly see-through
- several objects come in one image, with sparkles and glows that don't touch the object they belong to

Sprite Tidy fixes these inside the Unity Editor, without touching your original files.

> **Status:** v0.1 in development. Cleaning up single images and multi-object sheets works; aligning sprite sheet
> animations is planned for v0.2.

## What it does

1. **Removes the background.** Detects whether an image is already transparent, has a flat background, or has a fake
   checkerboard (light or dark, even when its two shades are nearly identical). The background is flood-filled from the
   border, holes such as the middle of a ring are found too, and anti-aliased edges are un-blended so no white or dark
   halo is left.
2. **Cleans up alpha.** Nearly opaque pixels become fully opaque; nearly invisible haze is cleared.
3. **Splits objects.** Every separate object becomes its own sprite, in reading order. Detached bits (sparkles,
   light rays) join the object they belong to, faint glows are shared out to the nearest object, and stray specks are dropped.
4. **Fits each sprite onto a canvas.** Trims it, scales it to fit inside the padding with high-quality resampling, and
   aligns it (center, bottom-center for things that stand on the ground, …). Sprites from one image can keep their
   relative sizes.
5. **Imports the results as sprites**, with the pivot at the alignment anchor.

## Installation

Requires Unity 2022.3 or newer.

In the Package Manager, choose **+ → Add package from git URL…** and enter:

```
https://github.com/melloBlack/sprite-tidy.git?path=/Packages/com.melloblack.spritetidy
```

Or add it to `Packages/manifest.json`:

```json
"com.melloblack.spritetidy": "https://github.com/melloBlack/sprite-tidy.git?path=/Packages/com.melloblack.spritetidy"
```

To pin a release, append its tag to the URL, for example `#v0.1.0`.

## Usage

1. Open **Window → Sprite Tidy**, or right-click images or folders in the Project window and choose
   **Sprite Tidy → Open in Sprite Tidy**.
2. Add images by dragging them (or whole folders) into the window, or select them and press **Add Selected**.
3. Pick an image in the list to preview it: the original, the cleaned image and the finished sprites.
   The preview updates as you change the settings.
4. Press **Process**. The sprites are written to a `Tidy` folder next to each source image (or to the output folder
   you choose) and imported as sprites. Processing again overwrites them and keeps their GUIDs, so references survive.

To reuse settings, press **Save As…** next to the preset field, or create one with
**Assets → Create → Sprite Tidy → Preset**. Every setting has a tooltip.

### From code

The image processing has no Unity dependencies and can be called directly:

```csharp
using MelloBlack.SpriteTidy;
using MelloBlack.SpriteTidy.Core;

var settings = new TidySettings();
settings.Canvas.Width = settings.Canvas.Height = 256;
settings.Canvas.VerticalAnchor = VerticalAnchor.Bottom;

var result = TidyPipeline.Process(SpriteTidyIO.Load("Assets/Art/icons.png"), settings);
for (int i = 0; i < result.Sprites.Count; i++)
    SpriteTidyIO.SavePng(result.Sprites[i].Image, $"Assets/Art/icon_{i + 1}.png");
```

## Development

This repository is a Unity 6 (6000.0) project used to develop and test the package, which lives in
[`Packages/com.melloblack.spritetidy`](Packages/com.melloblack.spritetidy). The package itself supports Unity 2022.3 and newer.

Run the tests from **Window → General → Test Runner → EditMode**. To try the pipeline on a folder of your own images
without importing them, run:

```
Unity -batchmode -quit -projectPath . -executeMethod SampleBatch.Run -sampleIn <folder> -sampleOut <folder>
```

It writes the cleaned image and a contact sheet of the sprites for each input, plus a `summary.txt`.

## Contributing

Issues and pull requests are welcome. If you have AI-generated images that Sprite Tidy handles badly,
please attach them to an issue: real examples make the best test cases.

## License

[MIT](LICENSE)
