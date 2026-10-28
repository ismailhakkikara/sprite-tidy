# Changelog

All notable changes to this package are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Sprite Tidy window with four settings: Mode (Single or Multiple), Output (Sheet, Separate or Atlas), Size and Pivot,
  and a preview of the original and the result.
- Background removal for plain colors and fake checkerboards (also dark ones whose two colors are nearly the same),
  with removal of holes and un-blending of soft edges.
- Alpha cleanup: nearly opaque pixels become fully opaque and faint haze is removed.
- Object finding: each separate object in reading order, with loose bits and faint glows given to the nearest object.
- One shared scale for all objects and pinning at the pivot, so sprite sheet frames keep the same size and position.
- Results are saved next to the original with a `_fixed` suffix and imported as sprites; saving again keeps the sprite IDs.
- `SpriteFixer` and `SpriteTidySaver` for doing the same from code.
