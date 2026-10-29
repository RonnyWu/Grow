# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Independent feature switches for component icons, separators, indent guides, the right-edge
  active toggle, Inspector header actions, Project/Hierarchy context menus and the toolbar menu.
  Only the subsystems with an enabled feature are subscribed; the master switch still disables
  everything.
- A Hierarchy active toggle pinned to the right edge of each row, with Undo support and space
  reserved before the component icons.
- Indent-guide style settings: dashed/solid and a custom color with a Reset button. The dash pattern
  and 1 px line width stay fixed so the connectors remain centered on the foldout triangle.
- English and Simplified Chinese UI text. Lens follows Grow's user-global
  **Preferences > Grow > General** language.
- Hierarchy component icons are now clickable: a plain left-click opens a focused Inspector
  window for that component, matching the component's **Properties...** menu item. Selection
  and Ctrl/Shift/Alt/Command/right-click stay with the Hierarchy.
- Formatted Hierarchy separators: `##RRGGBB[#RRGGBB]Text` (3/6-digit colors, `##-` for the
  theme background), with child objects left in place and `GameObject/Grow/Create Separator`.
  Rendering is read-only; it never reparents children.
- `Grow` toolbar menu includes **Create Separator**.

### Changed

- The Preferences page is **Grow > Lens** instead of **Grow > Grow Lens**. The settings file is
  schema 4; older files are migrated in memory. The old shared context-menu switch is copied to the
  new Project and Hierarchy switches, the active toggle starts enabled, and the guide style defaults
  to dashed with the theme-derived color.
- Lens now depends on `com.ronny.grow` and derives its Preferences page from the shared
  `GrowSettingsPageBase`, so margin, label width and failure containment match the built-in pages.
- The Preferences page is drawn with a transient `SerializedObject` and
  `EditorGUILayout.PropertyField` for native label/control rows. The master switch gates the feature
  list but leaves the feature switches editable while off, so they can be prepared before enabling;
  details moved into tooltips.
- Settings are grouped into Inspector, Project, Hierarchy and Toolbar enhancements. Each group row is
  a derived batch switch; Hierarchy shows all-on/all-off/mixed and updates its five children with one
  save and one host update. The guide style and color controls are indented one extra level under
  Indent guides and remain visible but disabled when that feature is off.
- Project and Hierarchy context menus are separate switches, so they can be enabled independently.
- Context menus and the toolbar menu use the selected language.

### Fixed

- Hierarchy connectors now use the live `TreeViewGUI` geometry and the visible row/depth
  stream: ancestor columns draw T (branch continues) or L (last child) shapes with horizontal
  arms, never the current row's own foldout column. Search results, scene headers and
  unresolved layouts draw nothing.
- The Preferences page now updates when the shared Grow language changes instead of staying in
  English.
- The Lens Preferences page now uses the same left/top margin and label width as the built-in Grow
  settings pages.
- The transient `SerializedObject` proxy no longer inherits `NotEditable` from
  `HideFlags.HideAndDontSave`, so its PropertyFields are interactive instead of permanently grayed.
- Guide lines are pixel-snapped and offset one pixel on X to match Unity 2022.3's foldout arrow
  visual center. The parent vertical breaks around the arm's pixel row and the arm starts at the
  vertical's left edge, so the arm owns the corner pixel without overlap or a gap.
- Toggling the master switch or a feature switch now repaints the Settings window and editor views
  immediately instead of waiting for the next mouse movement.
- The Preferences page's search keywords are rebuilt when the shared language changes, so the page
  stays findable in both languages.

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
