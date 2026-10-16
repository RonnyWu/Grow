// SPDX-License-Identifier: MIT

using System;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// The public face of the kernel lifecycle.
    /// </summary>
    /// <remarks>
    /// It forwards to the state machine behind it, which is internal on purpose: this facade is the
    /// only way in, and the seam where automatic entry and the <c>IGrowBootstrapper</c> overload will
    /// land later.
    /// </remarks>
    public static class GrowBoot {
        /// <summary>Current lifecycle state.</summary>
        public static GrowState State => GrowRuntime.State;

        /// <summary>True once <see cref="Boot"/> has completed.</summary>
        public static bool IsReady => GrowRuntime.IsReady;

        /// <summary>
        /// Raised after every state change, with the previous and the new state.
        /// </summary>
        /// <remarks>
        /// Handlers must not throw and must not re-enter the lifecycle. A handler that throws is
        /// skipped and reported; it never changes what the machine did.
        /// </remarks>
        public static event Action<GrowState, GrowState> StateChanged {
            add => GrowRuntime.StateChanged += value;
            remove => GrowRuntime.StateChanged -= value;
        }

        /// <summary>
        /// Starts a cycle: from <see cref="GrowState.None"/> or <see cref="GrowState.Shutdown"/> it
        /// moves to <see cref="GrowState.Reset"/>. From any later state it does nothing, so it never
        /// moves the runtime backwards.
        /// </summary>
        public static void Reset() {
            GrowRuntime.Reset();
        }

        /// <summary>
        /// Installs kernel modules. Requires <see cref="Reset"/> first and fails fast otherwise;
        /// repeating it does nothing.
        /// </summary>
        public static void Install() {
            GrowRuntime.Install();
        }

        /// <summary>
        /// Boots the kernel. Requires <see cref="Install"/> first and fails fast otherwise; repeating
        /// it does nothing. <see cref="IsReady"/> becomes true when this completes.
        /// </summary>
        public static void Boot() {
            GrowRuntime.Boot();
        }

        /// <summary>
        /// Ends the cycle. Does nothing before <see cref="Reset"/>, and is safe to repeat; call
        /// <see cref="Reset"/> afterwards to start a new cycle.
        /// </summary>
        public static void Shutdown() {
            GrowRuntime.Shutdown();
        }
    }
}
