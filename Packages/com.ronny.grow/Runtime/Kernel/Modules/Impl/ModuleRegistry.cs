// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Grow.Core.Log;
using Grow.Kernel.Ioc;
using Grow.Kernel.Ioc.Interface;
using UnityEngine;

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
        private readonly List<IModule> _startupOrder = new List<IModule>();

        private readonly List<string> _failed = new List<string>();
        private readonly HashSet<string> _failedSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ModuleFailure> _failures = new List<ModuleFailure>();
        private readonly HashSet<IModule> _initializeFailed = new HashSet<IModule>();
        private readonly HashSet<IModule> _yieldWarned = new HashSet<IModule>();

        private IEnumerator _loadRoutine;
        private IModule _loadModule;
        private IBootLoadable _loadable;
        private AsyncOperation _loadWait;
        private int _loadCursor;
        private int _loadTotal;
        private int _loadCompleted;
        private float _loadProgress;
        private bool _loadFailed;
        private string _loadFailureMessage;

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
            get {
                lock (_gate) {
                    return _ready;
                }
            }
        }

        /// <summary>Current phase.</summary>
        public ModulePhase Phase {
            get {
                lock (_gate) {
                    return _context.Phase;
                }
            }
        }

        /// <summary>Failed module names, deduplicated and in discovery order.</summary>
        public IReadOnlyList<string> FailedModules {
            get {
                lock (_gate) {
                    return _failed.ToArray();
                }
            }
        }

        /// <summary>True when the latest bootstrap/shutdown recorded no failure.</summary>
        public bool Healthy {
            get {
                lock (_gate) {
                    return _failed.Count == 0;
                }
            }
        }

        /// <summary>True while an asynchronous load routine or an unvisited loadable module remains.</summary>
        internal bool HasPendingLoad {
            get {
                lock (_gate) {
                    return _loadRoutine != null || _loadWait != null || HasRemainingLoadable();
                }
            }
        }

        /// <summary>True when the latest bootstrap aborted in the load pass.</summary>
        internal bool LoadFailed {
            get {
                lock (_gate) {
                    return _loadFailed;
                }
            }
        }

        /// <summary>Message of the first load failure, or <c>null</c> when the boot is healthy.</summary>
        internal string LoadFailureMessage {
            get {
                lock (_gate) {
                    return _loadFailureMessage;
                }
            }
        }

        /// <summary>Overall load progress, 1 when there is nothing to load.</summary>
        internal float LoadProgress {
            get {
                lock (_gate) {
                    return _loadTotal <= 0 ? 1f : _loadProgress;
                }
            }
        }

        /// <summary>Name of the module currently loading, or <c>null</c> between loads.</summary>
        internal string LoadingModuleName {
            get {
                lock (_gate) {
                    return _loadModule?.Name;
                }
            }
        }

        /// <summary>Structured failures from the latest bootstrap/shutdown.</summary>
        public IReadOnlyList<ModuleFailure> Failures {
            get {
                lock (_gate) {
                    return _failures.ToArray();
                }
            }
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

                for (var i = 0; i < _failures.Count; i++) {
                    builder.Append("\n  - ").Append(_failures[i]);
                }

                return builder.ToString();
            }
        }

        /// <summary>Registers a module; duplicate names and late registration fail fast.</summary>
        public void Register(IModule module) {
            if (module == null) {
                throw new ArgumentNullException(nameof(module));
            }

            lock (_gate) {
                if (_context.Phase != ModulePhase.None) {
                    throw new InvalidOperationException(
                        "[Modules] Modules must be registered before GrowModules.Bootstrap(). Register " +
                        "them from the earliest manual initialization (for example " +
                        "RuntimeInitializeLoadType.SubsystemRegistration), or use the composition root.");
                }

                var name = module.Name;
                for (var i = 0; i < _modules.Count; i++) {
                    if (!string.Equals(_modules[i].Name, name, StringComparison.Ordinal)) {
                        continue;
                    }

                    if (ReferenceEquals(_modules[i], module)) {
                        return;
                    }

                    throw new InvalidOperationException($"[Modules] Duplicate module name: {name}");
                }

                _modules.Add(module);
            }
        }

        /// <summary>
        /// Boots all modules in dependency order; idempotent.
        /// </summary>
        /// <remarks>
        /// When no module implements <see cref="IBootLoadable"/> — or every load finishes without
        /// yielding — the call runs the whole sequence and ends in <see cref="ModulePhase.Running"/>.
        /// Otherwise it stops in <see cref="ModulePhase.Load"/> and the composition root drives
        /// <see cref="AdvanceLoad"/> and <see cref="FinishStart"/>.
        /// </remarks>
        public void Bootstrap() {
            lock (_gate) {
                if (_ready || _bootstrapping) {
                    return;
                }

                _bootstrapping = true;
                try {
                    ClearFailures();
                    ResetLoadState();

                    _context.Phase = ModulePhase.Register;
                    var order = BuildStartupOrder();
                    _startupOrder.Clear();
                    _startupOrder.AddRange(order);

                    _initializeFailed.Clear();
                    for (var i = 0; i < order.Count; i++) {
                        InvokeRegister(order[i]);
                    }

                    DriveManaged(_context.DriveManagedInitialize());

                    _context.Phase = ModulePhase.Initialize;
                    for (var i = 0; i < order.Count; i++) {
                        if (!InvokeInitialize(order[i])) {
                            _initializeFailed.Add(order[i]);
                        }
                    }

                    CountLoadableModules();
                    if (AdvanceLoadCore() == LoadStepResult.Completed) {
                        FinishStartCore();
                    }
                } finally {
                    _bootstrapping = false;
                }
            }
        }

        /// <summary>
        /// Advances the asynchronous load pass by one step; the composition root calls it per frame.
        /// </summary>
        internal LoadStepResult AdvanceLoad() {
            lock (_gate) {
                return AdvanceLoadCore();
            }
        }

        /// <summary>Runs the start pass after every load finished; idempotent.</summary>
        internal void FinishStart() {
            lock (_gate) {
                if (_ready || _context.Phase != ModulePhase.Load) {
                    return;
                }

                FinishStartCore();
            }
        }

        /// <summary>Drops the active load routine without running any remaining module.</summary>
        internal void AbortLoad() {
            lock (_gate) {
                ClearCurrentLoad();
                _loadCursor = _startupOrder.Count;
            }
        }

        /// <summary>Stops and disposes started modules in reverse order; idempotent.</summary>
        public void Shutdown() {
            lock (_gate) {
                if (_shuttingDown) {
                    return;
                }

                if (!_ready && _context.Phase == ModulePhase.None) {
                    return;
                }

                _shuttingDown = true;
                try {
                    ClearFailures();
                    ClearCurrentLoad();

                    _context.Phase = ModulePhase.Stop;
                    for (var i = _started.Count - 1; i >= 0; i--) {
                        InvokeStop(_started[i]);
                    }

                    _context.Phase = ModulePhase.Dispose;
                    for (var i = _started.Count - 1; i >= 0; i--) {
                        InvokeDispose(_started[i]);
                    }

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
                    ResetLoadState();
                    _startupOrder.Clear();
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

        private void CountLoadableModules() {
            _loadTotal = 0;
            for (var i = 0; i < _startupOrder.Count; i++) {
                var module = _startupOrder[i];
                if (!_initializeFailed.Contains(module) && module is IBootLoadable) {
                    _loadTotal++;
                }
            }
        }

        private bool HasRemainingLoadable() {
            for (var i = _loadCursor; i < _startupOrder.Count; i++) {
                var module = _startupOrder[i];
                if (!_initializeFailed.Contains(module) && module is IBootLoadable) {
                    return true;
                }
            }

            return false;
        }

        private LoadStepResult AdvanceLoadCore() {
            while (true) {
                if (_loadWait != null) {
                    if (!_loadWait.isDone) {
                        UpdateLoadProgress();
                        return LoadStepResult.Pending;
                    }

                    _loadWait = null;
                }

                if (_loadRoutine == null) {
                    var started = StartNextLoad();
                    if (started != LoadStepResult.Pending) {
                        return started;
                    }
                }

                bool moved;
                try {
                    moved = _loadRoutine.MoveNext();
                } catch (Exception exception) {
                    var module = _loadModule;
                    ClearCurrentLoad();
                    return FailLoad(module, exception);
                }

                if (!moved) {
                    ClearCurrentLoad();
                    _loadCompleted++;
                    UpdateLoadProgress();
                    continue;
                }

                var yielded = _loadRoutine.Current;
                if (yielded == null) {
                    UpdateLoadProgress();
                    return LoadStepResult.Pending;
                }

                if (yielded is AsyncOperation operation) {
                    if (!operation.isDone) {
                        _loadWait = operation;
                    }

                    UpdateLoadProgress();
                    return LoadStepResult.Pending;
                }

                WarnUnsupportedYield(_loadModule, yielded);
                UpdateLoadProgress();
                return LoadStepResult.Pending;
            }
        }

        private LoadStepResult StartNextLoad() {
            while (_loadCursor < _startupOrder.Count) {
                var module = _startupOrder[_loadCursor++];
                if (_initializeFailed.Contains(module)) {
                    continue;
                }

                if (!(module is IBootLoadable loadable)) {
                    continue;
                }

                // Only a module that opted into the load phase observes it, so a synchronous graph
                // keeps the Register → Initialize → Start sequence.
                _context.Phase = ModulePhase.Load;

                IEnumerator routine;
                try {
                    routine = loadable.Load(_context);
                } catch (Exception exception) {
                    return FailLoad(module, exception);
                }

                if (routine == null) {
                    _loadCompleted++;
                    UpdateLoadProgress();
                    continue;
                }

                _loadRoutine = routine;
                _loadModule = module;
                _loadable = loadable;
                return LoadStepResult.Pending;
            }

            return LoadStepResult.Completed;
        }

        private void FinishStartCore() {
            _context.Phase = ModulePhase.Start;
            for (var i = 0; i < _startupOrder.Count; i++) {
                if (_initializeFailed.Contains(_startupOrder[i])) {
                    continue;
                }

                InvokeStart(_startupOrder[i]);
            }

            _started.Clear();
            _started.AddRange(_startupOrder);

            DriveManaged(_context.DriveManagedStart());

            _context.Phase = ModulePhase.Running;
            _ready = true;
        }

        private LoadStepResult FailLoad(IModule module, Exception exception) {
            var name = module != null ? module.Name : "(unknown)";
            GrowLog.Error(
                $"[Modules] Module {name}.Load threw and aborted the boot: {exception}",
                LogCategory);
            RecordFailure(name, ModuleFailureKind.LoadException,
                "Load threw; the boot fails and the composition rolls back.");

            if (!_loadFailed) {
                _loadFailed = true;
                _loadFailureMessage = $"Module {name} failed to load: {exception.Message}";
            }

            ClearCurrentLoad();
            _loadCursor = _startupOrder.Count;
            return LoadStepResult.Faulted;
        }

        private void ClearCurrentLoad() {
            _loadRoutine = null;
            _loadModule = null;
            _loadable = null;
            _loadWait = null;
        }

        private void ResetLoadState() {
            ClearCurrentLoad();
            _loadCursor = 0;
            _loadTotal = 0;
            _loadCompleted = 0;
            _loadProgress = 0f;
            _loadFailed = false;
            _loadFailureMessage = null;
            _yieldWarned.Clear();
        }

        private void UpdateLoadProgress() {
            var current = 0f;
            if (_loadable != null) {
                try {
                    current = Math.Clamp(_loadable.LoadProgress, 0f, 1f);
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"[Modules] Module {_loadModule?.Name}.LoadProgress threw: {exception}",
                        LogCategory);
                    _loadable = null;
                }
            }

            var value = _loadTotal <= 0
                ? 1f
                : Math.Clamp((_loadCompleted + current) / _loadTotal, 0f, 1f);
            if (value > _loadProgress) {
                _loadProgress = value;
            }
        }

        private void WarnUnsupportedYield(IModule module, object value) {
#if UNITY_EDITOR
            if (module == null || !_yieldWarned.Add(module)) {
                return;
            }

            GrowLog.Warning(
                $"[Modules] Module {module.Name}.Load yielded a {value.GetType().Name}; only null " +
                "and AsyncOperation are supported, so it is treated as one frame.",
                LogCategory);
#endif
        }

        private void ClearFailures() {
            _failed.Clear();
            _failedSet.Clear();
            _failures.Clear();
        }

        private void RecordFailure(string name, ModuleFailureKind kind, string reason) {
            if (string.IsNullOrEmpty(name)) {
                return;
            }

            if (_failedSet.Add(name)) {
                _failed.Add(name);
            }

            _failures.Add(new ModuleFailure(name, kind, _context.Phase, reason));
        }

        private void DriveManaged(IReadOnlyList<object> failedInstances) {
            if (failedInstances == null) {
                return;
            }

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
            if (count == 0) {
                return result;
            }

            var deps = new List<int>[count];
            var invalid = new bool[count];

            for (var i = 0; i < count; i++) {
                deps[i] = new List<int>();
                var declared = _modules[i].Dependencies;
                if (declared == null) {
                    continue;
                }

                for (var d = 0; d < declared.Count; d++) {
                    var dependencyType = declared[d];
                    if (dependencyType == null) {
                        continue;
                    }

                    var found = -1;
                    List<string> matches = null;
                    for (var j = 0; j < count; j++) {
                        if (j == i) {
                            continue;
                        }

                        if (!dependencyType.IsInstanceOfType(_modules[j])) {
                            continue;
                        }

                        if (found < 0) {
                            found = j;
                        }

                        if (matches == null) {
                            matches = new List<string>(2);
                        }

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
                    if (invalid[i]) {
                        continue;
                    }

                    for (var k = 0; k < deps[i].Count; k++) {
                        if (!invalid[deps[i][k]]) {
                            continue;
                        }

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
                if (invalid[i]) {
                    continue;
                }

                for (var k = 0; k < deps[i].Count; k++) {
                    if (!invalid[deps[i][k]]) {
                        indegree[i]++;
                    }
                }
            }

            var placed = new bool[count];
            while (true) {
                var best = -1;
                for (var i = 0; i < count; i++) {
                    if (invalid[i] || placed[i] || indegree[i] != 0) {
                        continue;
                    }

                    if (best < 0) {
                        best = i;
                        continue;
                    }

                    if (_modules[i].Priority > _modules[best].Priority) {
                        best = i;
                    }
                }

                if (best < 0) {
                    break;
                }

                placed[best] = true;
                result.Add(_modules[best]);

                for (var i = 0; i < count; i++) {
                    if (invalid[i] || placed[i]) {
                        continue;
                    }

                    if (deps[i].Contains(best)) {
                        indegree[i]--;
                    }
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
                if (invalid[i]) {
                    continue;
                }

                DetectCyclesVisit(i, deps, state, cyclic, stack);
            }

            for (var i = 0; i < count; i++) {
                if (!cyclic[i] || invalid[i]) {
                    continue;
                }

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
                    for (var s = start; s < stack.Count; s++) {
                        cyclic[stack[s]] = true;
                    }
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
