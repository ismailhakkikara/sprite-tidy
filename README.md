# Sprite Tidy

A Unity editor tool that cleans up AI-generated sprites so they are ready to use.

Images from ChatGPT and other image generators rarely come out consistent:

- canvas sizes vary (1024×1024, 1536×1024, …)
- margins differ from one image to the next
- the subject is not centered
- the background is not transparent, or is a fake checkerboard
- in multi-object images and sprite sheet animations, frames drift and change size

Sprite Tidy fixes these inside the Unity Editor, without touching your original files.

> **Status:** early development. The features below are planned; nothing is usable yet.

## Planned features

### v0.1 — Single images

- Background removal for solid and fake-checkerboard backgrounds, with edge halo cleanup
- Trim to content
- Fit onto a fixed canvas with padding and alignment (center, bottom-center, …)
- High-quality resampling with premultiplied alpha (no dark fringes)
- Presets and batch processing of whole folders
- Editor window with before/after preview

### v0.2 — Sheets and animations

- Split multiple objects in one image into separate sprites
- Detect sprite sheet frames, apply one shared scale and align them on an anchor
- Re-pack into a clean grid, slice automatically, set pivots and optionally create an AnimationClip

### Later

- Process images automatically on import, based on folder rules
- Optional ML-based background removal for complex backgrounds

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

## Development

This repository is a Unity 6 (6000.0) project used to develop and test the package, which lives in
[`Packages/com.melloblack.spritetidy`](Packages/com.melloblack.spritetidy). The package itself supports Unity 2022.3 and newer.

Run the tests from **Window → General → Test Runner → EditMode**.

## Contributing

Issues and pull requests are welcome. If you have AI-generated images that Sprite Tidy handles badly,
please attach them to an issue: real examples make the best test cases.

## License

[MIT](LICENSE)
