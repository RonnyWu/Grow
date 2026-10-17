// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using Grow.Core.Log;
using Grow.Kernel.Ioc;
using Grow.Kernel.Ioc.Interface;

namespace Grow.Kernel.Modules.Impl {
    /// <summary>
    /// Module registry: dependency ordering, phase driving, hook isolation and failure reporting.
    /// </summary>
    /// <remarks>
    /// Dependency matching uses <see cref="Type.IsInstanceOfType(object)"/> only; there is no
    /// reflection or dynamic construction. A declaration must match exactly one registered module.
    /// Missing, ambiguous and cyclic dependencies skip the affected modules and their dependents;
    /// hook failures are isolated so the rest of the graph still runs.
    /// <para>
    /// The registration list survives shutdown, so a registry can be bootstrapped again. Failed
    /// module names are deduplicated for quick checks; <see cref="Failures"/> keeps every structured
    /// record for diagnostics.
    /// </para>
    /// </remarks>
    internal sealed class ModuleRegistry {
        private const string LogCategory = "Modules";

        private readonly object _gate = new object();
        private readonly List<IModule> _modules = new List<IModule>();
        private readonly ModuleContext _context;
        private readonly List<IModule> _started = new List<IModule>();

        private readonly List<string> _failed = new List<string>();
        private readonly HashSet<string> _failedSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ModuleFailure> _failures = new List<ModuleFailure>();
        private readonly HashSet<IModule> _initializeFailed = new HashSet<IModule>();

        private bool _ready;
        private bool _bootstrapping;
        private bool _shuttingDown;

        /// <summary>Creates an isolated registry for tests or embedded composition.</summary>
        public ModuleRegistry() : this(new ServiceCollection()) {
        }

        /// <summary>Creates a registry over a shared service collection.</summary>
        internal ModuleRegistry(ServiceCollection services) {
            _context = new ModuleContext(services);
            _context.FailureSink = (instance, kind, reason) => {
                lock (_gate) {
                    RecordFailure(instance?.GetType().Name, kind, reason);
                }
            };
        }

        /// <summary>True after <see cref="Bootstrap"/> completed.</summary>
        public bool IsReady {
            get { lock (_gate) return _ready; }
        }

        /// <summary>Current phase.</summary>
        public ModulePhase Phase {
            get { lock (_gate) return _context.Phase; }
        }

        /// <summary>Failed module names, deduplicated and in discovery order.</summary>
        public IReadOnlyList<string> FailedModules {
            get { lock (_gate) return _failed.ToArray(); }
        }

        /// <summary>True when the latest bootstrap/shutdown recorded no failure.</summary>
        public bool Healthy {
            get { lock (_gate) return _failed.Count == 0; }
        }

        /// <summary>Structured failures from the latest bootstrap/shutdown.</summary>
        public IReadOnlyList<ModuleFailure> Failures {
            get { lock (_gate) return _failures.ToArray(); }
        }

        /// <summary>Returns a single consistent assembly report.</summary>
        public string Describe() {
            lock (_gate) {
                var builder = new StringBuilder();
                builder.Append("[Modules] phase=").Append(_context.Phase)
                    .Append(", ready=").Append(_ready)
                    .Append(", healthy=").Append(_failed.Count == 0)
                    .Append(", registered=").Append(_modules.Count)
                    .Append(", started=").Append(_started.Count)
                    .Append(", failures=").Append(_failures.Count);

                for (var i = 0; i < _failures.Count; i++) builder.Append("\n  - ").Append(_failures[i]);
                return builder.ToString();
            }
        }

        /// <summary>Registers a module; duplicate names and late registration fail fast.</summary>
        public void Register(IModule module) {
            if (module == null) throw new ArgumentNullException(nameof(module));

            lock (_gate) {
                if (_context.Phase != ModulePhase.None) {
                    throw new InvalidOperationException(
                        "[Modules] Modules must be registered before GrowModules.Bootstrap(). Register " +
                        "them from the earliest manual initialization (for example " +
                        "RuntimeInitializeLoadType.SubsystemRegistration), or use the composition root.");
                }

                var name = module.Name;
                for (var i = 0; i < _modules.Count; i++) {
                    if (!string.Equals(_modules[i].Name, name, StringComparison.Ordinal)) continue;
                    if (ReferenceEquals(_modules[i], module)) return;
                    throw new InvalidOperationException($"[Modules] Duplicate module name: {name}");
                }

                _modules.Add(module);
            }
        }

        /// <summary>Boots all modules in dependency order; idempotent.</summary>
        public void Bootstrap() {
            lock (_gate) {
                if (_ready || _bootstrapping) return;

                _bootstrapping = true;
                try {
                    ClearFailures();

                    _context.Phase = ModulePhase.Register;
                    var order = BuildStartupOrder();

                    _initializeFailed.Clear();
                    for (var i = 0; i < order.Count; i++) InvokeRegister(order[i]);

                    DriveManaged(_context.DriveManagedInitialize());

                    _context.Phase = ModulePhase.Initialize;
                    for (var i = 0; i < order.Count; i++) {
                        if (!InvokeInitialize(order[i])) _initializeFailed.Add(order[i]);
                    }

                    _context.Phase = ModulePhase.Start;
                    for (var i = 0; i < order.Count; i++) {
                        if (_initializeFailed.Contains(order[i])) continue;
                        InvokeStart(order[i]);
                    }

                    _started.Clear();
                    _started.AddRange(order);

                    DriveManaged(_context.DriveManagedStart());

                    _context.Phase = ModulePhase.Running;
                    _ready = true;
                } finally {
                    _bootstrapping = false;
                }
            }
        }

        /// <summary>Stops and disposes started modules in reverse order; idempotent.</summary>
        public void Shutdown() {
            lock (_gate) {
                if (_shuttingDown) return;
                if (!_ready && _context.Phase == ModulePhase.None) return;

                _shuttingDown = true;
                try {
                    ClearFailures();

                    _context.Phase = ModulePhase.Stop;
                    for (var i = _started.Count - 1; i >= 0; i--) InvokeStop(_started[i]);

                    _context.Phase = ModulePhase.Dispose;
                    for (var i = _started.Count - 1; i >= 0; i--) InvokeDispose(_started[i]);

                    DriveManaged(_context.DriveManagedDeinitialize());

                    _started.Clear();
                    _context.ResetForNextBootstrap();

                    var scope = _context.TakeLifecycleScope();
                    if (scope != null) {
                        try {
                            scope.Dispose();
                        } catch (Exception exception) {
                            GrowLog.Error(
                                $"[Modules] Disposing the module LifecycleScope threw: {exception}",
                                LogCategory);
                        }
                    }

                    _context.Phase = ModulePhase.None;
                    _ready = false;
                } finally {
                    _shuttingDown = false;
                }
            }
        }

        /// <summary>Resolves a service; returns <c>null</c> when it is not registered.</summary>
        public T GetService<T>() where T : class {
            return _context.GetService<T>();
        }

        /// <summary>Resolves a service; throws when it is not registered.</summary>
        public T GetRequiredService<T>() where T : class {
            return _context.GetRequiredService<T>();
        }

        private void ClearFailures() {
            _failed.Clear();
            _failedSet.Clear();
            _failures.Clear();
        }

        private void RecordFailure(string name, ModuleFailureKind kind, string reason) {
            if (string.IsNullOrEmpty(name)) return;
            if (_failedSet.Add(name)) _failed.Add(name);
            _failures.Add(new ModuleFailure(name, kind, _context.Phase, reason));
        }

        private void DriveManaged(IReadOnlyList<object> failedInstances) {
            if (failedInstances == null) return;

            for (var i = 0; i < failedInstances.Count; i++) {
                RecordFailure(
                    failedInstances[i].GetType().Name,
                    ModuleFailureKind.ManagedServiceHookException,
                    "Managed-service hook threw and was isolated; the service stays unready.");
            }
        }

        private List<IModule> BuildStartupOrder() {
            var count = _modules.Count;
            var result = new List<IModule>(count);
            if (count == 0) return result;

            var deps = new List<int>[count];
            var invalid = new bool[count];

            for (var i = 0; i < count; i++) {
                deps[i] = new List<int>();
                var declared = _modules[i].Dependencies;
                if (declared == null) continue;

                for (var d = 0; d < declared.Count; d++) {
                    var dependencyType = declared[d];
                    if (dependencyType == null) continue;

                    var found = -1;
                    List<string> matches = null;
                    for (var j = 0; j < count; j++) {
                        if (j == i) continue;
                        if (!dependencyType.IsInstanceOfType(_modules[j])) continue;

                        if (found < 0) found = j;
                        if (matches == null) matches = new List<string>(2);
                        matches.Add(_modules[j].Name);
                    }

                    var matchCount = matches == null ? 0 : matches.Count;

                    if (matchCount == 0) {
                        invalid[i] = true;
                        GrowLog.Error(
                            $"[Modules] Module {_modules[i].Name} depends on {dependencyType.Name}, " +
                            "which is not registered; the module is skipped.",
                            LogCategory);
                        RecordFailure(
                            _modules[i].Name,
                            ModuleFailureKind.MissingDependency,
                            $"Dependency {dependencyType.Name} is not registered.");
                    } else if (matchCount > 1) {
                        invalid[i] = true;
                        GrowLog.Error(
                            $"[Modules] Module {_modules[i].Name} dependency " +
                            $"{dependencyType.FullName} matches multiple modules: " +
                            $"{string.Join(", ", matches)}; declare a concrete module type.",
                            LogCategory);
                        RecordFailure(
                            _modules[i].Name,
                            ModuleFailureKind.AmbiguousDependency,
                            $"Dependency {dependencyType.FullName} matches multiple modules: " +
                            $"{string.Join(", ", matches)}.");
                    } else if (!deps[i].Contains(found)) {
                        deps[i].Add(found);
                    }
                }
            }

            DetectCycles(deps, invalid);

            bool changed;
            do {
                changed = false;
                for (var i = 0; i < count; i++) {
                    if (invalid[i]) continue;

                    for (var k = 0; k < deps[i].Count; k++) {
                        if (!invalid[deps[i][k]]) continue;

                        invalid[i] = true;
                        GrowLog.Error(
                            $"[Modules] Module {_modules[i].Name} depends on a module that is not " +
                            "ready; the module is skipped.",
                            LogCategory);
                        RecordFailure(
                            _modules[i].Name,
                            ModuleFailureKind.DependencyNotReady,
                            "A dependency failed or is in a cycle, so this module was skipped.");
                        changed = true;
                        break;
                    }
                }
            } while (changed);

            var indegree = new int[count];
            for (var i = 0; i < count; i++) {
                if (invalid[i]) continue;
                for (var k = 0; k < deps[i].Count; k++) {
                    if (!invalid[deps[i][k]]) indegree[i]++;
                }
            }

            var placed = new bool[count];
            while (true) {
                var best = -1;
                for (var i = 0; i < count; i++) {
                    if (invalid[i] || placed[i] || indegree[i] != 0) continue;
                    if (best < 0) {
                        best = i;
                        continue;
                    }

                    if (_modules[i].Priority > _modules[best].Priority) best = i;
                }

                if (best < 0) break;

                placed[best] = true;
                result.Add(_modules[best]);

                for (var i = 0; i < count; i++) {
                    if (invalid[i] || placed[i]) continue;
                    if (deps[i].Contains(best)) indegree[i]--;
                }
            }

            return result;
        }

        private void DetectCycles(List<int>[] deps, bool[] invalid) {
            var count = deps.Length;
            var state = new int[count];
            var cyclic = new bool[count];
            var stack = new List<int>(count);

            for (var i = 0; i < count; i++) {
                if (invalid[i]) continue;
                DetectCyclesVisit(i, deps, state, cyclic, stack);
            }

            for (var i = 0; i < count; i++) {
                if (!cyclic[i] || invalid[i]) continue;

                invalid[i] = true;
                GrowLog.Error(
                    $"[Modules] Module {_modules[i].Name} is in a dependency cycle and was skipped.",
                    LogCategory);
                RecordFailure(
                    _modules[i].Name,
                    ModuleFailureKind.DependencyCycle,
                    "The module is in a dependency cycle, so topological order cannot be built.");
            }
        }

        private static void DetectCyclesVisit(
            int node,
            List<int>[] deps,
            int[] state,
            bool[] cyclic,
            List<int> stack) {
            state[node] = 1;
            stack.Add(node);

            var neighbors = deps[node];
            for (var k = 0; k < neighbors.Count; k++) {
                var next = neighbors[k];
                if (state[next] == 0) {
                    DetectCyclesVisit(next, deps, state, cyclic, stack);
                } else if (state[next] == 1) {
                    var start = stack.IndexOf(next);
                    for (var s = start; s < stack.Count; s++) cyclic[stack[s]] = true;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
        }

        private void InvokeRegister(IModule module) {
            try {
                module.OnRegister(_context);
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[Modules] Module {module.Name}.OnRegister threw and was isolated: {exception}",
                    LogCategory);
                RecordFailure(module.Name, ModuleFailureKind.HookException,
                    "OnRegister threw and was isolated.");
            }
        }

        private bool InvokeInitialize(IModule module) {
            try {
                module.OnInitialize(_context);
                return true;
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[Modules] Module {module.Name}.OnInitialize threw and was isolated: {exception}",
                    LogCategory);
                RecordFailure(module.Name, ModuleFailureKind.HookException,
                    "OnInitialize threw and was isolated.");
                return false;
            }
        }

        private void InvokeStart(IModule module) {
            try {
                module.OnStart(_context);
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[Modules] Module {module.Name}.OnStart threw and was isolated: {exception}",
                    LogCategory);
                RecordFailure(module.Name, ModuleFailureKind.HookException,
                    "OnStart threw and was isolated.");
            }
        }

        private void InvokeStop(IModule module) {
            try {
                module.OnStop();
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[Modules] Module {module.Name}.OnStop threw and was isolated: {exception}",
                    LogCategory);
                RecordFailure(module.Name, ModuleFailureKind.HookException,
                    "OnStop threw and was isolated.");
            }
        }

        private void InvokeDispose(IModule module) {
            try {
                module.OnDispose();
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[Modules] Module {module.Name}.OnDispose threw and was isolated: {exception}",
                    LogCategory);
                RecordFailure(module.Name, ModuleFailureKind.HookException,
                    "OnDispose threw and was isolated.");
            }
        }
    }
}
