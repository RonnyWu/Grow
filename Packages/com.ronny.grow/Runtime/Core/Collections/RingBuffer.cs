// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Collections {
    /// <summary>
    /// A fixed-capacity circular buffer.
    /// </summary>
    /// <remarks>
    /// The buffer allocates its array once and never grows, so after construction it adds no
    /// per-item allocation and no resizing copy. <see cref="Push"/> and <see cref="Pop"/> are O(1);
    /// <see cref="Peek"/> and the indexer are O(1). The indexer is oldest-first:
    /// <c>buffer[0]</c> is the next item <see cref="Pop"/> returns and <c>buffer[Count - 1]</c> is
    /// the most recently pushed one.
    /// <para>
    /// Two fullness policies share the same storage. <see cref="Push"/> keeps the newest N items and
    /// silently overwrites the oldest when full; <see cref="TryPush"/> is the bounded-queue policy
    /// and reports <c>false</c> instead of overwriting. Pick per call site.
    /// </para>
    /// <para>
    /// There is no enumerator: iterate with <c>for (var i = 0; i &lt; buffer.Count; i++)</c> over the
    /// indexer, or call <see cref="ToArray"/> when a snapshot is wanted. <see cref="ToArray"/>
    /// allocates; the indexer does not.
    /// </para>
    /// <para>Not thread-safe. It does not touch Unity.</para>
    /// </remarks>
    /// <typeparam name="T">Item type.</typeparam>
    public sealed class RingBuffer<T> {
        private readonly T[] _items;
        private int _head;
        private int _count;

        /// <summary>Creates a buffer with a fixed capacity.</summary>
        /// <param name="capacity">Number of items the buffer can hold; must be at least 1.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 1.</exception>
        public RingBuffer(int capacity) {
            if (capacity < 1) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _items = new T[capacity];
        }

        /// <summary>Maximum number of items the buffer can hold.</summary>
        public int Capacity => _items.Length;

        /// <summary>Number of items currently stored.</summary>
        public int Count => _count;

        /// <summary>True when the buffer holds no items.</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>True when the buffer is at capacity.</summary>
        public bool IsFull => _count == _items.Length;

        /// <summary>Returns the item at <paramref name="index"/>, where 0 is the oldest item.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The index is outside <c>[0, Count)</c>.</exception>
        public T this[int index] {
            get {
                if ((uint)index >= (uint)_count) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _items[(_head + index) % _items.Length];
            }
        }

        /// <summary>
        /// Adds an item at the newest end. When the buffer is full, the oldest item is overwritten
        /// and the count stays at capacity.
        /// </summary>
        public void Push(T item) {
            if (_count < _items.Length) {
                _items[(_head + _count) % _items.Length] = item;
                _count++;
                return;
            }

            _items[_head] = item;
            _head = (_head + 1) % _items.Length;
        }

        /// <summary>
        /// Adds an item at the newest end; returns <c>false</c> without changing the buffer when it
        /// is full.
        /// </summary>
        public bool TryPush(T item) {
            if (_count == _items.Length) {
                return false;
            }

            Push(item);
            return true;
        }

        /// <summary>Removes and returns the oldest item.</summary>
        /// <exception cref="InvalidOperationException">The buffer is empty.</exception>
        public T Pop() {
            if (_count == 0) {
                throw new InvalidOperationException("Cannot pop an empty ring buffer.");
            }

            var item = _items[_head];
            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            _count--;
            return item;
        }

        /// <summary>Removes and returns the oldest item; false when the buffer is empty.</summary>
        public bool TryPop(out T item) {
            if (_count == 0) {
                item = default;
                return false;
            }

            item = Pop();
            return true;
        }

        /// <summary>Returns the oldest item without removing it.</summary>
        /// <exception cref="InvalidOperationException">The buffer is empty.</exception>
        public T Peek() {
            if (_count == 0) {
                throw new InvalidOperationException("Cannot peek an empty ring buffer.");
            }

            return _items[_head];
        }

        /// <summary>Returns the oldest item without removing it; false when the buffer is empty.</summary>
        public bool TryPeek(out T item) {
            if (_count == 0) {
                item = default;
                return false;
            }

            item = _items[_head];
            return true;
        }

        /// <summary>Removes every item and releases the references they hold.</summary>
        public void Clear() {
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }

        /// <summary>Copies the items into a new array, oldest first.</summary>
        public T[] ToArray() {
            var result = new T[_count];
            for (var i = 0; i < _count; i++) {
                result[i] = _items[(_head + i) % _items.Length];
            }

            return result;
        }
    }
}
