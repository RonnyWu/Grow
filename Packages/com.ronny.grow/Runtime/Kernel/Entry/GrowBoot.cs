// SPDX-License-Identifier: MIT

using Grow.Core.Events;
using Grow.Kernel.Modules;

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

        /// <summary>
        /// True once the kernel is usable: every module started and every asynchronous load
        /// finished.
        /// </summary>
        public static bool IsReady => GrowRuntime.IsReady;

        /// <summary>
        /// Overall load progress in the 0..1 range; monotonically non-decreasing, and 1 when there
        /// is nothing to load.
        /// </summary>
        public static float Progress => GrowModules.LoadProgress;

        /// <summary>Name of the module currently loading, or <c>null</c> between loads.</summary>
        public static string LoadingModule => GrowModules.LoadingModuleName;

        /// <summary>
        /// Message of the first fatal boot failure in this cycle, or <c>null</c>. It stays available
        /// after the composition rolls back, until the next <see cref="GrowApp.Initialize()"/>.
        /// </summary>
        public static string FailureMessage => GrowRuntime.FailureMessage;

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
        /// <param name="complete">
        /// True reaches <see cref="GrowState.Ready"/> immediately; false stops at
        /// <see cref="GrowState.Booted"/> so an asynchronous load can run first.
        /// </param>
        internal static void Boot(bool complete = true) {
            GrowRuntime.Boot(complete);
        }

        /// <summary>Enters the loading state; internal composition-root entry.</summary>
        internal static void BeginLoading() {
            GrowRuntime.BeginLoading();
        }

        /// <summary>Completes the boot; internal composition-root entry.</summary>
        internal static void Ready() {
            GrowRuntime.Ready();
        }

        /// <summary>Records a fatal boot failure; internal composition-root entry.</summary>
        internal static void Fail(string message) {
            GrowRuntime.Fail(message);
        }

        /// <summary>Shuts the kernel down; internal composition-root entry.</summary>
        internal static void Shutdown() {
            GrowRuntime.Shutdown();
        }
    }
}
