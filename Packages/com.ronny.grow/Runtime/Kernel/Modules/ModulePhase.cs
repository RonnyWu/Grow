// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Modules {
    /// <summary>
    /// Module system phase, advanced by <see cref="GrowModules"/>.
    /// </summary>
    /// <remarks>
    /// Order: None → Register → Initialize → Load → Start → Running → Stop → Dispose → None.
    /// <see cref="Load"/> is entered only when at least one module implements
    /// <see cref="IBootLoadable"/>, so a synchronous graph keeps the shorter sequence.
    /// </remarks>
    public enum ModulePhase {
        /// <summary>Not started; also the terminal state after shutdown.</summary>
        None = 0,

        /// <summary>Register pass.</summary>
        Register,

        /// <summary>Initialize pass.</summary>
        Initialize,

        /// <summary>Asynchronous load pass, driven across frames.</summary>
        Load,

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
