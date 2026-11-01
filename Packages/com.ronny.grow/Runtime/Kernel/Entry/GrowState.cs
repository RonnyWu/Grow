// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Entry {
    /// <summary>
    /// Lifecycle state of the kernel, listed in the order a cycle passes through them.
    /// </summary>
    /// <remarks>
    /// The order is documentation, not logic: each entry spells out the states it accepts, so
    /// inserting a state never silently changes behaviour. A cycle moves strictly forward,
    /// <see cref="Shutdown"/> ends it, and <see cref="GrowBoot.Reset"/> begins a new one.
    /// <see cref="GrowState.Booted"/> is passed through inside <see cref="GrowBoot.Boot"/>;
    /// <see cref="GrowState.Loading"/> is the only state that may stay for several frames while
    /// <see cref="Modules.IBootLoadable"/> modules prepare themselves, and
    /// <see cref="GrowState.Failed"/> is observed before the composition rolls back.
    /// </remarks>
    public enum GrowState {
        /// <summary>Nothing has run yet; the process default.</summary>
        None = 0,

        /// <summary><see cref="GrowBoot.Reset"/> has run.</summary>
        Reset = 1,

        /// <summary><see cref="GrowBoot.Install"/> has run.</summary>
        Installed = 2,

        /// <summary>The module graph is initialized; asynchronous loading may still be ahead.</summary>
        Booted = 3,

        /// <summary>
        /// Modules are loading. The only state that intentionally spans frames before
        /// <see cref="Ready"/>.
        /// </summary>
        Loading = 4,

        /// <summary>
        /// The kernel is up and usable: every <c>OnStart</c> and every asynchronous load finished.
        /// </summary>
        Ready = 5,

        /// <summary>
        /// A boot load failed. Terminal for the cycle: only <see cref="GrowBoot.Reset"/> restarts it,
        /// and the composition rolls back to <see cref="Shutdown"/> after the notification.
        /// </summary>
        Failed = 6,

        /// <summary>The runtime has been shut down; terminal until a new cycle is started.</summary>
        Shutdown = 7
    }
}
