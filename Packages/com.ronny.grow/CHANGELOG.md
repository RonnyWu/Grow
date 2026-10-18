# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `Grow.Editor.Settings` — reusable editor settings infrastructure: the standard settings-page
  margin and label-width wrapper, YAML settings-asset load/save/delete helpers, and the
  project/user settings paths. Adds read-only **Project Settings > Grow** and
  **Preferences > Grow** pages that report package identity and current session state; no
  persisted setting is exposed yet.
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
- `Grow.Core.Collections` — passive collection primitives: `BinaryHeap<T>` (min-heap),
  `RingBuffer<T>` (fixed-capacity ring), `SlotMap<T>` + `SlotHandle` (dense generational slots)
  and `SortedBag<T>` (stable ordered multiset implementing `IReadOnlyList<T>`).
- `Grow.Core.Pool` — a thread-safe `ConcurrentObjectPool<T>` with lifecycle callbacks, and
  `StringBuilderPool` / `StringBuilderPool.Pooled` for string-builder rentals through a
  single-use token.
- `Grow.Kernel` assembly — mechanisms, starting with `Grow.Kernel.Entry`: the
  `GrowRuntime` state machine (manual `Reset` / `Install` / `Boot` / `Shutdown`
  entry points, with `Ready` reached by `Boot`), the `GrowBoot` thin facade, and
  `GrowState`.
- `Grow.Kernel.Loop` — `GrowLoop` plus the internal `IMonoExecutor` / `MonoExecutor`
  host and `PlayerLoopProfile` (five frame phases), built on the internal
  `PlayerLoopGraph` and `PlayerLoopManager` for copy-on-write insert/remove,
  idempotent install, targeted removal and snapshot restore.
- `Grow.Kernel.Lifecycle` — public `ILifecycle`, the facet interfaces in
  `ILifecycleFacets.cs`, `UpdateFlag`, `ILifecycleManager` and `MonoManager`. The
  `UpdaterBinding` is internal. `AttachApp` / `DetachApp` are all-or-nothing and
  retryable; the manager owns the subscriptions and de-duplicates by target.
- `Grow.Kernel.Lifecycle.LifecycleScope` — attaches plain objects to the lifecycle
  manager, supports retryable attach/detach, `AttachApp` / `DetachApp` and releases
  entries in reverse order.
- `Grow.Kernel.Lifecycle.UnifiedClass`, `Grow.Kernel.Lifecycle.UnifiedClassSingleton<T>`
  and `Grow.Kernel.Lifecycle.UnifiedClassService<T>` — self-managed plain objects and
  services with ordered initialize / contracts / start / update / teardown / reset stages,
  failure isolation and a main-thread `PostToMain` queue.
- `Grow.Kernel.Ioc` — `IOC.Root` plus `ServiceCollection` with parent scopes, exact
  `(service type, key)` identities and transient/singleton lifetimes. `AddUnique` /
  `InjectUnique` are keyed-singleton aliases; factories use `IServiceFactory<T>`.
  `GetService` returns `null` when absent, `GetRequiredService` throws
  `ServiceNotFoundException`, and the root is cleared in place at
  `SubsystemRegistration`.
- `Grow.Kernel.Modules` — public `IModule`, `IModuleContext`, `ModulePhase`,
  `ModuleFailure` and `GrowModules`; `ModuleRegistry` is internal. Modules are ordered by
  dependency topology and same-layer priority; missing, ambiguous and cyclic dependencies
  skip the affected modules and their transitive dependents, while hook failures are
  isolated and recorded. The context shares the IOC root table, drives managed services
  at phase boundaries and exposes one registry-wide `LifecycleScope`.
- `Grow.Kernel.Entry.GrowApp` — the composition root. It initializes at `BeforeSceneLoad`
  unless `AutoInitialize` is disabled, wires boot state, loop, lifecycle manager and
  modules in a fixed order, and shuts them down in reverse ownership order.
  Initialize / shutdown are idempotent; a failed initialization performs best-effort
  cleanup and rethrows.
- `Grow.Kernel.Entry.GrowSession` — the kernel-side `SubsystemRegistration` entry. It resets
  `GrowApp`, `GrowRuntime`, `IOC`, `MonoManager`, `LifecycleSession`, `GrowLoop`,
  `PlayerLoopManager`, `GrowLog` and `StringBuilderPool` in a fixed order; Core primitives may
  keep their own hooks because they cannot depend on the kernel.
- EditMode tests for both assemblies and PlayMode coverage for automatic startup and a
  same-frame restart.

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
- **Breaking (boot ownership):** `GrowBoot.Reset` / `Install` / `Boot` / `Shutdown` are
  internal. `GrowApp` is the only boot-state writer; call `GrowApp.Initialize` /
  `Shutdown` instead of driving the state machine directly. `MonoManager.Bind` / `Unbind`
  were internal from their introduction and are not part of this change.
- `GrowApp.Initialize` now publishes `GrowState.Installed` after `GrowModules.Bootstrap()`.
  Module hooks therefore observe `GrowState.Reset`, and a `StateChanged` handler for
  `Installed` observes a fully composed kernel.
- `GrowModules.Reset()` is now a public explicit reset: it shuts down started modules before
  replacing the registry, and a failed initialization also resets the registry.

### Fixed

- `PlayerLoopProfile.Install` now removes partially inserted systems when an install throws,
  and `Uninstall` still cleans up when the owner was never recorded. `PlayerLoopManager`
  records a system before applying it, so cleanup can find it even when `SetPlayerLoop`
  throws.
- `OnFixedUpdate` is inserted inside Unity's `FixedUpdate` group instead of as a top-level
  sibling, so it runs once per fixed step rather than once per frame.
- `MonoManager.Bind` replays application subscriptions and enabled updater bindings, so a
  `UnifiedClass` created before the host is bound attaches once the host exists.
  `MonoManager.Unbind` clears those bindings and no longer queues bound targets for replay.
- The `IModuleContext` remarks now say it shares the IOC root table instead of claiming a
  separate locator.

## [3.0.0-exp.1] - 2026-10-11

### Added

- Clean baseline: empty framework skeleton (Runtime / Editor / Tests assemblies). No implementation yet — planning first.
