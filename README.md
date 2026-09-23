# Grow

Runtime and editor framework for Unity, distributed as the embedded UPM package
[`Packages/com.ronny.grow`](Packages/com.ronny.grow).

> **Status: early design.** The framework is being redesigned from scratch. The API and
> package structure are not stable yet.

## Requirements

- Unity **2021.3.45f2** (C# 9). Newer Unity APIs are not used.
- Domain Reload disabled, Scene Reload kept — see
  [`adr-0002-domain-reload-disabled.md`](docs/architecture/adr-0002-domain-reload-disabled.md).

## Repository layout

| Path | What |
|---|---|
| `Packages/com.ronny.grow/` | The framework package — all runtime and editor code lives here |
| `docs/` | Documentation: Diátaxis guides plus engineering records |
| `.github/` | Platform metadata: workflows, issue and PR templates |
| `.drafts/` | Local-only drafts (plans, specs); not tracked |
| `Assets/` | Empty — code belongs to the package, not the project |

## Installation

This repository is a Unity project with the package embedded. Clone it and open it with
Unity 2021.3.45f2; the package at `Packages/com.ronny.grow` is picked up automatically.

## Documentation

Start at [`docs/README.md`](docs/README.md) for the document map; the conventions live in
[`docs/design/dsn-0001-doc-layout.md`](docs/design/dsn-0001-doc-layout.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT — see [LICENSE](LICENSE).
