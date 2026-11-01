// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Events;
using Grow.Kernel.Modules;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// A started composition: the kernel's public runtime handle for a game.
    /// </summary>
    /// <remarks>
    /// A host is created by <see cref="GrowBuilder.Build"/> and is the only supported owner of one
    /// composition in a session. It delegates to <see cref="GrowApp"/> and <see cref="GrowBoot"/>;
    /// it adds no boot logic of its own, so a host and a direct <see cref="GrowApp.Initialize()"/>
    /// observe the same state machine.
    /// <para>
    /// <see cref="Start"/> is idempotent and <see cref="Dispose"/> ends the composition and releases
    /// the process-wide builder slot. Disposing is what allows a second <see cref="GrowBuilder.Build"/>
    /// in the same session; the module registrations survive a shutdown, exactly as they do for
    /// <see cref="GrowApp.Shutdown()"/>.
    /// </para>
    /// </remarks>
    public sealed class GrowHost : IDisposable {
        private static GrowHost _current;

        private readonly GrowBootOptions _options;
        private bool _capturedAutoInitialize;
        private bool _disposed;

        internal GrowHost(GrowBootOptions options) {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Current lifecycle state.</summary>
        public GrowState State => GrowBoot.State;

        /// <summary>True when every module started and every asynchronous load finished.</summary>
        public bool IsReady => GrowBoot.IsReady;

        /// <summary>Overall boot progress in the 0..1 range.</summary>
        public float Progress => GrowBoot.Progress;

        /// <summary>Raised after every lifecycle state change.</summary>
        public GrowEvent<StateChange> StateChanged => GrowBoot.StateChanged;

        /// <summary>True while this host is the one carried by the process.</summary>
        public bool IsActive => !_disposed && ReferenceEquals(_current, this);

        /// <summary>Resolves a service; returns <c>null</c> when it is not registered.</summary>
        public T GetService<T>() where T : class {
            return GrowModules.GetService<T>();
        }

        /// <summary>Resolves a service; throws when it is not registered.</summary>
        public T GetRequiredService<T>() where T : class {
            return GrowModules.GetRequiredService<T>();
        }

        /// <summary>Starts the composition; idempotent and safe to repeat after a shutdown.</summary>
        /// <exception cref="ObjectDisposedException">The host was disposed.</exception>
        public void Start() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(GrowHost));
            }

            GrowApp.Initialize();
        }

        /// <summary>Shuts the composition down; idempotent.</summary>
        /// <remarks>Dispose does this for you; calling it early is allowed and keeps the host.</remarks>
        public void Shutdown() {
            if (_disposed) {
                return;
            }

            GrowApp.Shutdown();
        }

        /// <summary>Returns a non-throwing snapshot of the composition state.</summary>
        public string Describe() {
            return GrowApp.Describe();
        }

        /// <summary>Shuts the composition down and releases the builder slot.</summary>
        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            GrowApp.Shutdown();

            if (ReferenceEquals(_current, this)) {
                _current = null;
            }

            RestoreAutoInitialize();
        }

        internal void Claim() {
            if (_current != null) {
                throw new InvalidOperationException(
                    "[Grow] Only one GrowHost may exist per session. Dispose the current host " +
                    "before building another.");
            }

            _current = this;
        }

        internal void CaptureAutoInitialize() {
            if (!_options.CaptureAutoInitialize || !GrowApp.AutoInitialize) {
                return;
            }

            GrowApp.AutoInitialize = false;
            _capturedAutoInitialize = true;
        }

        internal void ReleaseClaim() {
            if (ReferenceEquals(_current, this)) {
                _current = null;
            }
        }

        /// <summary>Drops the process-wide slot at <c>SubsystemRegistration</c>.</summary>
        internal static void ResetStaticState() {
            var host = _current;
            _current = null;
            host?.RestoreAutoInitialize();
        }

        private void RestoreAutoInitialize() {
            if (!_capturedAutoInitialize) {
                return;
            }

            _capturedAutoInitialize = false;
            GrowApp.AutoInitialize = true;
        }
    }
}
