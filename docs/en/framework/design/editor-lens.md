# Editor lens

**Read this in other languages:** [English](editor-lens.md) | [简体中文](../../../zh-CN/framework/design/editor-lens.md)

*Design record* — the optional `com.ronny.grow.lens` editor tool package.

Lens is a set of editor conveniences, not a framework mechanism. It ships as a separate editor-only
package so the kernel package keeps its behaviour unchanged for every consumer.

## Goals

- Keep the kernel package free of global editor hooks and Unity internal API risk.
- Make the package opt-in: with the master switch off, no `hierarchyWindowItemOnGUI`,
  `projectWindowItemOnGUI`, `finishedDefaultHeaderGUI`, `hierarchyChanged` or
  `EditorApplication.update` callback is registered. Individual feature switches can then
  remove a subsystem without disabling the rest.
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
com.ronny.grow.lens   editor-only, depends on Grow.Editor
```

Lens references `Grow.Editor` for the shared settings-page base and language service, and its
`package.json` declares a dependency on `com.ronny.grow`. It never references the Store packages or
the runtime kernel, so the package graph stays acyclic. The `Grow.Editor` friends list includes
`Grow.Lens.Editor`; the page shell is still internal rather than public API.

## Settings

`UserSettings/Grow/Lens.json` is a small per-project JSON file. Schema 5 stores the master
`Enabled` switch, one boolean for each feature, the component-icon click option, the separator-menu
option and the indent-guide style settings: `HierarchyComponentIcons`,
`HierarchyComponentInspector`, `HierarchySeparators`, `HierarchySeparatorMenu`,
`HierarchyIndentGuides`, `HierarchyActiveToggle`, `HierarchyContextMenus`, `ProjectContextMenus`,
`AssetHeader`, `Toolbar`, `HierarchyGuideDashed`, `HierarchyGuideCustomColor` and
`HierarchyGuideColor`. It lives outside `Assets/`, so Unity does not import it and it does not ship
in a build; this repository also ignores `UserSettings/`. The master switch defaults to `false`; a
missing or unreadable file also resolves to `false`. Feature switches default to `true`, while
opening a component Inspector on icon click defaults to `false`.

Schema 1 contains only `Enabled`; schema 2 stored the old shared `ContextMenus` switch. Loading an
older file normalizes it in memory to schema 5 and copies the old context-menu value to both
`ProjectContextMenus` and `HierarchyContextMenus`. Schema 3 added those two switches and
`HierarchyActiveToggle`; schema 4 added the dashed/solid guide style and custom guide color; schema 5
adds the component-icon click option and the separate separator-menu option. The default guide style
is dashed with the current editor theme's text color at alpha 0.32.

The page groups settings into **Hierarchy**, **Separators**, **Inspector**, **Project** and
**Toolbar**. The Hierarchy group batch-switches the four regular-item features and shows all-on,
all-off or mixed. Separators are a separate group because a separator is an independent item row,
not a decoration on a normal row; its child switch controls the Create Separator menu. Turning the
master switch off cascades the native disabled state to every feature row; the master switch itself
stays editable so Lens can be turned back on.

The settings page is **Preferences > Grow > Lens**: the category is **Grow** and the page label is
**Lens**, not **Grow Lens**, so the tree does not repeat the product prefix. It derives from the
shared `GrowSettingsPageBase`, which supplies the same margin, label width and failure containment as
the built-in Grow pages. A 12-line transient `ScriptableObject` exposes the existing serializable
settings data to `SerializedObject`; ordinary rows are native `EditorGUILayout.PropertyField`
controls. The JSON file remains the source of truth. One frame produces one data candidate and one
atomic commit, which applies the host immediately and requests a repaint; no domain reload is
required. There is no menu or toolbar shortcut for changing settings.

The page treats the file as the single source of truth and never lies about a write. One page
interaction produces at most one file write and at most one host update, and the in-memory values
are replaced only after the write succeeds, so a failed save leaves the running editor on the last
committed values and shows an error instead. A damaged file, a file written by a newer version and
a read-only file are shown as warnings and make the page read-only: they are never overwritten or
downgraded to the current schema. Style-only changes (dashed guides, guide color) repaint without
touching the host subscriptions.

The page follows Unity's default Preferences form: labels occupy the shared label column and native
`PropertyField` controls occupy the control column. Single-feature groups reuse the same serialized
property for their group and child rows. Only the derived Hierarchy group needs an extra Boolean so
`EditorGUI.showMixedValue` can display its partial state; its visible state text (All on / Partial /
All off) is appended to the label. A mixed group turns fully on when clicked, and a fully on group
turns off. The master switch adds a status line; while it is off, every feature row is disabled.
Hierarchy children follow the visual order of the row: indent guides, component icons (with the
nested click option), the active toggle, then context menus. Separators form their own group with
the Create Separator menu as a child. The guide color is also a `PropertyField`, with an
`#RRGGBBAA` readout and a non-expanding **Reset** button. Footer buttons use their natural widths
instead of stretching across the window.

## Language

Lens bundles English and Simplified Chinese labels under `Editor/Localization/Locales`. The language
comes from Grow's shared localization service and follows the user-global
**Preferences > Grow > General** choice. The settings page, context menus, toolbar menu and asset
header read the language at draw/open time, so switching the language is reflected on the next
repaint without restarting the editor. The page's search keywords are rebuilt when the language
changes, so a term typed in either language still finds the page.

## Host

`GrowLensHost` is the only place that subscribes or unsubscribes editor callbacks. Features expose
`Subscribe` / `Unsubscribe` methods and never subscribe in their own initializers.

`Apply()` is idempotent. It resolves the master switch and each feature, then attaches only the
subsystems that need a callback:

- one Hierarchy row callback while component icons, separators, indent guides or the active toggle
  are enabled;
- the asset header callback while asset header actions are enabled;
- the Project context-menu callback while Project context menus are enabled;
- the Hierarchy context-menu callback while Hierarchy context menus are enabled;
- the toolbar poller and button while the toolbar menu is enabled.

Turning a feature off removes or gates only that feature; turning the master switch off removes all
subscriptions. `Apply()` is called when:

- the editor loads and `[InitializeOnLoadMethod]` runs, including after a domain reload;
- the master switch changes;
- any feature toggle changes.

`GrowLensHost.Disable(reason)` is the failure path: it logs an error, marks Lens disabled for the
current editor session, and removes every subscription. The saved preference stays enabled, so a
restart or an explicit off/on toggle retries with fresh failure counters.

The host also treats batch mode as disabled, so a headless test run never installs editor callbacks.

## Features

| Feature | Implementation | Notes |
| --- | --- | --- |
| Hierarchy component icons | `ItemComponentIcon` | Right-aligned icons packed in component order; clicking opens a focused Inspector only while its click option is on, otherwise the row selects normally |
| Hierarchy separators | `HierarchySeparator` | Read-only `##RRGGBB[#RRGGBB]Text` headers with a separate Create Separator menu switch; search mode skips them |
| Hierarchy tree connectors | `ItemIndentLine` | T/L connectors on ancestor columns plus horizontal arms, from the live `TreeViewGUI` geometry and visible row depths |
| Hierarchy active toggle | `ItemActiveToggle` | Pinned checkbox at the row's right edge; toggles `activeSelf` with Undo and reserves space before component icons |
| Asset header actions | `GrowAssetHead` | Copy full path, asset path and GUID; Rider actions are optional and the row is hidden for excluded types |
| Project context menus | `GrowContextMenus` + `GrowMenuActions` | Ctrl+right-click in the Project window; calls Unity APIs and Unity menu items directly |
| Hierarchy context menus | `GrowContextMenus` + `GrowMenuActions` | Ctrl+right-click in the Hierarchy window; includes separator creation |
| Toolbar menu | `GrowToolbar` | One left-side **Grow** entry that opens a menu; no Play mode container changes |

### Hierarchy component icons

The row callback receives the row rectangle, so the icons need no tree internals. Icons are packed
from the right edge of the row in component order, and the visible count is limited by the end of the
object name; a narrow Hierarchy shows fewer icons. The icon set is cached per object and rebuilt on
`hierarchyChanged`.

With **Open Inspector on icon click** off, icons are display-only and an icon click behaves like a
normal row click. With the option on, a plain left click opens Unity's focused Inspector for that
component (`PropertyEditor.OpenPropertyEditor`) through a delayed call and consumes the event. Any
modifier, a right click or a click outside the icon rect leaves the event to the Hierarchy. If the
internal entry point cannot be resolved, the fallback selects the component and opens the Inspector
window.

### Hierarchy active toggle

`ItemActiveToggle` pins a 16-pixel checkbox two pixels from the row's right edge. Component icons
subtract its reserved width, so the two never overlap. A plain left click records Undo, calls
`GameObject.SetActive`, marks the object dirty and consumes the event; the checkbox reflects
`activeSelf`. The toggle is available on separator rows as well, because those are ordinary
GameObjects.

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
entirely. Creation uses `GameObject > Grow > Create Separator...`, the Hierarchy context menu or the
toolbar menu while both the separator feature and its Create Separator menu switch are on. A small
modal window edits the title, background, automatic/custom text color and insertion above or below
the selection, with a live preview. With no scene selection it appends to the active scene. The new
object is selected and pinged. The command is disabled while a Prefab Stage is open, because a
separator is scene content and must not be written into a prefab asset. With multiple scenes loaded,
the object is created in the selected object's scene.

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

The line style is configurable while indent guides are enabled: `Dashed guides` selects the fixed
dashed pattern or a solid line. `Guide color` selects a custom color and `Reset` restores the
current editor theme's text color at alpha 0.32. Line width, dash length and gap stay fixed so the connector column remains
aligned with the foldout triangle. Vertical lines are pixel-snapped around the guide X. The parent
vertical is split around the arm's pixel row, and the horizontal arm starts at the vertical's left
edge, so the arm owns the corner pixel: a T/L joint neither overlaps by half a line nor leaves a
missing corner.

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
- English and Simplified Chinese are bundled; adding another language means adding a locale JSON file
  under `Editor/Localization/Locales`.

## Testing

The package has an EditMode test assembly for the parts that do not need IMGUI: the settings JSON
round-trip, feature-switch defaults and legacy migration, language selection and fallback, separator
parsing, component icon layout, connector geometry, the reflection probes against the 2022.3 internal
members, and Hierarchy path helpers. Drawing, clicks, menus and the toolbar are checked manually in
the editor; the automated suite cannot drive them.

Manual smoke checklist for Unity 2022.3:

1. Enable Grow Lens under **Preferences > Grow > Lens**; the toolbar entry appears without a restart.
2. Open the **Grow** toolbar menu and check Preferences, Console and Take Screenshot.
3. Select a material or texture and confirm the Inspector header actions; select a Hierarchy object
   and confirm component icons, the right-edge active toggle and tree connectors.
4. Turn **Open Inspector on icon click** off and click a component icon; the GameObject is selected
   as usual. Turn it on and click the icon; the focused Inspector opens. Confirm Undo on the active
   toggle, and that modifier/right clicks still belong to the Hierarchy.
5. Ctrl+right-click a Project item and a Hierarchy row; confirm the menus act on the clicked item.
6. Turn **Create Separator menu** off and confirm all three creation entries are unavailable; turn it
   on, open the separator editor, set a title and colors, insert above the selection and confirm the
   preview, row and native selected state.
7. Search the Hierarchy and confirm separators and connectors disappear.
8. Turn individual feature switches off and confirm only that subsystem disappears; turn the master
   switch off and confirm the Grow entry and every decoration disappear.
9. Change the language in **Preferences > Grow > General** and confirm the Lens page, context menu,
   toolbar menu and asset header update without a restart.
10. Enter Play mode, change the editor layout, and confirm the Grow entry re-attaches without
    duplicate buttons.
11. Run `pwsh ci/run-tests.ps1 -Mode EditMode`; batch mode must not install callbacks.

## See also

- [Editor inspector](editor-inspector.md) — the annotation-driven Inspector.
- [Architecture](../architecture/README.md) — where the editor assemblies sit in Grow.
- [Glossary](../reference/glossary.md) — terms shared across these pages.
