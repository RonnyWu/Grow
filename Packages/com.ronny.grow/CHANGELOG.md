# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `Grow.Core` assembly — passive primitives, starting with `Grow.Core.Log`: the
  `GrowLog` facade, the `ILogSink` extension point, the `UnityLogSink` default
  sink, and the `LogEntry` / `LogLevel` value types.
- `Grow.Core.Events` — a single-thread, steady-state zero-allocation event primitive.
  `GrowEvent` / `GrowEvent<T>` are the subscription surfaces; the nested owner-only
  `GrowEvent.Publisher` / `GrowEvent<T>.Publisher` are created with `new ...Publisher()`.
  `Publish` is synchronous and registration-ordered; nesting is capped at 64; Core has no
  queue, `Post` / `Drain` or cross-frame delivery. Subscriptions are token-based
  (`GrowSubscription`), each `Subscribe` is independent, changes made during a dispatch
  apply after the pass, and listener faults go to an optional sink or are rethrown by the
  outermost publish after every listener has run. `SubscribeOnce` consumes its slot before
  the call; `SubscriptionBag` disposes a group of tokens LIFO. A `Publisher.Dispose` called
  during a dispatch is deferred until the current pass ends.
- `Grow.Kernel` assembly — mechanisms, starting with `Grow.Kernel.Entry`: the
  `GrowRuntime` state machine (manual `Reset` / `Install` / `Boot` / `Shutdown`
  entry points, with `Ready` reached by `Boot`), the `GrowBoot` thin facade, and
  `GrowState`.
- `Grow.Kernel.Loop` — `GrowLoop` plus the internal `IMonoExecutor` / `MonoExecutor`
  host and `PlayerLoopProfile` (five frame phases), built on the internal
  `PlayerLoopGraph` and `PlayerLoopManager` for copy-on-write insert/remove,
  idempotent install, targeted removal and snapshot restore.
- EditMode tests for both assemblies.

### Changed

- **Breaking (assembly layout):** the single `Grow` runtime assembly is split into
  `Grow.Core` and `Grow.Kernel`. The `Grow` assembly no longer exists, so runtime
  code that referenced it — or the `Grow.*` runtime namespaces — must move to
  `Grow.Core.*` / `Grow.Kernel.*`. The baseline `Grow` assembly shipped no types,
  so no consumer code could have depended on it.
- **Breaking (boot event):** `GrowBoot.StateChanged` is now a
  `GrowEvent<StateChange>` instead of a C# `event Action<GrowState, GrowState>`. Subscribe
  with `GrowBoot.StateChanged.Subscribe(change => ...)` and dispose the returned token;
  the payload exposes `Previous` / `Next`.

## [3.0.0-exp.1] - 2026-10-11

### Added

- Clean baseline: empty framework skeleton (Runtime / Editor / Tests assemblies). No implementation yet — planning first.
