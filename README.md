# Sprite Tidy

A Unity editor tool that cleans up AI-generated sprites so they are ready to use.

Images from ChatGPT and other image generators rarely come out consistent:

- canvas sizes vary (1024×1024, 1536×1024, 1254×1254, …)
- margins differ from one image to the next, and the object is not centered
- the background is not transparent: a plain color, or a fake checkerboard painted into the image
- "transparent" images are not quite opaque either: most of their solid pixels have alpha 250–253, so sprites are a little see-through
- in sprite sheets, frames move around and change size from one frame to the next

Sprite Tidy fixes these inside the Unity Editor. Your original file is never changed.

> **Status:** v0.1 in development.

## Usage

Open **Window → Sprite Tidy**, or right-click an image in the Project window and choose **Fix with Sprite Tidy**.

| Setting | What it does |
|---|---|
| **Image** | The picture to fix. |
| **Mode** | **Single**: the picture shows one object. **Multiple**: it shows several, like a sprite sheet. |
| **Output** | Only for Multiple. **Sheet**: one texture with the objects in a grid of equal cells, in the same rows as the source; good for animations. **Separate**: one file for each object; good for icons. To pack separate sprites into one texture for performance, add them to a Unity [Sprite Atlas](https://docs.unity3d.com/Manual/sprite/atlas/atlas-landing.html). |
| **Size** | Width and height of each sprite, in pixels. 512 by default. |
| **Pivot** | Where each object is pinned inside its sprite; the sprite pivot is set to the same point. **Bottom** makes objects stand on the bottom edge, centered on their base: good for animations. |

The preview shows the original and the result, with the pivot of each sprite marked in red. Press **Fix** to save.
The result is saved next to the original, with `_fixed` added to the name (`chest_fixed.png`, or `chest_fixed_01.png`,
`chest_fixed_02.png`, … for Separate) and imported as sprites. Fixing again overwrites the same files and keeps the
sprite IDs, so animations and prefabs that use them keep working.

## What happens to the image

1. **The background is removed.** Sprite Tidy finds out whether the image is already transparent, has a plain
   background, or has a fake checkerboard (light or dark, even when its two colors are nearly the same). Holes that show
   the checkerboard, like the middle of a ring, are removed too, and soft edges are un-blended so no white or dark halo is left.
2. **Alpha is cleaned up.** Nearly opaque pixels become fully opaque; haze you can hardly see is removed.
3. **Objects are found.** Each separate object is found in reading order. Loose bits like sparkles join the object they
   belong to, faint glows go to the nearest object, and stray specks are dropped. In Single mode everything stays one object.
4. **Objects are scaled and pinned.** All objects share one scale: the largest at which every object, pinned at the pivot,
   fits inside its sprite. So no frame grows or shrinks against the others, and each one sits at the same point.

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

To use a fixed release, add its tag to the end of the URL, for example `#v0.1.0`.

### From code

```csharp
using MelloBlack.SpriteTidy;
using MelloBlack.SpriteTidy.Core;

var settings = new FixSettings { Mode = ImageMode.Multiple, Output = OutputType.Sheet, Size = 256, Pivot = Pivot.Bottom };
var images = SpriteFixer.Fix(SpriteTidyIO.Load("Assets/Art/chest.png"), settings);
SpriteTidySaver.Save("Assets/Art/chest.png", images);
```

## Development

This repository is a Unity 6 (6000.0) project used to build and test the package, which lives in
[`Packages/com.melloblack.spritetidy`](Packages/com.melloblack.spritetidy). The package itself supports Unity 2022.3 and newer.

Run the tests from **Window → General → Test Runner → EditMode**. To try the window on your own images, put them in
`Assets/LocalSamples`, which git ignores. To fix a whole folder of images outside the project, run:

```
Unity -batchmode -quit -projectPath . -executeMethod SampleBatch.Run -sampleIn <folder> -sampleOut <folder> [-pivot Bottom]
```

For each image it writes the sheet and an "onion skin" of all frames on top of each other: frames that move around
show up as doubled edges.

## Contributing

Issues and pull requests are welcome. If you have AI-generated images that Sprite Tidy handles badly,
please attach them to an issue: real examples make the best test cases.

## License

[MIT](LICENSE)
