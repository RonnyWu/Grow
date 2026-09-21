// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;

namespace Grow.Core.Event {
    public sealed class GrowEvent : IGrowEventRaiser {
        private const int DefaultCapacity = 8;

        private readonly InvocationList<Action> _list;

        public GrowEvent() : this(DefaultCapacity) { }

        public GrowEvent(int capacity) {
            _list = new InvocationList<Action>(capacity);
        }

        public int Count => _list.Count;

        public void Add(Action handler) => _list.Add(handler);

        public void Remove(Action handler) => _list.Remove(handler);

        void IGrowEventRaiser.Invoke() {
            if (!_list.BeginDispatch(out var snapshot, out var count)) return;
            try {
                for (var i = 0; i < count; i++) snapshot[i]();
            } finally {
                _list.EndDispatch();
            }
        }

        void IGrowEventRaiser.Clear() => _list.Clear();
    }
}
