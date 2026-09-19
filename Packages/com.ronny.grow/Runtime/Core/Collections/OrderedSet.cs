// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Grow.Core.Collections {
    public sealed class OrderedSet<T> {
        private const int DefaultCapacity = 8;

        private readonly List<T> _items;
        private readonly List<bool> _alive;
        private readonly Dictionary<T, int> _index;
        private int _version;
        private int _tombstoneCount;

        public OrderedSet() : this(null, DefaultCapacity) { }

        public OrderedSet(int capacity) : this(null, capacity) { }

        public OrderedSet(IEqualityComparer<T> comparer) : this(comparer, DefaultCapacity) { }

        public OrderedSet(IEqualityComparer<T> comparer, int capacity) {
            if (capacity < 1) capacity = DefaultCapacity;
            _items = new List<T>(capacity);
            _alive = new List<bool>(capacity);
            _index = comparer == null
                ? new Dictionary<T, int>(capacity)
                : new Dictionary<T, int>(capacity, comparer);
        }

        public int Count => _index.Count;

        public int Version => _version;

        public bool Add(T item) {
            if (_index.ContainsKey(item)) return false;
            _index[item] = _items.Count;
            _items.Add(item);
            _alive.Add(true);
            _version++;
            return true;
        }

        public bool Contains(T item) => _index.ContainsKey(item);

        public bool Remove(T item) {
            if (!_index.TryGetValue(item, out var slot)) return false;
            _alive[slot] = false;
            _items[slot] = default;
            _index.Remove(item);
            _tombstoneCount++;
            _version++;
            return true;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator {
            private readonly OrderedSet<T> _set;
            private readonly int _version;
            private int _index;
            private T _current;

            internal Enumerator(OrderedSet<T> set) {
                _set = set;
                _version = set._version;
                _index = -1;
                _current = default;
            }

            public T Current => _current;

            public bool MoveNext() {
                if (_version != _set._version) {
                    throw new System.InvalidOperationException("OrderedSet was modified during enumeration.");
                }
                var items = _set._items;
                var alive = _set._alive;
                var i = _index + 1;
                while (i < items.Count) {
                    if (alive[i]) {
                        _index = i;
                        _current = items[i];
                        return true;
                    }
                    i++;
                }
                _index = items.Count;
                _current = default;
                return false;
            }
        }
    }
}
