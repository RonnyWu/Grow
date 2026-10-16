# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `Grow.Core` assembly — passive primitives, starting with `Grow.Core.Log`: the
  `GrowLog` facade, the `ILogSink` extension point, the `UnityLogSink` default
  sink, and the `LogEntry` / `LogLevel` value types.
- `Grow.Kernel` assembly — mechanisms, starting with `Grow.Kernel.Entry`: the
  `GrowRuntime` state machine (manual `Reset` / `Install` / `Boot` / `Shutdown`
  entry points, with `Ready` reached by `Boot`), the `GrowBoot` thin facade, and
  `GrowState`.
- EditMode tests for both assemblies.

### Changed

- **Breaking (assembly layout):** the single `Grow` runtime assembly is split into
  `Grow.Core` and `Grow.Kernel`. The `Grow` assembly no longer exists, so runtime
  code that referenced it — or the `Grow.*` runtime namespaces — must move to
  `Grow.Core.*` / `Grow.Kernel.*`. The baseline `Grow` assembly shipped no types,
  so no consumer code could have depended on it.

## [3.0.0-exp.1] - 2026-10-11

### Added

- Clean baseline: empty framework skeleton (Runtime / Editor / Tests assemblies). No implementation yet — planning first.
