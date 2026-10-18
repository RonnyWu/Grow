# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Versioned bucket files, a scope-wide `manifests` index and `manifests.bak` recovery.
- `byte[]` values stored in per-store `<store>.blobs/` directories with length and CRC32
  verification.
- Fixed bucket hashing, startup bucket splitting and safe-point diagnostics.
- `StoreOptions` for bucket/blob targets, the bucket-count limit and the lifecycle budget.
- `StoreModule.Diagnostics` for `TrySplit` and `TryGc`.
- Package documentation for installing and using the store.
- EditMode coverage for manifests, buckets, blobs, corruption/reset, GC, splitting, provider
  ownership and lifecycle saves.

### Behavior

- Store files use `<scope>/manifests`, `<scope>/<store>.<bucket>.<version>.kv` and
  `<scope>/<store>.blobs/`; stores of one type share one scope directory.
- The file provider owns one scope per store type and rejects a second provider for the same
  scope.
- A clean `Save` performs no disk IO.
- A failed manifest commit leaves the previous manifest readable; new bucket versions are
  orphans and are collected later.
- Corrupted bucket and blob files are never silently overwritten.
- Lifecycle saves use a budget over pending bytes, store count and fsync count.

## [3.0.0-exp.1] - 2026-10-17

### Added

- Initial package.
