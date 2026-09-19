# AGENTS.md

## What this is

Unity game framework "Grow", pinned to **Unity 2021.3.45f2** (C# 9, .NET 4.7.1). Do not use
APIs newer than 2021.3 LTS (`linearVelocity`, Unity 6 APIs, etc. are unavailable here).

The framework is an **embedded UPM package at `Packages/com.ronny.grow`**, not under `Assets/`.
`Assets/` holds only `.gitkeep`. Put all code in the package.

## Repo state (check before assuming history)

- `git log` has a very short history and is actively being rewritten. Backup branches
  (`old`, `backup-*`) exist. Run `git status`/`git log` before assuming a clean or linear tree;
  runtime and doc files are frequently staged but uncommitted.
- Gitignored, do not trust or edit: root `*.csproj`/`*.sln` (Unity-generated; the stale
  `GrowFrameworkEditTests.csproj` points at an old `Packages/grow-framework/` path) and
  `/.superpowers/` (scratch from a prior architecture that references packages like
  `com.grow.framework` and `grow-core` that no longer exist).

## Architecture (authority: `docs/grow-architecture-rationale.md`, §8)

- Microkernel shape: `Core → Kernel → Services → {Integrations, Tooling}`. Dependencies point
  inward. `Integrations` and `Tooling` are adapter rings; nothing may depend on them, and they
  are mutually independent.
- Runtime L1 dirs: `Packages/com.ronny.grow/Runtime/{Core,Kernel,Services,Integrations,Tooling}`.
  Namespace = `Grow.<L1>.<L2>[.<L3>]`. Facets are transparent: `Runtime/` and `Tests/` do not
  appear in namespaces; editor leaf is `.Editor` (e.g. `Grow.Services.Ui.Editor`), not a global
  `Grow.Editor` concept root. Editor asm/nsp is `Grow.Editor`.
- Cross-module use only a module's `Contracts/`; behavior lives in `Systems/`, private helpers in
  `Internal/`. Third-party/backend code lives only in `Integrations/<Tech>` and is gated by
  asmdef `versionDefines` + `defineConstraints`.
- Banned L1 names: `Utils`, `Common`, `Misc`, `Helpers`, `Managers`.
- ADRs live in `docs/architecture/ADR-NNNN-topic.md`. ADR-0002 (Domain Reload disabled) is the
  only one present; `ADR-0001` is referenced by old notes but does not exist.

## Domain-reload contract (ADR-0002) — hard rule

- Supported run mode: **Domain Reload disabled, Scene Reload kept**. Applied by the idempotent
  menu `Tools/Grow/Setup/Apply Project Settings`; `Diagnose` is read-only. Never add
  `[InitializeOnLoad]` auto-writes of project settings.
- Every static field/singleton/cache must be resettable from `GrowBoot.OnReset` (fired at
  `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` in
  `Runtime/Kernel/Entry/GrowBoot.cs`); static events must unsubscribe there. `[assembly:
  AlwaysLinkAssembly]` (`Runtime/AssemblyInfo.cs`) keeps the boot alive.
- Runtime code must not assume a domain reload did or did not happen.
- Scene Reload is not disabled yet; doing so needs a new scene-reset contract/ADR first.

## Build / test

- No test assembly exists (`Tests/` only has `.gitkeep`) and no CLI test command is configured.
  CI (`.github/workflows/ci.yml`) is a `workflow_dispatch` placeholder.
- Verify by compiling in Unity 2021.3.45f2 and running EditMode tests in the Test Runner once a
  test asmdef is added. No lint/format tooling is configured.

## Conventions

- Every `.cs` file starts with the 3-line MIT header (Copyright 2026 Ronny Wu).
- Conventional Commits; update `Packages/com.ronny.grow/CHANGELOG.md` (Keep a Changelog /
  SemVer) for user-visible changes.
- Commit or push only when explicitly asked.
