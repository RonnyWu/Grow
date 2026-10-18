// SPDX-License-Identifier: MIT

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Grow.Core.Pool {
    /// <summary>
    /// A thread-safe object pool: idle objects sit in a concurrent queue, leased objects are tracked
    /// by identity so a duplicate or unknown release is ignored.
    /// </summary>
    /// <remarks>
    /// Idle objects are handed out in FIFO order. <see cref="Acquire"/> and <see cref="Release"/> are
    /// safe from any thread. <see cref="Clear"/> is not: call it only when no other thread is using
    /// the pool.
    /// <para>
    /// This is the pool the engine does not provide: <see cref="UnityEngine.Pool.ObjectPool{T}"/> is
    /// explicitly not thread-safe and <c>UnityEngine.Pool</c> has no concurrent variant. For
    /// single-threaded pooling use <see cref="UnityEngine.Pool.ObjectPool{T}"/> directly.
    /// </para>
    /// <para>
    /// The type also implements <see cref="UnityEngine.Pool.IObjectPool{T}"/> so it can be handed to
    /// code written against the engine interface: <c>Get</c> maps to <see cref="Acquire"/>,
    /// <c>CountInactive</c> maps to <see cref="AvailableCount"/>, and the <c>Get(out T)</c> overload
    /// returns a <see cref="UnityEngine.Pool.PooledObject{T}"/> that releases on dispose.
    /// </para>
    /// <para>
    /// The counters are atomic but not a snapshot: <see cref="Count"/> can change between its two
    /// reads. It is a diagnostic value, not a synchronisation point.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Pooled object type.</typeparam>
    public sealed class ConcurrentObjectPool<T> : UnityEngine.Pool.IObjectPool<T> where T : class {
        private readonly ConcurrentQueue<T> _idle = new();
        private readonly ConcurrentDictionary<T, byte> _leased;
        private readonly Func<T> _create;
        private readonly Action<T> _activate;
        private readonly Action<T> _deactivate;
        private readonly Action<T> _destroy;
        private int _idleCount;
        private int _allocatedCount;

        /// <summary>Creates a pool, optionally filling it with idle objects.</summary>
        /// <param name="capacity">Maximum number of idle objects retained; must be at least 1.</param>
        /// <param name="prefillSize">
        /// Number of objects created up front. Values above <paramref name="capacity"/> are clamped.
        /// </param>
        /// <param name="create">
        /// Factory used when the pool has no idle object; cannot be <c>null</c> and must return a new
        /// instance each time it is called.
        /// </param>
        /// <param name="activate">Optional callback invoked after an object is leased.</param>
        /// <param name="deactivate">Optional callback invoked before an object returns to the pool.</param>
        /// <param name="destroy">Optional callback invoked when an object is dropped or the pool is cleared.</param>
        /// <exception cref="ArgumentNullException"><paramref name="create"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="capacity"/> is below 1, or <paramref name="prefillSize"/> is negative.
        /// </exception>
        public ConcurrentObjectPool(int capacity, int prefillSize, Func<T> create,
            Action<T> activate = null, Action<T> deactivate = null, Action<T> destroy = null) {
            if (create == null) {
                throw new ArgumentNullException(nameof(create));
            }

            if (capacity < 1) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (prefillSize < 0) {
                throw new ArgumentOutOfRangeException(nameof(prefillSize));
            }

            Capacity = capacity;
            _create = create;
            _activate = activate;
            _deactivate = deactivate;
            _destroy = destroy;
            _leased = new ConcurrentDictionary<T, byte>(ReferenceComparer<T>.Instance);

            var fill = Math.Min(prefillSize, capacity);
            for (var i = 0; i < fill; i++) {
                _idle.Enqueue(create());
                _idleCount++;
            }
        }

        /// <summary>Number of objects currently leased out.</summary>
        public int AllocatedCount => Volatile.Read(ref _allocatedCount);

        /// <summary>Number of idle objects waiting in the pool.</summary>
        public int AvailableCount => Volatile.Read(ref _idleCount);

        /// <summary>Total objects the pool owns: <see cref="AllocatedCount"/> + <see cref="AvailableCount"/>.</summary>
        public int Count => Volatile.Read(ref _allocatedCount) + Volatile.Read(ref _idleCount);

        /// <summary>
        /// Maximum number of idle objects retained. A release that would exceed it destroys the
        /// object instead of keeping it.
        /// </summary>
        public int Capacity { get; }

        /// <summary>Takes an object from the pool, creating one when the pool is empty.</summary>
        /// <exception cref="InvalidOperationException">
        /// The factory returned an instance that is already leased out.
        /// </exception>
        public T Acquire() {
            T item;
            if (_idle.TryDequeue(out var idle)) {
                Interlocked.Decrement(ref _idleCount);
                item = idle;
            } else {
                item = _create();
                if (ReferenceEquals(item, null)) {
                    throw new InvalidOperationException("The pool factory returned null.");
                }
            }

            if (!_leased.TryAdd(item, 0)) {
                throw new InvalidOperationException(
                    "The pool factory returned an object that is already leased out.");
            }

            Interlocked.Increment(ref _allocatedCount);

            if (_activate == null) {
                return item;
            }

            try {
                _activate(item);
            } catch {
                Unlease(item);
                ReturnToIdle(item);
                throw;
            }

            return item;
        }

        /// <summary>
        /// Returns an object. Releasing an object the pool did not lease, or releasing the same
        /// object twice while it is still leased, is ignored. If <c>deactivate</c> throws, the
        /// object is destroyed instead of returned to idle and the exception propagates.
        /// </summary>
        /// <remarks>
        /// A release is identified by object identity, not by a lease token. If the object is
        /// acquired again after a release, a late second release of the old lease looks like a
        /// release of the new lease; callers must release each lease exactly once.
        /// </remarks>
        /// <param name="item">Object to return; <c>null</c> is ignored.</param>
        public void Release(T item) {
            if (ReferenceEquals(item, null)) {
                return;
            }

            if (!_leased.TryRemove(item, out _)) {
                return;
            }

            Interlocked.Decrement(ref _allocatedCount);

            try {
                _deactivate?.Invoke(item);
            } catch {
                // A half-deactivated object must not be handed out again.
                Destroy(item);
                throw;
            }

            ReturnToIdle(item);
        }

        /// <summary>
        /// Destroys every idle object. Leased objects stay leased: the caller still holds them and
        /// must release them. Not safe to call concurrently with <see cref="Acquire"/> or
        /// <see cref="Release"/>.
        /// </summary>
        public void Clear() {
            var drained = new List<T>();
            while (_idle.TryDequeue(out var item)) {
                drained.Add(item);
            }

            Interlocked.Add(ref _idleCount, -drained.Count);

            Exception first = null;
            for (var i = 0; i < drained.Count; i++) {
                try {
                    _destroy?.Invoke(drained[i]);
                } catch (Exception exception) {
                    first ??= exception;
                }
            }

            if (first != null) {
                throw first;
            }
        }

        int UnityEngine.Pool.IObjectPool<T>.CountInactive => AvailableCount;

        T UnityEngine.Pool.IObjectPool<T>.Get() {
            return Acquire();
        }

        UnityEngine.Pool.PooledObject<T> UnityEngine.Pool.IObjectPool<T>.Get(out T element) {
            element = Acquire();
            return new UnityEngine.Pool.PooledObject<T>(element, this);
        }

        private void Unlease(T item) {
            if (!_leased.TryRemove(item, out _)) {
                return;
            }

            Interlocked.Decrement(ref _allocatedCount);
        }

        private void ReturnToIdle(T item) {
            if (Interlocked.Increment(ref _idleCount) <= Capacity) {
                _idle.Enqueue(item);
                return;
            }

            Interlocked.Decrement(ref _idleCount);
            Destroy(item);
        }

        private void Destroy(T item) {
            _destroy?.Invoke(item);
        }
    }
}
