# Grow

**Read this in other languages:** [English](README.md) | [简体中文](i18n/README.zh-CN.md)

A modular Unity game framework built on a thin kernel.

- Status: **early planning** — the API and structure are not stable yet.
- Packages: Grow core + kernel [`Packages/com.ronny.grow`](Packages/com.ronny.grow) · Store
  contracts [`Packages/com.ronny.grow.store.contracts`](Packages/com.ronny.grow.store.contracts) ·
  Store implementation [`Packages/com.ronny.grow.store`](Packages/com.ronny.grow.store)
- Unity: **2022.3 LTS**

## Architecture

Grow is a **thin kernel plus a capability layer**: the kernel provides only mechanisms, and game
features are added as replaceable capabilities.

- **Kernel** — mechanisms only: the runtime plumbing that starts a game and drives its per-frame
  updates. It never owns or wraps any game feature.
- **Capability** — a game feature such as assets, saves, UI, or audio. Each one ships as a contract
  package plus a replaceable implementation package; no third-party type appears in the contract, so
  the implementation can be replaced wholesale without touching the caller.

Each layer depends only on the layers below it:

| Layer | Contents | State |
|---|---|---|
| Game | Your own game / application code | — |
| Framework | Meta package: assembles a recommended set of capabilities | Planned |
| Capabilities | Contract + implementation (implementations wrap third-party libraries) | In progress |
| Kernel | Mechanisms — entry, modules, service registry, lifetime, loop | In progress |
| Core | Passive primitives (e.g. logging); no third-party dependencies | In progress |

## Contributing

The project is still in early planning, so please [open an issue](https://github.com/RonnyWu/Grow/issues)
before starting on a pull request.

- All first-party code lives in the UPM packages under [`Packages/`](Packages/); `Assets/` is empty
  by design and must stay that way.
- Contributor documentation lives in [`docs/`](docs/README.md).

## License

MIT — see [LICENSE](LICENSE).
