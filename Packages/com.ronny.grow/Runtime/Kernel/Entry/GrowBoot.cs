// SPDX-License-Identifier: MIT

using Grow.Core.Events;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// Read-only observation surface of the kernel lifecycle.
    /// </summary>
    /// <remarks>
    /// <see cref="GrowApp"/> is the composition root and the only lifecycle writer. It forwards to the
    /// state machine behind it; the write entries are internal so a second caller cannot start or
    /// stop the kernel behind the composition root's back.
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
        /// Subscribers receive a <see cref="GrowSubscription"/>; dispose it to unsubscribe. Handlers
        /// must not throw and must not re-enter the lifecycle. A handler that throws is reported by the
        /// kernel sink and does not stop the other handlers.
        /// </remarks>
        public static GrowEvent<StateChange> StateChanged => GrowRuntime.StateChanged;

        /// <summary>Starts a cycle from None/Shutdown; internal composition-root entry.</summary>
        internal static void Reset() {
            GrowRuntime.Reset();
        }

        /// <summary>Installs kernel modules; internal composition-root entry.</summary>
        internal static void Install() {
            GrowRuntime.Install();
        }

        /// <summary>Boots the kernel; internal composition-root entry.</summary>
        internal static void Boot() {
            GrowRuntime.Boot();
        }

        /// <summary>Shuts the kernel down; internal composition-root entry.</summary>
        internal static void Shutdown() {
            GrowRuntime.Shutdown();
        }
    }
}
