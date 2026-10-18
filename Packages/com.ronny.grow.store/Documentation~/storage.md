# Data on disk

Grow Store writes one directory per scope. This page describes what appears there and what the store
guarantees when it saves.

## Scope directory

All stores of one `StoreType` share a directory:

```text
<scope>/
├── manifests
├── manifests.bak
├── settings.0.1.kv
├── settings.0.2.kv
├── settings.1.1.kv
├── settings.blobs/
│   └── <blobId>.bin
├── audio.0.1.kv
├── audio.blobs/
└── quarantine/
```

- `manifests` is the scope index. It records which bucket version is current for every store.
- `manifests.bak` is the previous complete index.
- `<store>.<bucket>.<version>.kv` holds one bucket of one store.
- `<store>.blobs/` holds `byte[]` values. It is created only when the store first writes a blob.
- `quarantine/` holds files that failed validation. The store never silently overwrites them.

The layout is managed by Grow Store and is not a public API. The bucket count and version numbers are
internal bookkeeping.

## How a save commits

`Save` writes new files first and switches the index last:

1. Each changed bucket is written to a temporary file, flushed and verified.
2. The temporary bucket is renamed to a new version.
3. The index is replaced safely with the new bucket version.
4. Old versions are removed after the index no longer references them.

If the game stops before the index switch, the old index still points at the old bucket version; the
new file is unused. If it stops after the switch, the new version is visible. A save never leaves a
half-written bucket as the current version.

`TrySaveAll` writes every healthy dirty store and replaces the index once. A failed bucket write
prevents the index switch; the previous state remains readable.

## Backup and recovery

On open, the store reads `manifests`. If that file is missing or invalid, it tries `manifests.bak`.
If both are unusable, it scans bucket headers and rebuilds the index. The store does not delete old
or unreadable files during recovery.

A bucket that fails its header or checksum check is moved to `quarantine/`, and the store becomes
corrupted. A committed blob pointer whose file is missing or fails its length/checksum check is also
corruption, not a missing value. See [Grow Store](index.md) for the `IsCorrupted` and `Reset`
behaviour.

## Growth and cleanup

- A store starts with one bucket.
- At startup, a bucket larger than `TargetBucketBytes` is split into more buckets.
- Splitting rewrites the store once and then switches the index.
- Running saves never split. A long session can grow a bucket past the target; the next startup
  converges it.
- `TargetBucketBytes` is a target, not a hard limit. `BlobWarningBytes` only logs.
- Blob files are reclaimed when no current or previous bucket version references them.

## Limits

| Item | Default | Meaning |
| --- | --- | --- |
| `TargetBucketBytes` | 2 MiB | Startup split target |
| `BlobWarningBytes` | 64 MiB | Soft log threshold |
| `BucketCountLimit` | 4096 | Startup split cap |

There is no per-value, key-count or scope size limit. Disk-full and IO errors are the only hard
failures. Capacity thresholds never throw.

## See also

- [Grow Store](index.md)
