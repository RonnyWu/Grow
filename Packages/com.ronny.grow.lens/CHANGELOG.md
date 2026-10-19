# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [3.0.0-exp.1] - 2026-10-19

### Added

- Optional, default-off editor enhancements with a single `GrowLensHost` subscription owner.
- Hierarchy component icons and indent guides.
- Asset header actions: copy full/asset path and GUID, open the file or its `.meta` in Rider.
- Project and Hierarchy context menus for copy path/name/GUID, create, reveal and delete.
- A single **Grow** entry on the left side of the main toolbar that opens a menu for common
  editor windows and screenshot actions.

### Behavior

- Nothing subscribes until the feature is enabled under **Preferences > Grow > Lens**.
- A failing callback logs once; three consecutive failures of the same feature disable Lens.
- Batch mode never subscribes.
- Context menus never modify hierarchy or asset data while drawing.
