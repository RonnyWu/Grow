// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace Grow.Core.Event {
    public interface IGrowEventRaiser {
        void Invoke();
        void Clear();
    }

    public interface IGrowEventRaiser<in T0> {
        void Invoke(T0 arg0);
        void Clear();
    }
}
