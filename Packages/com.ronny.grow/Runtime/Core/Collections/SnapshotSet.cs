// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Grow.Core.Collections {
    public sealed class SnapshotSet<T> {
        private const int DefaultCapacity = 8;

        private readonly OrderedSet<T> _set;
        private T[] _snapshot;
        private int _snapshotCount;
        private int _builtVersion = -1;
        private int _depth;

        public SnapshotSet() : this(null, DefaultCapacity) { }

        public SnapshotSet(int capacity) : this(null, capacity) { }

        public SnapshotSet(IEqualityComparer<T> comparer) : this(comparer, DefaultCapacity) { }

        public SnapshotSet(IEqualityComparer<T> comparer, int capacity) {
            if (capacity < 1) capacity = DefaultCapacity;
            _set = new OrderedSet<T>(comparer, capacity);
            _snapshot = new T[capacity];
        }

        public int Count => _set.Count;

        internal int SnapshotCount => _snapshotCount;

        internal int ReadDepth => _depth;

        public bool Add(T item) => _set.Add(item);

        public bool Remove(T item) => _set.Remove(item);

        public bool Contains(T item) => _set.Contains(item);

        public bool BeginRead(out T[] items, out int count) {
            if (_depth == 0 && _builtVersion != _set.Version) {
                Rebuild();
                _builtVersion = _set.Version;
            }
            items = _snapshot;
            count = _snapshotCount;
            if (count == 0) return false;
            _depth++;
            return true;
        }

        public void EndRead() {
            if (_depth == 0) return;
            _depth--;
            if (_depth == 0 && _builtVersion != _set.Version) {
                ClearSnapshot();
                _builtVersion = -1;
            }
        }

        private void ClearSnapshot() {
            for (var i = 0; i < _snapshotCount; i++) _snapshot[i] = default;
            _snapshotCount = 0;
        }

        private void Rebuild() {
            var active = _set.Count;
            if (_snapshot.Length < active) {
                var newSize = _snapshot.Length < DefaultCapacity ? DefaultCapacity : _snapshot.Length * 2;
                if (newSize < active) newSize = active;
                Array.Resize(ref _snapshot, newSize);
            }
            var count = _set.CopyActiveTo(_snapshot);
            for (var i = count; i < _snapshotCount; i++) _snapshot[i] = default;
            _snapshotCount = count;
        }
    }
}
