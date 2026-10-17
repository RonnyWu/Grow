// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Kernel.Lifecycle;

namespace Grow.Kernel.Modules {
    /// <summary>
    /// A module with a name, a same-layer priority and declared dependencies.
    /// </summary>
    /// <remarks>
    /// <see cref="GrowModules"/> drives modules in dependency order. Dependencies are declared as
    /// concrete module types; a declaration must match exactly one registered module. A missing or
    /// ambiguous dependency skips the dependent module, and a dependency cycle skips every module in
    /// the cycle (and, transitively, its dependents).
    /// </remarks>
    public interface IModule {
        /// <summary>Unique module name; duplicates are rejected at registration.</summary>
        string Name { get; }

        /// <summary>Same-layer ordering priority; higher values start first.</summary>
        int Priority { get; }

        /// <summary>Concrete module types this module depends on.</summary>
        IReadOnlyList<Type> Dependencies { get; }

        /// <summary>Registration hook; services can be registered on the context here.</summary>
        void OnRegister(IModuleContext context);

        /// <summary>Initialization hook.</summary>
        void OnInitialize(IModuleContext context);

        /// <summary>Startup hook.</summary>
        void OnStart(IModuleContext context);

        /// <summary>Stop hook, called in reverse startup order.</summary>
        void OnStop();

        /// <summary>Dispose hook, called in reverse startup order.</summary>
        void OnDispose();
    }

    /// <summary>
    /// The module-facing context: current phase, cross-module services and a module-owned scope.
    /// </summary>
    /// <remarks>
    /// Services registered here are the module container's table; the IOC root remains a separate
    /// locator. The module container drives managed services that implement initialize, start and
    /// deinitialize; the IOC never drives lifetimes.
    /// </remarks>
    public interface IModuleContext {
        /// <summary>Current module phase.</summary>
        ModulePhase Phase { get; }

        /// <summary>Registers an instance; a managed service is added to the container drive list.</summary>
        void Register<T>(T instance) where T : class;

        /// <summary>Registers a lazy factory; its product is cached on first resolve.</summary>
        void RegisterLazy<T>(Func<T> factory) where T : class;

        /// <summary>Resolves a service; returns <c>null</c> when it is not registered.</summary>
        T GetService<T>() where T : class;

        /// <summary>Resolves a service; throws when it is not registered.</summary>
        T GetRequiredService<T>() where T : class;

        /// <summary>
        /// Returns the registry-wide shared <see cref="LifecycleScope"/>; the same instance is
        /// returned until the container shuts down and disposes it.
        /// </summary>
        LifecycleScope Scope { get; }
    }
}
