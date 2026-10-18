// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Grow.Core.Events;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using Grow.Store.Contracts;
using Grow.Store.Format;
using Grow.Store.Import;
using Grow.Store.Internal;
using UnityEngine;

namespace Grow.Store.Scope {
    /// <summary>One scope directory: manifest, stores, blobs, lifecycle and recovery.</summary>
    internal sealed class StoreScope : IDisposable {
        private const string LogCategory = "Store";
        private const string ManifestName = "manifests";

        private static readonly object ActiveGate = new object();
        private static readonly HashSet<string> ActiveRoots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly object _gate = new object();
        private readonly string _root;
        private readonly StoreOptions _options;
        private readonly Dictionary<string, StoreFile> _stores =
            new Dictionary<string, StoreFile>(StringComparer.Ordinal);
        private StoreManifest _manifest;
        private GrowSubscription _pauseSubscription;
        private GrowSubscription _focusSubscription;
        private GrowSubscription _quitSubscription;
        private bool _disposed;
        private bool _splitting;

        public StoreScope(string root, StoreOptions options) {
            if (string.IsNullOrWhiteSpace(root)) {
                throw new ArgumentNullException(nameof(root));
            }

            _root = Path.GetFullPath(root);
            _options = options ?? new StoreOptions();
            _options.Validate();
            lock (ActiveGate) {
                if (!ActiveRoots.Add(_root)) {
                    throw new InvalidOperationException(
                        $"A store provider already owns the scope '{_root}'.");
                }
            }

            _manifest = StoreManifest.LoadOrRebuild(_root, out _);
            MigrateSingleFileStoresLocked();
            SubscribeLifecycle();
        }

        public StoreFile GetStore(string name) {
            lock (_gate) {
                ThrowIfDisposed();
                if (_stores.TryGetValue(name, out var existing)) {
                    return existing;
                }

                var store = new StoreFile(this, name);
                _stores.Add(name, store);
                return store;
            }
        }

        public bool TryGetStore(string name, out StoreFile store) {
            lock (_gate) {
                if (!_disposed && _stores.TryGetValue(name, out store)) {
                    return true;
                }

                store = null;
                return false;
            }
        }

        public bool Close(string name) {
            lock (_gate) {
                ThrowIfDisposed();
                if (!_stores.TryGetValue(name, out var store)) {
                    return false;
                }

                if (!store.IsCorrupted && store.IsDirty) {
                    try {
                        CommitLocked(new[] { store }, true);
                    } catch (Exception exception) {
                        GrowLog.Error(
                            $"[Store] Close failed to save '{name}': {exception}", LogCategory);
                        return false;
                    }
                }

                _stores.Remove(name);
                store.MarkClosed();
                return true;
            }
        }

        public bool TrySaveAll(out string error) {
            lock (_gate) {
                ThrowIfDisposed();
                var dirty = new List<StoreFile>();
                var corrupted = (string)null;
                foreach (var store in _stores.Values) {
                    if (!store.IsDirty) {
                        continue;
                    }

                    if (store.IsCorrupted) {
                        corrupted ??= $"store '{store.Name}' is corrupted";
                        continue;
                    }

                    dirty.Add(store);
                }

                if (dirty.Count > 0) {
                    try {
                        CommitLocked(dirty, true);
                    } catch (Exception exception) {
                        error = exception.Message;
                        return false;
                    }
                }

                if (corrupted != null) {
                    error = corrupted;
                    return false;
                }

                error = null;
                return true;
            }
        }

        public bool TrySaveAllWithinBudget() {
            lock (_gate) {
                if (_disposed) {
                    return false;
                }

                var selected = new List<StoreFile>();
                var bytes = 0L;
                var fsyncs = 0;
                foreach (var store in _stores.Values) {
                    if (!store.IsDirty || store.IsCorrupted) {
                        continue;
                    }

                    if (selected.Count >= _options.LifecycleMaxStores ||
                        fsyncs >= _options.LifecycleMaxFsyncs) {
                        break;
                    }

                    var pending = store.PendingBytes();
                    var storeFsyncs = Math.Max(1, store.Versions.Count);
                    if (pending > _options.LifecycleMaxBytes - bytes ||
                        fsyncs + storeFsyncs > _options.LifecycleMaxFsyncs ||
                        store.PendingBlobBytes() > _options.LifecycleMaxBlobBytes) {
                        continue;
                    }

                    selected.Add(store);
                    bytes += pending;
                    fsyncs += storeFsyncs;
                }

                if (selected.Count == 0) {
                    if (HasDirtyStores()) {
                        GrowLog.Warning(
                            "[Store] Lifecycle budget skipped every dirty store; data remains in memory.",
                            LogCategory);
                    }

                    return true;
                }

                try {
                    CommitLocked(selected, true);
                    return true;
                } catch (Exception exception) {
                    GrowLog.Error($"[Store] Lifecycle save failed: {exception}", LogCategory);
                    return false;
                }
            }
        }

        public bool DeleteStore(string name) {
            lock (_gate) {
                ThrowIfDisposed();
                if (_stores.TryGetValue(name, out var store)) {
                    _stores.Remove(name);
                    store.MarkClosed();
                }

                var hadRecord = _manifest.Stores.TryGetValue(name, out var record);
                if (hadRecord) {
                    _manifest.Stores.Remove(name);
                    if (!TryWriteManifestLocked()) {
                        _manifest.Stores[name] = record;
                        return false;
                    }
                }

                DeleteStoreFiles(name);
                return true;
            }
        }

        public bool ResetStore(StoreFile store) {
            lock (_gate) {
                ThrowIfDisposed();
                var hadRecord = _manifest.Stores.TryGetValue(store.Name, out var record);
                if (hadRecord) {
                    _manifest.Stores.Remove(store.Name);
                    if (!TryWriteManifestLocked()) {
                        _manifest.Stores[store.Name] = record;
                        return false;
                    }
                }

                DeleteStoreFiles(store.Name);
                return true;
            }
        }

        public bool TrySplit(string name, out string error) {
            lock (_gate) {
                ThrowIfDisposed();
                if (!_stores.TryGetValue(name, out var store)) {
                    error = "store is not open";
                    return false;
                }

                if (store.IsDirty) {
                    error = "store has uncommitted changes";
                    return false;
                }

                if (store.IsCorrupted) {
                    error = "store is corrupted; reset it first";
                    return false;
                }

                try {
                    SplitStoreLocked(store, true);
                    error = null;
                    return true;
                } catch (Exception exception) {
                    error = exception.Message;
                    return false;
                }
            }
        }

        public bool TryGc(string name, out string error) {
            lock (_gate) {
                ThrowIfDisposed();
                if (!_stores.TryGetValue(name, out var store)) {
                    error = "store is not open";
                    return false;
                }

                if (store.IsDirty) {
                    error = "store has uncommitted changes";
                    return false;
                }

                if (store.IsCorrupted) {
                    error = "store is corrupted; reset it first";
                    return false;
                }

                try {
                    GcBucketVersionsLocked(name);
                    GcBlobsLocked(name);
                    error = null;
                    return true;
                } catch (Exception exception) {
                    error = exception.Message;
                    return false;
                }
            }
        }

        public void Commit(IReadOnlyList<StoreFile> stores, bool flush) {
            lock (_gate) {
                CommitLocked(stores, flush);
            }
        }

        public void LoadStore(StoreFile store) {
            lock (_gate) {
                ThrowIfDisposed();
                var record = _manifest.GetStore(store.Name);
                if (record == null) {
                    store.SetLoaded(null, new Dictionary<string, StoreEntry>(
                        StringComparer.Ordinal), false);
                    return;
                }

                var entries = new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
                var corrupted = false;
                for (var bucket = 0; bucket < record.BucketCount; bucket++) {
                    if (!record.Buckets.TryGetValue(bucket, out var bucketRecord)) {
                        corrupted = true;
                        GrowLog.Error(
                            $"[Store] Missing bucket {bucket} in the manifest for '{store.Name}'.",
                            LogCategory);
                        continue;
                    }

                    var pair = new KeyValuePair<int, StoreManifest.BucketRecord>(
                        bucket, bucketRecord);
                    var path = Path.Combine(
                        _root, BucketFileName(store.Name, pair.Key, pair.Value.Version));
                    if (!File.Exists(path)) {
                        corrupted = true;
                        GrowLog.Error(
                            $"[Store] Missing bucket '{path}' for '{store.Name}'.", LogCategory);
                        continue;
                    }

                    if (!StoreBucketFile.TryRead(path, out var data, out var readError)) {
                        corrupted = true;
                        Quarantine(path);
                        GrowLog.Error(
                            $"[Store] Invalid bucket '{path}': {readError}", LogCategory);
                        continue;
                    }

                    if (!string.Equals(data.Store, store.Name, StringComparison.Ordinal) ||
                        data.Bucket != pair.Key || data.Version != pair.Value.Version ||
                        data.BucketCount != record.BucketCount) {
                        corrupted = true;
                        Quarantine(path);
                        GrowLog.Error(
                            $"[Store] Bucket identity mismatch in '{path}'.", LogCategory);
                        continue;
                    }

                    foreach (var entry in data.Entries) {
                        if (entries.ContainsKey(entry.Key)) {
                            corrupted = true;
                            GrowLog.Error(
                                $"[Store] Duplicate key '{entry.Key}' in '{store.Name}'.",
                                LogCategory);
                            continue;
                        }

                        entries.Add(entry.Key, entry.Value);
                    }
                }

                store.SetLoaded(record, entries, corrupted);
                if (!corrupted) {
                    MaybeSplitLocked(store, record);
                }
            }
        }

        public bool TryReadBlob(string store, string blobId, long length, uint crc,
            out byte[] data) {
            data = null;
            var path = Path.Combine(_root, store + ".blobs", blobId + ".bin");
            try {
                if (!File.Exists(path)) {
                    return false;
                }

                var bytes = File.ReadAllBytes(path);
                if (bytes.LongLength != length ||
                    StoreHash.Crc32(bytes, 0, bytes.Length) != crc) {
                    return false;
                }

                data = bytes;
                return true;
            } catch (Exception exception) {
                GrowLog.Error($"[Store] Failed to read blob '{path}': {exception}", LogCategory);
                return false;
            }
        }

        public string WriteBlob(string store, byte[] data) {
            var crc = StoreHash.Crc32(data, 0, data.Length);
            var id = Guid.NewGuid().ToString("N");
            var directory = Path.Combine(_root, store + ".blobs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, id + ".bin");
            var temporary = path + ".tmp";
            try {
                using (var stream = new FileStream(
                           temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }

                File.Move(temporary, path);
            } catch {
                TryDelete(temporary);
                throw;
            }

            if (data.LongLength > _options.BlobWarningBytes) {
                GrowLog.Warning(
                    $"[Store] Blob in '{store}' is {data.Length} bytes; " +
                    $"threshold is {_options.BlobWarningBytes}.", LogCategory);
            }

            return StoreValues.FormatPointer(id, data.LongLength, crc);
        }

        public void Dispose() {
            lock (_gate) {
                if (_disposed) {
                    return;
                }

                _disposed = true;
                foreach (var store in _stores.Values) {
                    store.MarkClosed();
                }

                _stores.Clear();
            }

            _pauseSubscription.Dispose();
            _focusSubscription.Dispose();
            _quitSubscription.Dispose();
            lock (ActiveGate) {
                ActiveRoots.Remove(_root);
            }
        }

        public static void MigrateSingleFileRuntimePath(string root) {
            var oldRoot = Path.Combine(Application.persistentDataPath, "Grow");
            var oldRootFull = Path.GetFullPath(oldRoot);
            var rootFull = Path.GetFullPath(root);
            if (string.Equals(oldRootFull, rootFull, StringComparison.OrdinalIgnoreCase) ||
                !rootFull.StartsWith(
                    oldRootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(oldRoot) || File.Exists(Path.Combine(root, "manifests"))) {
                return;
            }

            string[] files;
            try {
                files = Directory.GetFiles(oldRoot, "*.kv", SearchOption.TopDirectoryOnly);
            } catch {
                return;
            }

            if (files.Length == 0) {
                return;
            }

            try {
                Directory.CreateDirectory(root);
                for (var i = 0; i < files.Length; i++) {
                    var target = Path.Combine(root, Path.GetFileName(files[i]));
                    if (!File.Exists(target)) {
                        File.Move(files[i], target);
                    }
                }
            } catch (Exception exception) {
                GrowLog.Warning(
                    $"[Store] Could not migrate the single-file runtime path: {exception}", LogCategory);
            }
        }

        public static string GetRootDirectory(StoreType storeType, StoreOptions options,
            string rootOverride) {
            if (!string.IsNullOrEmpty(rootOverride)) {
                return Path.Combine(Path.GetFullPath(rootOverride), storeType.ToString());
            }

#if UNITY_EDITOR
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
            switch (storeType) {
                case StoreType.Editor:
                    return Path.GetFullPath(Path.Combine(projectRoot, "UserSettings/Grow/Editor"));
                case StoreType.Runtime:
                    return options.UsePlayerPathInEditor
                        ? Path.Combine(Application.persistentDataPath, "Grow/Runtime")
                        : Path.GetFullPath(
                            Path.Combine(projectRoot, "UserSettings/Grow/Runtime"));
                case StoreType.User:
                    return Path.GetFullPath(Path.Combine(projectRoot, "UserSettings/Grow/User"));
                default:
                    throw new ArgumentOutOfRangeException(nameof(storeType), storeType, null);
            }
#else
            switch (storeType) {
                case StoreType.Runtime:
                    return Path.Combine(Application.persistentDataPath, "Grow/Runtime");
                case StoreType.User:
                    return Path.Combine(Application.persistentDataPath, "Grow/User");
                case StoreType.Editor:
                    throw new NotSupportedException("The editor store is not available in a player.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(storeType), storeType, null);
            }
#endif
        }

        private void MigrateSingleFileStoresLocked() {
            if (!Directory.Exists(_root)) {
                return;
            }

            string[] files;
            try {
                files = Directory.GetFiles(_root, "*.kv", SearchOption.TopDirectoryOnly);
            } catch {
                return;
            }

            for (var i = 0; i < files.Length; i++) {
                var file = files[i];
                var fileName = Path.GetFileName(file);
                if (StoreManifest.TryParseFileName(fileName, out _, out _, out _)) {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(fileName);
                try {
                    name = StoreNames.Normalize(name);
                } catch (ArgumentException) {
                    continue;
                }

                if (_manifest.GetStore(name) != null || HasVersionedBuckets(name)) {
                    continue;
                }

                if (!StoreSingleFile.TryRead(file, out var singleFile, out var readError)) {
                    GrowLog.Warning(
                        $"[Store] Cannot migrate single-file store '{file}': {readError}", LogCategory);
                    continue;
                }

                var entries = new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
                foreach (var pair in singleFile) {
                    var entry = pair.Value;
                    if (entry.Kind == StoreValueKind.Blob && entry.Data != null) {
                        entry = entry.WithText(WriteBlob(name, entry.Data));
                    }

                    entries[pair.Key] = entry;
                }

                var record = new StoreManifest.StoreRecord(1);
                try {
                    var bytes = StoreBucketFile.Write(
                        Path.Combine(_root, BucketFileName(name, 0, 1)),
                        name, 0, 1, 1, entries, true);
                    record.Buckets[0] = new StoreManifest.BucketRecord(1, bytes);
                    _manifest.Stores[name] = record;
                    _manifest.Generation++;
                    if (!TryWriteManifestLocked()) {
                        _manifest.Stores.Remove(name);
                        continue;
                    }

                    File.Move(file, file + ".migrated");
                    GrowLog.Info($"[Store] Migrated single-file store '{file}'.", LogCategory);
                } catch (Exception exception) {
                    _manifest.Stores.Remove(name);
                    GrowLog.Error(
                        $"[Store] Failed to migrate single-file store '{file}': {exception}",
                        LogCategory);
                }
            }
        }

        private bool HasVersionedBuckets(string name) {
            try {
                var files = Directory.GetFiles(_root, "*.kv", SearchOption.TopDirectoryOnly);
                for (var i = 0; i < files.Length; i++) {
                    if (StoreManifest.TryParseFileName(
                            Path.GetFileName(files[i]), out var parsed, out _, out _) &&
                        string.Equals(parsed, name, StringComparison.Ordinal)) {
                        return true;
                    }
                }
            } catch {
                // A failed probe is treated as "no versioned buckets".
            }

            return false;
        }

        private void CommitLocked(IReadOnlyList<StoreFile> stores, bool flush) {
            ThrowIfDisposed();
            var plans = new List<StoreCommitPlan>();
            for (var i = 0; i < stores.Count; i++) {
                var plan = stores[i].PrepareCommit();
                if (plan != null && plan.Buckets.Count > 0) {
                    plans.Add(plan);
                }
            }

            if (plans.Count == 0) {
                return;
            }

            Directory.CreateDirectory(_root);
            var oldRecords = new Dictionary<string, StoreManifest.StoreRecord>(StringComparer.Ordinal);
            try {
                for (var i = 0; i < plans.Count; i++) {
                    var plan = plans[i];
                    for (var b = 0; b < plan.Buckets.Count; b++) {
                        var bucket = plan.Buckets[b];
                        var path = Path.Combine(
                            _root, BucketFileName(plan.Store.Name, bucket.Bucket, bucket.Version));
                        bucket.Bytes = StoreBucketFile.Write(
                            path, plan.Store.Name, bucket.Bucket, bucket.Version,
                            plan.Store.BucketCount, bucket.Entries, flush);
                    }
                }

                for (var i = 0; i < plans.Count; i++) {
                    var plan = plans[i];
                    if (!oldRecords.ContainsKey(plan.Store.Name)) {
                        oldRecords[plan.Store.Name] = Clone(_manifest.GetStore(plan.Store.Name));
                    }

                    var record = _manifest.EnsureStore(plan.Store.Name);
                    record.BucketCount = plan.Store.BucketCount;
                    for (var b = 0; b < plan.Buckets.Count; b++) {
                        var bucket = plan.Buckets[b];
                        record.Buckets[bucket.Bucket] =
                            new StoreManifest.BucketRecord(bucket.Version, bucket.Bytes);
                    }
                }

                _manifest.Generation++;
                WriteManifestLocked();
            } catch {
                RestoreRecords(oldRecords);
                throw;
            }

            for (var i = 0; i < plans.Count; i++) {
                plans[i].Store.ApplyCommit(plans[i]);
                GcBucketVersionsLocked(plans[i].Store.Name);
                GcBlobsLocked(plans[i].Store.Name);
            }
        }

        private void MaybeSplitLocked(StoreFile store, StoreManifest.StoreRecord record) {
            if (_splitting || record == null) {
                return;
            }

            var needsSplit = false;
            foreach (var bucket in record.Buckets) {
                if (bucket.Value.Bytes > _options.TargetBucketBytes) {
                    needsSplit = true;
                    break;
                }
            }

            if (!needsSplit) {
                return;
            }

            SplitStoreLocked(store, false);
        }

        private void SplitStoreLocked(StoreFile store, bool force) {
            var record = _manifest.GetStore(store.Name);
            if (record == null) {
                if (force) {
                    throw new InvalidOperationException("The store has no persisted data.");
                }

                return;
            }

            var entries = store.ExportEntries();
            var oldCount = record.BucketCount;
            var newCount = oldCount;
            while (newCount < _options.BucketCountLimit && HasOversizedBucket(record, entries,
                       newCount)) {
                newCount *= 2;
                if (newCount > Math.Max(1, entries.Count) && newCount > oldCount) {
                    break;
                }
            }

            if (newCount == oldCount) {
                if (force && HasOversizedBucket(record, entries, oldCount)) {
                    GrowLog.Warning(
                        $"[Store] '{store.Name}' cannot split further; a single value exceeds " +
                        $"{_options.TargetBucketBytes} bytes.", LogCategory);
                }

                return;
            }

            _splitting = true;
            var replacement = new StoreManifest.StoreRecord(newCount);
            var versions = new Dictionary<int, long>();
            var bytes = new Dictionary<int, long>();
            try {
                for (var bucket = 0; bucket < newCount; bucket++) {
                    var subset = new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
                    foreach (var entry in entries) {
                        if ((StoreHash.Hash(entry.Key) & (uint)(newCount - 1)) == (uint)bucket) {
                            subset.Add(entry.Key, entry.Value);
                        }
                    }

                    record.Buckets.TryGetValue(bucket, out var old);
                    var version = old.Version + 1;
                    if (version <= 0) {
                        version = 1;
                    }

                    var path = Path.Combine(_root, BucketFileName(store.Name, bucket, version));
                    bytes[bucket] = StoreBucketFile.Write(
                        path, store.Name, bucket, version, newCount, subset, true);
                    versions[bucket] = version;
                    replacement.Buckets[bucket] =
                        new StoreManifest.BucketRecord(version, bytes[bucket]);
                }

                _manifest.Stores[store.Name] = replacement;
                _manifest.Generation++;
                WriteManifestLocked();
                store.ApplySplit(newCount, versions, bytes);
                GcBucketVersionsLocked(store.Name);
                GcBlobsLocked(store.Name);
            } catch {
                _manifest.Stores[store.Name] = record;
                throw;
            } finally {
                _splitting = false;
            }
        }

        private bool HasOversizedBucket(StoreManifest.StoreRecord record,
            IReadOnlyDictionary<string, StoreEntry> entries, int bucketCount) {
            var sizes = new Dictionary<int, long>();
            foreach (var entry in entries) {
                var bucket = (int)(StoreHash.Hash(entry.Key) & (uint)(bucketCount - 1));
                sizes.TryGetValue(bucket, out var size);
                sizes[bucket] = size + StoreValues.EstimateBytes(entry.Key, entry.Value);
            }

            foreach (var size in sizes.Values) {
                if (size > _options.TargetBucketBytes) {
                    return true;
                }
            }

            return false;
        }

        private void WriteManifestLocked() {
            var text = _manifest.ToText();
            var bytes = Encoding.UTF8.GetBytes(text);
            Directory.CreateDirectory(_root);
            var manifestPath = Path.Combine(_root, ManifestName);
            var temporary = manifestPath + ".tmp";
            try {
                using (var stream = new FileStream(
                           temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (!StoreManifest.TryLoad(temporary, out _, out var error)) {
                    throw new IOException($"Temporary manifest failed verification: {error}");
                }

                ReplaceManifest(temporary, manifestPath);
            } catch {
                TryDelete(temporary);
                throw;
            }
        }

        private void ReplaceManifest(string temporary, string manifestPath) {
            if (!File.Exists(manifestPath)) {
                File.Move(temporary, manifestPath);
                return;
            }

            var backup = manifestPath + ".bak";
            try {
                File.Replace(temporary, manifestPath, backup);
            } catch (PlatformNotSupportedException) {
                FallbackReplace(temporary, manifestPath, backup);
            } catch (IOException) {
                FallbackReplace(temporary, manifestPath, backup);
            }
        }

        private void FallbackReplace(string temporary, string manifestPath, string backup) {
            var backupTemporary = backup + ".tmp";
            try {
                File.Copy(manifestPath, backupTemporary, true);
                using (var stream = new FileStream(
                           backupTemporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                    stream.Flush(true);
                }

                if (File.Exists(backup)) {
                    File.Delete(backup);
                }

                File.Move(backupTemporary, backup);
                File.Delete(manifestPath);
                File.Move(temporary, manifestPath);
            } catch {
                TryDelete(backupTemporary);
                throw;
            }
        }

        private bool TryWriteManifestLocked() {
            try {
                if (!File.Exists(Path.Combine(_root, ManifestName)) &&
                    _manifest.Stores.Count == 0) {
                    return true;
                }

                WriteManifestLocked();
                return true;
            } catch (Exception exception) {
                GrowLog.Error($"[Store] Failed to write manifests: {exception}", LogCategory);
                return false;
            }
        }

        private void GcBucketVersionsLocked(string store) {
            if (!Directory.Exists(_root)) {
                return;
            }

            var allowed = new HashSet<string>(StringComparer.Ordinal);
            AddVersions(_manifest.GetStore(store), allowed);
            var backupPath = Path.Combine(_root, ManifestName + ".bak");
            if (StoreManifest.TryLoad(backupPath, out var backup, out _)) {
                AddVersions(backup.GetStore(store), allowed);
            }

            string[] files;
            try {
                files = Directory.GetFiles(_root, store + ".*.*.kv", SearchOption.TopDirectoryOnly);
            } catch {
                return;
            }

            for (var i = 0; i < files.Length; i++) {
                if (!StoreManifest.TryParseFileName(
                        Path.GetFileName(files[i]), out var name, out var bucket,
                        out var version) ||
                    !string.Equals(name, store, StringComparison.Ordinal)) {
                    continue;
                }

                var key = bucket.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                          version.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!allowed.Contains(key)) {
                    TryDelete(files[i]);
                }
            }
        }

        private void GcBlobsLocked(string store) {
            var directory = Path.Combine(_root, store + ".blobs");
            if (!Directory.Exists(directory)) {
                return;
            }

            var used = new HashSet<string>(StringComparer.Ordinal);
            if (!CollectBlobIds(store, _manifest.GetStore(store), used)) {
                GrowLog.Warning(
                    $"[Store] Skipping blob GC for '{store}': a referenced bucket could not be read.",
                    LogCategory);
                return;
            }

            var backupPath = Path.Combine(_root, ManifestName + ".bak");
            if (StoreManifest.TryLoad(backupPath, out var backup, out _) &&
                !CollectBlobIds(store, backup.GetStore(store), used)) {
                GrowLog.Warning(
                    $"[Store] Skipping blob GC for '{store}': a backup bucket could not be read.",
                    LogCategory);
                return;
            }

            string[] files;
            try {
                files = Directory.GetFiles(directory, "*.bin", SearchOption.TopDirectoryOnly);
            } catch {
                return;
            }

            for (var i = 0; i < files.Length; i++) {
                var id = Path.GetFileNameWithoutExtension(files[i]);
                if (!used.Contains(id)) {
                    TryDelete(files[i]);
                }
            }

            try {
                var temporary = Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly);
                for (var i = 0; i < temporary.Length; i++) {
                    TryDelete(temporary[i]);
                }
            } catch {
                // Best effort.
            }
        }

        private bool CollectBlobIds(string store, StoreManifest.StoreRecord record,
            HashSet<string> used) {
            if (record == null) {
                return true;
            }

            foreach (var pair in record.Buckets) {
                var path = Path.Combine(_root, BucketFileName(store, pair.Key, pair.Value.Version));
                if (!StoreBucketFile.TryRead(path, out var data, out _)) {
                    return false;
                }

                foreach (var entry in data.Entries.Values) {
                    if (StoreValues.TryGetPointer(entry, out var id, out _, out _)) {
                        used.Add(id);
                    }
                }
            }

            return true;
        }

        private void DeleteStoreFiles(string store) {
            if (!Directory.Exists(_root)) {
                return;
            }

            TryDeleteDirectory(Path.Combine(_root, store + ".blobs"));
            string[] files;
            try {
                files = Directory.GetFiles(_root, "*.kv", SearchOption.TopDirectoryOnly);
            } catch {
                return;
            }

            for (var i = 0; i < files.Length; i++) {
                if (StoreManifest.TryParseFileName(
                        Path.GetFileName(files[i]), out var parsed, out _, out _) &&
                    string.Equals(parsed, store, StringComparison.Ordinal)) {
                    TryDelete(files[i]);
                }
            }

            var quarantine = Path.Combine(_root, "quarantine");
            if (Directory.Exists(quarantine)) {
                try {
                    var stale = Directory.GetFiles(
                        quarantine, "*", SearchOption.TopDirectoryOnly);
                    for (var i = 0; i < stale.Length; i++) {
                        if (QuarantineBelongsTo(Path.GetFileName(stale[i]), store)) {
                            TryDelete(stale[i]);
                        }
                    }
                } catch {
                    // Best effort.
                }
            }
        }

        private static bool QuarantineBelongsTo(string fileName, string store) {
            var marker = fileName.IndexOf(".kv.", StringComparison.Ordinal);
            if (marker < 0) {
                return false;
            }

            var original = fileName.Substring(0, marker + 3);
            return StoreManifest.TryParseFileName(original, out var parsed, out _, out _) &&
                   string.Equals(parsed, store, StringComparison.Ordinal);
        }

        private void Quarantine(string path) {
            var quarantine = Path.Combine(_root, "quarantine");
            Directory.CreateDirectory(quarantine);
            var target = Path.Combine(
                quarantine, Path.GetFileName(path) + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") +
                           "." + Guid.NewGuid().ToString("N"));
            try {
                File.Move(path, target);
            } catch (Exception exception) {
                GrowLog.Error($"[Store] Could not quarantine '{path}': {exception}", LogCategory);
            }
        }

        private void SubscribeLifecycle() {
            var manager = MonoManager.Instance;
            if (!manager.Valid) {
                return;
            }

            var pause = manager.AppPause;
            var focus = manager.AppFocus;
            var quit = manager.AppQuit;
            if (pause != null) {
                _pauseSubscription = pause.Subscribe(paused => {
                    if (paused) {
                        TrySaveAllWithinBudget();
                    }
                });
            }

            if (focus != null) {
                _focusSubscription = focus.Subscribe(focused => {
                    if (!focused) {
                        TrySaveAllWithinBudget();
                    }
                });
            }

            if (quit != null) {
                _quitSubscription = quit.Subscribe(() => TrySaveAll(out _));
            }
        }

        private bool HasDirtyStores() {
            foreach (var store in _stores.Values) {
                if (store.IsDirty) {
                    return true;
                }
            }

            return false;
        }

        private static void AddVersions(StoreManifest.StoreRecord record, HashSet<string> versions) {
            if (record == null) {
                return;
            }

            foreach (var pair in record.Buckets) {
                versions.Add(
                    pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                    pair.Value.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private static StoreManifest.StoreRecord Clone(StoreManifest.StoreRecord record) {
            if (record == null) {
                return null;
            }

            var clone = new StoreManifest.StoreRecord(record.BucketCount);
            foreach (var pair in record.Buckets) {
                clone.Buckets[pair.Key] = pair.Value;
            }

            return clone;
        }

        private void RestoreRecords(Dictionary<string, StoreManifest.StoreRecord> records) {
            foreach (var pair in records) {
                if (pair.Value == null) {
                    _manifest.Stores.Remove(pair.Key);
                } else {
                    _manifest.Stores[pair.Key] = pair.Value;
                }
            }
        }

        private static string BucketFileName(string store, int bucket, long version) {
            return string.Concat(
                store, ".", bucket.ToString(System.Globalization.CultureInfo.InvariantCulture), ".",
                version.ToString(System.Globalization.CultureInfo.InvariantCulture), ".kv");
        }

        private void ThrowIfDisposed() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(StoreProvider));
            }
        }

        private static void TryDelete(string path) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            } catch {
                // Best effort; a later GC retries.
            }
        }

        private static void TryDeleteDirectory(string path) {
            try {
                if (Directory.Exists(path)) {
                    Directory.Delete(path, true);
                }
            } catch {
                // Best effort.
            }
        }
    }
}
