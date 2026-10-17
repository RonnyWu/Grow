// SPDX-License-Identifier: MIT

using Grow.Core.Events;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// Public facade for the kernel lifecycle state machine.
    /// </summary>
    /// <remarks>
    /// It forwards to the internal state machine; callers drive it through the four write entries.
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

        /// <summary>Starts a cycle from None/Shutdown.</summary>
        public static void Reset() {
            GrowRuntime.Reset();
        }

        /// <summary>Installs kernel modules.</summary>
        public static void Install() {
            GrowRuntime.Install();
        }

        /// <summary>Boots the kernel.</summary>
        public static void Boot() {
            GrowRuntime.Boot();
        }

        /// <summary>Shuts the kernel down.</summary>
        public static void Shutdown() {
            GrowRuntime.Shutdown();
        }
    }
}
