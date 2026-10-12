# Grow

A lightweight, modular Unity game framework.

> **Status: early planning (`v3.0.0-exp.1`).** The repository restarted from a clean baseline; the
> API and structure are not stable yet.

- Package: [`Packages/com.ronny.grow`](Packages/com.ronny.grow)
- Unity: **2022.3 LTS** (development uses **2022.3.62f3**)

Open the repository with Unity 2022.3.62f3; the package at `Packages/com.ronny.grow` is picked up
automatically. All code belongs in the package — `Assets/` stays empty.

## Architecture

Grow is a **thin kernel plus an optional capability layer**. The kernel owns the *mechanisms* a game
needs to run — startup, lifetime, update driving, module registration, scheduling — and a small set
of passive primitives. It never owns or wraps a *capability*; assets, saves, UI, audio and the like
live in separate, replaceable packages built on mature libraries.

Layers (each depends on the ones below):

| Layer | Contents |
|---|---|
| Game | user game / application code |
| Framework | meta package — assembles a recommended capability set and a default composition root |
| Capabilities | contracts + implementations (third-party backends) + kernel adapters |
| Kernel | mechanisms — entry, modules, service registry, loop, lifecycle |
| Core | passive, time-free primitives; no third-party dependencies |

A capability is a *contract + implementation + kernel adapter*. The contract is the stable,
user-facing API and never exposes a third-party type, so an implementation can be swapped without
touching its consumers.

**Architecture layers and delivery packages are two different axes.** Layers describe the module
structure; packages describe how code is shipped:

- `com.ronny.grow` — kernel package: Core + Kernel, no third-party dependencies
- `com.ronny.grow.<cap>.contracts` — capability contracts, no third-party dependencies
- `com.ronny.grow.<cap>[.<backend>]` — capability implementations, bringing their own dependencies
- `com.ronny.grow.framework` — meta package that pulls in a recommended capability set

**v1 targets the kernel only** — entry, modules, a minimal service registry (not a DI container),
lifecycle and loop, plus logging — shipped as the single kernel package. Capabilities and the meta
package come later.

## License

MIT — see [LICENSE](LICENSE).
