# Store

**Read this in other languages:** [English](store.md) | [简体中文](../../../zh-CN/framework/design/store.md)

*Design record* — the file-backed key/value store capability.

Store gives Grow a small, file-backed key/value store for primitive values and `byte[]`. It does not
define a schema, a record model, or a serialization format; callers keep those decisions in their own
code and use `byte[]` when they need to store encoded data.

## Goals

- Store primitive values and `byte[]` without a database or a third-party backend.
- Keep every store readable as ordinary files, with one visible bucket in the common case.
- Share one directory per `StoreType`, not one directory per store.
- Split large stores into buckets on first access, never during a save.
- Keep `Save` explicit and flush on commit; never run an automatic save timer.
- Treat only physical disk and IO failures as hard failures.
- Never use `EditorPrefs`, `PlayerPrefs`, `Microsoft.Win32` or the registry.

## Non-goals

- Schema or record types beyond the primitive value kinds.
- Serialization, queries, indexes or a document model.
- WAL, replay, delta files or compaction.
- Multi-process writers, cloud sync, encryption or compression.
- Content-addressed blob deduplication.

## Scopes

A store type selects a scope directory. All stores of one type share that directory; only different
types get different directories.

| `StoreType` | Editor | Player |
| --- | --- | --- |
| `Editor` | `<project>/UserSettings/Grow/Editor` | Not supported |
| `Runtime` | `<project>/UserSettings/Grow/Runtime` | `<persistentDataPath>/Grow/Runtime` |
| `User` | `<project>/UserSettings/Grow/User` | `<persistentDataPath>/Grow/User` |

Editor `Runtime` and player `Runtime` are separate by default. `UsePlayerPathInEditor` opts into the
player path in the editor. `Editor` is unsupported on player platforms.

## Directory layout

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

A store named `settings` owns `settings.<bucket>.<version>.kv` and, lazily, `settings.blobs/`.
Different stores share the directory but not files. A `StoreType` owns exactly one directory.

## Names

Store names are folded to lower case and then validated:

- 1–64 characters;
- only `a-z`, `0-9`, `.`, `_`, `-`;
- must start and end with `a-z` or `0-9`;
- no `..`, path separators, control characters, whitespace, leading or trailing `.`;
- no Windows device names (`con`, `prn`, `aux`, `nul`, `com1`–`com9`, `lpt1`–`lpt9`);
- no reserved names `manifests`, `manifests.bak`, `manifests.tmp`, `quarantine`;
- no name ending in `.blobs`.

Invalid names are rejected with `ArgumentException`; they are never silently rewritten.

## File format

### Manifests

One `manifests` file per scope records the current bucket map:

```text
// grow-store-manifests 1 hash=fnv1a32-mix1 generation=42 bodyCrc32=9f3a1c2d
store settings bucketCount=2
  bucket 0 version=2 bytes=18342
  bucket 1 version=1 bytes=9210
store audio bucketCount=1
  bucket 0 version=2 bytes=4210
```

`bucketCount` is a power of two. `bytes` is the logical bucket size and a hint for bucket splitting.
`bodyCrc32` covers the lines after the header. `manifests.bak` holds the previous complete index.

### Bucket files

```text
// grow-store-bucket 1 hash=fnv1a32-mix1 store=settings bucket=0 version=2 bucketCount=2 bytes=18342 dataCrc32=9f3a1c2d
a<TAB>string<TAB>hello
b<TAB>int<TAB>2
avatar<TAB>blob<TAB>7f3a1c2d9e8b4a6c8d0e1f2a3b4c5d6e:1048576:9f3a1c2d
```

- The header must match the file name.
- The body is sorted by the raw key using ordinal comparison.
- The line shape is `escapedKey<TAB>kind<TAB>escapedValue`.
- `dataCrc32` covers all body bytes; line endings are LF and the file is UTF-8 without a BOM.
- Kinds are `int`, `long`, `float`, `double`, `bool`, `string` and `blob`.
- `bool` is `0` or `1`; numbers use the invariant culture; strings escape `\`, TAB, LF and CR.
- `blob` stores `<blobId>:<length>:<crc32>`; the bytes live outside the bucket.

`version` is a per-bucket commit number, not a semantic version. It starts at 1, never reuses a
value, and uses `long`.

## Save and commit

`Save` commits the current cache and flushes it. A clean, already loaded store performs no write.

1. Group changed keys by bucket.
2. For each affected bucket, write `<store>.<bucket>.<next>.kv.tmp`, flush it and rename it to the
   final versioned name.
3. Update the `manifests` entry with the new bucket versions.
4. Replace `manifests` safely.
5. Drop bucket versions that are no longer referenced.

A multi-bucket save is atomic because the manifest is the switch point. A crash before the switch
leaves the old manifest readable and at most orphan bucket versions. A crash after the switch exposes
the new versions. A failed manifest write keeps the old index.

`TrySaveAll` writes every healthy dirty store first and then replaces the manifest once. A corrupted
dirty store is skipped and reported as a failure. A bucket write failure prevents the manifest commit;
already written versions are orphans and every healthy store stays dirty.

### Safe manifest replacement

The primary path writes and verifies `manifests.tmp`, then calls
`File.Replace(manifests.tmp, manifests, manifests.bak)`. The backup is produced by the replace
itself. When `File.Replace` is unavailable, the fallback makes a verified `.bak` durable, then
deletes and moves the temporary manifest; the `.bak` covers the crash window.

## Recovery

On open:

1. Read and verify `manifests`.
2. Fall back to `manifests.bak`.
3. If both are invalid, scan bucket headers and rebuild the manifest.
4. Split and garbage-collect a store the first time it is loaded.
5. Load the store's buckets when the store is first accessed.

A bucket with a bad header or CRC is moved to `quarantine/` and the store becomes `Corrupted`.
Reads return only data that can be validated; writes throw `InvalidOperationException`; `TrySave`
returns `false`. `Reset` clears and rebuilds the store. A committed blob pointer whose file is
missing or fails its length/CRC check is corruption, not a miss.

## Bucket splitting

The bucket hash is a fixed algorithm, `FNV-1a 32` followed by a 32-bit mixing step. Its id is
`fnv1a32-mix1`; it is recorded in the manifest and bucket headers. `string.GetHashCode()` is never
used because it is not stable across processes or runtimes.

The bucket is `hash & (bucketCount - 1)`. Startup splitting is:

- check every store in the manifest;
- while any bucket exceeds `TargetBucketBytes`, double `bucketCount` and rewrite the store;
- write header-only files for empty buckets;
- switch the manifest only after every new bucket verifies;
- stop at `BucketCountLimit` (default 4096) and log once if a single key still exceeds the target.

Runtime saves never split. A long session can grow a bucket beyond the target; the next first
access converges it, or an explicit `IStoreDiagnostics.TrySplit` does so at a safe point.

## Blobs

`byte[]` values are written to `<store>.blobs/<blobId>.bin`; the bucket stores only a pointer. Blob
files are written before the pointer commit. The blob directory is created lazily and removed by GC
when empty. Blob ids are 32 lowercase hex characters from a GUID.

Blob GC scans only stores that have a `.blobs/` directory. It collects pointers from the current
manifest and `manifests.bak`, then removes unreferenced `.bin` and `.tmp` files. There is no content
deduplication: two keys with equal bytes use two files.

## Capacity and failure

| Setting | Default | Behaviour |
| --- | --- | --- |
| `TargetBucketBytes` | 2 MiB | Startup split target; never a hard limit |
| `BlobWarningBytes` | 64 MiB | Soft log threshold; never a hard limit |
| `BucketCountLimit` | 4096 | Startup split cap |

There is no per-value, key-count, store or scope limit. Logical size thresholds only log. Physical
disk and IO failures are reported through `IOException` or `TrySave`. There is no
`StoreCapacityException` and no `SplitRecommended` property.

## Contract

The public contract is deliberately small:

```csharp
public interface IStore
{
    T Get<T>(string key, T defaultValue = default);
    bool TryGet<T>(string key, out T value);
    IStore Set<T>(string key, T value);
    bool ContainsKey(string key);
    IStore Delete(string key);
    IStore Clear();
    IStore Save();
    bool TrySave(out string error);
    StoreSize Size { get; }
    bool IsDirty { get; }
    bool IsCorrupted { get; }
    bool Reset();
}

public interface IStoreProvider : IDisposable
{
    IStore GetStore(string storeName, StoreType storeType);
    bool TryGetStore(string storeName, StoreType storeType, out IStore store);
    bool Close(string storeName, StoreType storeType);
    bool TrySaveAll(out string error);
    bool DeleteStore(string storeName, StoreType storeType);
    bool IsSupported(StoreType storeType);
}
```

`IStore` is not `IDisposable`; the provider owns store lifetime. One provider serves all scopes, and
each scope has exactly one writer. A second provider opening an already-held scope fails on that first
open instead of overwriting the shared manifest.

`Close` saves a dirty, healthy store and releases it on success. A failed save keeps the store open
and dirty. A corrupted store is released without a write. `DeleteStore` removes the manifest record,
then deletes the store's files; a crash between those steps leaves orphan files for GC. `Reset`
rebuilds an empty store and best-effort deletes its blob directory.

`IStoreDiagnostics` is exposed by `StoreModule.Diagnostics` and provides `TrySplit` and `TryGc` at
safe points. It is not part of the everyday contract.

## Lifecycle

`StoreProvider` subscribes to pause, focus loss, quit and `StoreModule.OnStop`. Lifecycle saves use a
budget: pending bytes, store count and fsync count. A store with a pending blob above the configured
threshold is skipped and remains dirty. `Dispose` never saves implicitly.

The default `StoreModule` store is `settings`. `StoreOptions` lives in the implementation package and
configures `TargetBucketBytes`, `BlobWarningBytes`, `BucketCountLimit`, `UsePlayerPathInEditor` and
the lifecycle budget.

## Trade-offs

- A single-bucket store still writes a manifest on every save. The extra small write is accepted for
  one code path, one file format and one recovery model.
- The manifest is global to a scope, so its size grows with the store count. Per-store manifests are
  deferred until a real workload needs them.
- Startup splitting and blob GC can block loading. Runtime splitting is not automatic.
- The store is single-process and main-thread. No background writer, encryption, compression or
  cloud sync is planned for this capability.

## See also

- [Architecture](../architecture/README.md) — where capabilities sit in Grow.
- [Glossary](../reference/glossary.md) — terms shared across these pages.
