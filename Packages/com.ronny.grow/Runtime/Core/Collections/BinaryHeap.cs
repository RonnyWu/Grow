// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace Grow.Core.Collections {
    /// <summary>
    /// A binary min-heap: the smallest item leaves first.
    /// </summary>
    /// <remarks>
    /// <see cref="Enqueue"/> and <see cref="Dequeue"/> are O(log n); <see cref="Peek"/> is O(1).
    /// The heap is not stable: items that compare equal keep no insertion order, so a consumer that
    /// needs a deterministic order among equal items must encode a tie-breaker in the comparison
    /// (for example a monotonically increasing sequence number).
    /// <para>
    /// There is no arbitrary removal or key update; cancellation is normally handled with a
    /// tombstone flag plus a filtered dequeue. There is also no ordered enumeration: drain the heap
    /// to read every item in order.
    /// </para>
    /// <para>
    /// The name is deliberately not <c>PriorityQueue</c>. The .NET base class library has a
    /// <c>System.Collections.Generic.PriorityQueue&lt;TElement, TPriority&gt;</c> from .NET 6 on, which
    /// does not exist on the .NET Standard 2.1 profile this package targets; using that name here
    /// would turn into an ambiguity as soon as the profile moves forward.
    /// </para>
    /// <para>Not thread-safe. It does not touch Unity.</para>
    /// </remarks>
    /// <typeparam name="T">Item type. Ordering comes from the supplied comparer.</typeparam>
    public sealed class BinaryHeap<T> {
        private readonly List<T> _items = new();
        private readonly IComparer<T> _comparer;

        /// <summary>Creates a heap ordered by <see cref="Comparer{T}.Default"/>.</summary>
        /// <remarks>
        /// The default comparer requires <typeparamref name="T"/> to implement
        /// <see cref="IComparable{T}"/> (or <see cref="IComparable"/>); otherwise the first comparison
        /// throws. Pass an explicit comparer for any other ordering, including a max-heap.
        /// </remarks>
        public BinaryHeap() : this(null) { }

        /// <summary>Creates a heap ordered by <paramref name="comparer"/>.</summary>
        /// <param name="comparer">
        /// Ordering to use; <c>null</c> selects <see cref="Comparer{T}.Default"/>.
        /// </param>
        public BinaryHeap(IComparer<T> comparer) {
            _comparer = comparer ?? Comparer<T>.Default;
        }

        /// <summary>Number of items in the heap.</summary>
        public int Count => _items.Count;

        /// <summary>True when the heap holds no items.</summary>
        public bool IsEmpty => _items.Count == 0;

        /// <summary>Returns the smallest item without removing it.</summary>
        /// <exception cref="InvalidOperationException">The heap is empty.</exception>
        public T Peek() {
            if (_items.Count == 0) {
                throw new InvalidOperationException("Cannot peek an empty heap.");
            }

            return _items[0];
        }

        /// <summary>Adds an item.</summary>
        public void Enqueue(T item) {
            _items.Add(item);
            SiftUp(_items.Count - 1);
        }

        /// <summary>Removes and returns the smallest item.</summary>
        /// <exception cref="InvalidOperationException">The heap is empty.</exception>
        public T Dequeue() {
            if (_items.Count == 0) {
                throw new InvalidOperationException("Cannot dequeue an empty heap.");
            }

            var smallest = _items[0];
            var last = _items.Count - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);
            if (_items.Count > 1) {
                SiftDown(0);
            }

            return smallest;
        }

        /// <summary>Returns the smallest item without removing it; false when the heap is empty.</summary>
        public bool TryPeek(out T item) {
            if (_items.Count == 0) {
                item = default;
                return false;
            }

            item = _items[0];
            return true;
        }

        /// <summary>Removes and returns the smallest item; false when the heap is empty.</summary>
        public bool TryDequeue(out T item) {
            if (_items.Count == 0) {
                item = default;
                return false;
            }

            item = Dequeue();
            return true;
        }

        /// <summary>Removes every item.</summary>
        public void Clear() {
            _items.Clear();
        }

        private void SiftUp(int index) {
            while (index > 0) {
                var parent = (index - 1) / 2;
                if (_comparer.Compare(_items[index], _items[parent]) >= 0) {
                    return;
                }

                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index) {
            while (true) {
                var left = index * 2 + 1;
                if (left >= _items.Count) {
                    return;
                }

                var right = left + 1;
                var smallest = right < _items.Count &&
                               _comparer.Compare(_items[right], _items[left]) < 0
                    ? right
                    : left;

                if (_comparer.Compare(_items[index], _items[smallest]) <= 0) {
                    return;
                }

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int left, int right) {
            var item = _items[left];
            _items[left] = _items[right];
            _items[right] = item;
        }
    }
}
