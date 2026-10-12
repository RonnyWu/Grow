# Grow

**Read this in other languages:** [English](README.md) | [简体中文](i18n/README.zh-CN.md)

A modular Unity game framework built on a thin kernel.

- Status: **early planning** — the API and structure are not stable yet.
- Package: [`Packages/com.ronny.grow`](Packages/com.ronny.grow)
- Unity: **2022.3 LTS**

## Architecture

Grow is a **thin kernel plus a capability layer**: the kernel handles mechanisms, and game features
are added as replaceable capabilities.

- **Kernel** — mechanisms only: startup, lifetime, frame driving, module registration, scheduling,
  plus a few utilities that have no timing concerns, such as logging. It neither touches nor wraps any
  game feature.
- **Capability** — a game feature such as assets, saves, UI, or audio. Each one ships as its own
  package and exposes a single stable **contract**; no third-party type appears in that contract, so
  the implementation can be replaced wholesale without touching the caller.

Layers, each depending only on the ones below it:

| Layer | Contents | State |
|---|---|---|
| Game | Your own game / application code | — |
| Framework | Meta package: wires a set of capabilities with default composition | Planned |
| Capabilities | Contract + implementation (the implementation wraps third-party libraries) | Planned |
| Kernel | Mechanisms — entry, modules, service registry, lifetime, loop | In progress |
| Core | Low-level primitives with no third-party dependencies | In progress |

## Contributing

The project is still in early planning, so please [open an issue](https://github.com/RonnyWu/Grow/issues)
before starting on a pull request.

- All first-party code lives in the UPM package at `Packages/com.ronny.grow`; `Assets/` is empty by
  design and must stay that way.

## License

MIT — see [LICENSE](LICENSE).
