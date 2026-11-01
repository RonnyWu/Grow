# Grow

**Read this in other languages:** [English](README.md) | [简体中文](i18n/README.zh-CN.md)

A modular Unity game framework built on a thin kernel.

- Status: **early planning** — the API and structure are not stable yet.
- Packages: Grow core + kernel with the annotation-driven Inspector
  [`Packages/com.ronny.grow`](Packages/com.ronny.grow) · Store contracts
  [`Packages/com.ronny.grow.store.contracts`](Packages/com.ronny.grow.store.contracts) · Store
  implementation [`Packages/com.ronny.grow.store`](Packages/com.ronny.grow.store) · scene flow
  [`Packages/com.ronny.grow.scenes`](Packages/com.ronny.grow.scenes) · optional editor lens
  [`Packages/com.ronny.grow.lens`](Packages/com.ronny.grow.lens)
- Unity: **2022.3 LTS**

## Architecture

Grow is a **thin kernel plus a capability layer**: the kernel provides only mechanisms, and game
features are added as replaceable capabilities.

- **Kernel** — mechanisms only: the runtime plumbing that starts a game and drives its per-frame
  updates. It never owns or wraps any game feature.
- **Capability** — a game feature such as assets, saves, UI, or audio. A capability with more than
  one interchangeable backend splits into a contract package plus implementation packages, and no
  third-party type appears in the contract, so the implementation can be replaced without touching
  the caller. A capability with a single backend (such as scene flow) stays one package.

Each layer depends only on the layers below it:

| Layer | Contents | State |
|---|---|---|
| Game | Your own game / application code | — |
| Framework | Meta package: assembles a recommended set of capabilities | Planned |
| Capabilities | Implementation, plus a contract when a capability has more than one backend | In progress |
| Kernel | Mechanisms — entry, modules, service registry, lifetime, loop, scheduling | In progress |
| Core | Passive primitives (e.g. logging); no third-party dependencies | In progress |

## Contributing

The project is still in early planning, so please [open an issue](https://github.com/RonnyWu/Grow/issues)
before starting on a pull request.

- All first-party code lives in the UPM packages under [`Packages/`](Packages/); `Assets/` is empty
  by design and must stay that way.
- Contributor documentation lives in [`docs/`](docs/README.md).

## License

MIT — see [LICENSE](LICENSE).
