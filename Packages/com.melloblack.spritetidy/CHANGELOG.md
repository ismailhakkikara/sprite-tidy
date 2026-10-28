# Changelog

All notable changes to this package are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Background removal for flat colors and fake checkerboards (including dark, low-contrast ones), with removal of
  enclosed holes and un-blending of anti-aliased edges.
- Alpha cleanup: nearly opaque pixels become fully opaque and faint haze is cleared.
- Object splitting: every separate object in an image becomes its own sprite, in reading order, with detached
  fragments and faint glows attached to the nearest object.
- Canvas fitting with padding, alignment anchors, optional shared scale per image and bicubic or Lanczos resampling
  with premultiplied alpha.
- Sprite Tidy window with before/after preview, presets and batch processing; results are imported as sprites with
  the pivot at the alignment anchor.
- `TidyPipeline` API for running the clean-up from code.
