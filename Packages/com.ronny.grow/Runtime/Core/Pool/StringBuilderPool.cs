// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Grow.Core.Pool {
    /// <summary>
    /// A process-wide <see cref="StringBuilder"/> pool tuned for the main thread.
    /// </summary>
    /// <remarks>
    /// Each thread keeps two lock-free slots; a third concurrent rent on the same thread falls back to
    /// a locked global stack. A builder larger than <see cref="MaxRetainedCapacity"/> is never pooled,
    /// so one oversized concatenation cannot pin a large buffer forever.
    /// <para>
    /// Rent through <see cref="Rent"/> and release through <see cref="Pooled.Dispose"/>; the token
    /// returns the builder exactly once, even when the token is copied. The token is a small class
    /// so copies can share its single-use flag; the builder itself is still reused. A rented builder
    /// is cleared before it enters the pool, so a subsequent rent starts empty.
    /// </para>
    /// <para>
    /// <see cref="Rent"/> and <see cref="Return"/> are thread-safe; a returned builder enters the
    /// calling thread's slots or the locked global stack. The slots are static and therefore survive
    /// a Play session when the domain reload is disabled. <see cref="ResetStaticState"/> clears the
    /// calling thread's slots and the global stack, and a private <c>SubsystemRegistration</c> hook
    /// calls it for the main thread. Builders in other threads' slots are left to the garbage
    /// collector.
    /// </para>
    /// </remarks>
    public static class StringBuilderPool {
        private const int DefaultCapacity = 512;
        private const int MaxRetainedCapacity = 4096;
        private const int MaxGlobalPoolSize = 64;

        [ThreadStatic] private static StringBuilder _slot1;
        [ThreadStatic] private static StringBuilder _slot2;

        private static readonly object Gate = new();
        private static readonly Stack<StringBuilder> Global = new(MaxGlobalPoolSize);

        /// <summary>Rents a builder; the token returns it on <see cref="Pooled.Dispose"/>.</summary>
        /// <param name="minCapacity">
        /// Capacity the builder must be able to hold without growing. Requests above the retention
        /// limit (4096) are served by a fresh builder that is not pooled on return.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="minCapacity"/> is negative.</exception>
        public static Pooled Rent(int minCapacity = DefaultCapacity) {
            if (minCapacity < 0) {
                throw new ArgumentOutOfRangeException(nameof(minCapacity));
            }

            return new Pooled(RentInternal(minCapacity));
        }

        /// <summary>Returns a builder. A builder above the retention limit is dropped.</summary>
        /// <param name="builder">Builder to return; <c>null</c> is ignored.</param>
        internal static void Return(StringBuilder builder) {
            if (builder == null) {
                return;
            }

            if (builder.Capacity > MaxRetainedCapacity) {
                return;
            }

            builder.Clear();
            if (TryReturnToThreadSlot(builder)) {
                return;
            }

            lock (Gate) {
                if (Global.Count < MaxGlobalPoolSize) {
                    Global.Push(builder);
                }
            }
        }

        /// <summary>Drops every builder the calling thread has cached, plus the global stack.</summary>
        /// <remarks>
        /// The private runtime hook calls this at <c>SubsystemRegistration</c> so a Play session does
        /// not inherit buffers from the previous one when the domain reload is disabled. Tests and
        /// hosts may call it explicitly.
        /// </remarks>
        internal static void ResetStaticState() {
            _slot1 = null;
            _slot2 = null;
            lock (Gate) {
                Global.Clear();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnLoad() {
            ResetStaticState();
        }

        private static StringBuilder RentInternal(int minCapacity) {
            if (minCapacity > MaxRetainedCapacity) {
                return new StringBuilder(minCapacity);
            }

            return TryRentFromThreadSlot(minCapacity, out var builder)
                ? builder
                : RentFromGlobalPoolOrCreate(minCapacity);
        }

        private static bool TryRentFromThreadSlot(int minCapacity, out StringBuilder builder) {
            builder = _slot1;
            if (builder != null) {
                _slot1 = null;
                PrepareForUse(builder, minCapacity);
                return true;
            }

            builder = _slot2;
            if (builder != null) {
                _slot2 = null;
                PrepareForUse(builder, minCapacity);
                return true;
            }

            builder = null;
            return false;
        }

        private static StringBuilder RentFromGlobalPoolOrCreate(int minCapacity) {
            StringBuilder builder = null;
            lock (Gate) {
                if (Global.Count > 0) {
                    builder = Global.Pop();
                }
            }

            builder ??= new StringBuilder(Math.Max(DefaultCapacity, minCapacity));
            PrepareForUse(builder, minCapacity);
            return builder;
        }

        private static bool TryReturnToThreadSlot(StringBuilder builder) {
            if (_slot1 == null) {
                _slot1 = builder;
                return true;
            }

            if (_slot2 == null) {
                _slot2 = builder;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrepareForUse(StringBuilder builder, int minCapacity) {
            if (minCapacity > builder.Capacity) {
                builder.EnsureCapacity(minCapacity);
            }
        }

        /// <summary>A rented builder; <see cref="Dispose"/> returns it exactly once.</summary>
        /// <remarks>
        /// A class token so copies of the reference share the single-use flag. Disposing twice, or
        /// through a copy, returns the builder once; using <see cref="Builder"/> after disposal is a
        /// caller error. Keep the token alive while the builder is in use, and do not cache the
        /// builder beyond the token: dropping the token leaks the builder out of the pool. The token
        /// is one small allocation per rent, while the builder is reused.
        /// </remarks>
        public sealed class Pooled : IDisposable {
            private StringBuilder _builder;

            internal Pooled(StringBuilder builder) {
                _builder = builder;
            }

            /// <summary>The rented builder, or <c>null</c> after <see cref="Dispose"/>.</summary>
            public StringBuilder Builder => _builder;

            /// <summary>Returns the builder to the pool.</summary>
            public void Dispose() {
                var builder = Interlocked.Exchange(ref _builder, null);
                if (builder != null) {
                    Return(builder);
                }
            }

            /// <summary>Materialises the content and returns the builder in one step.</summary>
            /// <returns>The content, or <c>null</c> when the token was already disposed.</returns>
            public string ToStringAndDispose() {
                var builder = Interlocked.Exchange(ref _builder, null);
                if (builder == null) {
                    return null;
                }

                var text = builder.ToString();
                Return(builder);
                return text;
            }
        }
    }
}
