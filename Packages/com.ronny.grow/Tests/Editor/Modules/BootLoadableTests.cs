// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Modules;
using Grow.Kernel.Modules.Impl;
using Grow.Tests.Editor.Lifecycle;
using NUnit.Framework;

namespace Grow.Tests.Editor.Modules {
    public class BootLoadableTests {
        private CapturingSink _sink;

        [SetUp]
        public void SetUp() {
            _sink = new CapturingSink();
            GrowLog.SetSink(_sink);
            GrowLog.MinimumLevel = LogLevel.Trace;
        }

        [TearDown]
        public void TearDown() {
            GrowLog.Reset();
        }

        [Test]
        public void Bootstrap_SynchronousLoad_ReachesRunning() {
            var module = new LoadProbeModule("Sync", _ => null);
            var registry = new ModuleRegistry();
            registry.Register(module);

            registry.Bootstrap();

            Assert.AreEqual(ModulePhase.Running, registry.Phase);
            Assert.IsTrue(registry.IsReady);
            Assert.AreEqual(1, module.LoadCount);
            Assert.AreEqual(1, module.StartCount);
            CollectionAssert.AreEqual(
                new[] { ModulePhase.Register, ModulePhase.Initialize, ModulePhase.Load,
                    ModulePhase.Start },
                module.Phases);
        }

        [Test]
        public void Bootstrap_LoadThatCompletesImmediately_ReachesRunning() {
            var module = new LoadProbeModule("Immediate", _ => NoYield());
            var registry = new ModuleRegistry();
            registry.Register(module);

            registry.Bootstrap();

            Assert.AreEqual(ModulePhase.Running, registry.Phase);
            Assert.AreEqual(1, module.StartCount);
            Assert.IsTrue(registry.Healthy);
        }

        [Test]
        public void Bootstrap_YieldingLoad_StopsAtLoad() {
            var module = new LoadProbeModule("Slow", _ => YieldOnce());
            var registry = new ModuleRegistry();
            registry.Register(module);

            registry.Bootstrap();

            Assert.AreEqual(ModulePhase.Load, registry.Phase);
            Assert.IsFalse(registry.IsReady);
            Assert.IsTrue(registry.HasPendingLoad);
            Assert.AreEqual(0, module.StartCount, "OnStart must wait for every load");
            Assert.AreEqual("Slow", registry.LoadingModuleName);
        }

        [Test]
        public void AdvanceLoad_CompletesThenFinishStartStartsModules() {
            var module = new LoadProbeModule("Slow", _ => TwoYieldSteps());
            var registry = new ModuleRegistry();
            registry.Register(module);
            registry.Bootstrap();

            Assert.AreEqual(LoadStepResult.Pending, registry.AdvanceLoad());
            Assert.AreEqual(LoadStepResult.Completed, registry.AdvanceLoad());
            Assert.IsFalse(registry.HasPendingLoad);
            Assert.AreEqual(0, module.StartCount);

            registry.FinishStart();

            Assert.AreEqual(ModulePhase.Running, registry.Phase);
            Assert.IsTrue(registry.IsReady);
            Assert.AreEqual(1, module.StartCount);
            Assert.IsNull(registry.LoadingModuleName);
        }

        [Test]
        public void FinishStart_IsIdempotent() {
            var module = new LoadProbeModule("Slow", _ => YieldOnce());
            var registry = new ModuleRegistry();
            registry.Register(module);
            registry.Bootstrap();
            registry.AdvanceLoad();
            registry.AdvanceLoad();

            registry.FinishStart();
            registry.FinishStart();

            Assert.AreEqual(1, module.StartCount);
        }

        [Test]
        public void AdvanceLoad_RunsModulesSeriallyInStartupOrder() {
            var order = new List<string>();
            var first = new LoadProbeModule("A", _ => TwoSteps(order, "A"));
            var second = new LoadProbeModule("B", _ => OneStep(order, "B"));
            var registry = new ModuleRegistry();
            registry.Register(first);
            registry.Register(second);

            registry.Bootstrap();
            Assert.AreEqual("A1", string.Join(",", order));

            while (registry.HasPendingLoad) {
                registry.AdvanceLoad();
            }

            CollectionAssert.AreEqual(new[] { "A1", "A2", "A3", "B1", "B2" }, order);
        }

        [Test]
        public void AdvanceLoad_UnsupportedYield_WaitsOneFrame() {
            var resumed = false;
            var module = new LoadProbeModule(
                "Odd", _ => YieldsAnIntThenResumes(() => resumed = true));
            var registry = new ModuleRegistry();
            registry.Register(module);

            registry.Bootstrap();

            Assert.AreEqual(ModulePhase.Load, registry.Phase);
            Assert.IsFalse(resumed, "the unsupported value must not resume in the same call");

            Assert.AreEqual(LoadStepResult.Pending, registry.AdvanceLoad());
            Assert.IsTrue(resumed);

            Assert.AreEqual(LoadStepResult.Completed, registry.AdvanceLoad());
            Assert.IsTrue(
                _sink.Entries.Exists(entry => entry.Message.Contains("yielded a Int32")),
                "the unsupported value must be reported once");
        }

        [Test]
        public void LoadFailure_AfterAYield_FaultsThePass() {
            var module = new LoadProbeModule("Broken", _ => ThrowsAfterAYield());
            var registry = new ModuleRegistry();
            registry.Register(module);
            registry.Bootstrap();

            Assert.AreEqual(LoadStepResult.Faulted, registry.AdvanceLoad());

            Assert.IsTrue(registry.LoadFailed);
            StringAssert.Contains("Broken", registry.LoadFailureMessage);
            StringAssert.Contains("boom", registry.LoadFailureMessage);
            Assert.IsFalse(registry.HasPendingLoad);
            Assert.AreEqual(0, module.StartCount);
            Assert.AreEqual(ModulePhase.Load, registry.Phase);
            CollectionAssert.Contains(registry.FailedModules, "Broken");
            Assert.AreEqual(ModuleFailureKind.LoadException, registry.Failures[0].Kind);
        }

        [Test]
        public void LoadThrow_BeforeReturningARoutine_FaultsThePass() {
            var module = new LoadProbeModule(
                "Broken", _ => throw new InvalidOperationException("immediate boom"));
            var registry = new ModuleRegistry();
            registry.Register(module);

            registry.Bootstrap();

            Assert.IsTrue(registry.LoadFailed);
            StringAssert.Contains("immediate boom", registry.LoadFailureMessage);
            Assert.IsFalse(registry.IsReady);
            Assert.AreEqual(0, module.StartCount);
        }

        [Test]
        public void InitializeFailure_SkipsTheLoadPass() {
            var module = new LoadProbeModule(
                "Skip", _ => YieldOnce(), _ => throw new InvalidOperationException("init boom"));
            var registry = new ModuleRegistry();
            registry.Register(module);

            registry.Bootstrap();

            Assert.AreEqual(0, module.LoadCount, "a module that failed to initialize must not load");
            Assert.AreEqual(ModulePhase.Running, registry.Phase);
            CollectionAssert.Contains(registry.FailedModules, "Skip");
        }

        [Test]
        public void Shutdown_DuringLoad_DoesNotStartModules() {
            var module = new LoadProbeModule("Slow", _ => YieldOnce());
            var registry = new ModuleRegistry();
            registry.Register(module);
            registry.Bootstrap();

            registry.Shutdown();

            Assert.AreEqual(ModulePhase.None, registry.Phase);
            Assert.IsFalse(registry.IsReady);
            Assert.IsFalse(registry.HasPendingLoad);
            Assert.AreEqual(0, module.StartCount);
        }

        [Test]
        public void HasPendingLoad_TracksThePass() {
            var module = new LoadProbeModule("Slow", _ => YieldOnce());
            var registry = new ModuleRegistry();
            registry.Register(module);

            Assert.IsFalse(registry.HasPendingLoad, "nothing is pending before bootstrap");

            registry.Bootstrap();
            Assert.IsTrue(registry.HasPendingLoad);

            registry.AdvanceLoad();
            Assert.AreEqual(LoadStepResult.Completed, registry.AdvanceLoad());
            Assert.IsFalse(registry.HasPendingLoad);
        }

        [Test]
        public void LoadProgress_IsOneWhenNothingLoads() {
            var registry = new ModuleRegistry();
            registry.Register(new LoadProbeModule("Sync", _ => null));
            registry.Bootstrap();

            Assert.AreEqual(1f, registry.LoadProgress);
        }

        [Test]
        public void LoadProgress_IsMonotonicAndClamped() {
            var first = new LoadProbeModule("A", _ => TwoYieldSteps()) { LoadProgress = 5f };
            var second = new LoadProbeModule("B", _ => YieldOnce());
            var registry = new ModuleRegistry();
            registry.Register(first);
            registry.Register(second);

            registry.Bootstrap();
            Assert.AreEqual(0.5f, registry.LoadProgress,
                "A is clamped to 1 but only one of the two modules is loading");

            first.LoadProgress = 0f;
            registry.AdvanceLoad();
            Assert.AreEqual(0.5f, registry.LoadProgress, "progress must not regress");

            Assert.AreEqual(LoadStepResult.Pending, registry.AdvanceLoad());
            Assert.AreEqual(LoadStepResult.Completed, registry.AdvanceLoad());
            Assert.AreEqual(1f, registry.LoadProgress);
        }

        [Test]
        public void LoadProgress_NegativeValueIsClampedToZero() {
            var first = new LoadProbeModule("A", _ => TwoYieldSteps()) { LoadProgress = -5f };
            var second = new LoadProbeModule("B", _ => YieldOnce());
            var registry = new ModuleRegistry();
            registry.Register(first);
            registry.Register(second);

            registry.Bootstrap();

            Assert.AreEqual(0f, registry.LoadProgress);
        }

        [Test]
        public void Bootstrap_NullLoad_StillCountsTheModuleAsLoaded() {
            var registry = new ModuleRegistry();
            var module = new LoadProbeModule("Null", _ => null);
            registry.Register(module);

            registry.Bootstrap();

            Assert.AreEqual(1, module.LoadCount);
            Assert.AreEqual(1f, registry.LoadProgress);
            Assert.AreEqual(ModulePhase.Running, registry.Phase);
        }

        private static IEnumerator NoYield() {
            yield break;
        }

        private static IEnumerator YieldOnce() {
            yield return null;
        }

        private static IEnumerator TwoYieldSteps() {
            yield return null;
            yield return null;
        }

        private static IEnumerator YieldsAnIntThenResumes(Action resumed) {
            yield return 42;
            resumed();
            yield return null;
        }

        private static IEnumerator ThrowsAfterAYield() {
            yield return null;
            throw new InvalidOperationException("boom");
        }

        private static IEnumerator OneStep(List<string> order, string name) {
            order.Add(name + "1");
            yield return null;
            order.Add(name + "2");
        }

        private static IEnumerator TwoSteps(List<string> order, string name) {
            order.Add(name + "1");
            yield return null;
            order.Add(name + "2");
            yield return null;
            order.Add(name + "3");
        }

        private sealed class LoadProbeModule : IModule, IBootLoadable {
            private readonly Func<IModuleContext, IEnumerator> _load;
            private readonly Action<IModuleContext> _initialize;

            internal LoadProbeModule(
                string name,
                Func<IModuleContext, IEnumerator> load,
                Action<IModuleContext> initialize = null) {
                Name = name;
                _load = load;
                _initialize = initialize;
            }

            public string Name { get; }

            public int Priority => 0;

            public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

            public List<ModulePhase> Phases { get; } = new List<ModulePhase>();

            public float LoadProgress { get; set; }

            public int LoadCount { get; private set; }

            public int StartCount { get; private set; }

            public void OnRegister(IModuleContext context) {
                Phases.Add(context.Phase);
            }

            public void OnInitialize(IModuleContext context) {
                Phases.Add(context.Phase);
                _initialize?.Invoke(context);
            }

            public void OnStart(IModuleContext context) {
                Phases.Add(context.Phase);
                StartCount++;
            }

            public void OnStop() {
            }

            public void OnDispose() {
            }

            public IEnumerator Load(IModuleContext context) {
                Phases.Add(context.Phase);
                LoadCount++;
                return _load(context);
            }
        }

        private sealed class CapturingSink : ILogSink {
            internal List<LogEntry> Entries { get; } = new();

            public void Write(in LogEntry entry) {
                Entries.Add(entry);
            }
        }
    }
}
