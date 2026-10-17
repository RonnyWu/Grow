// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Ioc;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Lifecycle.Impl;
using Grow.Kernel.Modules;
using NUnit.Framework;
namespace Grow.Tests.Editor.Modules {
    internal sealed class OrderLog {
        private readonly List<string> _calls = new List<string>();

        public string Joined => string.Join(",", _calls);

        public void Add(string call) => _calls.Add(call);

        public void Clear() => _calls.Clear();

        public string Phase(string hook) {
            var prefix = hook + ":";
            var matched = new List<string>();
            for (var i = 0; i < _calls.Count; i++) {
                if (_calls[i].StartsWith(prefix, StringComparison.Ordinal)) {
                    matched.Add(_calls[i].Substring(prefix.Length));
                }
            }

            return string.Join(",", matched);
        }

        public int Count(string call) {
            var total = 0;
            for (var i = 0; i < _calls.Count; i++) {
                if (string.Equals(_calls[i], call, StringComparison.Ordinal)) total++;
            }

            return total;
        }

        public int IndexOf(string call) {
            for (var i = 0; i < _calls.Count; i++) {
                if (string.Equals(_calls[i], call, StringComparison.Ordinal)) return i;
            }

            return -1;
        }
    }

    internal abstract class ProbeModule : IModule {
        private readonly Type[] _dependencies;
        private readonly int _priority;

        protected readonly OrderLog Log;
        protected readonly string ModuleName;

        protected ProbeModule(
            OrderLog log,
            string name,
            int priority,
            Type[] dependencies,
            bool throwOnRegister = false,
            bool throwOnStart = false) {
            Log = log;
            ModuleName = name;
            _priority = priority;
            _dependencies = dependencies ?? Array.Empty<Type>();
            ThrowOnRegister = throwOnRegister;
            ThrowOnStart = throwOnStart;
        }

        public bool ThrowOnRegister { get; }
        public bool ThrowOnStart { get; }

        public List<ModulePhase> Phases { get; } = new List<ModulePhase>();

        public string Name => ModuleName;
        public int Priority => _priority;
        public IReadOnlyList<Type> Dependencies => _dependencies;

        public virtual void OnRegister(IModuleContext context) {
            Phases.Add(context.Phase);
            Log.Add("Register:" + ModuleName);
            if (ThrowOnRegister) throw new InvalidOperationException("probe register failure");
        }

        public virtual void OnInitialize(IModuleContext context) {
            Phases.Add(context.Phase);
            Log.Add("Initialize:" + ModuleName);
        }

        public virtual void OnStart(IModuleContext context) {
            Phases.Add(context.Phase);
            Log.Add("Start:" + ModuleName);
            if (ThrowOnStart) throw new InvalidOperationException("probe start failure");
        }

        public virtual void OnStop() => Log.Add("Stop:" + ModuleName);
        public virtual void OnDispose() => Log.Add("Dispose:" + ModuleName);
    }

    internal sealed class TopoC : ProbeModule {
        public TopoC(OrderLog log) : base(log, "C", 0, null) { }
    }

    internal sealed class TopoCDuplicate : ProbeModule {
        public TopoCDuplicate(OrderLog log) : base(log, "C", 0, null) { }
    }

    internal sealed class TopoB : ProbeModule {
        public TopoB(OrderLog log) : base(log, "B", 0, new[] { typeof(TopoC) }) { }
    }

    internal sealed class TopoA : ProbeModule {
        public TopoA(OrderLog log) : base(log, "A", 0, new[] { typeof(TopoB) }) { }
    }

    internal sealed class PrioHigh : ProbeModule {
        public PrioHigh(OrderLog log) : base(log, "High", 10, null) { }
    }

    internal sealed class PrioMid : ProbeModule {
        public PrioMid(OrderLog log) : base(log, "Mid", 5, null) { }
    }

    internal sealed class PrioLow : ProbeModule {
        public PrioLow(OrderLog log) : base(log, "Low", 5, null) { }
    }

    internal sealed class NeverRegisteredDependency {
    }

    internal sealed class DepMissing : ProbeModule {
        public DepMissing(OrderLog log) : base(log, "Missing", 0, new[] { typeof(NeverRegisteredDependency) }) { }
    }

    internal sealed class DepMissingOther : ProbeModule {
        public DepMissingOther(OrderLog log) : base(log, "Other", 0, null) { }
    }

    internal sealed class CycleX : ProbeModule {
        public CycleX(OrderLog log) : base(log, "X", 0, new[] { typeof(CycleY) }) { }
    }

    internal sealed class CycleY : ProbeModule {
        public CycleY(OrderLog log) : base(log, "Y", 0, new[] { typeof(CycleX) }) { }
    }

    internal sealed class CycleFree : ProbeModule {
        public CycleFree(OrderLog log) : base(log, "Free", 0, null) { }
    }

    internal sealed class ThrowOnRegisterModule : ProbeModule {
        public ThrowOnRegisterModule(OrderLog log) : base(log, "ThrowRegister", 0, null, throwOnRegister: true) { }
    }

    internal sealed class ThrowOnStartModule : ProbeModule {
        public ThrowOnStartModule(OrderLog log) : base(log, "ThrowStart", 0, null, throwOnStart: true) { }
    }

    internal sealed class ExceptionSurvivor : ProbeModule {
        public ExceptionSurvivor(OrderLog log) : base(log, "Survivor", 0, null) { }
    }

    internal interface ISharedDependencyMode {
    }

    internal sealed class SharedDependencyA : ProbeModule, ISharedDependencyMode {
        public SharedDependencyA(OrderLog log) : base(log, "SharedA", 0, null) { }
    }

    internal sealed class SharedDependencyB : ProbeModule, ISharedDependencyMode {
        public SharedDependencyB(OrderLog log) : base(log, "SharedB", 0, null) { }
    }

    internal sealed class NeedsSharedDependency : ProbeModule {
        public NeedsSharedDependency(OrderLog log) : base(log, "NeedsShared", 0,
            new[] { typeof(ISharedDependencyMode) }) { }
    }

    internal sealed class ResolveService {
        public string Value;
    }

    internal sealed class LazyService {
    }

    internal sealed class AbsentService {
    }

    internal sealed class LazyFactoryCounter {
        public int Count;
    }

    internal sealed class EagerServiceModule : ProbeModule {
        private readonly ResolveService _service;

        public EagerServiceModule(OrderLog log, ResolveService service)
            : base(log, "EagerService", 0, null) {
            _service = service;
        }

        public override void OnRegister(IModuleContext context) {
            base.OnRegister(context);
            context.Register(_service);
        }
    }

    internal sealed class LazyServiceModule : ProbeModule {
        private readonly LazyFactoryCounter _counter;

        public LazyServiceModule(OrderLog log, LazyFactoryCounter counter)
            : base(log, "LazyService", 0, null) {
            _counter = counter;
        }

        public override void OnRegister(IModuleContext context) {
            base.OnRegister(context);
            context.RegisterLazy<LazyService>(() => {
                _counter.Count++;
                return new LazyService();
            });
        }
    }

    internal class RecordingManagedService : IManagedService {
        private readonly OrderLog _log;
        private readonly string _name;

        public RecordingManagedService(OrderLog log, string name) {
            _log = log;
            _name = name;
        }

        public bool ThrowOnInitialize { get; set; }
        public bool ThrowOnStart { get; set; }
        public int InitializeCount { get; private set; }
        public int StartCount { get; private set; }
        public int DeinitializeCount { get; private set; }

        public void OnInitialize() {
            InitializeCount++;
            _log.Add("SvcInit:" + _name);
            if (ThrowOnInitialize) throw new InvalidOperationException("managed service initialize failure");
        }

        public void OnStart() {
            StartCount++;
            _log.Add("SvcStart:" + _name);
            if (ThrowOnStart) throw new InvalidOperationException("managed service start failure");
        }

        public void OnDeinitialize() {
            DeinitializeCount++;
            _log.Add("SvcDeinit:" + _name);
        }
    }

    internal sealed class RecordingManagedServiceB : RecordingManagedService {
        public RecordingManagedServiceB(OrderLog log, string name) : base(log, name) {
        }
    }

    internal sealed class MixedManagedServiceModule : ProbeModule {
        public RecordingManagedService First;
        public RecordingManagedServiceB Second;

        public MixedManagedServiceModule(OrderLog log) : base(log, "Managed", 0, null) { }

        public override void OnRegister(IModuleContext context) {
            base.OnRegister(context);
            if (First != null) context.Register(First);
            if (Second != null) context.Register(Second);
        }
    }

    internal sealed class ManagedServiceModule : ProbeModule {
        public readonly List<RecordingManagedService> Services = new List<RecordingManagedService>();

        public ManagedServiceModule(OrderLog log) : base(log, "Managed", 0, null) { }

        public override void OnRegister(IModuleContext context) {
            base.OnRegister(context);
            for (var i = 0; i < Services.Count; i++) context.Register(Services[i]);
        }
    }

    internal sealed class RegisterDuringStartService : IManagedService {
        private readonly IModuleContext _context;
        public readonly RecordingManagedService Late;

        public int InitializeCount;
        public int StartCount;
        public int DeinitializeCount;

        public RegisterDuringStartService(IModuleContext context, RecordingManagedService late) {
            _context = context;
            Late = late;
        }

        public void OnInitialize() => InitializeCount++;

        public void OnStart() {
            StartCount++;
            if (Late != null) _context.Register(Late);
        }

        public void OnDeinitialize() => DeinitializeCount++;
    }

    internal sealed class LateRegistrationModule : ProbeModule {
        private readonly RecordingManagedService _late;

        public RegisterDuringStartService First;

        public LateRegistrationModule(OrderLog log, RecordingManagedService late)
            : base(log, "LateReg", 0, null) {
            _late = late;
        }

        public override void OnRegister(IModuleContext context) {
            base.OnRegister(context);
            First = new RegisterDuringStartService(context, _late);
            context.Register(First);
        }
    }

    internal sealed class PlainScopeTarget : IInitializable, IUpdatable, IDeinitializable {
        public int InitializeCount;
        public int UpdateCount;
        public int DeinitializeCount;

        public void OnInitialize() => InitializeCount++;
        public void OnUpdate() => UpdateCount++;
        public void OnDeinitialize() => DeinitializeCount++;
    }

    internal sealed class ScopeModule : ProbeModule {
        public readonly PlainScopeTarget Target = new PlainScopeTarget();
        public LifecycleScope FirstScope;
        public LifecycleScope SecondScope;

        public ScopeModule(OrderLog log) : base(log, "Scope", 0, null) { }

        public override void OnRegister(IModuleContext context) {
            base.OnRegister(context);
            FirstScope = context.Scope;
            SecondScope = context.Scope;
            FirstScope.Attach(Target);
        }
    }

    internal sealed class ModulesNullLogSink : ILogSink {
        public void Write(in LogEntry entry) { }
    }

    public abstract class ModulesTestBase {
        [SetUp]
        public void ModulesBaseSetUp() {
            GrowLog.SetSink(new ModulesNullLogSink());
            MonoManager.ResetStaticState();
            GrowModules.ResetStaticState();
            IOC.Clear();
        }

        [TearDown]
        public void ModulesBaseTearDown() {
            GrowModules.Shutdown();
            GrowModules.ResetStaticState();
            MonoManager.ResetStaticState();
            IOC.Clear();
            GrowLog.Reset();
        }
    }
}
