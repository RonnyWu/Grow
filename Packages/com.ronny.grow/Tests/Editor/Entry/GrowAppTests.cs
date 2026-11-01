// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Entry;
using Grow.Kernel.Ioc;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Loop;
using Grow.Kernel.Modules;
using Grow.Kernel.Scheduling;
using NUnit.Framework;

namespace Grow.Tests.Editor.Entry {
    [Category("Grow.Integration")]
    public class GrowAppTests {
        private GrowAppProbeModule _module;

        [SetUp]
        public void SetUp() {
            GrowLog.SetSink(new GrowAppNullLogSink());
            GrowApp.Shutdown();
            GrowApp.ResetStaticState();
            GrowLoop.ResetStaticState();
            MonoManager.ResetStaticState();
            GrowScheduler.ResetStaticState();
            GrowModules.ResetStaticState();
            IOC.Clear();
            GrowBoot.Shutdown();
            _module = null;
        }

        [TearDown]
        public void TearDown() {
            GrowApp.Shutdown();
            GrowApp.ResetStaticState();
            GrowLoop.ResetStaticState();
            MonoManager.ResetStaticState();
            GrowScheduler.ResetStaticState();
            GrowModules.ResetStaticState();
            IOC.Clear();
            GrowBoot.Shutdown();
            GrowLog.Reset();
        }

        [Test]
        public void BeforeInitialize_RootAndLoopAreAbsent() {
            Assert.IsFalse(GrowApp.IsInitialized);
            Assert.IsNull(GrowApp.Root);
            Assert.IsFalse(GrowLoop.IsInstalled);
            Assert.IsFalse(MonoManager.Instance.Valid);
            StringAssert.Contains("initialized=False", GrowApp.Describe());
            StringAssert.Contains("loop=not-installed", GrowApp.Describe());
        }

        [Test]
        public void Initialize_WiresBootLoopManagerAndModules() {
            GrowApp.Initialize();

            Assert.IsTrue(GrowApp.IsInitialized);
            Assert.IsNotNull(GrowApp.Root);
            Assert.IsTrue(GrowLoop.IsInstalled);
            Assert.IsTrue(MonoManager.Instance.Valid);
            Assert.IsTrue(GrowModules.IsReady);
            Assert.AreEqual(ModulePhase.Running, GrowModules.Phase);
            Assert.AreEqual(GrowState.Ready, GrowBoot.State);
        }

        [Test]
        public void Shutdown_ThenInitialize_DropsQueuedTimersAndPostedActions() {
            GrowApp.Initialize();
            var fired = 0;
            GrowScheduler.NextFrame(() => fired++);
            GrowScheduler.Post(() => fired++);

            GrowApp.Shutdown();
            GrowApp.Initialize();

            GrowScheduler.Tick(0f, 0f);
            GrowScheduler.FlushPosted();

            Assert.AreEqual(0, fired, "a new cycle must not run the previous cycle's queued work");
        }

        [Test]
        public void Shutdown_UnwindsEverything() {
            GrowApp.Initialize();

            GrowApp.Shutdown();

            Assert.IsFalse(GrowApp.IsInitialized);
            Assert.IsNull(GrowApp.Root);
            Assert.IsFalse(GrowLoop.IsInstalled);
            Assert.IsFalse(MonoManager.Instance.Valid);
            Assert.AreEqual(ModulePhase.None, GrowModules.Phase);
            Assert.AreEqual(GrowState.Shutdown, GrowBoot.State);
        }

        [Test]
        public void Shutdown_ThenInitialize_ReArmsWholeComposition() {
            GrowApp.Initialize();
            var firstRootId = GrowApp.Root.GetInstanceID();

            GrowApp.Shutdown();
            GrowApp.Initialize();

            Assert.IsTrue(GrowApp.IsInitialized);
            Assert.IsNotNull(GrowApp.Root);
            Assert.AreNotEqual(firstRootId, GrowApp.Root.GetInstanceID());
            Assert.IsTrue(GrowLoop.IsInstalled);
            Assert.IsTrue(MonoManager.Instance.Valid);
            Assert.AreEqual(GrowState.Ready, GrowBoot.State);
        }

        [Test]
        public void Shutdown_ThenInitialize_ReusesRegisteredModules() {
            var module = new GrowAppProbeModule();
            GrowApp.Initialize(new IModule[] { module });

            GrowApp.Shutdown();
            GrowApp.Initialize();

            Assert.AreEqual(2, module.RegisterCount);
            Assert.AreEqual(2, module.InitializeCount);
            Assert.AreEqual(2, module.StartCount);
            Assert.AreEqual(1, module.StopCount);
            Assert.AreEqual(1, module.DisposeCount);
            Assert.IsTrue(GrowModules.IsReady);
        }

        [Test]
        public void SettleForSessionEnd_ThenInitialize_WithTheSameModuleName_StartsAFreshRegistry() {
            var first = new GrowAppProbeModule();
            GrowApp.Initialize(new IModule[] { first });

            GrowApp.SettleForSessionEnd();

            var second = new GrowAppProbeModule();
            Assert.DoesNotThrow(() => GrowApp.Initialize(new IModule[] { second }));
            Assert.AreEqual(1, second.RegisterCount);
            Assert.AreEqual(1, second.InitializeCount);
            Assert.AreEqual(1, second.StartCount);
            Assert.AreEqual(1, first.DisposeCount);
        }

        [Test]
        public void InitializeAndShutdown_AreIdempotent() {
            GrowApp.Initialize();
            var root = GrowApp.Root;

            GrowApp.Initialize();
            Assert.AreSame(root, GrowApp.Root);

            GrowApp.Shutdown();
            Assert.DoesNotThrow(() => GrowApp.Shutdown());
            Assert.IsFalse(GrowApp.IsInitialized);
        }

        [Test]
        public void Initialize_WithModules_StartsThem() {
            _module = new GrowAppProbeModule();

            GrowApp.Initialize(new IModule[] { _module });

            Assert.AreEqual(1, _module.RegisterCount);
            Assert.AreEqual(1, _module.InitializeCount);
            Assert.AreEqual(1, _module.StartCount);
            Assert.AreEqual(ModulePhase.Running, GrowModules.Phase);

            GrowApp.Shutdown();
            Assert.AreEqual(1, _module.StopCount);
            Assert.AreEqual(1, _module.DisposeCount);
        }

        [Test]
        public void SettleForSessionEnd_DropsModuleTable_AndAllowsFreshRegistration() {
            _module = new GrowAppProbeModule();
            GrowApp.Initialize(new IModule[] { _module });

            GrowApp.SettleForSessionEnd();

            var next = new GrowAppProbeModule();
            GrowApp.Initialize(new IModule[] { next });

            Assert.AreEqual(1, _module.StartCount,
                "the old session's module must not be driven again");
            Assert.AreEqual(1, next.StartCount);
            Assert.AreEqual(ModulePhase.Running, GrowModules.Phase);
        }

        [Test]
        public void ReadyNotification_ObservesInitializedTrue() {
            var observed = false;
            var token = GrowBoot.StateChanged.Subscribe(change => {
                if (change.Next == GrowState.Ready) {
                    observed = GrowApp.IsInitialized;
                }
            });

            GrowApp.Initialize();

            Assert.IsTrue(observed, "Ready handlers must see the composition as initialized");
            token.Dispose();
        }

        [Test]
        public void InstalledNotification_ObservesModulesStarted() {
            _module = new GrowAppProbeModule();
            var startedWhenInstalled = -1;
            var token = GrowBoot.StateChanged.Subscribe(change => {
                if (change.Next == GrowState.Installed) {
                    startedWhenInstalled = _module.StartCount;
                }
            });

            GrowApp.Initialize(new IModule[] { _module });

            Assert.AreEqual(1, startedWhenInstalled,
                "the Installed notification must run after the modules have started");
            token.Dispose();
        }

        [Test]
        public void ShutdownFromReadyNotification_DoesNotNoOp() {
            var token = GrowBoot.StateChanged.Subscribe(change => {
                if (change.Next == GrowState.Ready) {
                    GrowApp.Shutdown();
                }
            });

            GrowApp.Initialize();

            Assert.IsFalse(GrowApp.IsInitialized);
            Assert.IsFalse(GrowLoop.IsInstalled);
            Assert.AreEqual(GrowState.Shutdown, GrowBoot.State);
            token.Dispose();
        }

        [Test]
        public void PlayerQuit_RunsCompositionShutdown() {
            _module = new GrowAppProbeModule();
            GrowApp.Initialize(new IModule[] { _module });

            GrowLoop.Host.QuitForPlayer();

            Assert.AreEqual(1, _module.StopCount);
            Assert.AreEqual(1, _module.DisposeCount);
            Assert.IsFalse(GrowApp.IsInitialized);
            Assert.IsFalse(MonoManager.Instance.Valid);
            Assert.IsFalse(GrowLoop.IsInstalled);
            Assert.AreEqual(GrowState.Shutdown, GrowBoot.State);
        }

        [Test]
        public void RegisterModule_ThenInitialize_StartsWithTheRegisteredModule() {
            _module = new GrowAppProbeModule();
            GrowApp.RegisterModule(_module);

            GrowApp.Initialize();

            Assert.AreEqual(1, _module.RegisterCount);
            Assert.AreEqual(1, _module.InitializeCount);
            Assert.AreEqual(1, _module.StartCount);
        }

        [Test]
        public void RegisterModule_AfterInitialize_Throws() {
            GrowApp.Initialize();

            Assert.Throws<InvalidOperationException>(() =>
                GrowApp.RegisterModule(new GrowAppProbeModule()));
        }

        [Test]
        public void Initialize_NullModules_IsSafe() {
            Assert.DoesNotThrow(() => GrowApp.Initialize((IEnumerable<IModule>)null));
            Assert.IsTrue(GrowApp.IsInitialized);
            Assert.IsTrue(GrowModules.IsReady);
        }

        [Test]
        public void RegisterModule_Null_Throws() {
            Assert.Throws<ArgumentNullException>(() => GrowApp.RegisterModule(null));
        }

        [Test]
        public void Describe_ReflectsWiringBeforeAndAfterInitialize() {
            var before = GrowApp.Describe();
            StringAssert.Contains("initialized=False", before);
            StringAssert.Contains("loop=not-installed", before);
            StringAssert.Contains("phase=None", before);

            GrowApp.Initialize();
            var after = GrowApp.Describe();
            StringAssert.Contains("initialized=True", after);
            StringAssert.Contains("loop=installed", after);
            StringAssert.Contains("boot=Ready", after);
            StringAssert.Contains("phase=Running", after);

            GrowApp.Shutdown();
            StringAssert.Contains("initialized=False", GrowApp.Describe());
        }

        private sealed class GrowAppProbeModule : IModule {
            public int RegisterCount { get; private set; }
            public int InitializeCount { get; private set; }
            public int StartCount { get; private set; }
            public int StopCount { get; private set; }
            public int DisposeCount { get; private set; }

            public string Name => "GrowAppProbe";
            public int Priority => 0;
            public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

            public void OnRegister(IModuleContext context) {
                RegisterCount++;
            }

            public void OnInitialize(IModuleContext context) {
                InitializeCount++;
            }

            public void OnStart(IModuleContext context) {
                StartCount++;
            }

            public void OnStop() {
                StopCount++;
            }

            public void OnDispose() {
                DisposeCount++;
            }
        }

        private sealed class GrowAppNullLogSink : ILogSink {
            public void Write(in LogEntry entry) { }
        }
    }
}
