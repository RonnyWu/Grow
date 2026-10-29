# Grow Lens

Optional editor productivity tools for Grow: Hierarchy component icons, formatted separators, tree
connectors, asset header actions, context menus and a toolbar menu.

The package is editor-only and has no runtime code. Nothing is subscribed until you enable Grow
Lens; each feature can then be switched off independently.

## Requirements

- Unity **2022.3**.
- The project package `com.ronny.grow.lens`, present in this repository at
  `Packages/com.ronny.grow.lens`. The package is not published to a registry.
- Editor-only. Lens depends on `com.ronny.grow` for the shared settings-page shell and language
  service, and installs no runtime code of its own.

## Enable

Open **Edit > Preferences > Grow > Lens** and turn on **Enable Grow Lens**. The category is **Grow**
and the page label is **Lens**, so the tree reads Grow > Lens rather than repeating the product
prefix.

- The master switch is off by default. Settings are grouped into **Inspector enhancements**,
  **Project enhancements**, **Hierarchy enhancements** and **Toolbar enhancements**. Each group row
  is a derived batch switch for its children; **Hierarchy enhancements** shows all-on, all-off or
  the mixed marker. Children keep their own switches.
- Hierarchy includes an optional **Active toggle** drawn at the right edge of every row. It toggles
  the GameObject's `activeSelf` with Undo and reserves space before the component icons.
- The settings are per project and stored in `UserSettings/Grow/Lens.json`, outside `Assets/`, so
  they are not imported into the project and do not ship in a build. This repository also ignores
  `UserSettings/`, so the file is not committed here.
- The default is off. A missing or unreadable file also reads as off. A file written by the older
  single-switch format keeps all features enabled when the master switch is on.
- Feature switches remain editable while the master switch is off; their saved values apply as soon
  as Lens is enabled again.
- The change applies immediately; no domain reload is needed.
- Batch mode never subscribes, so a headless test run installs no editor callbacks.

Turning the master switch off removes every callback and the toolbar entry. Turning an individual
feature off removes only that feature; for example, separators can be disabled while component icons
and indent guides stay on. The `GameObject > Grow > Create Separator` item is disabled while
separators are off.

## Language

Lens follows the language selected in **Preferences > Grow > General**: English and Simplified
Chinese are bundled. The settings page, context menus, toolbar menu and asset header update on the
next repaint after the language changes.

## Hierarchy component icons

A Hierarchy row that belongs to a GameObject gets its component icons packed from the right edge, in
component order, so the first component is the rightmost icon and later components extend leftwards.
A separator row shows no component icons. Only the icons that fit between the row's right edge and
the end of the object name are drawn, so a long name or a narrow window shows fewer icons. Hovering
an icon sets the row tooltip to the component type; a missing script uses the warning icon and the
`Missing Script` tooltip.

- A plain left click opens a focused Inspector window for that component, the same window the
  component's **Properties...** menu item opens. The event is consumed, so the clicked object is not
  selected and the current selection stays as it was.
- Ctrl, Shift, Alt and Command clicks, and right clicks, are left to the Hierarchy; they select,
  multi-select or open the Hierarchy context menu as usual.
- If Unity's internal focused-Inspector API cannot be resolved, the click falls back to selecting the
  component and opening the Inspector window. That fallback does change the selection.
- The icon set is cached per object and rebuilt on `hierarchyChanged`.

## Hierarchy active toggle

When enabled, each Hierarchy row gets a checkbox pinned to its right edge. The checkbox reflects the
GameObject's `activeSelf`; a plain left click records Undo and calls `SetActive`. Component icons
reserve the right-edge space, so they never overlap the toggle.

## Hierarchy separators

A GameObject whose name uses the separator format is drawn as a colored section header. The row is
display-only: Lens never reparents children or edits the GameObject.

| Name | Background | Text color |
| --- | --- | --- |
| `##2E7D32 Level A` | `#2E7D32` | picked automatically from the background |
| `##F00 Title` | `#FF0000` (3-digit form is expanded) | picked automatically |
| `##2E7D32#FFFFFF Level A` | `#2E7D32` | `#FFFFFF` |
| `##-#FFD54F Note` | the editor theme background | `#FFD54F` |
| `##- Note` | the editor theme background | picked automatically |
| `##383838` | `#383838` | none; an empty band |

- A color is 3 or 6 hex digits and has no alpha channel. The optional text color follows the
  background color with another `#`.
- `##-` selects the theme background instead of a color, and may still be followed by a text color.
- Text is everything after the color, trimmed. To make the text start with `#`, separate it from the
  color with a space.
- Anything that does not match the format is an ordinary GameObject name. In particular, the
  single-`#` form is not recognized, and neither is a 5-digit color.
- A selected separator yields to the native row: the native background, name and selection highlight
  stay visible so the object can be renamed. The separator text survives a rename only if the name
  keeps the format.
- Hierarchy search skips separator rendering, so search results show the raw GameObject names.
- Creating one: **GameObject > Grow > Create Separator**, **Create Separator** in the Hierarchy
  context menu, or **Create Separator** in the toolbar menu. The new object is named
  `##383838 Section`, becomes a sibling after the active object (or a root object when no scene
  object is active), and is selected and pinged. The command is disabled while a Prefab Stage is
  open, because a separator is scene content and must not be written into a prefab asset. When
  several scenes are loaded, the separator is created in the active object's scene.
- A separator is a normal GameObject with only a Transform. It is saved with the scene and is
  present at runtime and in a build; remove it from the scene or strip it in a build step if that
  matters.

## Hierarchy tree connectors

Ancestor columns get T and L connectors with horizontal arms, drawn from the live `TreeViewGUI`
geometry and the visible row depths:

- A T shape means the branch continues through a following sibling; an L shape marks the last child.
- The current row's own foldout column is never used as a vertical guide.
- **Dashed guides** switches between the fixed dashed pattern and a solid line. **Guide color**
  selects a custom color; **Reset** restores the theme-derived color. The controls appear while
  indent guides are enabled.
- Horizontal arms start at the vertical line's left edge, and the vertical is split around the arm's
  pixel row, so the arm owns the corner pixel while avoiding overlap or a missing corner.
- Search results draw nothing, and neither does a layout whose geometry cannot be resolved; that case
  logs one warning instead of guessing pixels.

Connectors are the only feature that needs the Hierarchy tree geometry. Component icons and
separators depend only on the row rectangle and the object.

## Asset header actions

For a single selected asset, the Inspector header gets a row of actions after Unity's own header:

| Action | Behaviour |
| --- | --- |
| `Copy Full Path` | Copies the absolute path on disk. |
| `Copy Asset Path` | Copies the project-relative path. |
| `Copy GUID` | Copies the asset GUID. |
| `Meta` | Opens the `.meta` file in Rider. |
| `File` | Opens the asset file in Rider; disabled for folders and other `DefaultAsset` items. |

The row is hidden in these cases:

- more than one object is selected;
- the object has no asset path or no GUID;
- the type is `GameObject`, `MonoScript`, `TextAsset`, `Shader`, `Font`, `AudioClip` or
  `ComputeShader`, or derives from one of them — those importers already draw their own header.

The `Meta` and `File` buttons appear only when the Rider package integration can be resolved. Without
Rider the copy actions remain.

## Context menus

Hold Ctrl and right-click (Command on macOS) in the Project or Hierarchy window. The clicked item is
selected first if it was not selected already, so actions that use the selection act on it.

### Project window

- `Copy Name`, `Copy Path`, `Copy GUID`
- `Show in Explorer`
- `Create Folder`, `Create Scene`, `Create Material`, `Create Prefab`, `Create Prefab Variant`
- `Delete`

The copy, reveal and delete items are disabled when the clicked item has no asset identity.

### Hierarchy window

- `Copy Name`, `Copy Path` (root-to-leaf path, e.g. `Root/Child/Leaf`), `Copy GUID`
- `Create Empty And Reset`, `Create Empty Child And Reset`, `Create Empty Parent And Reset` — run
  Unity's own create command and then reset the new object's local position, rotation and scale
- `Create Separator`
- `Delete`

`Copy GUID` is disabled for a plain scene object, which has no asset identity; on a prefab instance
it copies the nearest prefab asset's GUID. `Delete` runs Unity's **Edit > Delete** and adds no
confirmation of its own.

The menu items call Unity APIs or Unity's own menu items directly, and drawing never modifies
hierarchy or asset data.

## Toolbar menu

Lens adds one **Grow** entry to the left side of the main toolbar. It opens a menu:

- `Preferences`, `Project Settings`, `Build Settings`
- `Package Manager`, `Console`, `Profiler`, `Memory Profiler`, `Frame Debugger`, `Input Debugger`
- `Open C# Project`
- `Create Separator`
- `Take Screenshot`, `Open Screenshots Folder`

`Take Screenshot` writes `screenshot_HHmmss_fff.png` to the project root's `Screenshots/` folder,
which this repository ignores. The entry re-attaches after the toolbar is rebuilt; it does not add a
row of buttons and does not touch the Play mode controls.

## Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| No feature appears | The master switch or that specific feature is off, or the editor is in batch mode. | Enable Lens and the feature under **Preferences > Grow > Lens**. |
| The page is read-only | The settings file is damaged, was written by a newer version, or is marked read-only. | Check the warning at the top of the page; fix or remove `UserSettings/Grow/Lens.json` and reopen the page. |
| A change did not stick | The write failed; the page shows an error and keeps the previous values. | Check the Console for the file error, fix the path or permissions, and change the setting again. |
| Hierarchy decorations are missing | Another editor tool is drawing over the row, or the project is in search mode. | Clear the Hierarchy search; disable the other row tool. |
| Connectors are missing | The Hierarchy tree geometry cannot be resolved. | Check the Console for the one-time warning; icons and separators are unaffected. |
| A separator is not colored | The name does not match the format. | Use `##RRGGBB[#RRGGBB]Text` or `##-`. |
| Component icons show an old set | The icon cache is rebuilt on `hierarchyChanged`. | Change the Hierarchy, or reopen the scene. |
| No asset header actions | The asset type is excluded, or several objects are selected. | Select one supported asset. |
| Rider buttons are missing | The Rider package integration could not be resolved. | Install `com.unity.ide.rider`; the copy actions still work. |
| Lens disabled itself | The same drawing feature failed three times in a row. | Read the Console error, then toggle Lens off and on or restart the editor; the saved preference is unchanged. |

## Failure containment

Every row callback and menu action runs through a guard. The first failure of a drawing feature
logs once; three consecutive failures of the same feature disable Lens for the current editor
session and remove every callback, so a broken Unity internal cannot fill the Console every repaint.
The saved preference is left enabled: restarting the editor, or toggling Lens off and on, retries
with a clean failure count. Menu actions log their own errors but never disable the package.

## Limits

- Unity's internal toolbar, Hierarchy window and Inspector header shapes are not public contracts. A
  Unity upgrade can disable the affected feature; the failure containment keeps it from repeating.
- Context menus require Ctrl+right-click, or Command+right-click on macOS.
- Separators are scene objects, not editor-only annotations; they exist at runtime.
- English and Simplified Chinese are bundled; add a locale JSON file under
  `Editor/Localization/Locales` to add another language.
- Automated tests cover the pure parts — settings and feature-switch round-trips, legacy migration,
  language selection, separator parsing, icon layout, guide geometry, reflection probes and
  Hierarchy path helpers. Drawing and clicking are checked by hand.

## Check it works

1. Enable Grow Lens under **Preferences > Grow > Lens**; the toolbar entry appears without a restart.
2. Open the **Grow** toolbar menu and run `Preferences`, `Console` and `Take Screenshot`.
3. Select a material or texture and confirm the Inspector header actions.
4. Select a Hierarchy object and confirm the component icons and tree connectors.
5. Click a component icon with a plain left click; the focused Inspector opens and the selection does
   not change. Ctrl-click and right-click still belong to the Hierarchy.
6. Add a separator with **GameObject > Grow > Create Separator**, rename it to
   `##2E7D32#FFFFFF Section`, and confirm the row, then select it and confirm the native row returns.
7. Press Ctrl+right-click on a Project item and on a Hierarchy row; confirm the menus act on the
   clicked item.
8. Search the Hierarchy and confirm separators and connectors disappear while the results are shown.
9. Turn individual feature switches off and confirm only that subsystem disappears; turn the master
   switch off and confirm the toolbar entry and every decoration disappear.
10. Change the language in **Preferences > Grow > General** and confirm the Lens page, menus and
    asset header update without a restart.
11. Run `pwsh ci/run-tests.ps1 -Mode EditMode`; batch mode must install no callbacks.

## See also

- [Grow](../../com.ronny.grow/Documentation~/index.md) — the kernel package.
- [Editor inspector](../../com.ronny.grow/Documentation~/editor-inspector.md) — the annotation-driven
  Inspector.
- [Grow Lens changelog](../CHANGELOG.md)
