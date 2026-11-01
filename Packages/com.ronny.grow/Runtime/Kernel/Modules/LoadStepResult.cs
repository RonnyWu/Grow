// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Modules {
    /// <summary>Current step of the asynchronous module load pass.</summary>
    internal enum LoadStepResult {
        /// <summary>The active load yielded; call again on the next frame.</summary>
        Pending = 0,

        /// <summary>Every loadable module finished or had nothing to load.</summary>
        Completed = 1,

        /// <summary>A load threw; the boot must fail.</summary>
        Faulted = 2
    }
}
