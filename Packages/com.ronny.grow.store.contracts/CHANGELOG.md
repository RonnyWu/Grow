# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `StoreSize` reports `DataBytes`, `BlobBytes` and the logical `TotalBytes` of a store.
- `IStoreDiagnostics` exposes safe-point `TrySplit` and `TryGc` maintenance operations.
- `IStore.IsCorrupted` and `IStore.Reset` make an unreadable store observable and recoverable.

### Changed

- `IStore` is not `IDisposable`; the provider owns store lifetime.
- `IStore.Save` commits and flushes the store.
- `IStoreProvider.Close` and `DeleteStore` return `bool`; a failed close keeps the store open.
- `IStoreProvider.DeleteStore` removes the index entry before deleting files.
- `StoreType` documents the editor, runtime and user scope paths.

## [3.0.0-exp.1] - 2026-10-17

### Added

- Initial package.
