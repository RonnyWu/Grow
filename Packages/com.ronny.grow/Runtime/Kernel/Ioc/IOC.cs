// SPDX-License-Identifier: MIT

using Grow.Kernel.Ioc.Interface;
using UnityEngine;

namespace Grow.Kernel.Ioc {
    /// <summary>
    /// Static service locator over the process-wide root <see cref="ServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// Keyed identities are exact: a keyed lookup never falls back to keyless and vice versa.
    /// <see cref="CreateScope"/> returns an isolated child that can resolve root registrations but
    /// never writes back to them. Duplicate registration replaces the previous descriptor.
    /// <para>
    /// This container is a pure locator; it does not drive lifecycle. Managed services are driven by
    /// the module container. The root is cleared in place at <c>SubsystemRegistration</c> so a Play
    /// session with domain reload disabled starts clean without invalidating existing references.
    /// </para>
    /// </remarks>
    public static class IOC {
        private static ServiceCollection _service = new ServiceCollection();

        /// <summary>Resolves a service; returns <c>null</c> when absent.</summary>
        public static T GetService<T>(string key = "") where T : class {
            return _service.GetService<T>(key);
        }

        /// <summary>Resolves a service; throws <see cref="Common.ServiceNotFoundException"/> when absent.</summary>
        public static T GetRequiredService<T>(string key = "") where T : class {
            return _service.GetRequiredService<T>(key);
        }

        /// <summary>True when the exact identity is registered.</summary>
        public static bool Contains<T>(string key = "") where T : class {
            return _service.Contains<T>(key);
        }

        /// <summary>Removes the exact identity from the root.</summary>
        public static bool Remove<T>(string key = "") where T : class {
            return _service.Remove<T>(key);
        }

        /// <summary>Clears root registrations.</summary>
        public static void Clear() {
            _service.Clear();
        }

        /// <summary>Registers a keyless pre-built singleton.</summary>
        public static void AddSingleton<T>(T instance) where T : class {
            _service.AddSingleton(instance);
        }

        /// <summary>Registers a keyed singleton; "unique" is the historical API name.</summary>
        public static void AddUnique<T>(T instance, string key = "") where T : class {
            _service.AddUnique(instance, key);
        }

        /// <summary>Registers a transient factory.</summary>
        public static void InjectInstance<T, TFactory>()
            where T : class
            where TFactory : IServiceFactory<T>, new() {
            _service.InjectInstance<T, TFactory>();
        }

        /// <summary>Registers a keyed singleton factory; "unique" is the historical name.</summary>
        public static void InjectUnique<T, TFactory>(string key = "")
            where T : class
            where TFactory : IServiceFactory<T>, new() {
            _service.InjectUnique<T, TFactory>(key);
        }

        /// <summary>Registers a keyless singleton factory.</summary>
        public static void InjectSingleton<T, TFactory>()
            where T : class
            where TFactory : IServiceFactory<T>, new() {
            _service.InjectSingleton<T, TFactory>();
        }

        /// <summary>Creates a child scope of the root.</summary>
        public static IServiceCollection CreateScope() {
            return _service.CreateScope();
        }

        /// <summary>The process-wide root collection; use <see cref="CreateScope"/> for isolation.</summary>
        public static ServiceCollection Root => _service;

        /// <summary>Clears the root in place at the start of a play session.</summary>
        internal static void ResetStaticState() {
            _service.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnLoad() {
            ResetStaticState();
        }
    }
}
