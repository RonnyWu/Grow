// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Ioc;
using Grow.Kernel.Lifecycle;

namespace Grow.Kernel.Modules.Impl {
    /// <summary>
    /// Module context: a view over the shared service collection plus managed-service driving.
    /// </summary>
    /// <remarks>
    /// The service table is shared with the IOC root; this context only tracks which registrations
    /// and managed services it owns, so shutdown can remove the module's entries. It never uses
    /// reflection.
    /// <para>
    /// The context lock protects its ownership and managed lists. User hooks run outside the lock and
    /// against a snapshot; lazy factories run inside the IOC collection's lock discipline (the
    /// collection calls them outside its own lock).
    /// </para>
    /// </remarks>
    internal sealed class ModuleContext : IModuleContext {
        private const string LogCategory = "Modules";

        private readonly object _gate = new object();
        private readonly ServiceCollection _services;
        private readonly HashSet<Type> _owned = new HashSet<Type>();
        private readonly List<ManagedEntry> _managed = new List<ManagedEntry>();
        private readonly HashSet<object> _managedSet = new HashSet<object>(ReferenceComparer.Instance);

        private LifecycleScope _scope;

        public ModuleContext(ServiceCollection services) {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>Current phase; written by the registry.</summary>
        public ModulePhase Phase { get; internal set; }

        /// <summary>Receives runtime managed-service failures so the registry can record them.</summary>
        internal Action<object, ModuleFailureKind, string> FailureSink { get; set; }

        /// <inheritdoc />
        public void Register<T>(T instance) where T : class {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            ManagedEntry entry;
            var type = typeof(T);
            lock (_gate) {
                _owned.Add(type);
                _services.AddSingleton(instance);
                entry = TrackManagedLocked(instance, fromLazy: false);
            }

            if (entry != null && Phase == ModulePhase.Running) {
                DriveImmediate(entry);
            }
        }

        /// <inheritdoc />
        public void RegisterLazy<T>(Func<T> factory) where T : class {
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (_gate) {
                _owned.Add(typeof(T));
                _services.AddSingleton(factory);
            }
        }

        /// <inheritdoc />
        public T GetService<T>() where T : class {
            var instance = _services.GetService<T>();
            TrackResolved(instance);
            return instance;
        }

        /// <inheritdoc />
        public T GetRequiredService<T>() where T : class {
            var instance = _services.GetRequiredService<T>();
            TrackResolved(instance);
            return instance;
        }

        private void TrackResolved<T>(T instance) where T : class {
            if (instance == null) {
                return;
            }

            ManagedEntry managed = null;
            lock (_gate) {
                if (_owned.Contains(typeof(T))) {
                    managed = TrackManagedLocked(instance, fromLazy: true);
                }
            }

            if (managed != null && Phase == ModulePhase.Running) {
                DriveImmediate(managed);
            }
        }

        /// <summary>Returns the registry-wide shared lifecycle scope.</summary>
        public LifecycleScope Scope {
            get {
                lock (_gate) {
                    return _scope ??= new LifecycleScope();
                }
            }
        }

        /// <summary>Initializes managed services in registration order; returns failed instances.</summary>
        internal IReadOnlyList<object> DriveManagedInitialize() {
            var failed = new List<object>();
            var entries = SnapshotManaged();
            for (var i = 0; i < entries.Length; i++) {
                DriveInitializeEntry(entries[i], failed);
            }

            return failed;
        }

        /// <summary>
        /// Initializes then starts managed services, repeating until no new service appears.
        /// A service whose initialization failed is not started in this cycle.
        /// </summary>
        internal IReadOnlyList<object> DriveManagedStart() {
            var failed = new List<object>();
            const int maxPasses = 8;

            for (var pass = 0; pass < maxPasses; pass++) {
                var entries = SnapshotManaged();
                for (var i = 0; i < entries.Length; i++) {
                    if (!DriveInitializeEntry(entries[i], failed)) {
                        continue;
                    }

                    DriveStartEntry(entries[i], failed);
                }

                if (SnapshotManaged().Length == entries.Length) {
                    return failed;
                }
            }

            GrowLog.Error(
                "[Modules] Managed services kept registering during start; the graph may be unbounded.",
                LogCategory);
            return failed;
        }

        /// <summary>Deinitializes managed services in reverse registration order.</summary>
        internal IReadOnlyList<object> DriveManagedDeinitialize() {
            var failed = new List<object>();
            var entries = SnapshotManaged();

            for (var i = entries.Length - 1; i >= 0; i--) {
                var entry = entries[i];
                if (entry.Instance is IDeinitializable deinitializable) {
                    try {
                        deinitializable.OnDeinitialize();
                    } catch (Exception exception) {
                        GrowLog.Error(
                            $"[Modules] Managed service {entry.Instance.GetType().Name}.OnDeinitialize " +
                            $"threw: {exception}",
                            LogCategory);
                        failed.Add(entry.Instance);
                    }
                }

                entry.InitializeDriven = false;
                entry.InitializeSucceeded = false;
                entry.StartDriven = false;
            }

            return failed;
        }

        /// <summary>Removes this context's registrations and managed entries before the next bootstrap.</summary>
        internal void ResetForNextBootstrap() {
            lock (_gate) {
                foreach (var type in _owned) {
                    _services.Remove(type, string.Empty);
                }

                _owned.Clear();
                _managed.Clear();
                _managedSet.Clear();
            }
        }

        /// <summary>Removes and returns the module scope; the caller owns its disposal.</summary>
        internal LifecycleScope TakeLifecycleScope() {
            lock (_gate) {
                var scope = _scope;
                _scope = null;
                return scope;
            }
        }

        private ManagedEntry TrackManagedLocked(object instance, bool fromLazy) {
            if (!IsManaged(instance)) {
                return null;
            }

            if (!_managedSet.Add(instance)) {
                return null;
            }

            var entry = new ManagedEntry(instance, fromLazy);
            _managed.Add(entry);
            return entry;
        }

        private void UntrackManagedLocked(object instance) {
            if (instance == null) {
                return;
            }

            if (!_managedSet.Remove(instance)) {
                return;
            }

            for (var i = _managed.Count - 1; i >= 0; i--) {
                if (!ReferenceEquals(_managed[i].Instance, instance)) {
                    continue;
                }

                _managed.RemoveAt(i);
                return;
            }
        }

        private ManagedEntry[] SnapshotManaged() {
            lock (_gate) {
                return _managed.ToArray();
            }
        }

        private void DriveImmediate(ManagedEntry entry) {
            var failed = new List<object>();
            if (DriveInitializeEntry(entry, failed)) {
                DriveStartEntry(entry, failed);
            }

            for (var i = 0; i < failed.Count; i++) {
                ReportFailure(
                    failed[i],
                    ModuleFailureKind.ManagedServiceHookException,
                    "Runtime managed-service hook threw; the service stays unready.");
            }
        }

        private bool DriveInitializeEntry(ManagedEntry entry, List<object> failed) {
            if (entry.InitializeDriven) {
                return entry.InitializeSucceeded;
            }

            entry.InitializeDriven = true;

            if (!(entry.Instance is IInitializable initializable)) {
                entry.InitializeSucceeded = true;
                return true;
            }

            try {
                initializable.OnInitialize();
                entry.InitializeSucceeded = true;
                return true;
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[Modules] Managed service {entry.Instance.GetType().Name}.OnInitialize threw: " +
                    exception,
                    LogCategory);
                failed.Add(entry.Instance);
                return false;
            }
        }

        private void DriveStartEntry(ManagedEntry entry, List<object> failed) {
            if (entry.StartDriven) {
                return;
            }

            if (!entry.InitializeSucceeded) {
                return;
            }

            entry.StartDriven = true;

            if (!(entry.Instance is IStartable startable)) {
                return;
            }

            try {
                startable.OnStart();
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[Modules] Managed service {entry.Instance.GetType().Name}.OnStart threw: " +
                    exception,
                    LogCategory);
                failed.Add(entry.Instance);
            }
        }

        private void ReportFailure(object instance, ModuleFailureKind kind, string reason) {
            try {
                FailureSink?.Invoke(instance, kind, reason);
            } catch (Exception exception) {
                GrowLog.Error($"[Modules] Failure sink threw: {exception}", LogCategory);
            }
        }

        private static bool IsManaged(object instance) {
            return instance is IManagedService;
        }

        private sealed class ManagedEntry {
            public readonly object Instance;
            public readonly bool FromLazy;
            public bool InitializeDriven;
            public bool InitializeSucceeded;
            public bool StartDriven;

            public ManagedEntry(object instance, bool fromLazy) {
                Instance = instance;
                FromLazy = fromLazy;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object> {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
