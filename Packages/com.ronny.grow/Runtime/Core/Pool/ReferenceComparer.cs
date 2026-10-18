// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Grow.Core.Pool {
    /// <summary>
    /// Compares pooled objects by identity, never by an overridden <see cref="object.Equals(object)"/>.
    /// </summary>
    /// <remarks>
    /// Pool bookkeeping must not treat two distinct instances as the same object just because they
    /// are value-equal; that would let a duplicate release hand one instance to two callers.
    /// </remarks>
    internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class {
        public static readonly ReferenceComparer<T> Instance = new();

        private ReferenceComparer() { }

        public bool Equals(T x, T y) {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj) {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
