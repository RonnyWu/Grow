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
- Component icons have a separate **Open Inspector on icon click** option, off by default. While
  off, clicking an icon selects the GameObject normally instead of opening the component Inspector.
- Separators are a dedicated group. A separate **Create Separator menu** option controls the
  Hierarchy, toolbar and GameObject menu entries.
- Create Separator opens a small editor for the title, background color, automatic or custom text
  color, insertion above/below the selection, and a live preview. The previous hard-coded
  one-click object is gone.
- A Hierarchy active toggle pinned to the right edge of each row, with Undo support and space
  reserved before the component icons.
- Preferences rows use native `EditorGUILayout.PropertyField` controls in Unity's default
  label/control columns.
- Group labels include a visible state (All on / Partial / All off); a click turns the group on
  unless it is fully on.
- Hierarchy item rows follow the visual order of the tree row: indent guides, component icons, active
  toggle and context menus.
- The guide color also uses a `PropertyField`, with an `#RRGGBBAA` readout and a non-expanding Reset
  button.
- Footer buttons use their natural widths instead of stretching across the window.
- The master switch shows a status line; while it is off, every feature row is disabled.
- The guide color row shows an `#RRGGBBAA` readout and keeps **Reset** disabled until the color is
  customized.
- The page footer opens the language page, reveals the settings file and restores the defaults
  behind a confirmation dialog that also turns Grow Lens off.
- Simplified Chinese uses Unity's window names in the group titles with the English name in
  parentheses, and the two context-menu switches name their window and shortcut.
- Indent-guide style settings: dashed/solid and a custom color with a Reset button. The dash pattern
  and 1 px line width stay fixed so the connectors remain centered on the foldout triangle.
- English and Simplified Chinese UI text. Lens follows Grow's user-global
  **Preferences > Grow > General** language.
- Hierarchy component icons stay display-only by default. With **Open Inspector on icon click**
  enabled, a plain left-click opens that component's focused Inspector; modifier and right clicks
  stay with the Hierarchy.
- Formatted Hierarchy separators: `##RRGGBB[#RRGGBB]Text` (3/6-digit colors, `##-` for the
  theme background), with child objects left in place and `GameObject/Grow/Create Separator...`.
  Rendering is read-only; it never reparents children.
- `Grow` toolbar menu includes **Create Separator...**.

### Changed

- The Preferences page is **Grow > Lens** instead of **Grow > Grow Lens**. The settings file is
  schema 5; older files are migrated in memory. The old shared context-menu switch is copied to the
  new Project and Hierarchy switches, the active toggle starts enabled, and the guide style defaults
  to dashed with the current editor theme's text color at alpha 0.32.
- Lens now depends on `com.ronny.grow` and derives its Preferences page from the shared
  `GrowSettingsPageBase`, so margin, label width and failure containment match the built-in pages.
- A minimal transient serialized view feeds the native `PropertyField` rows; each frame commits one
  data candidate and applies the host at most once. Turning the master switch off cascades the
  native disabled state to every feature row.
- Settings are grouped into Hierarchy, Separators, Inspector, Project and Toolbar. The Hierarchy
  group shows all-on/all-off/mixed across its four regular-item switches; Separators is independent
  because a separator is its own row rather than a decoration on a normal item. The guide style and
  color controls are indented one extra level under Indent guides and remain visible but disabled
  when that feature is off.
- Project and Hierarchy context menus are separate switches, so they can be enabled independently.
- Context menus and the toolbar menu use the selected language.

### Fixed

- Hierarchy connectors now use the live `TreeViewGUI` geometry and the visible row/depth
  stream: ancestor columns draw T (branch continues) or L (last child) shapes with horizontal
  arms, never the current row's own foldout column. Search results, scene headers and
  unresolved layouts draw nothing.
- The Preferences page now updates when the shared Grow language changes instead of staying in
  English.
- The Lens Preferences page now uses the shared page margin and label width with native serialized
  fields, matching Unity's default Preferences form instead of overlapping custom toggle rows.
- Guide lines are pixel-snapped and offset one pixel on X to match Unity 2022.3's foldout arrow
  visual center. The parent vertical breaks around the arm's pixel row and the arm starts at the
  vertical's left edge, so the arm owns the corner pixel without overlap or a gap.
- Toggling the master switch or a feature switch now repaints the Settings window and editor views
  immediately instead of waiting for the next mouse movement.
- The Preferences page's search keywords are rebuilt when the shared language changes, so the page
  stays findable in both languages.
- The page reports settings-file problems instead of failing silently: a damaged file, a file
  written by a newer version and a read-only file make the page read-only and are never
  overwritten, and a failed write keeps the previous values and shows an error. A damaged or newer
  file is no longer replaced or downgraded on the next change.
- One page interaction now produces at most one file write and at most one host update, and a
  style-only change repaints without touching the host subscriptions.
- A session-level disable after a feature failure is no longer written to disk by a later change,
  so Grow Lens comes back after a restart.

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
