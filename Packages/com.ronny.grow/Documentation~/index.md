# Grow

A modular Unity game framework built on a thin kernel.

- **Status:** early planning — the API and structure are not stable yet.
- **Package:** `com.ronny.grow` (embedded in this repository).
- **Unity:** **2022.3 LTS**.

## Overview

Grow separates *what runs a game* from *what a game is made of*.

- **Kernel** — mechanisms only: startup, lifetime, per-frame updates, module
  registration, and scheduling. The kernel owns no game feature and never wraps one.
- **Capability** — a game feature such as assets, saves, UI, or audio. Each capability
  ships as its own package behind a stable contract; no third-party type appears in that
  contract, so the implementation can be replaced without touching callers.

Each layer depends only on the layers below it:

| Layer | Contents |
|---|---|
| Game | Your own application code |
| Framework | Meta package: assembles a recommended set of capabilities |
| Capabilities | Contract + implementation (implementations wrap third-party libraries) |
| Kernel | Mechanisms — entry, modules, service registry, lifetime, loop |
| Core | Passive primitives (e.g. logging); no third-party dependencies |

## Editor inspector

`Grow.Core.Attributes` and `Grow.Editor.Inspector` provide an optional annotation-driven
Inspector. Drawers for `GrowLabel`, `GrowReadOnly`, `GrowHelpBox`, `GrowSeparator` and
`GrowEnumFlags` work in the default Inspector; the driver annotations (`GrowGroup`,
`GrowPropertyOrder`, `GrowShowIf` / `GrowHideIf` / `GrowEnableIf` / `GrowDisableIf`,
`GrowOnValueChanged` and `GrowButton`) require an Editor that derives from `GrowEditor`:

```csharp
[CustomEditor(typeof(MyConfig))]
public sealed class MyConfigEditor : GrowEditor {
}
```

`GrowEditorBase` is the reusable workflow base for fully custom IMGUI panels. Saving is
explicit through `SaveAll()`; there is no background auto-save. `GrowEnumFlags` is explicit
opt-in, so the package does not replace every enum field in the project with a custom drawer.

See [Editor inspector](editor-inspector.md) for the attribute reference, condition syntax, button
arguments and troubleshooting.

The optional `com.ronny.grow.lens` package adds editor-only productivity tools (clickable
Hierarchy component icons, tree connectors and separators, asset header actions, context menus and a
toolbar menu). It is disabled by default, has no runtime dependency on the kernel, and is not
required by it.

## Editor settings

The package registers two pages:

- **Edit > Project Settings... > Grow > General** shows the installed package identity and a
  shortcut to the package documentation.
- **Edit > Preferences > Grow > General** lets you choose the editor UI language and set the
  minimum log level for the current editor session.

Language is a user-global preference and is stored under Unity's Preferences folder, so it
follows you across projects. The supported editor UI languages are English and Simplified
Chinese. French, Russian and Spanish live under test fixtures only and do not appear in the
language selector. The log level is session state: `GrowLog` resets it to `Info` at the start
of each domain reload and play session. Project-level settings are added only when a feature
consumes them.

## Requirements

- Unity **2022.3 LTS** (the `unity` field of `package.json`).
- C# 9, `.NET Standard 2.1` API profile.

## Installation

There is no published release yet. The package is embedded in this repository at
`Packages/com.ronny.grow` and is picked up automatically when the project is opened in
Unity.

## Feedback

Please open an issue at <https://github.com/RonnyWu/Grow/issues>.

## License

MIT — see the [LICENSE](https://github.com/RonnyWu/Grow/blob/main/LICENSE) in the
repository root.
