// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace Grow.Core.Collections {
    /// <summary>
    /// A dense map keyed by generational handles: <see cref="Add"/> returns a
    /// <see cref="SlotHandle"/>, <see cref="Remove"/> invalidates it, and a stale handle can never
    /// reach a newer value that reuses the same slot.
    /// </summary>
    /// <remarks>
    /// <see cref="Add"/> is O(1) amortised (the slot arrays double when full), <see cref="Remove"/>
    /// is O(1) (a swap-remove keeps the values dense) and <see cref="TryGet"/> is O(1).
    /// <para>
    /// Values and their handles are stored in two dense arrays, so a loop over
    /// <see cref="Values"/> / <see cref="Handles"/> touches contiguous memory. The order is not
    /// insertion order: a swap-remove moves the last value into the hole, which is what keeps the
    /// arrays dense without per-entry nodes.
    /// </para>
    /// <para>
    /// <see cref="Values"/> and <see cref="Handles"/> are spans over the internal arrays. They are
    /// valid until the next mutation (which may also reallocate them), and mutating the map while
    /// iterating the spans is not supported: collect the handles first, then remove.
    /// </para>
    /// <para>
    /// <see cref="Clear"/> keeps the capacity but bumps every used slot's generation, so every
    /// handle issued before the clear becomes invalid.
    /// </para>
    /// <para>
    /// A slot's generation wraps back to 1 after roughly 2^31 removals. A handle issued more than
    /// one full cycle ago can collide with a current one; no handle is ever produced with generation
    /// 0 or a negative value. Not thread-safe. It does not touch Unity.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Stored value type.</typeparam>
    public sealed class SlotMap<T> {
        private const int DefaultCapacity = 4;

        private T[] _values;
        private SlotHandle[] _handles;
        private int[] _generations;
        private int[] _sparse;
        private int[] _free;

        private int _slotCapacity;
        private int _slotCount;
        private int _count;
        private int _freeCount;

        /// <summary>Creates a map with a small initial capacity.</summary>
        public SlotMap() : this(DefaultCapacity) { }

        /// <summary>Creates a map with an explicit initial capacity.</summary>
        /// <param name="capacity">Initial number of slots; must be at least 1.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 1.</exception>
        public SlotMap(int capacity) {
            if (capacity < 1) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _slotCapacity = capacity;
            _values = new T[capacity];
            _handles = new SlotHandle[capacity];
            _generations = new int[capacity];
            _sparse = new int[capacity];
            _free = new int[capacity];
            for (var i = 0; i < capacity; i++) {
                _sparse[i] = -1;
            }
        }

        /// <summary>Number of values currently stored.</summary>
        public int Count => _count;

        /// <summary>True when the map holds no values.</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>Number of slot entries currently allocated; the map grows past it when needed.</summary>
        public int Capacity => _slotCapacity;

        /// <summary>
        /// Values in dense order. The span is valid until the next mutation.
        /// </summary>
        public ReadOnlySpan<T> Values => new(_values, 0, _count);

        /// <summary>
        /// Handles aligned with <see cref="Values"/> (same index, same dense position). The span is
        /// valid until the next mutation.
        /// </summary>
        public ReadOnlySpan<SlotHandle> Handles => new(_handles, 0, _count);

        /// <summary>Stores a value and returns its handle.</summary>
        /// <param name="value">Value to store; <c>null</c> is allowed for reference types.</param>
        public SlotHandle Add(T value) {
            int slot;
            if (_freeCount > 0) {
                slot = _free[--_freeCount];
            } else {
                if (_slotCount == _slotCapacity) {
                    GrowSlots();
                }

                slot = _slotCount++;
            }

            var generation = NextGeneration(_generations[slot]);
            _generations[slot] = generation;
            var dense = _count;
            _values[dense] = value;
            _handles[dense] = new SlotHandle(slot, generation);
            _sparse[slot] = dense;
            _count++;
            return _handles[dense];
        }

        /// <summary>
        /// Reads the value for a handle; false when the handle is stale. A handle issued by another
        /// map is not detected.
        /// </summary>
        public bool TryGet(SlotHandle handle, out T value) {
            if (!TryGetDenseIndex(handle, out var dense)) {
                value = default;
                return false;
            }

            value = _values[dense];
            return true;
        }

        /// <summary>Reads the value for a handle.</summary>
        /// <exception cref="KeyNotFoundException">The handle is stale. A handle from another map is
        /// not detected.</exception>
        public T Get(SlotHandle handle) {
            if (!TryGet(handle, out var value)) {
                throw new KeyNotFoundException($"No value is stored for {handle}.");
            }

            return value;
        }

        /// <summary>Removes the value for a handle; false when the handle is already stale.</summary>
        public bool Remove(SlotHandle handle) {
            if (!TryGetDenseIndex(handle, out var dense)) {
                return false;
            }

            var last = _count - 1;
            if (dense != last) {
                var movedHandle = _handles[last];
                _values[dense] = _values[last];
                _handles[dense] = movedHandle;
                _sparse[movedHandle.Index] = dense;
            }

            _values[last] = default;
            _handles[last] = default;
            _count--;

            var slot = handle.Index;
            _sparse[slot] = -1;
            _generations[slot] = NextGeneration(_generations[slot]);

            if (_freeCount >= _free.Length) {
                Array.Resize(ref _free, Math.Max(_free.Length * 2, _slotCapacity));
            }

            _free[_freeCount++] = slot;
            return true;
        }

        /// <summary>Removes every value and invalidates every issued handle; capacity is kept.</summary>
        public void Clear() {
            for (var i = 0; i < _slotCount; i++) {
                _values[i] = default;
                _handles[i] = default;
                _sparse[i] = -1;
                _generations[i] = NextGeneration(_generations[i]);
            }

            _count = 0;
            _freeCount = _slotCount;
            if (_free.Length < _slotCount) {
                Array.Resize(ref _free, _slotCapacity);
            }

            for (var i = 0; i < _slotCount; i++) {
                _free[i] = i;
            }
        }

        private bool TryGetDenseIndex(SlotHandle handle, out int dense) {
            dense = -1;
            if (handle.Generation <= 0) {
                return false;
            }

            var slot = handle.Index;
            if ((uint)slot >= (uint)_slotCount) {
                return false;
            }

            if (_generations[slot] != handle.Generation) {
                return false;
            }

            var index = _sparse[slot];
            if (index < 0) {
                return false;
            }

            dense = index;
            return true;
        }

        private static int NextGeneration(int generation) {
            unchecked {
                generation++;
            }

            return generation <= 0 ? 1 : generation;
        }

        private void GrowSlots() {
            var newCapacity = _slotCapacity * 2;
            Array.Resize(ref _values, newCapacity);
            Array.Resize(ref _handles, newCapacity);
            Array.Resize(ref _generations, newCapacity);

            var oldSparseLength = _sparse.Length;
            Array.Resize(ref _sparse, newCapacity);
            for (var i = oldSparseLength; i < newCapacity; i++) {
                _sparse[i] = -1;
            }

            _slotCapacity = newCapacity;
        }
    }
}
