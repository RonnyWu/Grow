// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Grow.Core.Events;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Loop;
using Grow.Kernel.Modules;
using Grow.Kernel.Scheduling;
using UnityEngine;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// The single composition root: boot state, loop host, lifecycle manager and modules.
    /// </summary>
    /// <remarks>
    /// Initialization order is fixed: <see cref="GrowBoot.Reset"/>, <see cref="GrowLoop.Install"/>,
    /// <see cref="MonoManager.Bind"/>, <see cref="GrowScheduler.Attach"/>,
    /// <see cref="GrowModules.Shutdown"/>,
    /// <see cref="GrowModules.Bootstrap"/>, <see cref="GrowBoot.Install"/>, then
    /// <see cref="GrowBoot.Boot"/>. The defensive shutdown settles a module table left by a previous
    /// session before the new bootstrap. The Installed notification is published only after the
    /// module graph has been initialized, so an observer never sees Installed while the graph is
    /// still coming up. Shutdown reverses the ownership order: the boot pump first, then modules
    /// (they may use the loop), the scheduler subscriptions, the manager binding, the loop host and
    /// the boot state.
    /// <para>
    /// A module that implements <see cref="IBootLoadable"/> keeps the boot in
    /// <see cref="GrowState.Loading"/> across frames: the composition root subscribes a pump that
    /// advances one load step per frame, runs the start pass when the last load finishes, and only
    /// then publishes <see cref="GrowState.Ready"/>. A load that throws publishes
    /// <see cref="GrowState.Failed"/> once and rolls the composition back. A graph without such a
    /// module still reaches <see cref="GrowState.Ready"/> before <see cref="Initialize()"/> returns.
    /// </para>
    /// <para>
    /// Initialize and shutdown are idempotent and safe to repeat. A failed initialization performs
    /// best-effort cleanup in reverse order and rethrows. Modules must be registered before
    /// initialization;
    /// <see cref="RegisterModule"/> and <see cref="Initialize(IEnumerable{IModule})"/> are the
    /// explicit registration paths. Registration belongs to a play session: the module registry is
    /// settled when Play Mode exits, so a new session can register fresh instances.
    /// </para>
    /// <para>
    /// The root is installed automatically at <c>BeforeSceneLoad</c> unless
    /// <see cref="AutoInitialize"/> is turned off, and is torn down on <c>ExitingPlayMode</c>.
    /// <see cref="GrowSession"/> invalidates the composition flags at <c>SubsystemRegistration</c>
    /// so a session without domain reload starts clean.
    /// </para>
    /// </remarks>
    public static class GrowApp {
        private const string LogCategory = "Grow";

        private enum CompositionState {
            None = 0,
            Initializing = 1,
            Ready = 2,
            ShuttingDown = 3
        }

        private static int _state = (int)CompositionState.None;
        private static GrowSubscription _bootPumpSubscription;

        /// <summary>
        /// When true (the default), the composition initializes automatically at
        /// <c>BeforeSceneLoad</c>. Set it to false in a <c>SubsystemRegistration</c> hook to own the
        /// startup timing manually.
        /// </summary>
        public static bool AutoInitialize { get; set; } = true;

        /// <summary>
        /// True once the kernel is fully ready: <see cref="Initialize()"/> composed the kernel and
        /// no asynchronous module load is still running.
        /// </summary>
        public static bool IsInitialized =>
            Volatile.Read(ref _state) == (int)CompositionState.Ready && GrowBoot.IsReady;

        /// <summary>The host object, or <c>null</c> when the loop is not installed.</summary>
        public static GameObject Root {
            get {
                var host = GrowLoop.Host;
                return host != null ? host.gameObject : null;
            }
        }

        /// <summary>Registers a module; must happen before <see cref="Initialize()"/>.</summary>
        public static void RegisterModule(IModule module) {
            if (module == null) {
                throw new ArgumentNullException(nameof(module));
            }

            GrowModules.Register(module);
        }

        /// <summary>Registers modules and then initializes the composition root.</summary>
        public static void Initialize(IEnumerable<IModule> modules) {
            if (modules != null) {
                foreach (var module in modules) {
                    RegisterModule(module);
                }
            }

            Initialize();
        }

        /// <summary>Initializes the kernel in a fixed order; idempotent.</summary>
        public static void Initialize() {
            if (Interlocked.CompareExchange(
                    ref _state,
                    (int)CompositionState.Initializing,
                    (int)CompositionState.None) != (int)CompositionState.None) {
                if (Volatile.Read(ref _state) == (int)CompositionState.ShuttingDown) {
                    GrowLog.Warning(
                        "[Grow] Initialize() ignored while the composition is shutting down.",
                        LogCategory);
                }

                return;
            }

            try {
                GrowBoot.Reset();
                GrowLoop.Install();
                MonoManager.Instance.Bind(GrowLoop.Executor);
                GrowScheduler.Attach(MonoManager.Instance);
                GrowModules.Shutdown();
                GrowModules.Bootstrap();
                GrowBoot.Install();

                GrowLoop.QuitCommitted = Shutdown;

                // Ready must be observable from a StateChanged(Ready) handler, so commit the
                // composition state before Boot publishes the notification.
                Interlocked.Exchange(ref _state, (int)CompositionState.Ready);
                GrowBoot.Boot(complete: false);

                if (GrowModules.LoadFailed) {
                    FailBoot(GrowModules.LoadFailureMessage);
                    return;
                }

                if (GrowModules.HasPendingLoad) {
                    GrowBoot.BeginLoading();
                    if (!StartBootPump()) {
                        return;
                    }

                    GrowLog.Info(
                        "[Grow] Initializing: waiting for asynchronous module loads.", LogCategory);
                    return;
                }

                GrowBoot.Ready();

                GrowLog.Info("[Grow] Initialized: loop, lifecycle manager, modules and boot state.",
                    LogCategory);
            } catch {
                Rollback();
                throw;
            }
        }

        /// <summary>
        /// Shuts the composition down in reverse ownership order; idempotent. Registered modules are
        /// kept so a later <see cref="Initialize()"/> can restart the same composition; the module
        /// table is dropped by <see cref="SettleForSessionEnd"/>.
        /// </summary>
        public static void Shutdown() {
            if (Interlocked.CompareExchange(
                    ref _state,
                    (int)CompositionState.ShuttingDown,
                    (int)CompositionState.Ready) != (int)CompositionState.Ready) {
                if (Volatile.Read(ref _state) == (int)CompositionState.Initializing) {
                    GrowLog.Warning(
                        "[Grow] Shutdown() ignored while the composition is initializing.",
                        LogCategory);
                }

                return;
            }

            try {
                Step("pump", StopBootPump);
                Step("modules", GrowModules.Shutdown);
                Step("scheduler", GrowScheduler.Detach);
                Step("manager", MonoManager.Instance.Unbind);
                Step("loop", GrowLoop.Uninstall);
                Step("boot", GrowBoot.Shutdown);
            } finally {
                GrowLoop.QuitCommitted = null;
                // Keep the module table: Shutdown() is a same-session stop/start boundary. The
                // registry is dropped by SettleForSessionEnd() (Play Mode exit) and Rollback().
                Interlocked.Exchange(ref _state, (int)CompositionState.None);
                GrowLog.Info("[Grow] Shutdown.", LogCategory);
            }
        }

        /// <summary>Returns a non-throwing snapshot of the composition state.</summary>
        public static string Describe() {
            var executor = GrowLoop.Executor;
            var builder = new StringBuilder();
            builder.Append("[Grow] initialized=").Append(IsInitialized)
                .Append(", state=").Append((CompositionState)Volatile.Read(ref _state))
                .Append(", root=").Append(Root != null ? Root.name : "(none)")
                .Append(", executor=").Append(executor != null && executor.Valid
                    ? executor.GetType().Name
                    : "(none)")
                .Append(", manager=").Append(SafeManagerState())
                .Append(", loop=").Append(GrowLoop.IsInstalled ? "installed" : "not-installed")
                .Append(", boot=").Append(GrowBoot.State)
                .Append('\n');
            builder.Append("  ").Append(GrowModules.Describe());
            return builder.ToString();
        }

        /// <summary>Closes a play session and drops the module table for the next session.</summary>
        internal static void SettleForSessionEnd() {
            Shutdown();
            GrowModules.ResetStaticState();
            GrowLoop.QuitCommitted = null;
        }

        /// <summary>Subscribes the boot pump to the frame loop; false when the host is unavailable.</summary>
        private static bool StartBootPump() {
            StopBootPump();
            var update = MonoManager.Instance.Update;
            if (update == null) {
                FailBoot("The loop host is unavailable; the asynchronous boot cannot advance.");
                return false;
            }

            _bootPumpSubscription = update.Subscribe(PumpBoot);
            return true;
        }

        private static void StopBootPump() {
            _bootPumpSubscription.Dispose();
            _bootPumpSubscription = default;
        }

        /// <summary>
        /// Advances one asynchronous load step. The composition root subscribes this to the update
        /// phase; tests call it directly because Edit Mode has no running player loop.
        /// </summary>
        internal static void PumpBoot() {
            if (GrowBoot.State != GrowState.Loading) {
                StopBootPump();
                return;
            }

            switch (GrowModules.AdvanceLoad()) {
                case LoadStepResult.Pending:
                    return;
                case LoadStepResult.Completed:
                    StopBootPump();
                    GrowModules.FinishStart();
                    GrowBoot.Ready();
                    GrowLog.Info("[Grow] Boot completed after asynchronous module loads.",
                        LogCategory);
                    return;
                default:
                    FailBoot(GrowModules.LoadFailureMessage);
                    return;
            }
        }

        /// <summary>Publishes the failure, then rolls the composition back.</summary>
        private static void FailBoot(string message) {
            StopBootPump();
            GrowBoot.Fail(message ?? "The kernel boot failed while loading modules.");
            Shutdown();
        }

        private static void Rollback() {
            StopBootPump();
            Step("modules", GrowModules.Shutdown);
            Step("scheduler", GrowScheduler.Detach);
            Step("manager", MonoManager.Instance.Unbind);
            Step("loop", GrowLoop.Uninstall);
            Step("boot", GrowBoot.Shutdown);
            GrowModules.ResetStaticState();
            GrowLoop.QuitCommitted = null;
            Interlocked.Exchange(ref _state, (int)CompositionState.None);
        }

        private static void Step(string name, Action action) {
            try {
                action();
            } catch (Exception exception) {
                GrowLog.Error($"[Grow] {name} teardown failed: {exception}", LogCategory);
            }
        }

        private static string SafeManagerState() {
            try {
                return MonoManager.Instance.Valid ? "bound" : "unbound";
            } catch (ObjectDisposedException) {
                return "disposed";
            }
        }

        /// <summary>Drops composition flags at the start of a play session without running user hooks.</summary>
        internal static void ResetStaticState() {
            StopBootPump();
            Interlocked.Exchange(ref _state, (int)CompositionState.None);
            GrowLoop.QuitCommitted = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad() {
            if (AutoInitialize) {
                Initialize();
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorHook() {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state) {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) SettleForSessionEnd();
        }
#endif
    }
}
