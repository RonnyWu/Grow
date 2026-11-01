// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Kernel.Modules;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// Composes a kernel: the module list plus the startup switches behind one
    /// <see cref="GrowHost"/>.
    /// </summary>
    /// <remarks>
    /// This is the assembly point a game uses instead of calling <see cref="GrowApp.RegisterModule"/>
    /// and <see cref="GrowApp.Initialize()"/> by hand. Build it once, after
    /// <c>RuntimeInitializeLoadType.SubsystemRegistration</c> has finished — for example in
    /// <c>AfterAssembliesLoaded</c>:
    /// <code>
    /// private static GrowHost _host;
    ///
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    /// private static void Main() {
    ///     _host = new GrowBuilder()
    ///         .AddModule(new GameModule())
    ///         .Build();
    /// }
    /// </code>
    /// <para>
    /// The load type matters. The kernel resets its static state from its own
    /// <c>SubsystemRegistration</c> hook, and Unity does not order that hook against a game's. A host
    /// built too early would be released by the reset; <c>AfterAssembliesLoaded</c> runs after every
    /// <c>SubsystemRegistration</c> hook and before the automatic <c>BeforeSceneLoad</c> start.
    /// </para>
    /// <para>
    /// The builder knows only kernel types: capability convenience such as <c>UseStore()</c> is an
    /// extension method in that capability's package, so the kernel never learns about a capability.
    /// The future meta package, when it exists, is this same shape with a default list of
    /// capabilities.
    /// </para>
    /// <para>
    /// Only one host may exist per session. <see cref="Build"/> claims the process-wide slot,
    /// registers the modules and turns off automatic initialization; the host releases the slot when
    /// disposed. A second build throws instead of silently sharing global state.
    /// </para>
    /// </remarks>
    public sealed class GrowBuilder {
        private readonly List<IModule> _modules = new List<IModule>();
        private readonly GrowBootOptions _options = new GrowBootOptions();

        private bool _built;

        /// <summary>Adds a module; modules start in dependency order.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="module"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The builder already built a host.</exception>
        public GrowBuilder AddModule(IModule module) {
            if (module == null) {
                throw new ArgumentNullException(nameof(module));
            }

            if (_built) {
                throw new InvalidOperationException(
                    "[Grow] AddModule must run before Build(); the module list is already fixed.");
            }

            _modules.Add(module);
            return this;
        }

        /// <summary>Applies startup switches; a <c>null</c> delegate is ignored.</summary>
        public GrowBuilder ConfigureBoot(Action<GrowBootOptions> configure) {
            if (_built) {
                throw new InvalidOperationException(
                    "[Grow] ConfigureBoot must run before Build(); the options are already applied.");
            }

            configure?.Invoke(_options);
            return this;
        }

        /// <summary>
        /// Registers the modules and returns the host. With
        /// <see cref="GrowBootOptions.AutoStart"/> the host is already started when this returns.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The builder already built a host, or another host exists in this session.
        /// </exception>
        public GrowHost Build() {
            if (_built) {
                throw new InvalidOperationException(
                    "[Grow] A builder builds exactly one host; create a new builder instead.");
            }

            _built = true;

            var host = new GrowHost(_options);
            host.Claim();
            try {
                for (var i = 0; i < _modules.Count; i++) {
                    GrowApp.RegisterModule(_modules[i]);
                }

                host.CaptureAutoInitialize();
            } catch {
                host.ReleaseClaim();
                throw;
            }

            if (_options.AutoStart) {
                try {
                    host.Start();
                } catch {
                    // A failed start must not keep the slot, the AutoInitialize capture or a host the
                    // caller never received; Dispose releases both and rolls back best-effort.
                    host.Dispose();
                    throw;
                }
            }

            return host;
        }
    }
}
