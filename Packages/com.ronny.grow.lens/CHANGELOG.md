# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Hierarchy component icons are now clickable: a plain left-click opens a focused Inspector
  window for that component, matching the component's **Properties...** menu item. Selection
  and Ctrl/Shift/Alt/Command/right-click stay with the Hierarchy.
- Formatted Hierarchy separators: `##RRGGBB[#RRGGBB]Text` (3/6-digit colors, `##-` for the
  theme background), with child objects left in place and `GameObject/Grow/Create Separator`.
  Rendering is read-only; it never reparents children.
- `Grow` toolbar menu includes **Create Separator**.

### Fixed

- Hierarchy connectors now use the live `TreeViewGUI` geometry and the visible row/depth
  stream: ancestor columns draw T (branch continues) or L (last child) shapes with horizontal
  arms, never the current row's own foldout column. Search results, scene headers and
  unresolved layouts draw nothing.

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
- A failing drawing callback logs once; three consecutive failures of the same feature disable Lens
  for the current editor session without changing the saved preference. Menu actions log their own
  errors but never disable the package.
- Batch mode never subscribes.
- Context menus never modify hierarchy or asset data while drawing.
