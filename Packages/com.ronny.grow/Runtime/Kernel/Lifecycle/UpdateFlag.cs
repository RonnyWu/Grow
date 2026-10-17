// SPDX-License-Identifier: MIT

using System;

namespace Grow.Kernel.Lifecycle {
    /// <summary>
    /// Declares which update phases a lifecycle object wants to receive from
    /// <see cref="ILifecycleManager"/>.
    /// </summary>
    /// <remarks>
    /// The values are bit flags so a target can combine phases without allocating. Keep the numeric
    /// values stable: they are part of the public contract and the coverage test pins the set.
    /// </remarks>
    [Flags]
    public enum UpdateFlag {
        /// <summary>No update phase.</summary>
        None = 0,

        /// <summary>Every game frame, after scene scripts update.</summary>
        Update = 1 << 1,

        /// <summary>Every game frame, after the update group.</summary>
        LateUpdate = 1 << 2,

        /// <summary>Fixed-interval physics update.</summary>
        FixedUpdate = 1 << 3,

        /// <summary>Before scene scripts update.</summary>
        PreUpdate = 1 << 4,

        /// <summary>After the whole update group, before late update.</summary>
        PostUpdate = 1 << 5
    }
}
