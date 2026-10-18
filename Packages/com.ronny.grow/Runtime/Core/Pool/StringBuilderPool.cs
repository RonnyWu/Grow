// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

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
    /// so copies can share its single-use flag; the builder itself is still reused. Do not call
    /// <see cref="Return"/> directly more than once for the same builder.
    /// </para>
    /// <para>
    /// The slots are static and therefore survive a Play session when the domain reload is disabled.
    /// <see cref="ResetStaticState"/> clears the slots of the calling thread and the global stack;
    /// it is called by <c>GrowSession</c> at <c>SubsystemRegistration</c>. Builders held in other
    /// threads' slots are plain managed objects and are left to the garbage collector.
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
        /// Capacity the builder must be able to hold without growing. Requests above
        /// <see cref="MaxRetainedCapacity"/> are served by a fresh builder that is not pooled on return.
        /// </param>
        public static Pooled Rent(int minCapacity = DefaultCapacity) {
            return new Pooled(RentInternal(minCapacity));
        }

        /// <summary>Returns a builder. A builder above the retention limit is dropped.</summary>
        /// <param name="builder">Builder to return; <c>null</c> is ignored.</param>
        public static void Return(StringBuilder builder) {
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
        /// Called by <c>GrowSession</c> at <c>SubsystemRegistration</c> so a Play session does not
        /// inherit buffers from the previous one when the domain reload is disabled.
        /// </remarks>
        public static void ResetStaticState() {
            _slot1 = null;
            _slot2 = null;
            lock (Gate) {
                Global.Clear();
            }
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
        /// caller error. The token is one small allocation per rent, while the builder is reused.
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
