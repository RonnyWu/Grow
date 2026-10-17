// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Modules {
    /// <summary>
    /// Module system phase, advanced by <see cref="GrowModules"/>.
    /// </summary>
    /// <remarks>
    /// Order: None → Register → Initialize → Start → Running → Stop → Dispose → None.
    /// </remarks>
    public enum ModulePhase {
        /// <summary>Not started; also the terminal state after shutdown.</summary>
        None = 0,

        /// <summary>Register pass.</summary>
        Register,

        /// <summary>Initialize pass.</summary>
        Initialize,

        /// <summary>Start pass.</summary>
        Start,

        /// <summary>Boot completed.</summary>
        Running,

        /// <summary>Stop pass, in reverse startup order.</summary>
        Stop,

        /// <summary>Dispose pass, in reverse startup order.</summary>
        Dispose
    }
}
