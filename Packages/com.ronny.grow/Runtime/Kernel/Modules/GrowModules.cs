// SPDX-License-Identifier: MIT

using Grow.Kernel.Ioc;
using UnityEngine;

namespace Grow.Kernel.Modules {
    /// <summary>
    /// Static module-system facade over a <see cref="Impl.ModuleRegistry"/>.
    /// </summary>
    /// <remarks>
    /// Register modules before <see cref="Bootstrap"/>; late registration fails fast because a
    /// partially booted module graph cannot be made consistent safely. The facade holds the
    /// process-wide registry; it has no automatic session-reset hook because registration and reset
    /// would share the same unspecified <c>SubsystemRegistration</c> order. The composition root
    /// settles an old registry before bootstrapping; tests and hosts can call <see cref="Reset"/>.
    /// The registry shares the IOC root service table.
    /// <para>
    /// A module that implements <see cref="IBootLoadable"/> turns <see cref="Bootstrap"/> into the
    /// first half of a boot: the call stops in <see cref="ModulePhase.Load"/> and the composition
    /// root drives the load pass and <see cref="FinishStart"/>. Without such a module the call
    /// still ends in <see cref="ModulePhase.Running"/>.
    /// </para>
    /// </remarks>
    public static class GrowModules {
        private static Impl.ModuleRegistry _registry = new(IOC.Root);

        /// <summary>True after <see cref="Bootstrap"/> completed.</summary>
        public static bool IsReady => _registry.IsReady;

        /// <summary>Current module phase.</summary>
        public static ModulePhase Phase => _registry.Phase;

        /// <summary>Failures from the latest bootstrap/shutdown, deduplicated by name.</summary>
        public static System.Collections.Generic.IReadOnlyList<string> FailedModules =>
            _registry.FailedModules;

        /// <summary>True when the latest bootstrap/shutdown recorded no failure.</summary>
        public static bool Healthy => _registry.Healthy;

        /// <summary>Structured failures from the latest bootstrap/shutdown.</summary>
        public static System.Collections.Generic.IReadOnlyList<ModuleFailure> Failures =>
            _registry.Failures;

        /// <summary>Returns a one-line-plus-details assembly report.</summary>
        public static string Describe() {
            return _registry.Describe();
        }

        /// <summary>Registers a module; must run before <see cref="Bootstrap"/>.</summary>
        public static void Register(IModule module) {
            _registry.Register(module);
        }

        /// <summary>Boots all registered modules; idempotent.</summary>
        public static void Bootstrap() {
            _registry.Bootstrap();
        }

        /// <summary>Shuts down started modules; idempotent.</summary>
        public static void Shutdown() {
            _registry.Shutdown();
        }

        /// <summary>Resolves a service; returns <c>null</c> when it is not registered.</summary>
        public static T GetService<T>() where T : class {
            return _registry.GetService<T>();
        }

        /// <summary>Resolves a service; throws when it is not registered.</summary>
        public static T GetRequiredService<T>() where T : class {
            return _registry.GetRequiredService<T>();
        }

        /// <summary>True while an asynchronous module load is still ahead.</summary>
        internal static bool HasPendingLoad => _registry.HasPendingLoad;

        /// <summary>True when the latest bootstrap aborted in the load pass.</summary>
        internal static bool LoadFailed => _registry.LoadFailed;

        /// <summary>Message of the first load failure, or <c>null</c>.</summary>
        internal static string LoadFailureMessage => _registry.LoadFailureMessage;

        /// <summary>Overall load progress, 1 when there is nothing to load.</summary>
        internal static float LoadProgress => _registry.LoadProgress;

        /// <summary>Name of the module currently loading, or <c>null</c> between loads.</summary>
        internal static string LoadingModuleName => _registry.LoadingModuleName;

        /// <summary>Advances the asynchronous load pass by one step.</summary>
        internal static LoadStepResult AdvanceLoad() {
            return _registry.AdvanceLoad();
        }

        /// <summary>Runs the start pass after every load finished.</summary>
        internal static void FinishStart() {
            _registry.FinishStart();
        }

        /// <summary>
        /// Stops started modules and replaces the registry with an empty one.
        /// </summary>
        /// <remarks>
        /// Intended for tests and hosts that explicitly reset a session. A started module receives
        /// <c>OnStop</c> and <c>OnDispose</c> before the registry is replaced. The static facade has no
        /// automatic <c>SubsystemRegistration</c> hook: modules are commonly registered in that same
        /// load phase, where hook order is unspecified.
        /// </remarks>
        public static void Reset() {
            ResetStaticState();
        }

        /// <summary>Session reset used by the composition root; also backs <see cref="Reset"/>.</summary>
        internal static void ResetStaticState() {
            _registry.Shutdown();
            _registry = new Impl.ModuleRegistry(IOC.Root);
        }
    }
}
