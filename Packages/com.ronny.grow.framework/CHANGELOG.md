# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [3.0.0-exp.1] - 2026-11-02

### Added

- `Grow.Framework` meta package. `GrowFramework.Create()` returns a `GrowBuilder` with the default
  capability set — currently the store and the scene flow — plus optional `StoreOptions` and an
  initial scene name. It adds no abstraction and no discovery: a game still calls `AddModule` and
  `Build`.
- A Vertical Slice sample: one module that loads over two frames and one that persists a session
  through the store capability, composed with `GrowFramework.Create()`.
