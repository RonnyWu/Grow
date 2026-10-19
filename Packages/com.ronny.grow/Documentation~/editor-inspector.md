# Editor inspector

Grow ships an optional, annotation-driven Inspector. You describe a serialized type with attributes
next to its fields and methods; a small editor base class draws the resulting panel. The feature is
per type: a type without a Grow editor keeps Unity's default Inspector, and no attribute changes
global Inspector behaviour.

The attributes live in `Grow.Core.Attributes` (runtime assembly, no editor dependency). The drawers
and editors live in `Grow.Editor` (editor-only, auto-referenced by Unity's predefined assemblies).

## Two tiers

| Tier | Attributes | Drawn without a custom Editor |
| --- | --- | --- |
| Drawers | `GrowLabel`, `GrowReadOnly`, `GrowHelpBox`, `GrowSeparator`, `GrowEnumFlags` | Yes |
| Driver-only | `GrowPropertyOrder`, `GrowGroup`, `GrowShowIf`, `GrowHideIf`, `GrowEnableIf`, `GrowDisableIf`, `GrowOnValueChanged`, `GrowButton` | No — requires an Editor derived from `GrowEditor` |

Driver-only attributes are plain `Attribute` types, so the default Inspector ignores them without an
error. Drawer attributes derive from `PropertyAttribute` and are handled by `PropertyDrawer` or
`DecoratorDrawer` classes. When several drawers target one field they chain in a fixed order —
`GrowLabel`, then `GrowReadOnly`, then `GrowEnumFlags` — so a field can carry a label, be read-only
and use the flags drawer at the same time. `GrowHelpBox` and `GrowSeparator` are decorators: they
draw above the field and do not change how the field itself is drawn.

## Drawers only

No custom Editor is needed for the drawer attributes:

```csharp
using Grow.Core.Attributes;
using UnityEngine;

public sealed class PlayerConfig : MonoBehaviour {
    [GrowLabel("Player name")]
    public string PlayerName = "Player";

    [GrowReadOnly]
    [Tooltip("Assigned by the build pipeline.")]
    public string BuildId;

    [GrowHelpBox("Shown above the level field.", GrowHelpBoxKind.Info)]
    [GrowSeparator]
    [Range(1, 99)]
    public int Level = 1;
}
```

| Attribute | Effect |
| --- | --- |
| `GrowLabel(string)` | Replaces the field label. An empty label falls back to the serialized name. On an enum member it supplies the name used by `GrowEnumFlags`. |
| `GrowReadOnly` | Draws the field disabled; the value is still serialized and can be written by code. |
| `GrowHelpBox(string, GrowHelpBoxKind)` | Draws an information, warning or error box above the field. |
| `GrowSeparator(float thickness = 1, float margin = 6)` | Draws a horizontal rule above the field. Negative values are clamped to zero. |
| `GrowEnumFlags(bool showCombos = true)` | Draws a `[Flags]` enum as a hierarchical multi-select; see **Enum flags** below. |

## A full panel

Derive an editor from `GrowEditor` and let the annotation model draw the panel:

```csharp
using Grow.Core.Attributes;
using Grow.Editor.Inspector;
using UnityEditor;
using UnityEngine;

public sealed class WeaponConfig : ScriptableObject {
    [GrowGroup("General")]
    [GrowLabel("Display name")]
    public string DisplayName = "Sword";

    [GrowGroup("General")]
    [GrowPropertyOrder(10)]
    public bool TwoHanded;

    [GrowGroup("Stats", GrowGroupKind.Box)]
    [GrowShowIf("TwoHanded")]
    [GrowLabel("Two-handed damage")]
    public float Damage = 12f;

    [GrowButton("Reset stats")]
    [GrowGroup("Stats", GrowGroupKind.Box)]
    public void ResetStats() {
        Damage = 0f;
    }
}

[CustomEditor(typeof(WeaponConfig))]
public sealed class WeaponConfigEditor : GrowEditor {
}
```

The resulting panel contains the read-only script header, then the annotated fields and buttons in
order, with groups, visibility conditions and callbacks applied. A type hierarchy can share one
editor by registering it on the base type with inheritance enabled:

```csharp
[CustomEditor(typeof(WeaponConfigBase), true)]
public sealed class WeaponConfigBaseEditor : GrowEditor {
}
```

To add custom content around the annotated fields, override `OnDraw`, call `base.OnDraw()` first and
append the extra GUI. To draw a single annotated field elsewhere in a custom panel, call the
protected `DrawAnnotatedField` with a property path or a `SerializedProperty`.

## Ordering and groups

- `GrowPropertyOrder(int)` sets the sort key; smaller values come first. Members without the
  attribute sort last (`int.MaxValue`).
- Fields and `[GrowButton]` methods share one sort space. At equal order, fields come before
  buttons; fields then follow their serialized order and buttons their method name. A button without
  an explicit order therefore appears after the fields.
- `GrowGroup("Path")` groups a field or a button. The path is split on `/`, so `"General/Physics"`
  nests a `Physics` group inside `General`; empty segments are ignored and whitespace is trimmed.
- `GrowGroupKind.Foldout` (the default) draws a collapsible header; `GrowGroupKind.Box` draws a
  framed card. A leaf group is a `Box` when any direct member asks for `Box`; a group with child
  groups is always a foldout.
- Foldouts start expanded. A group with no visible member is not drawn; buttons always count as
  visible for this purpose.
- A field or button without a group is drawn at the root, in the same order as the groups.

## Conditions

`GrowShowIf` and `GrowHideIf` control visibility; `GrowEnableIf` and `GrowDisableIf` control
editability. Each attribute takes one expression:

| Expression | Matches when |
| --- | --- |
| `Field` | The value is truthy: `true`, a non-zero number or enum, a non-empty string, or a non-null object. |
| `!Field` | The truthy test is inverted. |
| `Field == value` | The value equals the literal: a number, `true` or `false`, an enum member name or numeric value, a string, or `null`. |
| `Method()` | A parameterless method returns a truthy value. `Method() == value` compares its result instead. |

- `Field` may be a dotted path such as `Stats.Enabled`.
- Enum member names are matched case-sensitively; a numeric literal works as well.
- There is one comparison per attribute. `!=`, `&&`, `||`, ranges and arithmetic are not supported;
  add another attribute instead.
- A method may be private and may return any type; a method with parameters is not called.
- `ShowIf` and `EnableIf` require every attribute of that kind to match. `HideIf` hides the field as
  soon as one attribute matches; `DisableIf` disables it as soon as one matches.
- A clause that cannot be resolved — a misspelled member or an unparsable literal — is ignored, so
  it never hides or disables a field. Typos are not logged; declaring the same expression in a
  `GrowShowIf` and a `GrowHideIf` logs one warning when the model is built.
- With several objects selected, the field is drawn normally when every target is visible, drawn
  disabled when only some are visible, and hidden when none are; it is editable only when every
  target is editable.

## Value callbacks

`GrowOnValueChanged("Method")` calls a parameterless method on the edited object after the field
changes in the Inspector. The method is resolved on each selected target, public or private, and may
write other fields; the object is registered with Undo before the call. An exception thrown by the
callback is logged and does not stop the other targets.

## Buttons

`GrowButton` exposes an instance method as a card with a title and an `Invoke` button. An optional
label overrides the method name.

- Supported parameter types are `int`, `long`, `float`, `double`, `bool`, `string` and enums. A
  method with any other parameter type is skipped with a warning in the Console; static, generic and
  non-`void` methods are ignored.
- A method with parameters starts collapsed. Expand the title to edit its arguments; `Invoke` works
  in both states.
- Argument values persist in `EditorPrefs` under a key built from the target type's full name, the
  method signature and the parameter index, so overloads keep separate values. The value is written
  when `Invoke` is clicked; before that, the type default is used (the first enum member for enums,
  an empty string, zero or `false`).
- `Invoke` calls the method once on every selected target, in order, with Undo registered per target.
  An exception is logged and the remaining targets still run. The targets are then marked dirty and
  the Inspector repaints.
- A `[Flags]` enum argument uses the same hierarchical multi-select as `GrowEnumFlags`.

## Enum flags

`GrowEnumFlags` is opt-in per field; there is no project-wide enum drawer, so unannotated enums keep
Unity's native rendering (including `EnumFlagsField` for `[Flags]` enums).

- Only positive single-bit members are checkable. A zero value, a negative value and a multi-bit
  combination are not leaves; a multi-bit member whose bits are all visible can appear as a
  whole-toggle row when `showCombos` is `true`.
- `[Obsolete]` members stay visible but disabled, so existing bits are not lost.
- `Nothing` and `Everything` are pinned at the top. Their labels come from members named `None`,
  `Nothing`, `All` or `Everything` when the enum defines one, and fall back to `Nothing` and
  `Everything` otherwise.
- The tree has a search field and tri-state group rows. Search matches the full
  `value : Name : Label` text, keeps the parent group of each match, and hides Nothing, Everything
  and unrelated groups. A leaf shows the member name with
  `Name = Value` as trailing text, and a `/` in `GrowLabel` splits the member into group levels.
- With several objects selected and different values, the dropdown shows `Mixed…`.
- Annotate a `[Flags]` enum field directly. Arrays and `List<T>` fields fall back to Unity's native
  drawing, and a non-`[Flags]` enum logs a warning.

## Custom panels

`GrowEditorBase` is the reusable workflow for a fully hand-written IMGUI panel; it does not use the
annotation model. Derive from it and implement `OnDraw`:

```csharp
using Grow.Editor.Inspector.Workflow;
using UnityEditor;

[CustomEditor(typeof(MyTool))]
public sealed class MyToolEditor : GrowEditorBase {
    protected override bool ForceWideMode => true;

    protected override void OnDraw() {
        // Draw custom controls here; OnDraw is wrapped in a change check.
    }
}
```

The lifecycle is:

1. `OnSetup` runs once, on the first `Layout` event.
2. `OnCalc` runs on every `Layout` event, before drawing.
3. `OnDraw` runs every frame after the read-only script header. A change made inside it is applied to
   the targets and raises `OnChanged` in the same frame. A target that becomes dirty during the
   frame, for example because a button ran, also raises `OnChanged`.
4. `OnDisabled` runs when the Inspector is disabled; pending edits are applied afterwards.
5. `OnEditorDestroy` runs once, on destroy or before a domain reload.

Overrides control saving: `ForceWideMode` puts labels and fields on one line, `SaveScene` includes
the containing scenes in `SaveAll`, and `ApplyPrefabInstanceToAsset` writes prefab instances back to
their prefab asset. `MarkChanged()` reports a change made outside the automatic change check. The
base class draws the read-only script header through `DrawScriptHeader()`, which subclasses can call
as well.

## Saving

Saving is explicit. There is no background saver and no timer:

- `SaveAll()` applies pending edits and writes the current targets: dirty persistent assets, prefab
  instances when `ApplyPrefabInstanceToAsset` is enabled, and loaded dirty scenes when `SaveScene` is
  enabled. Unrelated dirty assets are not saved.
- Disabling the Inspector applies pending edits to the targets but writes no files; the scene or
  asset stays dirty until Unity or `SaveAll` saves it.

## Limits

- The driver model covers root-level serialized fields only. Fields inside a nested serializable
  class are not individually annotated by `GrowGroup`, conditions, callbacks or ordering. Drawer
  attributes are ordinary property drawers and still apply wherever Unity draws the field.
- Conditions have no boolean operators, ranges or arithmetic.
- The workflow always draws the script header before `OnDraw`; a custom panel that needs a different
  header must account for it.
- `GrowLabel` on an enum member is read by the `GrowEnumFlags` tree. Unity's native enum popup, used
  for a non-flags button argument, shows the member name instead.
- Button arguments are editor UI state, not game data. Their `EditorPrefs` keys are user-global, so
  two projects on the same machine share them.
- Inspector text is English. Localization covers the Settings pages only.

## Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| `GrowGroup`, `GrowShowIf` or `GrowButton` has no effect | The type has no Grow editor, or the editor is registered on a base type without inheritance. | Add `[CustomEditor(typeof(T))]` with a `GrowEditor` subclass; for a base type use `[CustomEditor(typeof(Base), true)]`. |
| Drawer attributes do not appear | A custom assembly definition does not reference `Grow.Editor`. | Add the reference to the editor assembly definition. |
| A `[GrowButton]` method is missing | The method is static, generic or non-`void`, or has an unsupported parameter type. | Use a `void` instance method and the supported parameter types; check the Console warning. |
| A condition is ignored | The member name or the enum literal does not resolve. | Check the dotted path and the case; unresolved clauses are ignored silently. |
| Edits are gone after reopening the project | The asset or scene was never saved. | Call `SaveAll()` or save with Unity. |
| Button arguments reset | `Invoke` was never clicked, so nothing was written to `EditorPrefs`. | Click `Invoke` once with the intended values. |
| `GrowEnumFlags` shows the native popup | The field is an array or `List<T>`, or the enum is not `[Flags]`. | Annotate a direct `[Flags]` enum field. |

## See also

- [Grow](index.md) — the package overview, requirements and editor settings.
- [Grow Editor Demo](../Samples~/GrowEditorDemo/README.md) — a sample with one component per topic.
