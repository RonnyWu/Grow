# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [3.0.0-exp.1] - 2026-11-01

### Added

- `Grow.Scenes` package: `ISceneFlow`, `SceneRequest` and `SceneModule` for loading, activating and
  unloading scenes through one controlled path. The flow polls its operation with
  `GrowScheduler.NextFrame`, allows one foreground operation at a time, reports failures on the
  request instead of throwing, and never fakes cancellation.
- `GrowBuilder.UseScenes()` extension so a game composes the capability without the kernel learning
  about scenes.
