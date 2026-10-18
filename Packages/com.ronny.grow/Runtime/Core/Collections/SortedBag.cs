// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;

namespace Grow.Core.Collections {
    /// <summary>
    /// An array-backed ordered multiset: items are kept sorted, duplicates are allowed, and the
    /// whole sequence can be indexed and enumerated.
    /// </summary>
    /// <remarks>
    /// <see cref="Enqueue"/> does a binary search (O(log n) comparisons) followed by a list insert
    /// (O(n) because the tail shifts). <see cref="Peek"/>, <see cref="Count"/> and the indexer are
    /// O(1); <see cref="Dequeue"/> and <see cref="RemoveAt"/> are O(n) because removing the head (or
    /// any position) shifts the rest. This is a read-friendly structure for small to medium sets,
    /// not a hot-loop queue.
    /// <para>
    /// Items that compare equal keep their insertion order: a new equal item is inserted after the
    /// existing ones, <see cref="IndexOf"/> returns the first equal item and <see cref="Remove"/>
    /// removes that first one. That makes it usable for turn order or a leaderboard where ties must
    /// not reorder.
    /// </para>
    /// <para>
    /// <see cref="Enqueue"/> and <see cref="Dequeue"/> do not allocate per item: the items live in
    /// one contiguous array. <see cref="EnqueueRange"/> with <c>fullSort: true</c> materialises the
    /// batch and a merge buffer, so it is not allocation-free. Use <see cref="BinaryHeap{T}"/> when
    /// only the head matters and inserts dominate; use <see cref="SortedSet{T}"/> when items must be
    /// unique.
    /// </para>
    /// <para>
    /// Implements <see cref="IReadOnlyList{T}"/>, so it can be passed to APIs that only read.
    /// </para>
    /// <para>Not thread-safe. It does not touch Unity.</para>
    /// </remarks>
    /// <typeparam name="T">Item type. Ordering comes from the supplied comparer.</typeparam>
    public sealed class SortedBag<T> : IReadOnlyList<T> {
        private readonly List<T> _items = new();
        private readonly IComparer<T> _comparer;

        /// <summary>Creates a bag ordered by <see cref="Comparer{T}.Default"/>.</summary>
        /// <remarks>
        /// The default comparer requires <typeparamref name="T"/> to implement
        /// <see cref="IComparable{T}"/> (or <see cref="IComparable"/>); otherwise the first comparison
        /// throws. Pass an explicit comparer for any other ordering, including a descending one.
        /// </remarks>
        public SortedBag() : this(null) { }

        /// <summary>Creates a bag ordered by <paramref name="comparer"/>.</summary>
        /// <param name="comparer">Ordering to use; <c>null</c> selects <see cref="Comparer{T}.Default"/>.</param>
        public SortedBag(IComparer<T> comparer) {
            _comparer = comparer ?? Comparer<T>.Default;
        }

        /// <summary>Number of items in the bag.</summary>
        public int Count => _items.Count;

        /// <summary>True when the bag holds no items.</summary>
        public bool IsEmpty => _items.Count == 0;

        /// <summary>
        /// Returns the item at <paramref name="index"/> in comparer order (ascending for the default
        /// comparer).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The index is outside the bag.</exception>
        public T this[int index] => _items[index];

        /// <summary>Returns the head item — the one the comparer ranks first — without removing it.</summary>
        /// <exception cref="InvalidOperationException">The bag is empty.</exception>
        public T Peek() {
            if (_items.Count == 0) {
                throw new InvalidOperationException("Cannot peek an empty bag.");
            }

            return _items[0];
        }

        /// <summary>Inserts an item at its ordered position, after any equal items.</summary>
        public void Enqueue(T item) {
            _items.Insert(FindInsertionIndex(item), item);
        }

        /// <summary>Adds a batch of items.</summary>
        /// <param name="values">Items to add.</param>
        /// <param name="fullSort">
        /// When <c>true</c>, sorts the incoming batch stably (O(k log k)) and merges it with the
        /// already-sorted items (O(n + k)), preserving insertion order among equal items. When
        /// <c>false</c>, inserts each item with <see cref="Enqueue"/> (O(n) per item). Use the full
        /// sort for large batches and incremental inserts for a few items into a large bag.
        /// </param>
        public void EnqueueRange(IEnumerable<T> values, bool fullSort = false) {
            if (values == null) {
                throw new ArgumentNullException(nameof(values));
            }

            if (ReferenceEquals(values, this)) {
                values = new List<T>(values);
            }

            if (!fullSort) {
                foreach (var value in values) {
                    Enqueue(value);
                }

                return;
            }

            var batch = new List<T>(values);
            if (batch.Count == 0) {
                return;
            }

            SortStable(batch);
            if (_items.Count == 0) {
                _items.AddRange(batch);
                return;
            }

            MergeSorted(batch);
        }

        /// <summary>Removes and returns the head item.</summary>
        /// <exception cref="InvalidOperationException">The bag is empty.</exception>
        public T Dequeue() {
            if (_items.Count == 0) {
                throw new InvalidOperationException("Cannot dequeue an empty bag.");
            }

            var head = _items[0];
            _items.RemoveAt(0);
            return head;
        }

        /// <summary>
        /// Returns the index of the first item that the comparer ranks equal to
        /// <paramref name="item"/>, or -1.
        /// </summary>
        /// <remarks>
        /// Equality is the comparer's, not <see cref="object.Equals(object)"/>: with a comparer that
        /// reads only a key, a partially filled probe object is enough to find an entry.
        /// </remarks>
        public int IndexOf(T item) {
            var index = _items.BinarySearch(item, _comparer);
            if (index < 0) {
                return -1;
            }

            while (index > 0 && _comparer.Compare(_items[index - 1], item) == 0) {
                index--;
            }

            return index;
        }

        /// <summary>Returns the first item matching <paramref name="match"/>, or <c>default</c>.</summary>
        /// <remarks>
        /// The result cannot distinguish "no match" from a match whose value is <c>default</c>; use
        /// <see cref="TryFind"/> when that distinction matters.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> is <c>null</c>.</exception>
        public T Find(Predicate<T> match) {
            if (match == null) {
                throw new ArgumentNullException(nameof(match));
            }

            return _items.Find(match);
        }

        /// <summary>Tries to find the first item matching <paramref name="match"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> is <c>null</c>.</exception>
        public bool TryFind(Predicate<T> match, out T item) {
            if (match == null) {
                throw new ArgumentNullException(nameof(match));
            }

            for (var i = 0; i < _items.Count; i++) {
                if (match(_items[i])) {
                    item = _items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        /// <summary>Removes the first item the comparer ranks equal to <paramref name="item"/>.</summary>
        /// <returns>False when no such item exists.</returns>
        public bool Remove(T item) {
            var index = IndexOf(item);
            if (index < 0) {
                return false;
            }

            _items.RemoveAt(index);
            return true;
        }

        /// <summary>Removes the item at <paramref name="index"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The index is outside the bag.</exception>
        public void RemoveAt(int index) {
            _items.RemoveAt(index);
        }

        /// <summary>Removes every item. The backing array keeps its capacity.</summary>
        public void Clear() {
            _items.Clear();
        }

        /// <summary>Enumerates the items in comparer order.</summary>
        public List<T>.Enumerator GetEnumerator() {
            return _items.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        private void MergeSorted(List<T> batch) {
            var merged = new T[_items.Count + batch.Count];
            var existing = 0;
            var incoming = 0;
            var target = 0;

            while (existing < _items.Count && incoming < batch.Count) {
                // `<=` keeps the existing item first when the two compare equal, so the merge is
                // stable across the boundary as well as inside the incoming batch.
                if (_comparer.Compare(_items[existing], batch[incoming]) <= 0) {
                    merged[target++] = _items[existing++];
                } else {
                    merged[target++] = batch[incoming++];
                }
            }

            while (existing < _items.Count) {
                merged[target++] = _items[existing++];
            }

            while (incoming < batch.Count) {
                merged[target++] = batch[incoming++];
            }

            _items.Clear();
            _items.AddRange(merged);
        }

        private void SortStable(List<T> batch) {
            var count = batch.Count;
            if (count < 2) {
                return;
            }

            var entries = new StableEntry[count];
            for (var i = 0; i < count; i++) {
                entries[i] = new StableEntry(batch[i], i);
            }

            Array.Sort(entries, (left, right) => {
                var comparison = _comparer.Compare(left.Value, right.Value);
                return comparison != 0 ? comparison : left.Order.CompareTo(right.Order);
            });

            for (var i = 0; i < count; i++) {
                batch[i] = entries[i].Value;
            }
        }

        private readonly struct StableEntry {
            public StableEntry(T value, int order) {
                Value = value;
                Order = order;
            }

            public T Value { get; }
            public int Order { get; }
        }

        private int FindInsertionIndex(T item) {
            var index = _items.BinarySearch(item, _comparer);
            if (index < 0) {
                return ~index;
            }

            // Walk past equal items so a new equal item lands after the existing ones.
            while (index + 1 < _items.Count &&
                   _comparer.Compare(_items[index + 1], item) == 0) {
                index++;
            }

            return index + 1;
        }
    }
}
