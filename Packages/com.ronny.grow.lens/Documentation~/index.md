# Grow Lens

Optional, default-off editor productivity tools for Grow. The package is editor-only and has
no runtime footprint.

## Installation

`com.ronny.grow.lens` is embedded in the repository at `Packages/com.ronny.grow.lens` and
depends on `com.ronny.grow`. Importing or enabling it is optional; the kernel package never
references it.

## Enable

Open **Edit > Preferences > Grow > Lens** and turn on **Enable editor enhancements**. The
setting lives in `UserSettings/Grow/Lens.json` and is per project. Everything is unsubscribed
while the switch is off, so disabled features add no per-row or per-frame callbacks.

The host also disables itself in batch mode and after three consecutive failures of the same
feature.

## Features

| Feature | What it does |
|---|---|
| Hierarchy component icons | Draws component icons from the right edge of a Hierarchy row; hover shows the component type |
| Hierarchy indent guides | Draws vertical dashed guides at each indent level |
| Asset header actions | Copy full path, asset path or GUID; open the file or its `.meta` in Rider when the Rider package is installed |
| Project / Hierarchy context menus | Ctrl+right-click: copy name/path/GUID, reveal or create assets, create empty objects, delete |
| Toolbar Grow menu | Adds one **Grow** entry to the left side of the main toolbar; it opens a menu instead of adding a row of buttons |

The menus call commands directly. They do not run Grow menu items through
`EditorApplication.ExecuteMenuItem`, do not reflect `EditorApplication.ValidateMenuItem`, and
never modify hierarchy or asset data while drawing.

## Non-goals

- Notes, document links, transform clipboard and other data-entry tools are not part of this
  package.
- No project-wide property drawer or inspector replacement.
- No `EditorPrefs` / registry-backed settings.

## Safety

- Every editor subscription is owned by one host (`GrowLensHost`); turning the feature off
  removes the callbacks instead of leaving no-op handlers behind.
- A failing callback logs once; three consecutive failures of the same feature disable Lens.
- The toolbar reflection targets Unity 2022.3 and is guarded; if the internal toolbar type or
  `m_Root` is missing, it logs once and stays inactive instead of spamming the console.

## Requirements

- Unity **2022.3 LTS**.
- `com.ronny.grow` (embedded in the same repository).

## License

MIT — see the repository root `LICENSE`.
