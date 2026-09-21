# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial repository and package skeleton.
- Add `Grow.Core.Collections.OrderedSet<T>` (insertion-ordered set, O(1) add/remove/contains).
- Add `Grow.Core.Collections.SnapshotSet<T>` (safe iteration under mutation: snapshot at read boundary, changes take effect next round; the array returned by `BeginRead` is a live internal buffer and must be treated as read-only until `EndRead`).
