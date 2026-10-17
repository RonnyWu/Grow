// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Ioc.Common {
    /// <summary>Instance lifetime policy used by the service collection.</summary>
    internal enum LifeTime {
        /// <summary>A new instance per resolution.</summary>
        Transient,

        /// <summary>One cached instance per registration identity.</summary>
        Singleton
    }
}
