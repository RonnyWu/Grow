// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Grow.Core.Collections;

namespace Grow.Core.Event {
    internal sealed class InvocationList<TDelegate> where TDelegate : Delegate {
        private const int DefaultCapacity = 8;

        private readonly SnapshotSet<TDelegate> _set;

        internal InvocationList() : this(DefaultCapacity) { }

        internal InvocationList(int capacity) {
            if (capacity < 1) capacity = DefaultCapacity;
            _set = new SnapshotSet<TDelegate>(capacity);
        }

        internal int Count => _set.Count;

        internal void Add(TDelegate handler) {
            if (handler == null) return;
            _set.Add(handler);
        }

        internal void Remove(TDelegate handler) {
            if (handler == null) return;
            _set.Remove(handler);
        }

        internal void Clear() => _set.Clear();

        internal bool BeginDispatch(out TDelegate[] snapshot, out int count) =>
            _set.BeginRead(out snapshot, out count);

        internal void EndDispatch() => _set.EndRead();
    }
}
