# Editor lens

**Read this in other languages:** [English](editor-lens.md) | [简体中文](../../../zh-CN/framework/design/editor-lens.md)

*Design record* — the optional `com.ronny.grow.lens` editor tool package.

Lens is a set of editor conveniences, not a framework mechanism. It ships as a separate editor-only
package so the kernel package keeps its behaviour unchanged for every consumer.

## Goals

- Keep the kernel package free of global editor hooks and Unity internal API risk.
- Make every feature opt-in: with the switch off, no `hierarchyWindowItemOnGUI`,
  `projectWindowItemOnGUI`, `finishedDefaultHeaderGUI`, `hierarchyChanged` or
  `EditorApplication.update` callback is registered.
- Keep one owner for all subscriptions so enabling and disabling cannot leave partial handlers.
- Degrade safely when a Unity internal API changes.

## Non-goals

- A general editor framework, a window framework or a replacement for Odin Inspector.
- Notes, document links, transform clipboard or other data-entry tools.
- Changing hierarchy or asset data while drawing.
- A project-wide property drawer or enum drawer.
- Registry-backed settings (`EditorPrefs` / `PlayerPrefs`).

## Package boundary

```text
com.ronny.grow        kernel + Grow.Editor (editor assembly)
com.ronny.grow.lens   editor-only, independent
```

Lens never references the Store packages or a Grow assembly, so the package graph stays acyclic.
The Lens assembly itself only uses Unity editor APIs; its `package.json` declares no package
dependency on the kernel.

## Settings

`UserSettings/Grow/Lens.json` is a small per-project JSON file with a single boolean (`Enabled`). It
lives outside `Assets/`, so Unity does not import it and it does not ship in a build; this repository
also ignores `UserSettings/`. The default is `false`; a missing or unreadable file also resolves to
`false`.

The settings page is **Preferences > Grow > Lens**. Changing the toggle applies the host immediately;
no domain reload is required. The toggle is also the only supported way to change the setting: there
is no menu or toolbar shortcut for it.

## Host

`GrowLensHost` is the only place that subscribes or unsubscribes editor callbacks. Features expose
`Subscribe` / `Unsubscribe` methods and never subscribe in their own initializers.

`Apply()` is idempotent, compares the desired state with the current one and does nothing when they
match. It is called when:

- the editor loads and `[InitializeOnLoadMethod]` runs, including after a domain reload;
- the settings toggle changes.

`GrowLensHost.Disable(reason)` is the failure path: it logs an error, marks Lens disabled for the
current editor session, and removes every subscription. The saved preference stays enabled, so a
restart or an explicit off/on toggle retries with fresh failure counters.

The host also treats batch mode as disabled, so a headless test run never installs editor callbacks.

## Features

| Feature | Implementation | Notes |
| --- | --- | --- |
| Hierarchy component icons | `ItemComponentIcon` | Right-aligned icons packed in component order; plain left-click opens a focused Inspector, matching the component's **Properties...** action |
| Hierarchy separators | `HierarchySeparator` | Read-only `##RRGGBB[#RRGGBB]Text` headers; create through **GameObject > Grow > Create Separator**; search mode skips them |
| Hierarchy tree connectors | `ItemIndentLine` | T/L connectors on ancestor columns plus horizontal arms, from the live `TreeViewGUI` geometry and visible row depths |
| Asset header actions | `GrowAssetHead` | Copy full path, asset path and GUID; Rider actions are optional and the row is hidden for excluded types |
| Project / Hierarchy menus | `GrowContextMenus` + `GrowMenuActions` | Ctrl+right-click; calls Unity APIs and Unity menu items directly |
| Toolbar menu | `GrowToolbar` | One left-side **Grow** entry that opens a menu; no Play mode container changes |

### Hierarchy component icons

The row callback receives the row rectangle, so the icons need no tree internals. Icons are packed
from the right edge of the row in component order, and the visible count is limited by the end of the
object name; a narrow Hierarchy shows fewer icons. The icon set is cached per object and rebuilt on
`hierarchyChanged`.

A plain left click on a visible icon opens Unity's focused Inspector for that component
(`PropertyEditor.OpenPropertyEditor`) through a delayed call, after the row's IMGUI event, and
consumes the event so the Hierarchy does not also select the row. Any modifier, a right click or a
click outside the icon rect leaves the event to the Hierarchy. If the internal entry point cannot be
resolved, the fallback selects the component and opens the Inspector window, which does change the
selection.

### Hierarchy separators

The separator is a display convention, not a special object type: a GameObject whose name matches the
format is drawn as a colored band with centered text. Nothing is reparented and the GameObject is not
edited, so children keep their place and the name stays the source of truth.

The format is `##` followed by a 3- or 6-digit background color or `-` for the theme background, an
optional `#` and 3- or 6-digit text color, then the display text. Examples: `##2E7D32 Level A`,
`##F00#FFF Title`, `##-#FFD54F Note`, `##383838`. Colors have no alpha channel. Text is trimmed;
anything that does not match is an ordinary name, including the single-`#` form and 5-digit colors.

A selected separator yields to the native row — Lens adds a thin selection marker and draws no band —
so the selection highlight and the rename field remain usable. Search mode skips separator drawing
entirely. Creation uses `GameObject > Grow > Create Separator`, the Hierarchy context menu or the
toolbar menu; the created object is named `##383838 Section`, is placed as a sibling after the active
scene object (or at the root) and is selected. The command is disabled while a Prefab Stage is open,
because a separator is scene content and must not be written into a prefab asset. With multiple
scenes loaded, the object is created in the active object's scene.

### Hierarchy tree connectors

The connector renderer reads the active window's `TreeViewGUI` geometry by reflection: base indent,
indent width, foldout width, row height, top-row margin and the visible row/depth list. It derives the
row depth from the callback content x and rejects the row when the x is not on a depth column, which
covers horizontal scroll and layout changes.

Each ancestor column gets a vertical segment when the branch continues, and the parent column gets a
full-height segment with a following sibling or a half-height segment on the last child, plus a
horizontal arm that stops at the foldout arrow for branches and reaches the content for leaves. The
current row's own foldout column is never used. Depth 0 (a scene header), a searching tree and any
unresolved geometry draw nothing; the unresolved case logs once.

## Asset header

`GrowAssetHead` appends a row to `Editor.finishedDefaultHeaderGUI` for a single persistent asset. It
draws the full path, the project-relative path, the GUID and, when the Rider integration resolves,
`Meta` and `File` buttons. The row is hidden for multi-selection, for objects without an asset path or
GUID, and for types whose importer already draws its own header (`GameObject`, `MonoScript`,
`TextAsset`, `Shader`, `Font`, `AudioClip`, `ComputeShader` and their subclasses). Rider is resolved
by reflection, so the package has no hard dependency on the Rider package.

## Context menus

`GrowContextMenus` handles a Ctrl+right-click (Command on macOS) in the Project and Hierarchy windows.
The clicked item is selected first when it is not already in the selection, so selection-based
commands act on it.

- **Project**: copy name, path or GUID; reveal in the file manager (`Show in Explorer`); create a
  folder, scene, material, prefab or prefab variant; delete. Items that need an asset identity are
  disabled without one.
- **Hierarchy**: copy name or root-to-leaf path; copy the GUID of the nearest prefab asset; create an
  empty object, child or parent through Unity's own menu and reset the new local transform; create a
  separator; delete.

`Delete` runs Unity's `Edit/Delete` and adds no confirmation. Menu actions run inside the failure
guard, and no action edits hierarchy or asset data while a row is being drawn.

## Toolbar

`GrowToolbar` reflects `UnityEditor.Toolbar`, gets `m_Root` and appends one `ToolbarButton` to
`ToolbarZoneLeftAlign`. It polls every three seconds, and only while the button is missing or detached
from its panel, so a toolbar rebuild can re-attach it without duplicate buttons. No Play mode
container is touched. If the internal type, `m_Root` or the zone is missing, the feature logs once and
stays inactive instead of throwing every frame.

The menu collects common editor windows and commands. Screenshot and open-folder actions use the
project root's `Screenshots/` folder, which this repository ignores.

## Failure containment

Drawing callbacks run through `GrowLensGuard.Run`; user actions use `GrowLensGuard.RunAction`.
The first drawing failure of a feature logs once; three consecutive failures of the same feature
disable Lens for the current editor session and remove all callbacks. A successful drawing call
resets that feature's counter. Menu-action failures are logged but never disable the package. This
prevents a broken Unity internal API from filling the console every repaint without turning a
user-action error into a persistent opt-out.

## Limits

- Unity internal toolbar, Hierarchy and header shapes are not contracts; a Unity upgrade can disable
  the affected feature.
- Tree connectors derive depth and sibling/last-child state from the current window's
  `TreeViewGUI`/visible rows. Search results, scene headers and unresolved internal layouts draw
  nothing (fail closed) instead of guessing pixels.
- Component icons are drawn on GameObject rows and not on separator rows; Project-window icons are
  not affected.
- Every decoration is drawn from `hierarchyWindowItemOnGUI`, so Lens decorates Hierarchy rows only; it
  has no Stage-view or Project-window overlay.
- Component icon clicks use a plain left click and open a focused Inspector window without changing
  the current selection; modifier clicks and right-clicks stay with the Hierarchy. The fallback path
  selects the component.
- Separators use `##RRGGBB`, the 3-digit `##RGB` or `##-`; the single-`#` form is not recognized.
  Selected separators yield to the native row; search mode skips separator rendering.
- Hierarchy icons are cleared on `hierarchyChanged`, not on every component edit; a component change
  that does not raise the event may keep its old icon set until the next change.
- Context menus require Ctrl+right-click. Copy and reveal actions use the clicked row; Delete acts on
  the current selection, and create actions run Unity's built-in asset/GameObject commands.
- Separators are real GameObjects, so they exist at runtime and in a build.
- The package has no localization catalog; its UI text is English.

## Testing

The package has an EditMode test assembly for the parts that do not need IMGUI: the settings JSON
round-trip, separator parsing, component icon layout, connector geometry, the reflection probes
against the 2022.3 internal members, and Hierarchy path helpers. Drawing, clicks, menus and the
toolbar are checked manually in the editor; the automated suite cannot drive them.

Manual smoke checklist for Unity 2022.3:

1. Enable Lens under **Preferences > Grow > Lens**; the toolbar entry appears without a restart.
2. Open the **Grow** toolbar menu and check Preferences, Console and Take Screenshot.
3. Select a material or texture and confirm the Inspector header actions; select a Hierarchy object
   and confirm component icons and tree connectors.
4. Click a component icon with a plain left click; the focused Inspector opens and the selection does
   not change. Modifier clicks and right-clicks still belong to the Hierarchy.
5. Ctrl+right-click a Project item and a Hierarchy row; confirm the menus act on the clicked item.
6. Create a separator with **GameObject > Grow > Create Separator**, rename it to
   `##2E7D32#FFFFFF Section`, then select it and confirm the native row returns.
7. Search the Hierarchy and confirm separators and connectors disappear.
8. Toggle Lens off and confirm the Grow entry and every Hierarchy/header decoration disappear.
9. Enter Play mode, change the editor layout, and confirm the Grow entry re-attaches without duplicate
   buttons.
10. Run `pwsh ci/run-tests.ps1 -Mode EditMode`; batch mode must not install callbacks.

## See also

- [Editor inspector](editor-inspector.md) — the annotation-driven Inspector.
- [Architecture](../architecture/README.md) — where the editor assemblies sit in Grow.
- [Glossary](../reference/glossary.md) — terms shared across these pages.
