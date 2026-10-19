# Editor inspector

**Read this in other languages:** [English](editor-inspector.md) | [简体中文](../../../zh-CN/framework/design/editor-inspector.md)

*Design record* — the annotation-driven Inspector shipped with `com.ronny.grow`.

This design covers the authoring attributes in `Grow.Core.Attributes` and the editor side in
`Grow.Editor.Inspector`. The optional `com.ronny.grow.lens` tool package has its own
[design record](editor-lens.md).

## Goals

- Let a serialized field declare its label, grouping, order, condition, read-only state and value
  callback once, next to the field.
- Let a method become a button with typed parameters, without writing a custom Editor.
- Keep the default Inspector usable: every attribute degrades to a no-op unless a Grow drawer or a
  property drawer handles it.
- Separate the draw workflow (`GrowEditorBase`) from the annotation model (`GrowEditor`) so a fully
  custom panel can reuse the lifecycle without inheriting the model.
- Keep the package free of Store, third-party libraries and a global enum drawer.

## Non-goals

- Replacing Unity's Inspector or implementing a general-purpose serialization/editor framework.
- A full expression language: conditions support a field path, a method call, negation and a single
  equality comparison.
- Automatic background saving. Saving is an explicit `SaveAll()` action.
- A project-wide `[Flags]` enum drawer. `GrowEnumFlags` is opt-in per field.
- Custom drawing for every Unity type; the model targets root-level serialized fields.

## Two tiers

| Tier | Attributes | Works in the default Inspector |
| --- | --- | --- |
| Property / decorator drawers | `GrowLabel`, `GrowReadOnly`, `GrowHelpBox`, `GrowSeparator`, `GrowEnumFlags` | Yes |
| Driver-only | `GrowPropertyOrder`, `GrowGroup`, `GrowShowIf` / `GrowHideIf` / `GrowEnableIf` / `GrowDisableIf`, `GrowOnValueChanged`, `GrowButton` | No; requires `GrowEditor` |

Driver-only attributes are plain `Attribute` types, so the default Inspector ignores them without
errors. The drawer attributes derive from `PropertyAttribute` and are handled by `PropertyDrawer` or
`DecoratorDrawer` classes. The property drawers chain in a fixed order — `GrowLabel` (`-1000`) →
`GrowReadOnly` (`-500`) → `GrowEnumFlags` (default order) — so a field may carry a label, a
read-only state and the flags drawer together; each drawer forwards to the next through
`PropertyField`. `GrowHelpBox` and `GrowSeparator` are decorator drawers and draw above the field
without changing how the field itself is drawn.

## Workflow

`GrowEditorBase : Editor` owns the draw pipeline:

1. On the first `Layout`, run `OnSetup` once and subscribe to `beforeAssemblyReload` for a one-shot
   `OnEditorDestroy`.
2. On every `Layout`, run `OnCalc`.
3. For each frame, draw the read-only script header, call `OnDraw` inside a change check, apply
   pending `SerializedObject` edits and raise `OnChanged` when this frame produced a change. A target
   that became dirty during the frame — an immediate apply or a `[GrowButton]` call — counts as a
   change too.
4. `OnDisable` calls `OnDisabled` and applies pending edits; it does not write files.
5. `SaveAll()` applies pending edits and writes the dirty persistent target assets, prefab instances
   when `ApplyPrefabInstanceToAsset` is enabled, and loaded dirty scenes when `SaveScene` is enabled.
   Unrelated dirty assets are not saved.

The base class deliberately has no scheduler and no automatic write path. A delayed save scheduler
needs instance-level state and a tested write path before it can be shipped; a manual action is
predictable and testable.

## Annotation model

`InspectorTypeModel` builds a model per target type:

- root-level visible properties except `m_Script` become `FieldModel`s; the model keeps the
  serialized order as the declaration tie-break;
- instance `void` methods with `GrowButton` become `ButtonModel`s. Static, generic and non-`void`
  methods are skipped; a method with an unsupported parameter type is skipped with one warning;
- fields and buttons share one sort space: explicit `GrowPropertyOrder` first, then fields before
  buttons, then serialized order for fields and method name for buttons. A member without an order
  uses `int.MaxValue`, so an unannotated button lands after the fields;
- `GrowGroup` paths are split on `/`, trimmed, stripped of empty segments, merged into a group tree,
  and emitted as an ordered token stream (`FieldToken`, `ButtonToken`, `GroupToken`). A group is a
  single contiguous block anchored by its earliest member;
- a leaf group is a `Box` when any direct member asks for `Box`; a group with children is always a
  `Foldout`.

The model is cached per type and cleared by `ClearCache()` (tests) or by a domain reload. Foldout
state is view state: it lives on the editor instance, not in the model.

## Conditions

`GrowShowIf` / `GrowHideIf` control visibility; `GrowEnableIf` / `GrowDisableIf` control
editability. The expression syntax is:

| Form | Meaning |
| --- | --- |
| `Field` | Truthy: `true`, a non-zero number or enum, a non-empty string, or a non-null object |
| `!Field` | Negated |
| `Field == value` | Equality with a number, `true` / `false`, an enum member name or value, a string, or `null` |
| `Method()` | Calls a parameterless method and tests its result; `Method() == value` compares it |

A field path may be dotted (`Stats.Enabled`). Enum member names are case-sensitive. There is one
comparison per attribute and no `!=`, `&&`, `||`, range or arithmetic support.

Combination rules:

- `ShowIf` and `EnableIf`: every attribute of that kind must match.
- `HideIf` and `DisableIf`: the field is hidden or disabled as soon as one attribute matches.
- A clause that cannot be resolved is ignored, so a typo never hides or disables a field. There is no
  warning for an unresolved clause; an identical `ShowIf` and `HideIf` expression logs one warning
  when the model is built.

With multiple targets, visibility is aggregated: all targets visible draws normally, some visible
draws disabled (mixed), none visible hides the field. Editability requires every target to be
editable. A clause is evaluated with reflection against each target, so it works without a serialized
object and is covered by EditMode tests.

## Buttons

`GrowButton` on an instance method draws a card with a title and an `Invoke` button. Supported
parameter types are `int`, `long`, `float`, `double`, `bool`, `string` and enums. The title is the
attribute label, falling back to the method name.

- A method with parameters starts collapsed; expanding it reveals one typed input per parameter.
- Values are kept in the renderer and persisted under `EditorPrefs` keys built from the target type's
  full name, the method signature and the parameter index, so overloads do not collide. Nothing is
  written until `Invoke` is clicked; before that, the type default is used.
- A `[Flags]` enum parameter reuses the same hierarchical multi-select as a field; a non-flags enum
  parameter uses Unity's native popup.
- The method is invoked once on every selected target, in order, with `Undo.RecordObject` per target.
  An exception is caught and logged; the remaining targets still run. Afterward the owner updates the
  `SerializedObject`, marks every target dirty, reports a change and repaints.

## Enum flags

`GrowEnumFlags` draws `[Flags]` enums as a hierarchical multi-select with search, tri-state group
rows and `Nothing` / `Everything` entries. Search matches the full `value : Name : Label` text,
keeps the parent group of each match, and hides `Nothing`, `Everything` and unrelated groups. Only
positive single-bit members participate; zero,
negative and multi-bit combination values are excluded from the checkable leaves. A multi-bit member
whose bits are all presented can appear as a whole-toggle combination row when `showCombos` is `true`
(the default). `[Obsolete]` members stay visible and disabled so bits are not lost. The `Nothing` and
`Everything` labels come from `None` / `Nothing` / `All` / `Everything` members when the enum defines
them, and fall back to `Nothing` / `Everything`.

There is no global enum drawer. Unannotated enums use Unity's native rendering, including its native
`EnumFlagsField`. The drawer applies to a direct `[Flags]` enum field; arrays and `List<T>` fields
fall back to native drawing, and a non-`[Flags]` enum logs a warning.

## Public API and naming

Every attribute carries the `Grow` prefix (`GrowButton`, `GrowLabel`, `GrowGroup`, ...), so the
package can coexist with Odin Inspector, NaughtyAttributes and similarly named attributes in the
same file. `GrowLabel` applies to serialized fields, including enum members; a button title uses
`GrowButton(label)`.

`GrowEditor` and `GrowEditorBase` are the intended extension points:

- `GrowEditor` adds the annotation model, `DrawAnnotatedField(path)` for drawing one field outside the
  token order, and `OnDraw` overrides that append to `base.OnDraw()`.
- `GrowEditorBase` adds the lifecycle (`OnSetup`, `OnCalc`, `OnDraw`, `OnChanged`, `OnDisabled`,
  `OnEditorDestroy`), `ForceWideMode`, `SaveScene`, `ApplyPrefabInstanceToAsset`, `MarkChanged()` and
  `SaveAll()`.

The model, condition evaluator, button renderer, drawers and enum helpers are implementation details
of the editor assembly.

## Limits

- Only root-level fields are tokenized; fields inside a nested serializable class are not annotated
  by the driver attributes. Drawer attributes are ordinary property drawers and still apply wherever
  Unity draws the field.
- Condition expressions do not support boolean operators, ranges, arithmetic or `!=`.
- The workflow draws a script header before `OnDraw`; a fully custom panel that needs a different
  header must account for it.
- `GrowLabel` on an enum member is read by the `GrowEnumFlags` tree; Unity's native enum popup, used
  for non-flags button parameters, does not display it.
- `EditorPrefs` is used for button parameter buffers. It is editor UI state, not game data; the keys
  are user-global (shared across projects on Windows) and values are written when the button is
  invoked.
- Inspector text is English. The package localization catalogs cover the Settings pages only.

## Testing

EditMode tests cover the parts without IMGUI: the type model (ordering, grouping, tooltips, button
collection), condition parsing and evaluation, multi-target aggregation, button parameter encoding and
signatures, and the enum-flag display data. Drawer rendering, saving, undo and clicks are checked by
hand in the editor. Run the suite with `pwsh ci/run-tests.ps1 -Mode EditMode`.

## See also

- [Editor lens](editor-lens.md) — the optional editor tool package.
- [Architecture](../architecture/README.md) — where the editor assemblies sit in Grow.
- [Glossary](../reference/glossary.md) — terms shared across these pages.
