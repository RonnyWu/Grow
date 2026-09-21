// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;

namespace Grow.Core.Event {
    public sealed class GrowEvent<T0> : IGrowEventRaiser<T0> {
        private const int DefaultCapacity = 8;

        private readonly InvocationList<Action<T0>> _list;

        public GrowEvent() : this(DefaultCapacity) { }

        public GrowEvent(int capacity) {
            _list = new InvocationList<Action<T0>>(capacity);
        }

        public int Count => _list.Count;

        public void Add(Action<T0> handler) => _list.Add(handler);

        public void Remove(Action<T0> handler) => _list.Remove(handler);

        void IGrowEventRaiser<T0>.Invoke(T0 arg0) {
            if (!_list.BeginDispatch(out var snapshot, out var count)) return;
            try {
                for (var i = 0; i < count; i++) {
                    var action = snapshot[i];
                    try {
                        action(arg0);
                    } catch (Exception ex) when (!EventFaults.IsFatal(ex)) {
                        try { EventFaults.Report(action, ex); } catch { }
                    }
                }
            } finally {
                _list.EndDispatch();
            }
        }

        void IGrowEventRaiser<T0>.Clear() => _list.Clear();
    }
}
