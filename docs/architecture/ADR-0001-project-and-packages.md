# ADR-0001 — Project and Package Structure

- Status: Accepted
- Date: 2026-09-10
- Deciders: Ronny Wu

## Context

Grow is a brand-new, open-source Unity game framework built from scratch. Before
the first line of code, the project skeleton must be settled: how source is
organized, how it is distributed, where dependency boundaries are drawn, and how
naming is unified. These are the most foundational and hardest-to-change
decisions, so they are recorded first.

Design goals: **clear structure, one-way dependencies, easy to understand and
maintain long term**, grounded in mature industry practice and avoiding
precedent-free over-engineering such as "one assembly/package per module".

## Decisions

### D1 · Repository and product name = `Grow`

The repository, product, and namespace root all use **Grow**. The GitHub
repository is assumed to be `https://github.com/RonnyWu/Grow` (to be confirmed
after the repository is created).

### D2 · Ship a single UPM package

The whole framework is distributed as a **single UPM package** with package ID
**`com.ronny.grow`**.

### D3 · Repository layout = Unity project + embedded package

The repository is itself a Unity project, with the package embedded at
`Packages/com.ronny.grow/` and distributed via a git subpath
(`...?path=Packages/com.ronny.grow`).

```
Grow/
├── Assets/                       development scenes and samples
├── Packages/
│   ├── manifest.json
│   └── com.ronny.grow/           the package
│       ├── package.json
│       ├── README.md / CHANGELOG.md / LICENSE.md / Third Party Notices.md
│       ├── Runtime/              Grow (assembly)
│       ├── Editor/               Grow.Editor (assembly)
│       └── Tests/
├── ProjectSettings/
├── docs/architecture/            ADR documents
└── .github/workflows/
```

### D4 · Exactly two assemblies in the package

- `Grow` — all runtime code.
- `Grow.Editor` — all editor-only code, compiled for the Editor only and never
  included in Player builds.

### D5 · Modules = folders + namespaces, not assemblies

All capability modules (extensions, collections, pools, logging, module startup,
events, assets, save, UI, scenes, audio, tween, data tables, localization, object
pooling, ...) are **folders + `Grow.*` namespaces** inside the `Grow` assembly.
No asmdef is created per module.

### D6 · Naming

- Runtime namespace root: `Grow` (sub-namespaces such as `Grow.Events`).
- Editor namespace root: `Grow.Editor` (sub-namespaces such as `Grow.Editor.Hierarchy`).
- Assembly names: `Grow`, `Grow.Editor`.
- No other namespace roots are introduced.

## Rationale

- **Single package**: mature Unity frameworks almost all ship as a single
  package/monolith (VContainer, Zenject, Mirror, FishNet, UniTask, Addressables,
  QFramework, ...). "One logical module = one UPM package" has no precedent and
  brings a version matrix, installation complexity, and OpenUPM scope bloat. Unity
  DOTS's multi-package setup is a "version hell" counterexample, and ASP.NET Core
  also consolidated from many packages back into a single shared framework.
- **Two assemblies**: Unity-first frameworks are typically "one Runtime assembly
  plus one Editor assembly". An assembly's job is to express dependency direction
  and compile boundaries, not to slice by topic.
- **No separate pure-C# core assembly**: almost no Unity-first framework does this;
  when pure .NET reuse is needed, the mainstream approach is to add an extra
  `.csproj` reusing the same source (VContainer and UniTask both do this) rather
  than adding an asmdef. Splitting a core assembly costs `internal` visibility
  across assemblies, a forced-public API surface, and `InternalsVisibleTo`
  maintenance.
- **Modules as folders**: splitting one asmdef per business topic only buys the
  *appearance* of modularity while incurring the costs above, so it is not done.
- **Layout B (Unity project + embedded package)**: the repository can be opened
  directly in Unity for development and testing, prioritizing developer experience.

## Consequences

- Positive: simple structure, clear boundaries, few install units, no version
  matrix; developers can open the repository and start working immediately.
- Cost: the install URL carries `?path=`; the repository root contains both dev
  resources and the package itself; all runtime code shares one assembly, so
  inter-module dependency direction can only be enforced by namespaces and review,
  not by the compiler.

## Alternatives considered (rejected)

- **Multiple packages (core / framework / enhance style)**: too high a version-matrix
  and installation cost, with no supporting precedent.
- **One asmdef per module**: slicing assemblies by topic — low benefit, high cost.
- **A separate pure-C# core assembly**: no Unity-first precedent, cost exceeds
  benefit; use a `.csproj` when needed instead.
- **Package at the repository root (library repo)**: a cleaner distribution URL,
  but the repository can no longer be opened directly in Unity.

## To be decided by later ADRs

- Unified module / service / lifecycle model.
- Module list and directory / namespace layout.
- Third-party dependency strategy (including the trade-off around "optional
  dependency isolation" vs the "exactly two assemblies" constraint, and specific
  selections).
- Editor-layer strategy.
- Testing / CI / versioning / distribution.
- Phased implementation roadmap.

## References

- Unity, "Organizing scripts into assemblies": https://docs.unity3d.com/Manual/assembly-definition-files.html
- Unity, "Conditionally including assemblies" (Define Constraints / Version Defines): https://docs.unity3d.com/Manual/assembly-definition-includes.html
- ASP.NET Core `Microsoft.AspNetCore.App` metapackage: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/metapackage-app
- Structural references: VContainer, UniTask, Addressables, Mirror, Entitas, QFramework.
