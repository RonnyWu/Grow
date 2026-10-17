// SPDX-License-Identifier: MIT

using Grow.Kernel.Ioc.Common;

namespace Grow.Kernel.Ioc.Interface {
    /// <summary>Registration and resolution surface of a service collection.</summary>
    /// <remarks>
    /// Identity is the exact pair (service type, key); a keyed query never falls back to the keyless
    /// registration, and vice versa. A child scope resolves its own registrations first, then walks
    /// toward the root; registrations and removals only affect the current scope.
    /// </remarks>
    public interface IServiceCollection {
        /// <summary>Resolves a service; returns <c>null</c> when absent.</summary>
        T GetService<T>(string key = "") where T : class;

        /// <summary>Resolves a service; throws <see cref="ServiceNotFoundException"/> when absent.</summary>
        T GetRequiredService<T>(string key = "") where T : class;

        /// <summary>True when this scope or an ancestor has the exact identity.</summary>
        bool Contains<T>(string key = "") where T : class;

        /// <summary>Removes the exact identity from this scope; ancestors are not touched.</summary>
        bool Remove<T>(string key = "") where T : class;

        /// <summary>Clears registrations in this scope only.</summary>
        void Clear();

        /// <summary>Registers a keyless pre-built singleton.</summary>
        void AddSingleton<T>(T instance) where T : class;

        /// <summary>Registers a keyed singleton; "unique" is the historical API name.</summary>
        void AddUnique<T>(T instance, string key = "") where T : class;

        /// <summary>Registers a transient factory (new instance per resolution).</summary>
        void InjectInstance<T, TFactory>()
            where T : class
            where TFactory : IServiceFactory<T>, new();

        /// <summary>Registers a keyed singleton factory; "unique" is the historical name.</summary>
        void InjectUnique<T, TFactory>(string key = "")
            where T : class
            where TFactory : IServiceFactory<T>, new();

        /// <summary>Registers a keyless singleton factory.</summary>
        void InjectSingleton<T, TFactory>()
            where T : class
            where TFactory : IServiceFactory<T>, new();

        /// <summary>Creates a child scope whose parent is this collection.</summary>
        IServiceCollection CreateScope();
    }
}
