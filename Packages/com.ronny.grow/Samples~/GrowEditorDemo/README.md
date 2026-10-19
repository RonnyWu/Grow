# Grow Editor Demo

A small Unity sample for the annotation-driven Inspector of `com.ronny.grow`
(`Grow.Core.Attributes` plus `Grow.Editor.Inspector`).

The sample takes one topic per component. The scene `GrowEditorDemo.unity` mounts all seven
components under a single root so each panel can be inspected in turn. The scene has no camera or
light: it is meant to be read in the Inspector, not played.

## Requirements

- Unity **2022.3**.
- `com.ronny.grow`, imported from this repository.
- No other package.

## Import and open

1. Open **Window > Package Manager**, select **Grow**, and import **Grow Editor Demo** from the
   package's Samples list. Unity copies the sample under `Assets/Samples/`.
2. Open `GrowEditorDemo.unity` from the imported folder.
3. Select the children of `Grow Editor Demo` one at a time and inspect the panel.

The components are also available from **Add Component > Grow > Samples > Grow Editor Demo > ...**
on any GameObject.

## What each component shows

| Child object | Component | Topic | Main attributes | Drawn by |
| --- | --- | --- | --- | --- |
| `01 Field Decoration (default inspector)` | `FieldDecorationDemo` | Field decoration | `GrowLabel`, `GrowHelpBox`, `GrowReadOnly`, `GrowSeparator`, plus Unity `Tooltip` and `Range` | The default Inspector; the component deliberately has no custom Editor |
| `02 Enum Flags` | `EnumFlagsDemo` | Enum drawing | `GrowEnumFlags`, `GrowLabel` on enum members | `GrowEditor` |
| `03 Group Layout` | `GroupLayoutDemo` | Grouping and order | `GrowGroup` with `Foldout` / `Box` and a nested path, `GrowPropertyOrder` | `GrowEditor` |
| `04 Conditional Fields` | `ConditionalFieldDemo` | Visibility and editability | `GrowShowIf`, `GrowHideIf`, `GrowEnableIf`, `GrowDisableIf` | `GrowEditor` |
| `05 Value Changed` | `ValueChangedDemo` | Value callbacks | `GrowOnValueChanged` | `GrowEditor` |
| `06 Button Actions` | `ButtonActionDemo` | Method buttons | `GrowButton` with zero, one and three parameters; `GrowGroup(..., Box)` mixing fields and buttons | `GrowEditor` |
| `07 Custom Workflow` | `CustomWorkflowDemo` | Fully custom panel | No annotations; hand-written IMGUI | `GrowEditorBase` |

Topics worth comparing:

- `01` shows the drawer tier: its attributes work without a custom Editor. The other annotated demos
  add the driver tier — ordering, grouping, conditions, callbacks and buttons — on top of the same
  drawers.
- `02` puts an annotated `[Flags]` field next to an unannotated one; the second keeps Unity's native
  drawing. The enum member labels use `/` to form groups in the multi-select tree.
- `03` defines several groups with one nested path, mixes `Foldout` and `Box`, and places one field
  with `GrowPropertyOrder(-10)` above everything else.
- `04` covers a bool condition, a negated condition, an enum equality, a method call and a
  `DisableIf`.
- `05` writes the callback result into a read-only field, so the effect of each edit is visible in
  the same panel.
- `06` shows that buttons and fields share ordering and grouping: the boxed debug group contains both
  a field and a button. Button arguments persist in `EditorPrefs`, which is user-global editor state,
  not scene data.
- `07` does not use the annotation model at all. It shows `ForceWideMode`, the `OnCalc` / `OnChanged`
  lifecycle, a hand-written `SaveAll()` button and a `SaveScene` switch that includes the scene in
  that save.

## How the driver editors are wired

Five of the seven components derive from the abstract `AnnotationDemoBehaviour`, and one editor
covers all of them:

```csharp
[CustomEditor(typeof(AnnotationDemoBehaviour), true)]
public sealed class AnnotationDemoEditor : GrowEditor {
    protected override void OnDraw() {
        base.OnDraw();
        // Appends the component's one-line summary as a help box.
    }
}
```

`FieldDecorationDemo` derives from `MonoBehaviour` directly so that no Grow editor applies to it;
`CustomWorkflowDemo` has its own `GrowEditorBase` subclass. The `DemoSummary` property on the base
class supplies the one-line help box at the bottom of each annotated panel. The base class is
abstract, declares no fields and cannot be attached to a GameObject.

## Condition syntax in the sample

`04 Conditional Fields` uses the general expression syntax:

| Expression | Meaning |
| --- | --- |
| `Advanced` | A truthy value: `true`, a non-zero number or enum, a non-empty string, a non-null object |
| `!Advanced` | Negated |
| `Difficulty == Hard` | Equality with an enum member name (case-sensitive); numbers, strings, `true` / `false` and `null` are also accepted |
| `IsReady()` | Calls a parameterless method and tests its result; the method may be private |

Each attribute holds one expression; there is no `&&`, `||`, `!=`, range or arithmetic. Several
`GrowShowIf` attributes must all match, while one matching `GrowHideIf` hides the field. A clause that
does not resolve is ignored rather than hiding the field. With several objects selected, a field that
is visible on only some targets is drawn disabled.

## Directory layout

```text
GrowEditorDemo/                 sample root
  GrowEditorDemo.unity          demo scene
  Scripts/
    AnnotationDemoBehaviour.cs  abstract base shared by the five annotation demos
    FieldDecorationDemo.cs
    EnumFlagsDemo.cs
    GroupLayoutDemo.cs
    ConditionalFieldDemo.cs
    ValueChangedDemo.cs
    ButtonActionDemo.cs
    CustomWorkflowDemo.cs
  Editor/
    AnnotationDemoEditor.cs
    CustomWorkflowDemoEditor.cs
```

Runtime scripts use the namespace `Grow.Samples.GrowEditorDemo`; editor scripts use
`Grow.Samples.GrowEditorDemo.Editor`.

## Optional editor tooling

The optional `com.ronny.grow.lens` package adds Hierarchy component icons, tree connectors,
formatted separators, asset header actions, context menus and a toolbar menu. It is off by default
and is enabled under **Preferences > Grow > Lens**. This sample does not depend on it.

## Notes

- The sample has no automated tests; it is checked by opening the scene and inspecting the panels.
- Button argument values are stored per user in `EditorPrefs` and are shared with other projects on
  the same machine.
- Opening `04` with `Advanced` off, `Difficulty` set to `Hard` and `Ready` off is the fastest way to
  see every condition branch change at once.
