# Grow Store

Grow Store is a file-backed key/value store for primitive values and `byte[]`. It is part of the
Grow framework and ships as the `com.ronny.grow.store` package.

**Status:** pre-release. The API and file format may still change before a stable release.

## Install

Add `com.ronny.grow.store` through the Unity Package Manager. The package depends on
`com.ronny.grow` and `com.ronny.grow.store.contracts`; install all three together.

The implementation assembly is not auto-referenced. Reference `Grow.Store` from the assemblies that
create or configure the store module. The contracts assembly is auto-referenced.

## Quick start

`StoreModule` owns an `IStoreProvider` and registers it as a service. Resolve the provider from the
Grow service registry, then open a named store:

```csharp
using Grow.Store.Contracts;

IStoreProvider stores = /* resolved from the Grow service registry */;
IStore store = stores.GetStore("example", StoreType.User);

store.Set("some-int", 42);
store.Set("some-text", "value");
store.Set("some-bytes", new byte[] { 1, 2, 3 });
store.Save();
```

Read values with `Get` or `TryGet`:

```csharp
int number = store.Get("some-int", 0);

if (store.TryGet<string>("some-text", out string text))
{
    // use text
}
```

`Set`, `Delete` and `Clear` only change the in-memory cache. Call `Save` once after a batch of
changes. A save with no changes performs no IO.

## Scopes

A scope selects where files live. All stores of one scope share one directory.

| `StoreType` | Editor | Player |
| --- | --- | --- |
| `Editor` | `UserSettings/Grow/Editor` | Not supported |
| `Runtime` | `UserSettings/Grow/Runtime` | `persistentDataPath/Grow/Runtime` |
| `User` | `UserSettings/Grow/User` | `persistentDataPath/Grow/User` |

The scope selects a directory; it does not restrict which values a store may hold. Editor `Runtime`
and player `Runtime` are separate by default; `UsePlayerPathInEditor` makes the editor use the player
path.

Store names are lower-case and use `a-z`, `0-9`, `.`, `_` and `-`. They cannot contain whitespace,
path separators or `..`, and cannot use the reserved names `manifests`, `quarantine` or a name ending
in `.blobs`.

## Supported values

| Kind | Notes |
| --- | --- |
| `int`, `long` | Invariant culture |
| `float`, `double` | Exact round-trip text |
| `bool` | Stored as `0` or `1` |
| `string` | Round-trips whitespace and control characters |
| `byte[]` | Stored in a separate blob file |

Other types are rejected with `NotSupportedException`.

## Saving

- `Save()` commits the cache and flushes it.
- `TrySave(out string error)` reports a failed write instead of throwing.
- `Save` never runs on a timer.
- `TrySaveAll` saves every healthy dirty store and commits the scope index once.
- On pause, focus loss and quit, `StoreProvider` saves within a small budget. A store with a pending
  blob above the configured threshold is skipped and remains dirty.

Two named stores share a scope directory but have separate files.

## Failure and recovery

Only physical disk or IO failures stop a save. Capacity thresholds log but do not fail:

| Setting | Default |
| --- | --- |
| `TargetBucketBytes` | 2 MiB |
| `BlobWarningBytes` | 64 MiB |
| `BucketCountLimit` | 4096 |

A corrupted store reports `IsCorrupted`. Reads return only data that can be validated; writes throw
`InvalidOperationException`; `TrySave` returns `false`. `Reset()` clears and rebuilds the store, and
`DeleteStore` on the provider removes it.

See [Data on disk](storage.md) for the file layout, backup and recovery behaviour.

## See also

- [Data on disk](storage.md)
