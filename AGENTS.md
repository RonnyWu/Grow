# AGENTS.md

## Repo state (check before assuming history)
- `main` currently holds only the **initial redesign baseline** (repository dotfiles + project/package skeleton); no framework code yet. Run `git log` before assuming history.
- The pre-redesign GrowFramework code exists only on the local `old` branch (legacy multi-package layout: `Packages/grow-core`, `grow-framework`, ...). Reference only; do not resurrect it.
- Root `*.csproj` / `*.sln` are Unity/Rider-generated and gitignored. They are currently **stale** (still reference removed `Packages/grow-*`). Never edit or trust them.
- `.superpowers/` is ignored local scratch from a finished SDD session; ignore it.

## What this is
- Unity **2021.3.45f2** project whose deliverable is the single embedded UPM package `com.ronny.grow`.
- Edit package source under `Packages/com.ronny.grow/{Runtime,Editor,Tests}`. `Assets/` is only dev scenes/samples.
- Runtime/Editor/Tests are empty `.gitkeep` skeletons — the framework is not implemented yet.

## Architecture rules (ADR-0001: docs/architecture/ADR-0001-project-and-packages.md)
- Ship exactly **two** assemblies: `Grow` (all runtime) and `Grow.Editor` (editor-only, never in Player builds).
- Modules are **folders + namespaces, not assemblies** — do not add per-module `.asmdef` files.
- Namespace roots: `Grow` (runtime) and `Grow.Editor` (editor), e.g. `Grow.Events`, `Grow.Editor.Hierarchy`.
- Banned legacy namespaces: `GrowEnhance.*`, `GrowEnhanceEdit.*`, `GrowEditor.*`, `Grow.Extensions.Scripts`.

## Conventions
- New architecture decisions: `docs/architecture/ADR-NNNN-topic.md` with the next zero-padded four-digit number and a kebab-case topic (written in English). See CONTRIBUTING.md and the index at docs/architecture/README.md.
- Commits: Conventional Commits, focused, explain the *why*.
- Unity YAML (`.asset`, `.prefab`, `.unity`, `.meta`, ...) is routed through `merge=unityyamlmerge` in `.gitattributes` (the driver is registered per-machine; without it Git falls back to a plain text merge). `.gitignore` is marked `-text` to keep its raw CR bytes — don't renormalize or hand-edit line endings.

## Build / test
- No CLI lint/typecheck/test setup exists. `.github/workflows/ci.yml` is a `workflow_dispatch` placeholder.
- Tests use the Unity Test Framework (`com.unity.test-framework` 1.1.33) and belong in `Packages/com.ronny.grow/Tests`; run them through the Unity Editor Test Runner, not `dotnet test`.
