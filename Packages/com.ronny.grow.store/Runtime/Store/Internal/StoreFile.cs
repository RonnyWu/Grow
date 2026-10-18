// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using Grow.Store.Contracts;
using Grow.Store.Format;
using Grow.Store.Scope;

namespace Grow.Store.Internal {
    /// <summary>One named store: cache, bucket state and commit plan.</summary>
    internal sealed class StoreFile : IStore {
        private readonly StoreScope _scope;
        private readonly string _name;
        private readonly object _gate = new object();
        private Dictionary<string, StoreEntry> _cache =
            new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
        private readonly HashSet<int> _dirtyBuckets = new HashSet<int>();
        private readonly Dictionary<int, long> _versions = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _bytes = new Dictionary<int, long>();
        private bool _loaded;
        private bool _dirty;
        private bool _corrupted;
        private bool _closed;
        private int _bucketCount = 1;

        public StoreFile(StoreScope scope, string name) {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name => _name;

        public bool IsClosed => _closed;

        public bool IsDirty {
            get {
                lock (_gate) {
                    return _dirty;
                }
            }
        }

        public bool IsCorrupted {
            get {
                lock (_gate) {
                    if (!_closed) {
                        EnsureLoadedLocked();
                    }

                    return _corrupted;
                }
            }
        }

        public StoreSize Size {
            get {
                lock (_gate) {
                    EnsureLoadedLocked();
                    long data = 0;
                    long blobs = 0;
                    foreach (var pair in _cache) {
                        data += StoreValues.EstimateBytes(pair.Key, pair.Value);
                        if (pair.Value.Data != null) {
                            blobs += pair.Value.Data.Length;
                        } else if (StoreValues.TryGetPointer(
                                       pair.Value, out _, out var length, out _)) {
                            blobs += length;
                        }
                    }

                    return new StoreSize(data, blobs);
                }
            }
        }

        public T Get<T>(string key, T defaultValue = default) {
            lock (_gate) {
                ThrowIfClosed();
                if (!StoreValues.IsSupported(typeof(T))) {
                    throw StoreValues.Unsupported<T>();
                }

                EnsureLoadedLocked();
                var normalized = StoreValues.NormalizeKey(key);
                if (!_cache.TryGetValue(normalized, out var entry)) {
                    return defaultValue;
                }

                if (TryReadEntry(entry, out T value)) {
                    return value;
                }

                if (entry.Kind == StoreValueKind.Blob || _corrupted) {
                    return defaultValue;
                }

                throw new InvalidCastException(
                    $"Store key '{normalized}' holds {entry.Kind}, not {typeof(T).Name}.");
            }
        }

        public bool TryGet<T>(string key, out T value) {
            lock (_gate) {
                ThrowIfClosed();
                if (!StoreValues.IsSupported(typeof(T))) {
                    throw StoreValues.Unsupported<T>();
                }

                EnsureLoadedLocked();
                var normalized = StoreValues.NormalizeKey(key);
                if (!_cache.TryGetValue(normalized, out var entry)) {
                    value = default;
                    return false;
                }

                return TryReadEntry(entry, out value);
            }
        }

        public IStore Set<T>(string key, T value) {
            lock (_gate) {
                ThrowIfClosed();
                ThrowIfCorrupted();
                EnsureLoadedLocked();
                if (!StoreValues.IsSupported(typeof(T))) {
                    throw StoreValues.Unsupported<T>();
                }

                var normalized = StoreValues.NormalizeKey(key);
                var entry = StoreValues.Create(value);
                if (_cache.TryGetValue(normalized, out var existing) && EntryEquals(existing, entry)) {
                    return this;
                }

                _cache[normalized] = entry;
                _dirtyBuckets.Add(BucketOf(normalized));
                _dirty = true;
                return this;
            }
        }

        public bool ContainsKey(string key) {
            lock (_gate) {
                ThrowIfClosed();
                EnsureLoadedLocked();
                return _cache.ContainsKey(StoreValues.NormalizeKey(key));
            }
        }

        public IStore Delete(string key) {
            lock (_gate) {
                ThrowIfClosed();
                ThrowIfCorrupted();
                EnsureLoadedLocked();
                var normalized = StoreValues.NormalizeKey(key);
                if (_cache.Remove(normalized)) {
                    _dirtyBuckets.Add(BucketOf(normalized));
                    _dirty = true;
                }

                return this;
            }
        }

        public IStore Clear() {
            lock (_gate) {
                ThrowIfClosed();
                ThrowIfCorrupted();
                EnsureLoadedLocked();
                if (_cache.Count == 0) {
                    return this;
                }

                _cache.Clear();
                for (var bucket = 0; bucket < _bucketCount; bucket++) {
                    _dirtyBuckets.Add(bucket);
                }

                _dirty = true;
                return this;
            }
        }

        public IStore Save() {
            ThrowIfClosed();
            if (IsCorrupted) {
                throw new InvalidOperationException(
                    $"Store '{_name}' is corrupted; call Reset before saving.");
            }

            _scope.Commit(new[] { this }, true);
            return this;
        }

        public bool TrySave(out string error) {
            try {
                Save();
                error = null;
                return true;
            } catch (ObjectDisposedException) {
                throw;
            } catch (Exception exception) {
                error = exception.Message;
                return false;
            }
        }

        public bool Reset() {
            lock (_gate) {
                ThrowIfClosed();
                if (!_scope.ResetStore(this)) {
                    return false;
                }

                _cache.Clear();
                _versions.Clear();
                _bytes.Clear();
                _dirtyBuckets.Clear();
                _bucketCount = 1;
                _dirty = false;
                _corrupted = false;
                _loaded = true;
                return true;
            }
        }

        internal void MarkClosed() {
            lock (_gate) {
                _closed = true;
            }
        }

        internal void MarkCorrupted() {
            lock (_gate) {
                _corrupted = true;
            }
        }

        internal StoreCommitPlan PrepareCommit() {
            lock (_gate) {
                if (!_dirty || _corrupted || _closed) {
                    return null;
                }

                EnsureLoadedLocked();
                if (_dirtyBuckets.Count == 0) {
                    _dirty = false;
                    return null;
                }

                var resolved = new Dictionary<string, StoreEntry>(_cache, StringComparer.Ordinal);
                var plan = new StoreCommitPlan(this, resolved);
                foreach (var bucket in _dirtyBuckets) {
                    var entries = new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
                    foreach (var pair in resolved) {
                        if (BucketOf(pair.Key) == bucket) {
                            entries[pair.Key] = pair.Value;
                        }
                    }

                    foreach (var key in new List<string>(entries.Keys)) {
                        var entry = entries[key];
                        if (entry.Kind != StoreValueKind.Blob || entry.Data == null) {
                            continue;
                        }

                        var pointer = _scope.WriteBlob(_name, entry.Data);
                        var resolvedEntry = entry.WithText(pointer);
                        entries[key] = resolvedEntry;
                        resolved[key] = resolvedEntry;
                    }

                    _versions.TryGetValue(bucket, out var version);
                    plan.Buckets.Add(new StoreBucketWrite(bucket, version + 1, entries));
                }

                return plan;
            }
        }

        internal void ApplyCommit(StoreCommitPlan plan) {
            lock (_gate) {
                _cache = plan.ResolvedCache;
                for (var i = 0; i < plan.Buckets.Count; i++) {
                    var bucket = plan.Buckets[i];
                    _versions[bucket.Bucket] = bucket.Version;
                    _bytes[bucket.Bucket] = bucket.Bytes;
                }

                _dirtyBuckets.Clear();
                _dirty = false;
                _loaded = true;
            }
        }

        internal IReadOnlyDictionary<string, StoreEntry> ExportEntries() {
            lock (_gate) {
                EnsureLoadedLocked();
                return new Dictionary<string, StoreEntry>(_cache, StringComparer.Ordinal);
            }
        }

        internal int BucketCount {
            get {
                lock (_gate) {
                    EnsureLoadedLocked();
                    return _bucketCount;
                }
            }
        }

        internal bool NeedsSplit(long targetBytes) {
            lock (_gate) {
                if (_corrupted) {
                    return false;
                }

                EnsureLoadedLocked();
                foreach (var pair in _bytes) {
                    if (pair.Value > targetBytes) {
                        return true;
                    }
                }

                return false;
            }
        }

        internal void ApplySplit(int bucketCount, IReadOnlyDictionary<int, long> versions,
            IReadOnlyDictionary<int, long> bytes) {
            lock (_gate) {
                _bucketCount = bucketCount;
                _versions.Clear();
                _bytes.Clear();
                foreach (var pair in versions) {
                    _versions[pair.Key] = pair.Value;
                }

                foreach (var pair in bytes) {
                    _bytes[pair.Key] = pair.Value;
                }

                _dirtyBuckets.Clear();
                _dirty = false;
                _loaded = true;
            }
        }

        internal void SetLoaded(StoreManifest.StoreRecord record,
            Dictionary<string, StoreEntry> entries, bool corrupted) {
            _bucketCount = record == null ? 1 : record.BucketCount;
            _versions.Clear();
            _bytes.Clear();
            if (record != null) {
                foreach (var pair in record.Buckets) {
                    _versions[pair.Key] = pair.Value.Version;
                    _bytes[pair.Key] = pair.Value.Bytes;
                }
            }

            _cache = entries ?? new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
            _corrupted = corrupted;
            _loaded = true;
        }

        internal IReadOnlyDictionary<int, long> Versions => _versions;

        internal IReadOnlyDictionary<int, long> BucketBytes => _bytes;

        internal long PendingBytes() {
            lock (_gate) {
                long total = 0;
                foreach (var pair in _cache) {
                    if (_dirtyBuckets.Contains(BucketOf(pair.Key))) {
                        total += StoreValues.EstimateBytes(pair.Key, pair.Value);
                    }
                }

                return total;
            }
        }

        internal long PendingBlobBytes() {
            lock (_gate) {
                long total = 0;
                foreach (var entry in _cache.Values) {
                    if (entry.Kind == StoreValueKind.Blob && entry.Data != null) {
                        total += entry.Data.Length;
                    }
                }

                return total;
            }
        }

        private bool TryReadEntry<T>(StoreEntry entry, out T value) {
            value = default;
            if (!StoreValues.IsSupported(typeof(T))) {
                throw StoreValues.Unsupported<T>();
            }

            if (typeof(T) != typeof(byte[])) {
                return StoreValues.TryGet(entry, out value);
            }

            if (entry.Kind != StoreValueKind.Blob) {
                return false;
            }

            if (entry.Data != null) {
                var copy = new byte[entry.Data.Length];
                Array.Copy(entry.Data, copy, copy.Length);
                value = (T)(object)copy;
                return true;
            }

            if (!StoreValues.TryParsePointer(entry.Text, out var blobId, out var length,
                    out var crc)) {
                _corrupted = true;
                return false;
            }

            if (!_scope.TryReadBlob(_name, blobId, length, crc, out var bytes)) {
                _corrupted = true;
                return false;
            }

            value = (T)(object)bytes;
            return true;
        }

        private void EnsureLoadedLocked() {
            if (_loaded) {
                return;
            }

            _loaded = true;
            _scope.LoadStore(this);
        }

        private int BucketOf(string key) {
            var count = _bucketCount < 1 ? 1 : _bucketCount;
            return (int)(StoreHash.Hash(key) & (uint)(count - 1));
        }

        private void ThrowIfClosed() {
            if (_closed) {
                throw new ObjectDisposedException(nameof(StoreFile));
            }
        }

        private void ThrowIfCorrupted() {
            if (_corrupted) {
                throw new InvalidOperationException(
                    $"Store '{_name}' is corrupted; call Reset before writing.");
            }
        }

        private static bool EntryEquals(StoreEntry left, StoreEntry right) {
            if (left.Kind != right.Kind) {
                return false;
            }

            if (left.Data != null || right.Data != null) {
                if (left.Data == null || right.Data == null || left.Data.Length != right.Data.Length) {
                    return false;
                }

                for (var i = 0; i < left.Data.Length; i++) {
                    if (left.Data[i] != right.Data[i]) {
                        return false;
                    }
                }

                return true;
            }

            return string.Equals(left.Text, right.Text, StringComparison.Ordinal);
        }
    }

    internal sealed class StoreCommitPlan {
        public StoreCommitPlan(StoreFile store, Dictionary<string, StoreEntry> resolvedCache) {
            Store = store;
            ResolvedCache = resolvedCache;
        }

        public StoreFile Store { get; }

        public Dictionary<string, StoreEntry> ResolvedCache { get; }

        public List<StoreBucketWrite> Buckets { get; } = new List<StoreBucketWrite>();
    }

    internal sealed class StoreBucketWrite {
        public StoreBucketWrite(int bucket, long version,
            Dictionary<string, StoreEntry> entries) {
            Bucket = bucket;
            Version = version;
            Entries = entries;
        }

        public int Bucket { get; }

        public long Version { get; }

        public long Bytes { get; set; }

        public Dictionary<string, StoreEntry> Entries { get; }
    }
}
