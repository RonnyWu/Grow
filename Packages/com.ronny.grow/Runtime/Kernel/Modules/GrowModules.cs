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
