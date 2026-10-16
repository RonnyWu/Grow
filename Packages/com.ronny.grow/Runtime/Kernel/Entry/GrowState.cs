// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Entry {
    /// <summary>
    /// Lifecycle state of the kernel, listed in the order a cycle passes through them.
    /// </summary>
    /// <remarks>
    /// The order is documentation, not logic: each entry spells out the states it accepts, so
    /// inserting a state never silently changes behaviour. A cycle moves strictly forward,
    /// <see cref="Shutdown"/> ends it, and <see cref="GrowBoot.Reset"/> begins a new one.
    /// <see cref="GrowState.Booted"/> is passed through inside <see cref="GrowBoot.Boot"/>, which
    /// continues straight to <see cref="Ready"/> while there is no asynchronous bootstrap barrier.
    /// </remarks>
    public enum GrowState {
        /// <summary>Nothing has run yet; the process default.</summary>
        None = 0,

        /// <summary><see cref="GrowBoot.Reset"/> has run.</summary>
        Reset = 1,

        /// <summary><see cref="GrowBoot.Install"/> has run.</summary>
        Installed = 2,

        /// <summary>Booting has finished; not yet usable. Transient while there is no async barrier.</summary>
        Booted = 3,

        /// <summary>The kernel is up and usable.</summary>
        Ready = 4,

        /// <summary>The runtime has been shut down; terminal until a new cycle is started.</summary>
        Shutdown = 5
    }
}
