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

## Editor settings

The package registers two read-only pages:

- **Edit > Project Settings > Grow** shows the package identity and the documentation path.
- **Edit > Preferences > Grow** shows the current session log level, log sink, and kernel state.

The pages are intentionally read-only until a setting has a real consumer. The reusable
infrastructure lives in `Packages/com.ronny.grow/Editor/Settings`: the standard settings-page
margin and label-width wrapper, YAML settings-asset load/save/delete helpers, and the
project/user settings paths.

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
