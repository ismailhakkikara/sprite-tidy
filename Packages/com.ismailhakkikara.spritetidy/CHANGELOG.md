# Changelog

All notable changes to this package are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- New name: the package is now `com.ismailhakkikara.spritetidy`, at https://github.com/ismailhakkikara/sprite-tidy,
  and its code is in the `IsmailHakkiKara.SpriteTidy` namespace. They were `com.melloblack.spritetidy` and
  `MelloBlack.SpriteTidy`. To update, replace the `com.melloblack.spritetidy` line in `Packages/manifest.json` with
  `"com.ismailhakkikara.spritetidy": "https://github.com/ismailhakkikara/sprite-tidy.git?path=/Packages/com.ismailhakkikara.spritetidy"`.

## [0.2.0] - 2026-10-28

### Added

- Overwrite: replaces the image itself with the result, so scenes, prefabs and animations that already use it keep
  working. The old Fix button is now Save as New.
- Several images at once: select them in the Project window and choose Fix with Sprite Tidy, or drag them into the
  window. Click an image to see its result; all of them are saved with the same settings.

### Changed

- Sprites cut some other way before, in the Sprite Editor for example, keep their names and IDs when the image is
  fixed again.

## [0.1.1] - 2026-10-28

### Added

- Margin: empty pixels between the object and the edges of its sprite, 8 by default. It used to be fixed at
  1/64 of the size, which is the same at the default size of 512. In a sheet it also keeps neighboring sprites
  from bleeding into each other.

## [0.1.0] - 2026-10-28

First release.

### Added

- Sprite Tidy window with Mode (Single or Multiple), Output (Sheet or Separate), Line up (Bottom, Center or Top,
  for sheets), Size and Pivot, and a preview of the result to the right of the settings or below them, whichever
  shows it bigger.
- Background removal for plain colors and fake checkerboards (also dark ones whose two colors are nearly the same),
  with removal of holes and un-blending of soft edges.
- Alpha cleanup: nearly opaque pixels become fully opaque and faint haze is removed.
- Object finding: each separate object in reading order, with loose bits and faint glows given to the nearest object.
- One shared scale for all objects. In a Sheet, frames are lined up and placed together at the pivot, so they keep
  the same size and position whatever pivot is chosen.
- Results are saved next to the original with a `_fixed` suffix and imported as sprites with High Quality compression,
  which keeps the smooth shading of generated art; saving again keeps the sprite IDs.
- `SpriteFixer` and `SpriteTidySaver` for doing the same from code.

[Unreleased]: https://github.com/ismailhakkikara/sprite-tidy/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/ismailhakkikara/sprite-tidy/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/ismailhakkikara/sprite-tidy/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ismailhakkikara/sprite-tidy/releases/tag/v0.1.0
